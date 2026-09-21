using System.Collections.Generic;
using Game.Core.Ports;
using Game.Core.Settings;
using UnityEngine;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>
    /// Carries the 사운드 settings into Unity's audio.
    /// </summary>
    /// <remarks>
    /// Master volume is applied through AudioListener.volume. MenuBgmController
    /// and EndingBgmController separately apply Music to their AudioSources when
    /// settings change, so the
    /// master gain is not multiplied twice. Footsteps, the warning chime, the hiding-timer tick,
    /// the searching-timer tick, the lobby start-countdown tick, the match-end bell,
    /// the destruction notice and UI button clicks separately use Effects; the last-thirty-seconds bed uses Music, so muting
    /// music silences it without taking the chime with it.
    /// </remarks>
    public sealed class UnitySoundSettingsApplier : ISoundSettingsApplier
    {
        public void Apply(SoundSettings settings)
        {
            var effects = Mathf.Clamp01(
                settings.Get(SoundVolume.Effects) / (float)SoundCatalog.MaxVolume);
            Game.Client.Players.PlayerFootstepAudio.EffectsVolume = effects;
            Game.Client.Match.MatchUrgencyAudio.EffectsVolume = effects;
            Game.Client.Match.HidingTimerTickAudio.EffectsVolume = effects;
            Game.Client.Match.SearchingTimerTickAudio.EffectsVolume = effects;
            Game.Client.Match.MatchEndBellAudio.EffectsVolume = effects;
            Game.Client.Match.DestructionNoticeAudio.EffectsVolume = effects;
            Game.Client.Lobby.LobbyStartCountdownTickAudio.EffectsVolume = effects;
            Game.Client.Common.UiButtonClickAudio.EffectsVolume = effects;
            Game.Client.Match.MatchUrgencyAudio.MusicVolume = Mathf.Clamp01(
                settings.Get(SoundVolume.Music) / (float)SoundCatalog.MaxVolume);
            AudioListener.volume = Mathf.Clamp01(
                settings.Get(SoundVolume.Master) / (float)SoundCatalog.MaxVolume);
        }
    }

    /// <summary>The microphones Unity can see on this machine.</summary>
    public sealed class UnityMicrophoneDevices : IMicrophoneDevices
    {
        public IReadOnlyList<string> Names
        {
            get
            {
#if UNITY_WEBGL
                // The browser hands out microphones through its own permission
                // prompt, which Unity's Microphone class does not go through.
                return System.Array.Empty<string>();
#else
                return Microphone.devices ?? System.Array.Empty<string>();
#endif
            }
        }
    }

    /// <summary>
    /// Makes the saved sound settings heard when the game starts.
    /// </summary>
    /// <inheritdoc cref="GraphicsSettingsStartup"/>
    public sealed class SoundSettingsStartup : IStartable
    {
        private readonly SoundSettingsSystem sound;

        public SoundSettingsStartup(SoundSettingsSystem sound)
        {
            this.sound = sound;
        }

        public void Start() => sound.ApplyToAudio();
    }
}
