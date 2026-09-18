using Game.Core.Match;
using UnityEngine;

namespace Game.Client.Match
{
    public interface ISearchingTimerTickAudio
    {
        void SetRemainingSeconds(MatchPhase phase, double remainingSeconds);
        void Hide();
    }

    /// <summary>
    /// 탐색 시간 종료 마지막 30초 동안, 매 초 정각에 한 번씩 울리는 짧은 "째각" 효과음.
    /// </summary>
    /// <remarks>
    /// Uses the exact same window as <see cref="MatchUrgencyAudio"/> (the chime and
    /// tension bed) so the red border, the tension bed and this tick all agree on
    /// when it is urgent. The HUD lives in the scene, not a prefab, so the clip
    /// comes from Resources instead of a serialized field. This is a HUD cue (2D,
    /// no attenuation/panning/doppler), not something happening at a place in the
    /// world. The tick is edge-triggered on the integer-second boundary via
    /// <see cref="SetRemainingSeconds"/> so a value that jumps (phase change,
    /// resync) never plays more than one tick per call, and leaving the warning
    /// window (or the Searching phase) resets the edge so re-entering it ticks
    /// again cleanly. Loading is lazy rather than in Awake so edit mode, which
    /// never runs lifecycle callbacks, still sees a usable component.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class SearchingTimerTickAudio : MonoBehaviour, ISearchingTimerTickAudio
    {
        public const string RootName = "SearchingTimerTickAudio";
        public const string TickResource = "Audio/SearchingTimerTick";

        /// <summary>효과음 볼륨. 째각 소리에 적용된다.</summary>
        public static float EffectsVolume { get; set; } = 1f;

        private AudioSource tickSource;
        private AudioClip tickClip;
        private bool sourcesReady;
        private int lastTickedSecond = int.MinValue;

        /// <summary>
        /// 째각 소리가 울려야 하는 순간인지. MatchUrgencyAudio의 붉은 테두리/긴장음과 같은 조건을 쓴다.
        /// </summary>
        public static bool ShouldTick(MatchPhase phase, double remainingSeconds) =>
            remainingSeconds > 0d && phase == MatchPhase.Searching && MatchTimerView.IsWarning(remainingSeconds);

        public static SearchingTimerTickAudio Create(Transform parent)
        {
            var rootObject = new GameObject(RootName);
            rootObject.transform.SetParent(parent, false);
            return rootObject.AddComponent<SearchingTimerTickAudio>();
        }

        private void EnsureSource()
        {
            if (sourcesReady) return;
            sourcesReady = true;
            tickClip = Resources.Load<AudioClip>(TickResource);

            var audioObject = new GameObject("Tick");
            audioObject.transform.SetParent(transform, false);
            tickSource = audioObject.AddComponent<AudioSource>();
            tickSource.playOnAwake = false;
            tickSource.loop = false;
            // A HUD cue, so no attenuation, panning or doppler.
            tickSource.spatialBlend = 0f;
            tickSource.dopplerLevel = 0f;
        }

        /// <summary>
        /// 매 프레임 현재 페이즈와 남은 시간을 전달받아, 탐색 마지막 30초 동안
        /// 초가 바뀔 때마다 한 번씩 울린다.
        /// </summary>
        public void SetRemainingSeconds(MatchPhase phase, double remainingSeconds)
        {
            if (!ShouldTick(phase, remainingSeconds))
            {
                lastTickedSecond = int.MinValue;
                return;
            }

            var totalSeconds = Mathf.Max(0, Mathf.CeilToInt((float)remainingSeconds));
            if (totalSeconds == lastTickedSecond)
            {
                return;
            }

            lastTickedSecond = totalSeconds;
            EnsureSource();
            if (tickSource != null && tickClip != null)
            {
                tickSource.volume = Mathf.Clamp01(EffectsVolume);
                tickSource.PlayOneShot(tickClip);
            }
        }

        public void Hide()
        {
            lastTickedSecond = int.MinValue;
        }

        private void OnDisable()
        {
            lastTickedSecond = int.MinValue;
            if (tickSource != null)
            {
                tickSource.Stop();
            }
        }
    }
}
