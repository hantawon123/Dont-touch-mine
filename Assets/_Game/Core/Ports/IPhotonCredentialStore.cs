namespace Game.Core.Ports
{
    /// <summary>
    /// Remembers the id and token this machine last signed in with, so a launch
    /// that cannot reach the backend can still connect to Photon
    /// (S15P21D205-925).
    /// </summary>
    /// <remarks>
    /// Without this, a player who opens the game while the backend is down has
    /// no token, connects to Photon anonymously, and is refused — the dashboard
    /// turns anonymous clients away so that a suspended player cannot get in by
    /// simply not sending one. That would stop people who were never suspended
    /// from playing every time the server restarts.
    /// <para>
    /// <b>Keeping the token does not weaken the block.</b> It says who this
    /// player is, not that they are allowed in; whether they are suspended is
    /// decided by the server on every connection. An old token belonging to a
    /// suspended account is refused exactly like a fresh one.
    /// </para>
    /// <para>
    /// The pair is saved and loaded together. A token without its id names
    /// nobody, and an id without its token is what we were trying to avoid.
    /// </para>
    /// </remarks>
    public interface IPhotonCredentialStore
    {
        /// <summary>
        /// The last pair saved, if there is one. False leaves both null.
        /// </summary>
        bool TryLoad(out string userId, out string photonToken);

        /// <summary>
        /// Keeps the pair for later launches. A blank half is ignored.
        /// </summary>
        void Save(string userId, string photonToken);

        /// <summary>
        /// Forgets the pair. Used when the account is gone, so the next launch
        /// does not connect as somebody who no longer exists.
        /// </summary>
        void Clear();
    }
}
