namespace Game.Core.Ports
{
    /// <summary>
    /// Remembers this machine's microphone and speaker on/off.
    /// </summary>
    /// <remarks>
    /// A choice about this computer rather than the account: the same player
    /// on another machine may well want the microphone open. Nothing here is
    /// sent anywhere.
    /// </remarks>
    public interface IVoicePreferencesStore
    {
        /// <summary>
        /// Reads what was saved. False when nothing has been saved here, which
        /// is the signal to start from the defaults rather than an error.
        /// </summary>
        bool TryLoad(out bool muted, out bool listening);

        void Save(bool muted, bool listening);
    }
}
