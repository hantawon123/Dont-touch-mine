using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Core.Ports;
using Game.Network.Session;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// The Photon connection waits for signing in before it reads the
    /// credentials it sends (S15P21D205-928).
    /// </summary>
    /// <remarks>
    /// S15P21D205-925 put the credentials on all three connections and its tests
    /// passed, because they only asked what the values look like once the
    /// profile holds them. Nothing asked whether the profile holds them yet at
    /// the moment a connection is made - and it did not: the room browser warms
    /// the lobby up the instant the home screen appears, while signing in is
    /// still in flight. Photon then refused the connection outright.
    /// <para>
    /// So this pins the gate itself rather than the shape of the values. The
    /// method is private and reached by reflection, the way HomeMenuLayoutTests
    /// reaches BuildLayout; going through the real JoinLobbyAsync would need a
    /// Photon server to answer.
    /// </para>
    /// </remarks>
    public sealed class PhotonAuthWaitsForSignInTests
    {
        [Test]
        public async Task ItWaitsUntilSigningInHasAnswered()
        {
            var account = new PendingAccount();
            var wait = WaitFor(account);

            // 아직 로그인이 답하지 않았습니다. 여기서 통과해 버리면 토큰 없이
            // 접속하게 되고, 그것이 이 버그였습니다.
            await UniTask.Yield();
            Assert.That(wait.Status.IsCompleted(), Is.False, "로그인을 기다리지 않았습니다.");

            account.Answer(true);
            await wait;
        }

        [Test]
        public async Task ItCarriesOnWhenThereIsNoAccount()
        {
            // 오프라인이나 타임아웃입니다. 여기서 계속 기다리면 백엔드 장애가 곧
            // 게임 접속 불가가 됩니다. fail-open 결정과 어긋납니다.
            var account = new PendingAccount();
            var wait = WaitFor(account);

            account.Answer(false);
            await wait;

            Assert.Pass();
        }

        [Test]
        public async Task WithNoSignInAtAll_ItDoesNotWait()
        {
            // 포트를 주지 않고 만든 경우입니다. 기다릴 대상이 없으므로 바로 지나가야
            // 하고, 그러지 않으면 이 서비스를 손으로 만드는 기존 테스트들이 멈춥니다.
            await WaitFor(null);

            Assert.Pass();
        }

        [Test]
        public async Task ACancelledConnectionStopsWaiting()
        {
            // 화면을 떠나면 접속 시도가 취소됩니다. 답하지 않는 로그인을 붙들고
            // 있으면 그 취소가 먹히지 않습니다.
            var account = new PendingAccount();
            using var cancellation = new CancellationTokenSource();
            var wait = WaitFor(account, cancellation.Token);

            cancellation.Cancel();
            await wait;

            Assert.Pass();
        }

        private static UniTask WaitFor(IAccountReady account, CancellationToken cancellation = default)
        {
            var service = new NetworkRunnerService(
                null, null, null, null, null, null, null, null, null, account);

            var method = typeof(NetworkRunnerService).GetMethod(
                "WaitForAccountAsync",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "NetworkRunnerService.WaitForAccountAsync is gone.");

            return (UniTask)method.Invoke(service, new object[] { cancellation });
        }

        /// <summary>Signing in that has not answered until it is told to.</summary>
        private sealed class PendingAccount : IAccountReady
        {
            private readonly UniTaskCompletionSource<bool> source =
                new UniTaskCompletionSource<bool>();

            public UniTask<bool> Ready => source.Task;

            public void Answer(bool signedIn) => source.TrySetResult(signedIn);
        }
    }
}
