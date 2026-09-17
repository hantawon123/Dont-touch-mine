using UnityEngine;

namespace Game.Client.Lobby
{
    public interface ILobbyStartCountdownTickAudio
    {
        void SetRemainingSeconds(double remainingSeconds);
        void Hide();
    }

    /// <summary>
    /// 로비에서 게임 시작을 누른 뒤 10초 카운트다운 동안, 매 초 정각에 한 번씩
    /// 울리는 짧은 "째각" 효과음.
    /// </summary>
    /// <remarks>
    /// The lobby HUD lives in the scene, not a prefab, so the clip comes from
    /// Resources instead of a serialized field, the same reasoning as the
    /// in-match timer ticks. This is a HUD cue (2D, no attenuation/panning/doppler).
    /// The tick is edge-triggered on the integer-second boundary via
    /// <see cref="SetRemainingSeconds"/> so a value that jumps (cancel, resync)
    /// never plays more than one tick per call, and leaving the countdown
    /// resets the edge so starting again ticks from the top. Loading is lazy
    /// rather than in Awake so edit mode, which never runs lifecycle callbacks,
    /// still sees a usable component.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class LobbyStartCountdownTickAudio : MonoBehaviour, ILobbyStartCountdownTickAudio
    {
        public const string RootName = "LobbyStartCountdownTickAudio";
        public const string TickResource = "Audio/LobbyStartCountdownTick";
        /// <summary>
        /// 로비 시작 카운트다운 길이. <c>MatchStarter</c>가 종료 시각에 더하는 10초와 같다.
        /// </summary>
        public const float DurationSeconds = 10f;

        /// <summary>효과음 볼륨. 째각 소리에 적용된다.</summary>
        public static float EffectsVolume { get; set; } = 1f;

        private AudioSource tickSource;
        private AudioClip tickClip;
        private bool sourcesReady;
        private int lastTickedSecond = int.MinValue;

        /// <summary>카운트다운이 화면에 떠 있는 동안, 표시 초가 바뀔 때마다 친다.</summary>
        public static bool ShouldTick(double remainingSeconds) =>
            remainingSeconds > 0d &&
            Mathf.Max(0, Mathf.CeilToInt((float)remainingSeconds)) <= DurationSeconds;

        public static LobbyStartCountdownTickAudio Create(Transform parent)
        {
            var rootObject = new GameObject(RootName);
            rootObject.transform.SetParent(parent, false);
            return rootObject.AddComponent<LobbyStartCountdownTickAudio>();
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
        /// 매 프레임 남은 시간을 전달받아, 카운트다운 초가 바뀔 때마다 한 번씩 울린다.
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
