namespace Game.Core.Ports
{
    /// <summary>
    /// Remembers which Photon region this machine last played on.
    /// </summary>
    /// <remarks>
    /// A choice about this computer rather than about the account, so it is
    /// stored beside the profile rather than sent anywhere: the same player on
    /// a different machine is in a different place.
    /// </remarks>
    public interface IServerRegionStore
    {
        /// <summary>
        /// Reads what was saved. False when nothing has been saved here, which
        /// is the signal to fall back to the default rather than an error.
        /// </summary>
        bool TryLoad(out string code);

        void Save(string code);
    }
}
