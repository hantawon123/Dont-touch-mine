using Game.Core.Settings;

namespace Game.Core.Voice
{
    /// <summary>
    /// Whether this player should be heard. Mute, 입력 모드 끄기, and a closed
    /// speaker close the microphone. 오픈 마이크 keeps it requested open
    /// without a talk key, but not while the speaker is off.
    /// </summary>
    public static class VoiceMutePolicy
    {
        public static bool IsMuted(
            bool preferenceMuted, string inputMode, bool listening = true) =>
            !listening || preferenceMuted || SoundCatalog.IsMicrophoneOff(inputMode);

        /// <summary>
        /// A held or latched talk key, or 오픈 마이크. Mute still wins on the
        /// recorder; this only decides whether talking was requested. A closed
        /// speaker is not a request.
        /// </summary>
        public static bool IsTalking(
            bool requested, string inputMode, bool listening = true) =>
            listening && (requested || SoundCatalog.IsOpenMicrophone(inputMode));
    }
}
