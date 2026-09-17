using R3;

namespace Game.Core.Ports
{
    /// <summary>
    /// The local microphone, as the rest of the game sees it.
    /// </summary>
    /// <remarks>
    /// A port because the screens that switch the microphone on and off live in
    /// <c>Game.Client</c>, which does not reference <c>Game.Network</c> where
    /// the voice SDK sits. What a mute button needs to know is whether it is
    /// muted, not which SDK carries the audio.
    /// <para>
    /// Only the local player is here. Hearing someone else is a property of that
    /// player's avatar, not of this machine's microphone, and the voice SDK
    /// already reports it there.
    /// </para>
    /// </remarks>
    public interface IVoiceControl
    {
        /// <summary>
        /// True once there is a voice room to talk to. False before the session
        /// joins one, which is most of the time spent on the menus.
        /// </summary>
        ReadOnlyReactiveProperty<bool> IsAvailable { get; }

        /// <summary>
        /// Silences this player whatever the talk key is doing. Held here rather
        /// than read back from the recorder so the button stays lit while the
        /// key is up.
        /// </summary>
        ReadOnlyReactiveProperty<bool> IsMuted { get; }

        /// <summary>True while audio is actually leaving this machine.</summary>
        ReadOnlyReactiveProperty<bool> IsTransmitting { get; }

        /// <summary>
        /// Whether this machine plays other people's voice. A silenced
        /// microphone can still hear the room. While the speaker is off the
        /// microphone stays closed and cannot be opened; turning the speaker
        /// back on restores the microphone choice from before it closed.
        /// </summary>
        ReadOnlyReactiveProperty<bool> IsListening { get; }

        void SetMuted(bool muted);

        /// <summary>Reports whether the talk key is held down.</summary>
        void SetTalking(bool talking);

        void SetListening(bool listening);

        /// <summary>
        /// Which microphone to capture from, by the name the 사운드 tab lists,
        /// or empty for whichever one the machine calls default.
        /// </summary>
        /// <remarks>
        /// Here rather than read from the settings by whoever owns the
        /// microphone, for the reason the rest of this port exists: the choice
        /// is made in <c>Game.Client</c> and the capture happens in
        /// <c>Game.Network</c>, and neither sees the other.
        /// <para>
        /// A name, not a device: what a name resolves to depends on which
        /// microphone back end is capturing, and that is the implementer's to
        /// know. An unknown name falls back to the default rather than
        /// silencing the player.
        /// </para>
        /// </remarks>
        void SetCaptureDevice(string deviceName);
    }
}
