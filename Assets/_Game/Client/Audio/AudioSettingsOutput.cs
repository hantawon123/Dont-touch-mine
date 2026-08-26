using System;
using Game.Core.Settings;

namespace Game.Client.Audio
{
    public static class AudioSettingsOutput
    {
        public static AudioSettingsState Current { get; private set; }

        public static event Action<AudioSettingsState> Changed;

        internal static void Publish(AudioSettingsState settings)
        {
            Current = settings ?? throw new ArgumentNullException(nameof(settings));
            Changed?.Invoke(settings);
        }
    }
}
