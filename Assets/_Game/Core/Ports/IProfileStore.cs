namespace Game.Core.Ports
{
    /// <summary>
    /// Where this machine keeps the local player's profile between runs.
    /// </summary>
    /// <remarks>
    /// A port rather than a concrete store because <c>Game.Core</c> has no engine
    /// reference and cannot reach Unity's preferences. It is also the seam a
    /// Steam or backend profile replaces later: the rest of the game asks for a
    /// nickname, not for where it was saved.
    /// </remarks>
    public interface IProfileStore
    {
        /// <summary>
        /// Reads what was saved. False when this machine has never saved a
        /// profile, which is the signal to use a first-run default rather than to
        /// treat the absence as an error.
        /// </summary>
        bool TryLoad(out string nickname, out int level);

        /// <summary>Records the profile so the next run starts with it.</summary>
        void Save(string nickname, int level);
    }
}
