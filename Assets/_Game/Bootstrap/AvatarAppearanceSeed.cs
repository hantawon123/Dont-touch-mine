using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core.Players;
using UnityEngine;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>
    /// Puts the appearance the account remembers back on at launch.
    /// </summary>
    /// <remarks>
    /// Read out of the sign-in reply rather than asked for: the account
    /// response already carries the appearance, so this costs no request. The
    /// part ids are not checked here — the closet runs them through its
    /// catalogue, which is the only thing that knows what can be worn.
    /// </remarks>
    internal sealed class AvatarAppearanceSeed : IStartable, IDisposable
    {
        private readonly BackendSignIn signIn;
        private readonly AvatarAppearanceState appearance;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();

        public AvatarAppearanceSeed(BackendSignIn signIn, AvatarAppearanceState appearance)
        {
            this.signIn = signIn ?? throw new ArgumentNullException(nameof(signIn));
            this.appearance = appearance ?? throw new ArgumentNullException(nameof(appearance));
        }

        public void Start()
        {
            SeedAsync().Forget(exception =>
            {
                if (exception is OperationCanceledException && lifetime.IsCancellationRequested) return;
                Debug.LogException(exception);
            });
        }

        public void Dispose()
        {
            lifetime.Cancel();
            lifetime.Dispose();
        }

        private async UniTask SeedAsync()
        {
            if (!await signIn.Ready.AttachExternalCancellation(lifetime.Token))
            {
                // No account this launch. The closet opens on its catalogue's
                // default, which is what a first run looks like anyway.
                return;
            }

            var account = signIn.Account;
            if (account.HasValue && account.Value.AppearanceSet)
            {
                appearance.Apply(account.Value.Appearance);
            }
        }
    }
}
