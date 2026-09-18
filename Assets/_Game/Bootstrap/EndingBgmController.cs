using System;
using Game.Core.Flow;
using Game.Core.Settings;
using UnityEngine;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>
    /// 하이라이트 구간만 재생하는 엔딩 BGM. 들어갈 때 페이드인, 나올 때 페이드아웃.
    /// </summary>
    /// <remarks>
    /// One looped track for highlight playback only. Result and lobby stay
    /// silent. Volume is the Music slider times
    /// <see cref="SoundCatalog.BgmPlaybackVolume"/> (default 50 already sounds
    /// like 40 on the file) times the fade. Master is already on AudioListener.
    /// The clip is assigned on <c>ProjectLifetimeScope</c>.
    /// </remarks>
    public sealed class EndingBgmController : IStartable, ITickable, IDisposable
    {
        public const float FadeSeconds = 1f;
        public const float PlaybackVolume = SoundCatalog.BgmPlaybackVolume;
        public const string ClipAssetPath = "Assets/_Game/Content/Audio/BGM/ending_bgm.mp3";

        private readonly AppFlowSystem flow;
        private readonly SoundSettingsSystem sound;
        private readonly AudioSource source;
        private bool started;
        private bool inEnding;
        private bool fading;
        private bool playing;
        private float musicVolume;
        private float fadeGain;

        public EndingBgmController(
            AppFlowSystem flow,
            SoundSettingsSystem sound,
            AudioSource source)
        {
            this.flow = flow;
            this.sound = sound;
            this.source = source;
        }

        public static bool ShouldPlay(AppFlowState state) =>
            state == AppFlowState.Highlight;

        public void Start()
        {
            if (started) return;
            started = true;
            flow.StateChanged += OnStateChanged;
            sound.AudioChanged += ApplyVolume;
            ApplyVolume(sound.Current);
            OnStateChanged(flow.CurrentState);
        }

        private void ApplyVolume(SoundSettings settings)
        {
            // Master is already applied through AudioListener.volume.
            musicVolume = Mathf.Clamp01(settings.Get(SoundVolume.Music) / (float)SoundCatalog.MaxVolume);
            source.volume = ResolvedVolume();
        }

        private void OnStateChanged(AppFlowState state)
        {
            var next = ShouldPlay(state);
            if (next == inEnding) return;
            inEnding = next;
            if (inEnding && !playing)
            {
                fadeGain = 0f;
                source.volume = 0f;
                source.Play();
                playing = true;
            }

            fading = true;
        }

        public void Tick() => AdvanceFade(Time.unscaledDeltaTime);

        private void AdvanceFade(float deltaTime)
        {
            if (!started || source == null || !fading) return;

            var target = inEnding ? 1f : 0f;
            fadeGain = Mathf.MoveTowards(fadeGain, target, Mathf.Max(0f, deltaTime) / FadeSeconds);
            source.volume = ResolvedVolume();
            if (fadeGain != target) return;
            fading = false;
            if (!inEnding)
            {
                source.Stop();
                playing = false;
            }
        }

        public void Dispose()
        {
            if (!started) return;
            started = false;
            inEnding = false;
            fading = false;
            playing = false;
            fadeGain = 0f;
            flow.StateChanged -= OnStateChanged;
            sound.AudioChanged -= ApplyVolume;
            if (source != null) source.Stop();
        }

        private float ResolvedVolume() => musicVolume * fadeGain * PlaybackVolume;
    }
}
