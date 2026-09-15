using Game.Core.Settings;

namespace Game.Core.Voice
{
    /// <summary>
    /// Whether this player should be heard. Mute and 입력 모드 끄기 close the
    /// microphone. 오픈 마이크 keeps it requested open without a talk key.
    /// </summary>
    public static class VoiceMutePolicy
    {
        public static bool IsMuted(bool preferenceMuted, string inputMode) =>
            preferenceMuted || SoundCatalog.IsMicrophoneOff(inputMode);

        /// <summary>
        /// A held or latched talk key, or 오픈 마이크. Mute still wins on the
        /// recorder; this only decides whether talking was requested.
        /// </summary>
        public static bool IsTalking(bool requested, string inputMode) =>
            requested || SoundCatalog.IsOpenMicrophone(inputMode);
    }
}
