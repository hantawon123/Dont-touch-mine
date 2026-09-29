using Cysharp.Threading.Tasks;

namespace Game.Core.Ports
{
    /// <summary>
    /// Answers when signing in has finished, so the code that needs what it
    /// produced can wait for it (S15P21D205-928).
    /// </summary>
    /// <remarks>
    /// Exists for the Photon connection. The credentials it sends come from the
    /// account, and connecting before the account arrives sends nothing at all -
    /// which Photon refuses outright once anonymous clients are turned off.
    /// <para>
    /// A port rather than a reference to the sign-in itself, because the code
    /// that connects lives in Game.Network and sign-in lives in Game.Bootstrap.
    /// The dependency runs one way only.
    /// </para>
    /// <para>
    /// <b>It always answers.</b> False means there is no account - offline, a
    /// timeout, a suspended account - and the caller carries on without one.
    /// Waiting forever would turn a backend outage into nobody being able to
    /// play, and the game itself does not need the backend.
    /// </para>
    /// </remarks>
    public interface IAccountReady
    {
        /// <summary>
        /// Completes with whether there is an account. Awaiting it more than
        /// once is fine, and awaiting it after it finished returns immediately.
        /// </summary>
        UniTask<bool> Ready { get; }
    }
}
