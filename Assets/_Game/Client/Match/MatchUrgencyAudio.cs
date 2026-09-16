using Game.Core.Match;
using UnityEngine;

namespace Game.Client.Match
{
    public interface IMatchUrgencyAudio
    {
        void Show();
        void Hide();
    }

    /// <summary>
    /// 탐색 마지막 30초에 들리는 소리. 경고 종이 한 번 울리고, 그 뒤로 긴장감 배경음이
    /// 루프로 깔린다. <see cref="MatchUrgencyBorderView"/>와 같은 조건으로 켜고 꺼진다.
    /// </summary>
    /// <remarks>
    /// The HUD lives in the scene, not a prefab, so the clips come from
    /// Resources instead of serialized fields. Both sources are 2D: this is a
    /// HUD cue, not something happening at a place in the world. The bed fades
    /// out rather than cutting, because the match can end early when every item
    /// is destroyed. Loading is lazy rather than in Awake so edit mode, which
    /// never runs lifecycle callbacks, still sees a usable component.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class MatchUrgencyAudio : MonoBehaviour, IMatchUrgencyAudio
    {
        public const string RootName = "UrgencyAudio";
        public const string ChimeResource = "Audio/WarningChime";
        public const string LoopResource = "Audio/TensionLoop";
        public const float FadeOutSeconds = .6f;

        /// <summary>효과음 볼륨. 경고 종에 적용된다.</summary>
        public static float EffectsVolume { get; set; } = 1f;

        /// <summary>음악 볼륨. 긴장감 배경음에 적용된다.</summary>
        public static float MusicVolume { get; set; } = 1f;

        private AudioSource chimeSource;
        private AudioSource loopSource;
        private AudioClip chimeClip;
        private bool sourcesReady;
        private bool active;
        private float fadeGain;

        /// <summary>
        /// 긴장 연출이 들려야 하는 순간인지. 붉은 테두리와 같은 조건을 쓴다.
        /// </summary>
        public static bool ShouldPlay(MatchPhase phase, double remainingSeconds) =>
            phase == MatchPhase.Searching && MatchTimerView.IsWarning(remainingSeconds);

        /// <summary>
        /// 배경음 페이드 값의 다음 상태. 켜져 있으면 즉시 최대, 꺼지면 선형으로 준다.
        /// </summary>
        internal static float AdvanceFade(float current, bool playing, float deltaSeconds)
        {
            if (playing) return 1f;
            if (deltaSeconds <= 0f) return Mathf.Clamp01(current);
            return Mathf.Clamp01(current - deltaSeconds / FadeOutSeconds);
        }

        public static MatchUrgencyAudio Create(Transform parent)
        {
            var rootObject = new GameObject(RootName);
            rootObject.transform.SetParent(parent, false);
            return rootObject.AddComponent<MatchUrgencyAudio>();
        }

        private void EnsureSources()
        {
            if (sourcesReady) return;
            sourcesReady = true;
            chimeClip = Resources.Load<AudioClip>(ChimeResource);
            chimeSource = CreateSource("Chime");
            loopSource = CreateSource("TensionLoop");
            loopSource.clip = Resources.Load<AudioClip>(LoopResource);
            loopSource.loop = true;
        }

        private AudioSource CreateSource(string objectName)
        {
            var audioObject = new GameObject(objectName);
            audioObject.transform.SetParent(transform, false);
            var source = audioObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            // A HUD cue, so no attenuation, panning or doppler.
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            return source;
        }

        public void Show()
        {
            if (active) return;
            active = true;
            fadeGain = 1f;
            EnsureSources();
            if (chimeSource != null && chimeClip != null)
            {
                chimeSource.volume = Mathf.Clamp01(EffectsVolume);
                chimeSource.PlayOneShot(chimeClip);
            }

            if (loopSource != null && loopSource.clip != null && !loopSource.isPlaying)
            {
                loopSource.volume = Mathf.Clamp01(MusicVolume);
                loopSource.Play();
            }
        }

        public void Hide()
        {
            active = false;
        }

        /// <remarks>Separate from Update so edit mode tests can step the fade.</remarks>
        internal void Advance(float deltaSeconds)
        {
            fadeGain = AdvanceFade(fadeGain, active, deltaSeconds);
            if (loopSource == null) return;
            loopSource.volume = Mathf.Clamp01(MusicVolume) * fadeGain;
            if (!active && fadeGain <= 0f && loopSource.isPlaying) loopSource.Stop();
        }

        private void Update() => Advance(Time.unscaledDeltaTime);

        private void OnDisable()
        {
            active = false;
            fadeGain = 0f;
            if (chimeSource != null) chimeSource.Stop();
            if (loopSource != null) loopSource.Stop();
        }
    }
}
