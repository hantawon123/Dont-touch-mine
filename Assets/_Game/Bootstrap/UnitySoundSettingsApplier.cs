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
    /// separately applies Music to its AudioSource when settings change, so the
    /// master gain is not multiplied twice. Other sound categories are not wired here.
    /// </remarks>
    public sealed class UnitySoundSettingsApplier : ISoundSettingsApplier
    {
        public void Apply(SoundSettings settings)
        {
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
