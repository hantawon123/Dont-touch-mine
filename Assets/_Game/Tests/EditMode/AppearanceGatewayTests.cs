using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Backend;
using Game.Core.Backend;
using Game.Core.Players;
using Game.Core.Ports;
using NUnit.Framework;

namespace Game.Architecture.Tests
{
    /// <summary>
    /// What the account gateway puts on the wire for an appearance, and what it
    /// makes of the answer.
    /// </summary>
    /// <remarks>
    /// The field names are a contract with a server that cannot tell us it
    /// stopped recognising them: a mistyped field is simply absent, and an
    /// absent part is a 400 the player reads as "it did not save".
    /// </remarks>
    public sealed class AppearanceGatewayTests
    {
        private const string UserId = "user-1";

        private static readonly AvatarAppearance Worn = new AvatarAppearance(
            "body_black", "hood_bear_purple", "shoes_pink", "face_smile");

        [Test]
        public async Task Storing_PutsTheFourPartsAsTheCaller()
        {
            var transport = new FakeTransport();
            transport.Answer(200, Account(appearanceSet: true));
            var accounts = new AccountGateway(SignedIn(transport));

            var stored = await accounts.SetAppearanceAsync(Worn, CancellationToken.None);

            Assert.That(stored.Ok, Is.True);
            Assert.That(transport.LastCall.Method, Is.EqualTo(HttpMethod.Put));
            Assert.That(
                transport.LastCall.Url,
                Does.EndWith("/api/v1/accounts/me/appearance"));
            Assert.That(Header(transport.LastCall, "X-User-Id"), Is.EqualTo(UserId));
        }

        [Test]
        public async Task TheBody_CarriesTheFieldNamesTheServerReads()
        {
            var transport = new FakeTransport();
            transport.Answer(200, Account(appearanceSet: true));
            var accounts = new AccountGateway(SignedIn(transport));

            await accounts.SetAppearanceAsync(Worn, CancellationToken.None);

            var body = transport.LastCall.JsonBody;
            Assert.That(body, Does.Contain("\"bodyColor\":\"body_black\""));
            Assert.That(body, Does.Contain("\"hood\":\"hood_bear_purple\""));
            Assert.That(body, Does.Contain("\"shoes\":\"shoes_pink\""));
            Assert.That(body, Does.Contain("\"face\":\"face_smile\""));
        }

        [Test]
        public async Task TheAnswer_CarriesBackWhatIsNowWorn()
        {
            var transport = new FakeTransport();
            transport.Answer(200, Account(appearanceSet: true));
            var accounts = new AccountGateway(SignedIn(transport));

            var stored = await accounts.SetAppearanceAsync(Worn, CancellationToken.None);

            Assert.That(stored.Value.AppearanceSet, Is.True);
            Assert.That(stored.Value.Appearance, Is.EqualTo(Worn));
        }

        /// <summary>
        /// The reason the flag exists: JsonUtility reads a null object as an
        /// object of empty fields, so an account that has never applied an
        /// appearance would otherwise come back wearing four empty ids.
        /// </summary>
        [Test]
        public async Task AnAccountThatNeverApplied_ComesBackWearingNothing()
        {
            var transport = new FakeTransport();
            transport.Answer(200, Account(appearanceSet: false));
            var accounts = new AccountGateway(SignedIn(transport));

            var signedIn = await accounts.RefreshAsync(CancellationToken.None);

            Assert.That(signedIn.Value.AppearanceSet, Is.False);
            Assert.That(signedIn.Value.Appearance, Is.EqualTo(AvatarAppearance.Default));
        }

        [Test]
        public async Task Clearing_DeletesTheSamePath()
        {
            var transport = new FakeTransport();
            transport.Answer(200, Account(appearanceSet: false));
            var accounts = new AccountGateway(SignedIn(transport));

            var cleared = await accounts.ClearAppearanceAsync(CancellationToken.None);

            Assert.That(cleared.Ok, Is.True);
            Assert.That(transport.LastCall.Method, Is.EqualTo(HttpMethod.Delete));
            Assert.That(
                transport.LastCall.Url,
                Does.EndWith("/api/v1/accounts/me/appearance"));
            Assert.That(cleared.Value.AppearanceSet, Is.False);
        }

        [Test]
        public async Task ARefusedPart_IsAFailedCallAndNotAnException()
        {
            var transport = new FakeTransport();
            transport.Answer(400, "{\"code\":\"INVALID_REQUEST\",\"message\":\"...\"}");
            var accounts = new AccountGateway(SignedIn(transport));

            var stored = await accounts.SetAppearanceAsync(
                new AvatarAppearance("몸 색상", "hood", "shoes", "face"),
                CancellationToken.None);

            Assert.That(stored.Ok, Is.False);
            Assert.That(stored.Failure, Is.EqualTo(BackendFailure.InvalidRequest));
        }

        private static string Account(bool appearanceSet)
        {
            var appearance = appearanceSet
                ? "{\"bodyColor\":\"body_black\",\"hood\":\"hood_bear_purple\"," +
                  "\"shoes\":\"shoes_pink\",\"face\":\"face_smile\"}"
                : "null";

            return "{\"userId\":\"" + UserId + "\",\"nickname\":\"나\"," +
                   "\"nicknameSet\":true,\"searchable\":true," +
                   "\"appearanceSet\":" + (appearanceSet ? "true" : "false") + "," +
                   "\"appearance\":" + appearance + "}";
        }

        private static BackendClient SignedIn(IHttpTransport transport)
        {
            var session = new BackendSession("device-under-test");
            session.Adopt(UserId);
            return new BackendClient(
                transport, new BackendEndpoint("http://localhost:8080"), session);
        }

        private static string Header(HttpCall call, string name)
        {
            for (var index = 0; index < call.Headers.Count; index++)
            {
                if (call.Headers[index].Name == name)
                {
                    return call.Headers[index].Value;
                }
            }

            return null;
        }

        private sealed class FakeTransport : IHttpTransport
        {
            private HttpCallResult next = HttpCallResult.Completed(200, "{}");

            public List<HttpCall> Calls { get; } = new List<HttpCall>();

            public HttpCall LastCall => Calls[Calls.Count - 1];

            public void Answer(long statusCode, string body)
            {
                next = HttpCallResult.Completed(statusCode, body);
            }

            public UniTask<HttpCallResult> SendAsync(HttpCall call, CancellationToken cancellation)
            {
                Calls.Add(call);
                return UniTask.FromResult(next);
            }
        }
    }
}
