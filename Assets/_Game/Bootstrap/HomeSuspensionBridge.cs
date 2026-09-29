using System;
using Cysharp.Threading.Tasks;
using Game.Client.Home;
using Game.Core.Backend;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>
    /// Puts the suspended notice on the home screen when sign-in says the
    /// account is suspended (S15P21D205-924).
    /// </summary>
    /// <remarks>
    /// A bridge rather than something the presenter does, for the same reason
    /// <see cref="HomeProfileBridge"/> is one: the presenter lives in
    /// Game.Client and <see cref="BackendSignIn"/> in Game.Bootstrap, and the
    /// dependency runs that way round only. The bridge is the seam that already
    /// exists for exactly this.
    /// <para>
    /// Until this, a suspended player reached home and watched every button fail
    /// in silence - the friend list empty, room creation refused, invites going
    /// nowhere - with nothing anywhere saying why.
    /// </para>
    /// </remarks>
    public sealed class HomeSuspensionBridge : IStartable
    {
        private readonly IHomeMenuView view;
        private readonly BackendSignIn signIn;

        public HomeSuspensionBridge(IHomeMenuView view, BackendSignIn signIn)
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.signIn = signIn ?? throw new ArgumentNullException(nameof(signIn));
        }

        public void Start()
        {
            ShowIfSuspendedAsync().Forget();
        }

        /// <remarks>
        /// <b>Only for Suspended.</b> Offline and timeout land in the same failed
        /// branch of sign-in and mean something else entirely: the player can
        /// keep going and the network may come back. Showing this for them would
        /// tell someone whose wifi dropped that they had been banned.
        /// <para>
        /// Nothing hides it again. Lifting a suspension takes the server's side
        /// and a fresh sign-in, which is the next launch - and leaving a notice
        /// that clears itself would suggest the account had recovered while every
        /// button behind it was still refused.
        /// </para>
        /// </remarks>
        private async UniTaskVoid ShowIfSuspendedAsync()
        {
            // Sign-in runs at startup and this screen opens right after, so the
            // answer is usually already in. Awaiting covers the case where it is
            // not, without holding the screen back.
            await signIn.Ready;

            if (signIn.Failure == BackendFailure.Suspended)
            {
                view.SetSuspendedNoticeVisible(true);
            }
        }
    }
}
