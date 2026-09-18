using UnityEngine;

namespace Game.Client.Match
{
    public interface IHidingTimerTickAudio
    {
        void SetRemainingSeconds(double remainingSeconds);
        void Hide();
    }

    /// <summary>
    /// 숨기기 시간 종료 <see cref="HidingActiveHudView.WarningSeconds"/>초 전부터,
    /// 매 초 정각에 한 번씩 울리는 짧은 "째각" 효과음.
    /// </summary>
    /// <remarks>
    /// The HUD lives in the scene, not a prefab, so the clip comes from Resources
    /// instead of a serialized field, the same reasoning as <see cref="MatchUrgencyAudio"/>.
    /// This is a HUD cue (2D, no attenuation/panning/doppler), not something happening
    /// at a place in the world. The tick is edge-triggered on the integer-second
    /// boundary via <see cref="SetRemainingSeconds"/> so a value that jumps (phase
    /// change, resync) never plays more than one tick per call, and leaving the
    /// warning window resets the edge so re-entering it (e.g. a resynced timer)
    /// ticks again cleanly. Loading is lazy rather than in Awake so edit mode, which
    /// never runs lifecycle callbacks, still sees a usable component.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class HidingTimerTickAudio : MonoBehaviour, IHidingTimerTickAudio
    {
        public const string RootName = "TimerTickAudio";
        public const string TickResource = "Audio/HidingTimerTick";

        /// <summary>효과음 볼륨. 째각 소리에 적용된다.</summary>
        public static float EffectsVolume { get; set; } = 1f;

        private AudioSource tickSource;
        private AudioClip tickClip;
        private bool sourcesReady;
        private int lastTickedSecond = int.MinValue;

        /// <summary>
        /// 째각 소리가 울려야 하는 순간인지. HidingActiveHudView의 경고 구간과 같은 조건을 쓴다.
        /// </summary>
        public static bool ShouldTick(double remainingSeconds) =>
            remainingSeconds > 0d && HidingActiveHudView.IsWarning(remainingSeconds);

        public static HidingTimerTickAudio Create(Transform parent)
        {
            var rootObject = new GameObject(RootName);
            rootObject.transform.SetParent(parent, false);
            return rootObject.AddComponent<HidingTimerTickAudio>();
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
        /// 매 프레임 남은 시간을 전달받아, 경고 구간에서 초가 바뀔 때마다 한 번씩 울린다.
        /// </summary>
        public void SetRemainingSeconds(double remainingSeconds)
        {
            if (!ShouldTick(remainingSeconds))
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
