using System;
using Game.Core.Flow;
using Game.Core.Ports;
using Game.Core.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>One continuous menu track, stopped when entering a game room.</summary>
    public sealed class MenuBgmController : IStartable, ITickable, IDisposable
    {
        private readonly AppFlowSystem flow;
        private readonly SoundSettingsSystem sound;
        private readonly AudioSource source;
        private readonly IMicrophoneTest microphoneTest;
        private bool started;
        private bool inMenu;
        private bool ducked;
        public const float FadeSeconds = 1f;

        /// <summary>
        /// How long to stay silent after a microphone test. Closing the
        /// capture device leaves the output noisy for a couple of seconds,
        /// so the track stays out of the mixer until that has passed.
        /// </summary>
        public const float MicReleaseSeconds = 3f;

        private float musicVolume;
        private float fadeGain = 1f;
        private float releaseHold;
        private bool fading;
        private bool playing;

        public MenuBgmController(
            AppFlowSystem flow,
            SoundSettingsSystem sound,
            AudioSource source,
            IMicrophoneTest microphoneTest = null)
        {
            this.flow = flow;
            this.sound = sound;
            this.source = source;
            this.microphoneTest = microphoneTest;
        }

        public static bool ShouldPlay(AppFlowState state) =>
            state is AppFlowState.Home or AppFlowState.RoomBrowser
                or AppFlowState.CharacterCloset or AppFlowState.Settings;

        public void Start()
        {
            if (started) return;
            started = true;
            flow.StateChanged += OnStateChanged;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
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
            // AppFlow starts at Home even while the startup Intro scene is active.
            // Stop immediately here so the intro's own ambience is the only track.
            if (SceneManager.GetActiveScene().name == "Intro")
            {
                inMenu = false;
                fading = false;
                playing = false;
                fadeGain = 0f;
                releaseHold = 0f;
                source.volume = 0f;
                source.Stop();
                return;
            }
            var next = ShouldPlay(state);
            if (next == inMenu) return;
            inMenu = next;
            if (inMenu && !playing && !ducked && releaseHold <= 0f)
            {
                source.Play();
                playing = true;
            }
            fading = true;
        }

        private void OnActiveSceneChanged(Scene previous, Scene current) =>
            OnStateChanged(flow.CurrentState);

        public void Tick() => AdvanceFade(Time.unscaledDeltaTime);

        private void AdvanceFade(float deltaTime)
        {
            if (!started || source == null) return;

            var nextDucked = microphoneTest != null && microphoneTest.IsRunning;
            if (nextDucked != ducked)
            {
                ducked = nextDucked;
                fading = true;
                if (ducked)
                {
                    releaseHold = 0f;
                }
                else
                {
                    fadeGain = 0f;
                    source.volume = 0f;
                    source.Stop();
                    playing = false;
                    releaseHold = MicReleaseSeconds;
                }
            }

            if (releaseHold > 0f)
            {
                releaseHold = Mathf.Max(0f, releaseHold - Mathf.Max(0f, deltaTime));
                source.volume = 0f;
                if (releaseHold > 0f)
                {
                    return;
                }

                if (inMenu && !playing)
                {
                    source.Play();
                    playing = true;
                }

                fading = true;
                return;
            }

            if (!fading) return;

            // A running microphone test only mutes the track. Leaving the
            // menu still stops it once the fade finishes. Fade-in waits
            // until the capture device has left the mixer.
            var target = inMenu && !ducked ? 1f : 0f;
            fadeGain = Mathf.MoveTowards(fadeGain, target, Mathf.Max(0f, deltaTime) / FadeSeconds);
            source.volume = musicVolume * fadeGain;
            if (fadeGain != target) return;
            fading = false;
            if (!inMenu || ducked)
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
            releaseHold = 0f;
            flow.StateChanged -= OnStateChanged;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            sound.AudioChanged -= ApplyVolume;
            if (source != null) source.Stop();
        }
    }
}
