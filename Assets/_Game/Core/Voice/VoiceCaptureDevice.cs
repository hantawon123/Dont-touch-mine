using System;
using Game.Core.Settings;

namespace Game.Core.Voice
{
    /// <summary>
    /// Which microphone the voice rig should capture from, read off the saved
    /// 사운드 settings.
    /// </summary>
    /// <remarks>
    /// The 사운드 tab's first entry is not a device. It is a code meaning
    /// "whichever one this machine calls default", and handing that code to a
    /// microphone back end would have it look for a device named "default" and
    /// find none. Turning it into nothing here is what keeps that lookup out of
    /// the rig.
    /// </remarks>
    public static class VoiceCaptureDevice
    {
        /// <summary>
        /// The device name to capture from, or the empty string for the
        /// machine's default.
        /// </summary>
        public static string ResolveAvailable(string requested, System.Collections.Generic.IReadOnlyList<string> devices)
        {
            var name = Requested(requested);
            if (name.Length == 0 || devices == null) return null;
            for (var i = 0; i < devices.Count; i++)
                if (string.Equals(name, devices[i], StringComparison.Ordinal)) return name;
            return null;
        }

        /// <summary>The saved name, or an empty string for the default device.</summary>
        public static string Requested(SoundSettings settings) =>
            Requested(settings.DeviceName);

        /// <inheritdoc cref="Requested(SoundSettings)"/>
        public static string Requested(string savedDeviceName) =>
            string.IsNullOrWhiteSpace(savedDeviceName)
            || string.Equals(savedDeviceName, SoundCatalog.DefaultDevice, StringComparison.Ordinal)
                ? string.Empty
                : savedDeviceName;
    }
}
