using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Client.Character;
using Game.Core.Players;
using Game.Core.Ports;
using UnityEngine;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>
    /// Carries an applied appearance to the account, and puts the old one back
    /// when it does not arrive.
    /// </summary>
    /// <remarks>
    /// Lives with the closet rather than with the application, because the
    /// closet is the only screen that applies one. The presenter does not know
    /// this exists: it settles the appearance and this follows.
    /// <para>
    /// A refused save is undone here rather than left to look successful. The
    /// player's choice stays on screen — the draft is the presenter's and is
    /// not touched — so the buttons come back on and they can try again.
    /// </para>
    /// </remarks>
    internal sealed class ClosetAppearanceSaver : IStartable, IDisposable
    {
        private readonly AvatarAppearanceState appearance;
        private readonly IAccountGateway accounts;
        private readonly ICharacterClosetView view;
        private readonly BackendSignIn signIn;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();

        private AvatarAppearance stored;
        private bool isPuttingBack;

        public ClosetAppearanceSaver(
            AvatarAppearanceState appearance,
            IAccountGateway accounts,
            ICharacterClosetView view,
            BackendSignIn signIn)
        {
            this.appearance = appearance ?? throw new ArgumentNullException(nameof(appearance));
            this.accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.signIn = signIn ?? throw new ArgumentNullException(nameof(signIn));
        }

        public void Start()
        {
            // What the account is believed to hold as of the screen opening.
            // Not necessarily what it holds — a save that failed on an earlier
            // visit put this back too.
            stored = appearance.Current;
            appearance.Changed += OnApplied;
        }

        public void Dispose()
        {
            appearance.Changed -= OnApplied;
            lifetime.Cancel();
            lifetime.Dispose();
        }

        private void OnApplied(AvatarAppearance applied)
        {
            if (isPuttingBack)
            {
                return;
            }

            SaveAsync(applied).Forget(exception => Debug.LogException(exception));
        }

        private async UniTask SaveAsync(AvatarAppearance applied)
        {
            var previous = stored;
            stored = applied;

            var result = await accounts.SetAppearanceAsync(applied, lifetime.Token);
            if (result.Ok)
            {
                // The sign-in snapshot seeds the closet on the next read of it;
                // kept current so it names this appearance rather than the one
                // sign-in saw.
                signIn.Adopt(result.Value);
                return;
            }

            Debug.LogWarning($"[Closet] Could not store the appearance: {result.Failure}.");

            // Guarded, so putting the value back does not read as a second
            // apply and send a second request.
            isPuttingBack = true;
            stored = previous;
            appearance.Apply(previous);
            isPuttingBack = false;

            view.ShowSaveError(CharacterClosetStyle.SaveErrorMessage);
        }
    }
}
