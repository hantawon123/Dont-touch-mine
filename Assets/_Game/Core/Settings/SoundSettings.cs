using System;
using System.Collections.Generic;

namespace Game.Core.Settings
{
    /// <summary>
    /// The five things on the 사운드 tab that are set by a slider rather than
    /// by a picker, in the order they are drawn.
    /// </summary>
    public enum SoundVolume
    {
        Master,
        Music,
        Ambience,
        Effects,
        Microphone
    }

    /// <summary>
    /// What the 사운드 tab holds.
    /// </summary>
    /// <remarks>
    /// Not built on <see cref="OptionValues"/> like the 그래픽 and 인터페이스
    /// tabs, because these rows are not all the same kind of thing: five are
    /// numbers on a slider, one is a device this machine happens to have, and
    /// one is a choice from a list. Writing them as what they are keeps the
    /// mixer and the microphone from parsing strings.
    /// </remarks>
    public readonly struct SoundSettings : IEquatable<SoundSettings>
    {
        private static readonly int VolumeCount = Enum.GetValues(typeof(SoundVolume)).Length;

        private readonly int[] volumes;

        private SoundSettings(int[] volumes, string deviceName, string inputMode)
        {
            this.volumes = volumes;
            DeviceName = deviceName ?? string.Empty;
            InputMode = inputMode ?? string.Empty;
        }

        /// <summary>
        /// Which microphone to listen to, by the name the machine gives it, or
        /// <see cref="SoundCatalog.DefaultDevice"/> for whichever one the
        /// machine calls its default — what a player who has never chosen
        /// gets, and what is left when the one they chose is unplugged.
        /// </summary>
        public string DeviceName { get; }

        /// <summary>A code from <see cref="SoundCatalog.InputModes"/>.</summary>
        public string InputMode { get; }

        /// <summary>Nothing chosen.</summary>
        public static SoundSettings Empty => default;

        /// <summary>
        /// How loud, from <see cref="SoundCatalog.MinVolume"/> to
        /// <see cref="SoundCatalog.MaxVolume"/>. A row never written reads as
        /// -1, which <see cref="SoundCatalog.Normalise"/> replaces with the
        /// default rather than with silence.
        /// </summary>
        public int Get(SoundVolume volume)
        {
            var index = (int)volume;
            if (volumes == null || index < 0 || index >= volumes.Length)
            {
                return SoundCatalog.Unset;
            }

            return volumes[index];
        }

        public SoundSettings With(SoundVolume volume, int percent)
        {
            var index = (int)volume;
            if (index < 0 || index >= VolumeCount)
            {
                throw new ArgumentOutOfRangeException(nameof(volume));
            }

            var next = new int[VolumeCount];
            for (var slot = 0; slot < VolumeCount; slot++)
            {
                next[slot] = Get((SoundVolume)slot);
            }

            next[index] = percent;
            return new SoundSettings(next, DeviceName, InputMode);
        }

        public SoundSettings WithDevice(string deviceName) =>
            new SoundSettings(CopyVolumes(), deviceName, InputMode);

        public SoundSettings WithInputMode(string inputMode) =>
            new SoundSettings(CopyVolumes(), DeviceName, inputMode);

        public bool Equals(SoundSettings other)
        {
            if (!string.Equals(DeviceName, other.DeviceName, StringComparison.Ordinal)
                || !string.Equals(InputMode, other.InputMode, StringComparison.Ordinal))
            {
                return false;
            }

            for (var index = 0; index < VolumeCount; index++)
            {
                var volume = (SoundVolume)index;
                if (Get(volume) != other.Get(volume))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj) => obj is SoundSettings other && Equals(other);

        public override int GetHashCode()
        {
            var hash = 17;
            for (var index = 0; index < VolumeCount; index++)
            {
                hash = (hash * 31) + Get((SoundVolume)index);
            }

            hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(DeviceName);
            return (hash * 31) + StringComparer.Ordinal.GetHashCode(InputMode);
        }

        public static bool operator ==(SoundSettings left, SoundSettings right) => left.Equals(right);

        public static bool operator !=(SoundSettings left, SoundSettings right) => !left.Equals(right);

        public override string ToString()
        {
            var parts = new List<string>();
            for (var index = 0; index < VolumeCount; index++)
            {
                var volume = (SoundVolume)index;
                parts.Add($"{volume}={Get(volume)}");
            }

            parts.Add($"Device={DeviceName}");
            parts.Add($"InputMode={InputMode}");
            return "Sound(" + string.Join(", ", parts) + ")";
        }

        private int[] CopyVolumes()
        {
            var copy = new int[VolumeCount];
            for (var index = 0; index < VolumeCount; index++)
            {
                copy[index] = Get((SoundVolume)index);
            }

            return copy;
        }
    }

    /// <summary>
    /// What the 사운드 rows allow: how far the sliders go, and what the input
    /// mode can be.
    /// </summary>
    public static class SoundCatalog
    {
        /// <summary>What a volume reads as before anybody has set it.</summary>
        public const int Unset = -1;

        public const int MinVolume = 0;
        public const int MaxVolume = 100;

        /// <summary>
        /// Half way, which is what the mock-up draws on every slider.
        /// </summary>
        public const int DefaultVolume = 50;

        public const string PushToTalk = "push";
        public const string OpenMic = "open";
        public const string MicOff = "off";

        /// <summary>
        /// Whether 입력 모드 is 끄기. That is the settings-tab mute: the
        /// microphone stays closed until the mode is something else.
        /// </summary>
        public static bool IsMicrophoneOff(string inputMode) =>
            string.Equals(inputMode, MicOff, StringComparison.Ordinal);

        /// <summary>
        /// Whether 입력 모드 is 오픈 마이크. The microphone stays open without
        /// a talk key, until mute or 끄기 close it.
        /// </summary>
        public static bool IsOpenMicrophone(string inputMode) =>
            string.Equals(inputMode, OpenMic, StringComparison.Ordinal);

        /// <summary>
        /// How the microphone decides when to listen.
        /// </summary>
        /// <remarks>
        /// Push-to-talk is the default rather than the open microphone the
        /// mock-up shows. A game that starts out transmitting whatever is said
        /// in the room is a privacy decision, not a convenience one, and it
        /// should be made deliberately rather than inherited from a picture.
        /// </remarks>
        public static OptionChoices InputModes { get; } = new OptionChoices(
            new OptionChoice(PushToTalk, "눌러서 말하기"),
            new OptionChoice(OpenMic, "오픈 마이크"),
            new OptionChoice(MicOff, "끄기"));

        /// <summary>
        /// The code for the machine's own choice of microphone. A code rather
        /// than an empty name so the device picker can list it beside the real
        /// devices, which is where the design puts it.
        /// </summary>
        public const string DefaultDevice = "default";

        /// <summary>Shown for <see cref="DefaultDevice"/>.</summary>
        public const string DefaultDeviceLabel = "기본 장치";

        /// <summary>
        /// The device picker's choices: the machine's default first, then every
        /// microphone the machine reports, by name.
        /// </summary>
        public static OptionChoices DeviceChoices(IReadOnlyList<string> availableDevices)
        {
            var choices = new List<OptionChoice> { new OptionChoice(DefaultDevice, DefaultDeviceLabel) };
            if (availableDevices != null)
            {
                foreach (var name in availableDevices)
                {
                    if (!string.IsNullOrWhiteSpace(name)
                        && !string.Equals(name, DefaultDevice, StringComparison.Ordinal))
                    {
                        // Shown exactly as the machine gives it. Two devices on
                        // one sound card differ only by what is inside the
                        // brackets, so trimming that would leave rows that
                        // cannot be told apart; the picker on this row is made
                        // wide enough to hold it instead.
                        choices.Add(new OptionChoice(name, name));
                    }
                }
            }

            return new OptionChoices(DefaultDevice, choices.ToArray());
        }

        /// <summary>
        /// What a player who has never opened the tab gets, and what 초기화
        /// puts back: every slider half way, the machine's own microphone, and
        /// push-to-talk.
        /// </summary>
        public static SoundSettings Defaults
        {
            get
            {
                var settings = SoundSettings.Empty;
                foreach (SoundVolume volume in Enum.GetValues(typeof(SoundVolume)))
                {
                    settings = settings.With(volume, DefaultVolume);
                }

                return settings
                    .WithDevice(DefaultDevice)
                    .WithInputMode(InputModes.Default.Code);
            }
        }

        /// <summary>
        /// Brings saved or offered values back within what the game can
        /// actually do: sliders inside their range, and an input mode we
        /// recognise.
        /// </summary>
        /// <param name="availableDevices">
        /// What this machine has. A saved device that is not among them falls
        /// back to the default rather than being kept, so an unplugged headset
        /// does not leave the game listening to nothing. Null skips the check,
        /// for callers with no way to ask.
        /// </param>
        public static SoundSettings Normalise(
            SoundSettings settings, IReadOnlyList<string> availableDevices = null)
        {
            var result = settings;
            foreach (SoundVolume volume in Enum.GetValues(typeof(SoundVolume)))
            {
                var percent = settings.Get(volume);
                result = result.With(
                    volume,
                    percent == Unset ? DefaultVolume : Clamp(percent));
            }

            if (!InputModes.TryFind(settings.InputMode, out var mode))
            {
                mode = InputModes.Default;
            }

            result = result.WithInputMode(mode.Code);

            var device = settings.DeviceName;
            var keepDevice = !string.IsNullOrWhiteSpace(device)
                             && (string.Equals(device, DefaultDevice, StringComparison.Ordinal)
                                 || availableDevices == null
                                 || Contains(availableDevices, device));

            return result.WithDevice(keepDevice ? device : DefaultDevice);
        }

        public static int Clamp(int percent) =>
            percent < MinVolume ? MinVolume : percent > MaxVolume ? MaxVolume : percent;

        private static bool Contains(IReadOnlyList<string> names, string name)
        {
            for (var index = 0; index < names.Count; index++)
            {
                if (string.Equals(names[index], name, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
