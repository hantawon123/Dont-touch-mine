using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Backend;
using Game.Bootstrap;
using Game.Client.Home;
using Game.Core.Flow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// When Home blocks an old build with the update notice (S15P21D205-1109).
    /// </summary>
    /// <remarks>
    /// The mistake worth guarding is the false alarm: an editor run, a dropped
    /// connection or a host error page must never tell somebody to update. A
    /// missed notice leaves a player where they were before this existed; a
    /// wrong one locks them out of a build that works.
    /// </remarks>
    public sealed class HomeUpdateBridgeTests
    {
        private const string Released = "0123456789abcdef0123456789abcdef01234567";
        private const string Older = "fedcba9876543210fedcba9876543210fedcba98";

        [Test]
        public void Compare_SameRevision_IsCurrent_IgnoringCase()
        {
            Assert.That(
                ClientVersionCheck.Compare(Released, Released.ToUpperInvariant()),
                Is.EqualTo(ClientVersionVerdict.Current));
        }

        [Test]
        public void Compare_DifferentRevision_IsOutdated()
        {
            Assert.That(
                ClientVersionCheck.Compare(Older, Released),
                Is.EqualTo(ClientVersionVerdict.Outdated));
        }

        [TestCase("0.1.0")]
        [TestCase("")]
        [TestCase(null)]
        [TestCase("0123456789abcdef")]
        [TestCase("g123456789abcdef0123456789abcdef01234567")]
        public void Compare_AnythingButAFullSha_IsUnknown(string value)
        {
            // 에디터는 0.1.0 이고 실패한 요청은 null 입니다. 어느 쪽이든 막으면 안 됩니다.
            Assert.That(ClientVersionCheck.Compare(value, Released), Is.EqualTo(ClientVersionVerdict.Unknown));
            Assert.That(ClientVersionCheck.Compare(Released, value), Is.EqualTo(ClientVersionVerdict.Unknown));
        }

        [Test]
        public async Task Gateway_ReadsTheRevision_FromCurrentJson()
        {
            var transport = new FakeTransport(HttpCallResult.Completed(
                200, "{\"revision\":\"" + Released + "\",\"release\":\"" + Released + "\"}"));
            var gateway = new ReleaseInfoGateway(transport, new BackendEndpoint("https://host.test/"));

            var revision = await gateway.FetchReleasedRevisionAsync(CancellationToken.None);

            Assert.That(revision, Is.EqualTo(Released));
            Assert.That(transport.LastCall.Url, Is.EqualTo("https://host.test/play/current.json"));
            Assert.That(transport.LastCall.Method, Is.EqualTo(HttpMethod.Get));
            Assert.That(gateway.DownloadPageUrl, Is.EqualTo("https://host.test/play/"));
        }

        [Test]
        public async Task Gateway_AnswersNull_ForEveryKindOfFailure()
        {
            var failures = new[]
            {
                HttpCallResult.Failed(HttpOutcome.ConnectionFailed),
                HttpCallResult.Failed(HttpOutcome.TimedOut),
                HttpCallResult.Completed(503, "No ready release"),
                HttpCallResult.Completed(200, "<html>nginx</html>"),
                HttpCallResult.Completed(200, "{\"revision\":null,\"release\":null}")
            };

            foreach (var failure in failures)
            {
                var gateway = new ReleaseInfoGateway(
                    new FakeTransport(failure), new BackendEndpoint("https://host.test"));
                Assert.That(
                    await gateway.FetchReleasedRevisionAsync(CancellationToken.None),
                    Is.Null,
                    $"{failure.Outcome} {failure.StatusCode} {failure.Body}");
            }
        }

        [Test]
        public async Task AnOldBuild_GetsTheNotice_WithTheDownloadPage()
        {
            using var home = new BuiltHome();

            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(@"\[Update\]"));
            await CheckAsync(home.View, Older, Released);

            Assert.That(home.View.IsUpdateNoticeVisible, Is.True);
            Assert.That(home.View.UpdateDownloadUrl, Is.EqualTo("https://host.test/play/"));
        }

        [Test]
        public async Task TheReleasedBuild_GetsNothing()
        {
            using var home = new BuiltHome();

            await CheckAsync(home.View, Released, Released);

            Assert.That(home.View.IsUpdateNoticeVisible, Is.False);
        }

        [Test]
        public async Task AnEditorRun_GetsNothing_AndDoesNotEvenAsk()
        {
            using var home = new BuiltHome();
            var transport = new FakeTransport(Answer(Released));

            await new HomeUpdateBridge(
                    home.View,
                    new ReleaseInfoGateway(transport, new BackendEndpoint("https://host.test")),
                    "0.1.0")
                .CheckAsync(CancellationToken.None);

            Assert.That(home.View.IsUpdateNoticeVisible, Is.False);
            Assert.That(transport.Calls, Is.Zero);
        }

        [Test]
        public async Task AHostThatDoesNotAnswer_GetsNothing()
        {
            using var home = new BuiltHome();
            var transport = new FakeTransport(HttpCallResult.Failed(HttpOutcome.ConnectionFailed));

            await new HomeUpdateBridge(
                    home.View,
                    new ReleaseInfoGateway(transport, new BackendEndpoint("https://host.test")),
                    Older)
                .CheckAsync(CancellationToken.None);

            Assert.That(home.View.IsUpdateNoticeVisible, Is.False);
        }

        [Test]
        public void ItRefusesToBeBuiltWithoutItsParts()
        {
            Assert.That(
                () => new HomeUpdateBridge(null, null, Released),
                Throws.InstanceOf<ArgumentNullException>());
        }

        private static UniTask CheckAsync(HomeMenuView view, string local, string released)
        {
            var gateway = new ReleaseInfoGateway(
                new FakeTransport(Answer(released)), new BackendEndpoint("https://host.test"));
            return new HomeUpdateBridge(view, gateway, local).CheckAsync(CancellationToken.None);
        }

        private static HttpCallResult Answer(string revision) =>
            HttpCallResult.Completed(200, "{\"revision\":\"" + revision + "\",\"release\":\"" + revision + "\"}");

        private sealed class FakeTransport : IHttpTransport
        {
            private readonly HttpCallResult answer;

            public FakeTransport(HttpCallResult answer)
            {
                this.answer = answer;
            }

            public int Calls { get; private set; }

            public HttpCall LastCall { get; private set; }

            public UniTask<HttpCallResult> SendAsync(HttpCall call, CancellationToken cancellation)
            {
                Calls++;
                LastCall = call;
                return UniTask.FromResult(answer);
            }
        }

        /// <summary>
        /// The real view, built the way HomeSuspensionBridgeTests builds it.
        /// </summary>
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
    }
}
