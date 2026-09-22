namespace Game.Core.Ports
{
    /// <summary>
    /// Remembers this machine's first-person / third-person choice.
    /// </summary>
    /// <remarks>
    /// A choice about this computer rather than the account: the same player
    /// on another machine may well want the other view. Nothing here is sent
    /// anywhere.
    /// </remarks>
    public interface ICameraViewStore
    {
        /// <summary>
        /// Reads what was saved. False when nothing has been saved here, which
        /// is the signal to start from third person rather than an error.
        /// </summary>
        bool TryLoad(out bool firstPerson);

        void Save(bool firstPerson);
    }
}
