using System;

namespace Game.Core.Settings
{
    public enum AudioSettingsError
    {
        None,
        InvalidVolume,
        InvalidChannel
    }

    public sealed class AudioSettingsState
    {
        public const int MinVolume = 0;
        public const int MaxVolume = 100;
        public const int DefaultVolume = 50;

        public AudioSettingsState()
            : this(
                DefaultVolume,
                DefaultVolume,
                DefaultVolume,
                DefaultVolume,
                true,
                DefaultVolume,
                DefaultVolume)
        {
        }

        public AudioSettingsState(
            int masterVolume,
            int bgmVolume,
            int sfxVolume,
            int uiVolume,
            bool voiceChatEnabled,
            int voiceVolume,
            int micVolume)
        {
            MasterVolume = ClampVolume(masterVolume);
            BgmVolume = ClampVolume(bgmVolume);
            SfxVolume = ClampVolume(sfxVolume);
            UiVolume = ClampVolume(uiVolume);
            VoiceChatEnabled = voiceChatEnabled;
            VoiceVolume = ClampVolume(voiceVolume);
            MicVolume = ClampVolume(micVolume);
        }

        public int MasterVolume { get; private set; }

        public int BgmVolume { get; private set; }

        public int SfxVolume { get; private set; }

        public int UiVolume { get; private set; }

        public int VoiceVolume { get; private set; }

        public int MicVolume { get; private set; }

        public bool VoiceChatEnabled { get; private set; }

        public float GetListenerVolume()
        {
            return ToLinear(MasterVolume);
        }

        public float GetSourceVolume(AudioChannel channel)
        {
            switch (channel)
            {
                case AudioChannel.Master:
                    return 1f;
                case AudioChannel.Bgm:
                    return ToLinear(BgmVolume);
                case AudioChannel.Sfx:
                    return ToLinear(SfxVolume);
                case AudioChannel.Ui:
                    return ToLinear(UiVolume);
                case AudioChannel.Voice:
                    return VoiceChatEnabled ? ToLinear(VoiceVolume) : 0f;
                case AudioChannel.Mic:
                    return VoiceChatEnabled ? ToLinear(MicVolume) : 0f;
                default:
                    throw new ArgumentOutOfRangeException(nameof(channel));
            }
        }

        public int GetPercent(AudioChannel channel)
        {
            switch (channel)
            {
                case AudioChannel.Master:
                    return MasterVolume;
                case AudioChannel.Bgm:
                    return BgmVolume;
                case AudioChannel.Sfx:
                    return SfxVolume;
                case AudioChannel.Ui:
                    return UiVolume;
                case AudioChannel.Voice:
                    return VoiceVolume;
                case AudioChannel.Mic:
                    return MicVolume;
                default:
                    throw new ArgumentOutOfRangeException(nameof(channel));
            }
        }

        public bool TrySetVolume(
            AudioChannel channel,
            int percent,
            out AudioSettingsError error)
        {
            if (!Enum.IsDefined(typeof(AudioChannel), channel))
            {
                error = AudioSettingsError.InvalidChannel;
                return false;
            }

            if (percent < MinVolume || percent > MaxVolume)
            {
                error = AudioSettingsError.InvalidVolume;
                return false;
            }

            switch (channel)
            {
                case AudioChannel.Master:
                    MasterVolume = percent;
                    break;
                case AudioChannel.Bgm:
                    BgmVolume = percent;
                    break;
                case AudioChannel.Sfx:
                    SfxVolume = percent;
                    break;
                case AudioChannel.Ui:
                    UiVolume = percent;
                    break;
                case AudioChannel.Voice:
                    VoiceVolume = percent;
                    break;
                case AudioChannel.Mic:
                    MicVolume = percent;
                    break;
                default:
                    error = AudioSettingsError.InvalidChannel;
                    return false;
            }

            error = AudioSettingsError.None;
            return true;
        }

        public bool TrySetVoiceChatEnabled(bool enabled, out AudioSettingsError error)
        {
            VoiceChatEnabled = enabled;
            error = AudioSettingsError.None;
            return true;
        }

        private static int ClampVolume(int percent)
        {
            if (percent < MinVolume)
            {
                return MinVolume;
            }

            if (percent > MaxVolume)
            {
                return MaxVolume;
            }

            return percent;
        }

        private static float ToLinear(int percent)
        {
            return percent / (float)MaxVolume;
        }
    }
}
