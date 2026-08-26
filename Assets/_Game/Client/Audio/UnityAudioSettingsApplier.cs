using System;
using Game.Core.Settings;
using UnityEngine;

namespace Game.Client.Audio
{
    public sealed class UnityAudioSettingsApplier : IAudioSettingsApplier
    {
        public void Apply(AudioSettingsState settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            AudioListener.volume = settings.GetListenerVolume();
        }
    }
}
