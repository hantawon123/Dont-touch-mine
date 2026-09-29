using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Bootstrap;
using Game.Client.Home;
using Game.Core.Backend;
using Game.Core.Home;
using Game.Core.Players;
using Game.Core.Ports;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// When the home screen says the account is suspended (S15P21D205-924).
    /// </summary>
    /// <remarks>
    /// The distinction this guards is the one worth getting wrong only once:
    /// signing in fails for suspension and for a dropped connection alike, and
    /// the two land in the same branch. Telling somebody whose wifi went away
    /// that they had been banned is worse than saying nothing at all.
    /// </remarks>
    public sealed class HomeSuspensionBridgeTests
    {
        [Test]
        public async Task ASuspendedAccount_GetsTheNotice()
        {
            using var home = new BuiltHome();

            await StartAsync(home.View, BackendFailure.Suspended);

            Assert.That(home.View.IsSuspendedNoticeVisible, Is.True);
        }

        [Test]
        public async Task AHealthyAccount_GetsNothing()
        {
            using var home = new BuiltHome();

            await StartAsync(home.View, BackendFailure.None);

            Assert.That(home.View.IsSuspendedNoticeVisible, Is.False);
        }

        [Test]
        public async Task BeingOffline_IsNotBeingSuspended()
        {
            // 정지와 오프라인은 같은 실패 분기로 떨어집니다. 여기서 가르지 않으면
            // 와이파이가 끊긴 사람에게 정지됐다고 말하게 됩니다.
            using var home = new BuiltHome();

            await StartAsync(home.View, BackendFailure.Offline);

            Assert.That(home.View.IsSuspendedNoticeVisible, Is.False);
        }

        [Test]
        public async Task ATimeout_IsNotBeingSuspended()
        {
            using var home = new BuiltHome();

            await StartAsync(home.View, BackendFailure.Timeout);

            Assert.That(home.View.IsSuspendedNoticeVisible, Is.False);
        }

        [Test]
        public void ItRefusesToBeBuiltWithoutItsParts()
        {
            Assert.That(
                () => new HomeSuspensionBridge(null, null),
                Throws.InstanceOf<ArgumentNullException>());
        }

        private static async UniTask StartAsync(HomeMenuView view, BackendFailure failure)
        {
            var accounts = new FakeAccounts(failure);
            var profile = new PlayerProfile("이름");
            var signIn = new BackendSignIn(accounts, profile);

            if (failure != BackendFailure.None)
            {
                LogAssert.Expect(
                    LogType.Warning,
                    new System.Text.RegularExpressions.Regex("Could not sign in"));
            }

            await signIn.StartAsync(CancellationToken.None);

            new HomeSuspensionBridge(view, signIn).Start();

            // 브리지가 Ready 를 기다렸다가 화면을 건드리므로 한 번 넘겨줍니다.
            await UniTask.Yield();
        }

        /// <summary>
        /// The real view, built the way HomeMenuLayoutTests builds it.
        /// </summary>
        /// <remarks>
        /// A hand-written fake would have to implement every member of
        /// IHomeMenuView to answer one question, and the first attempt at that
        /// compiled against Assembly-CSharp while failing in Unity - the test
        /// assembly is not part of that project. The real view also checks the
        /// thing the bridge is for: that the notice actually goes up.
        /// </remarks>
        private sealed class BuiltHome : IDisposable
        {
            private readonly GameObject root;

            public BuiltHome()
            {
                root = new GameObject("HomeMenuViewUnderTest");
                View = root.AddComponent<HomeMenuView>();

                var build = typeof(HomeMenuView).GetMethod(
                    "BuildLayout",
                    System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.NonPublic);
                Assert.That(build, Is.Not.Null, "HomeMenuView.BuildLayout is gone.");
                build.Invoke(View, null);
            }

            public HomeMenuView View { get; }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private sealed class FakeAccounts : IAccountGateway
        {
            private readonly BackendFailure failure;

            public FakeAccounts(BackendFailure failure)
            {
                this.failure = failure;
            }

            public UniTask<BackendResult<AccountSnapshot>> SignInAsync(
                CancellationToken cancellation)
            {
                return UniTask.FromResult(
                    failure == BackendFailure.None
                        ? BackendResult<AccountSnapshot>.Success(
                            new AccountSnapshot("user-1", "이름", true, true))
                        : BackendResult<AccountSnapshot>.Failed(failure));
            }

            public UniTask<BackendResult<AccountSnapshot>> RefreshAsync(
                CancellationToken cancellation) =>
                SignInAsync(cancellation);

            public UniTask<BackendResult<AccountSnapshot>> RenameAsync(
                string nickname, CancellationToken cancellation) =>
                SignInAsync(cancellation);

            public UniTask<BackendResult<AccountSnapshot>> SetSearchableAsync(
                bool searchable, CancellationToken cancellation) =>
                SignInAsync(cancellation);

            public UniTask<BackendResult<AccountSnapshot>> SetAppearanceAsync(
                AvatarAppearance appearance, CancellationToken cancellation) =>
                SignInAsync(cancellation);

            public UniTask<BackendResult<AccountSnapshot>> ClearAppearanceAsync(
                CancellationToken cancellation) =>
                SignInAsync(cancellation);

            public UniTask<BackendResult> DeleteAccountAsync(CancellationToken cancellation) =>
                UniTask.FromResult(BackendResult.Success());
        }

    }
}
