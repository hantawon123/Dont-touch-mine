using System;
using Game.Core.Flow;
using Game.Core.Settings;
using UnityEngine;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>One continuous menu track, stopped when entering a game room.</summary>
    public sealed class MenuBgmController : IStartable, ITickable, IDisposable
    {
        private readonly AppFlowSystem flow;
        private readonly SoundSettingsSystem sound;
        private readonly AudioSource source;
        private bool started;
        private bool inMenu;
        public const float FadeSeconds = 1f;
        private float musicVolume;
        private float fadeGain = 1f;
        private bool fading;
        private bool playing;

        public MenuBgmController(AppFlowSystem flow, SoundSettingsSystem sound, AudioSource source)
        {
            this.flow = flow;
            this.sound = sound;
            this.source = source;
        }

        public static bool ShouldPlay(AppFlowState state) =>
            state is AppFlowState.Home or AppFlowState.RoomBrowser
                or AppFlowState.CharacterCloset or AppFlowState.Settings;

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
            source.volume = musicVolume * fadeGain;
        }

        private void OnStateChanged(AppFlowState state)
        {
            var next = ShouldPlay(state);
            if (next == inMenu) return;
            inMenu = next;
            if (inMenu && !playing)
            {
                source.Play();
                playing = true;
            }
            fading = true;
        }

        public void Tick() => AdvanceFade(Time.unscaledDeltaTime);

        private void AdvanceFade(float deltaTime)
        {
            if (!started || !fading || source == null) return;
            var target = inMenu ? 1f : 0f;
            fadeGain = Mathf.MoveTowards(fadeGain, target, Mathf.Max(0f, deltaTime) / FadeSeconds);
            source.volume = musicVolume * fadeGain;
            if (fadeGain != target) return;
            fading = false;
            if (!inMenu)
            {
                source.Stop();
                playing = false;
            }
        }

        public void Dispose()
        {
            if (!started) return;
            started = false;
            inMenu = false;
            fading = false;
            playing = false;
            flow.StateChanged -= OnStateChanged;
            sound.AudioChanged -= ApplyVolume;
            if (source != null) source.Stop();
        }
    }
}
