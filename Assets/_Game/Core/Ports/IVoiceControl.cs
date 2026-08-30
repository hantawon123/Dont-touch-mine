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

        void SetMuted(bool muted);

        /// <summary>Reports whether the talk key is held down.</summary>
        void SetTalking(bool talking);
    }
}
