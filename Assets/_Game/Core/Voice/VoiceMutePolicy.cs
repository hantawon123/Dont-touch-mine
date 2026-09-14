using Game.Core.Settings;

namespace Game.Core.Voice
{
    /// <summary>
    /// Whether this player should be heard. Two switches close the
    /// microphone: the in-game mute, and 입력 모드 끄기 on the sound tab.
    /// </summary>
    public static class VoiceMutePolicy
    {
        public static bool IsMuted(bool preferenceMuted, string inputMode) =>
            preferenceMuted || SoundCatalog.IsMicrophoneOff(inputMode);
    }
}
