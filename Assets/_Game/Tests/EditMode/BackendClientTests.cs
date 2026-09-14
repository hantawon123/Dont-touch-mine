using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Backend;
using Game.Core.Backend;
using Game.Core.Home;
using Game.Core.Ports;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Architecture.Tests
{
    /// <summary>
    /// Drives the backend layer through a fake transport, so what it sends and
    /// what it makes of an answer are checked without a server.
    /// </summary>
    public sealed class BackendClientTests
    {
        private const string DeviceId = "device-under-test";
        private const string UserId = "user-1";
        private const string AccountToken = "signed-token-1";

        [Test]
        public async Task SignIn_SendsDeviceIdWithoutIdentifyingHeader()
        {
            var transport = new FakeTransport();
            transport.Answer(200, "{\"userId\":\"user-1\",\"nickname\":\"이름\",\"nicknameSet\":true}");
            var accounts = new AccountGateway(Client(transport, out _));

            var result = await accounts.SignInAsync(CancellationToken.None);

            Assert.That(result.Ok, Is.True);
            Assert.That(result.Value.UserId, Is.EqualTo(UserId));
            Assert.That(result.Value.NicknameSet, Is.True);
            Assert.That(transport.LastCall.JsonBody, Does.Contain(DeviceId));
            Assert.That(Header(transport.LastCall, "X-User-Id"), Is.Null);
        }

        [Test]
        public async Task SignIn_AdoptsTheAccountSoLaterCallsIdentify()
        {
            var transport = new FakeTransport();
            transport.Answer(201, "{\"userId\":\"user-1\",\"nickname\":\"이름\",\"nicknameSet\":false,"
                + "\"photonToken\":\"" + AccountToken + "\"}");
            var client = Client(transport, out _);
            await new AccountGateway(client).SignInAsync(CancellationToken.None);

            transport.Answer(200, "{\"friends\":[]}");
            await new FriendGateway(client).ListFriendsAsync(CancellationToken.None);

            Assert.That(Header(transport.LastCall, "X-User-Id"), Is.EqualTo(UserId));

            // The id is public, so the server's signature over it goes with it.
            Assert.That(Header(transport.LastCall, "X-Account-Token"), Is.EqualTo(AccountToken));

            // The device credential does not. Only what cannot be undone asks for it.
            Assert.That(Header(transport.LastCall, "X-Device-Id"), Is.Null);
        }

        [Test]
        public async Task AServerThatIssuedNoToken_GetsNoTokenHeader()
        {
            // A server without a signing secret leaves photonToken out and checks
            // nothing. An empty header would only be one more thing to ignore.
            var transport = new FakeTransport();
            transport.Answer(201, "{\"userId\":\"user-1\",\"nickname\":\"이름\",\"nicknameSet\":false}");
            var client = Client(transport, out _);
            await new AccountGateway(client).SignInAsync(CancellationToken.None);

            transport.Answer(200, "{\"friends\":[]}");
            await new FriendGateway(client).ListFriendsAsync(CancellationToken.None);

            Assert.That(Header(transport.LastCall, "X-User-Id"), Is.EqualTo(UserId));
            Assert.That(Header(transport.LastCall, "X-Account-Token"), Is.Null);
        }

        [Test]
        public async Task TheTokenNeverTravelsInTheUrl()
        {
            var transport = new FakeTransport();
            var client = SignedIn(transport, out _);

            transport.Answer(200, "{\"friends\":[]}");
            await new FriendGateway(client).ListFriendsAsync(CancellationToken.None);

            // nginx writes query strings to its access log.
            Assert.That(transport.LastCall.Url, Does.Not.Contain(AccountToken));
        }

        [Test]
        public async Task CallsNeedingAnAccount_AreRefusedBeforeBeingSent()
        {
            var transport = new FakeTransport();
            var friends = new FriendGateway(Client(transport, out _));

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("needs an account"));
            var result = await friends.ListFriendsAsync(CancellationToken.None);

            Assert.That(result.Failure, Is.EqualTo(BackendFailure.NotSignedIn));
            Assert.That(transport.Calls, Is.Empty);
        }

        [Test]
        public async Task DeletingAnAccount_SendsTheCredentialAndForgetsTheAccount()
        {
            var transport = new FakeTransport();
            var client = SignedIn(transport, out var session);

            transport.Answer(204, string.Empty);
            var result = await new AccountGateway(client).DeleteAccountAsync(CancellationToken.None);

            Assert.That(result.Ok, Is.True);
            Assert.That(Header(transport.LastCall, "X-Device-Id"), Is.EqualTo(DeviceId));
            Assert.That(Header(transport.LastCall, "X-Account-Token"), Is.EqualTo(AccountToken));
            Assert.That(session.SignedIn, Is.False);
        }

        [Test]
        public async Task TheCredentialNeverTravelsInTheUrl()
        {
            var transport = new FakeTransport();
            var client = SignedIn(transport, out _);

            transport.Answer(204, string.Empty);
            await new AccountGateway(client).DeleteAccountAsync(CancellationToken.None);

            // nginx writes query strings to its access log.
            Assert.That(transport.LastCall.Url, Does.Not.Contain(DeviceId));
        }

        [Test]
        public async Task ErrorCodesDecide_NotStatusCodes()
        {
            // Both are 404 and they mean opposite things: one says this player
            // must issue an account again, the other says show a message.
            Assert.That(
                await FailureOf(404, "{\"code\":\"ACCOUNT_NOT_FOUND\",\"message\":\"x\"}"),
                Is.EqualTo(BackendFailure.AccountNotFound));
            Assert.That(
                await FailureOf(404, "{\"code\":\"TARGET_NOT_FOUND\",\"message\":\"x\"}"),
                Is.EqualTo(BackendFailure.TargetNotFound));
        }

        [Test]
        public async Task EveryErrorCodeTheServerCanSend_IsRecognised()
        {
            var codes = new Dictionary<string, BackendFailure>
            {
                { "MISSING_HEADER", BackendFailure.MissingHeader },
                { "INVALID_REQUEST", BackendFailure.InvalidRequest },
                { "SELF_FRIEND_REQUEST", BackendFailure.SelfRequest },
                { "SELF_BLOCK", BackendFailure.SelfRequest },
                { "ACCOUNT_NOT_FOUND", BackendFailure.AccountNotFound },
                { "TARGET_NOT_FOUND", BackendFailure.TargetNotFound },
                { "FRIEND_REQUEST_NOT_FOUND", BackendFailure.RequestNotFound },
                { "NOT_FRIENDS", BackendFailure.NotFriends },
                { "NICKNAME_TAKEN", BackendFailure.NicknameTaken },
                { "ALREADY_FRIENDS", BackendFailure.AlreadyFriends },
                { "REQUEST_ALREADY_SENT", BackendFailure.RequestAlreadySent },
                { "CONFLICT", BackendFailure.Conflict },
                { "NICKNAME_GENERATION_FAILED", BackendFailure.ServerError }
            };

            foreach (var pair in codes)
            {
                var failure = await FailureOf(400, "{\"code\":\"" + pair.Key + "\"}");
                Assert.That(failure, Is.EqualTo(pair.Value), pair.Key);
            }
        }

        [Test]
        public async Task AnUnreadableErrorIsNotGuessedAt()
        {
            // A 404 from a mistyped path carries no code of the server's. Reading
            // it as "that user does not exist" would put a false sentence on the
            // screen.
            Assert.That(
                await FailureOf(404, "<html>Not Found</html>"),
                Is.EqualTo(BackendFailure.Unknown));
            Assert.That(
                await FailureOf(500, string.Empty),
                Is.EqualTo(BackendFailure.ServerError));
        }

        [Test]
        public async Task UnreachableAndSlowAreToldApart()
        {
            Assert.That(
                await FailureOf(HttpOutcome.ConnectionFailed), Is.EqualTo(BackendFailure.Offline));
            Assert.That(
                await FailureOf(HttpOutcome.TimedOut), Is.EqualTo(BackendFailure.Timeout));
            Assert.That(
                await FailureOf(HttpOutcome.Cancelled), Is.EqualTo(BackendFailure.Cancelled));
        }

        [Test]
        public async Task PresenceNamesAreMappedByString()
        {
            var transport = new FakeTransport();
            var client = SignedIn(transport, out _);
            transport.Answer(200, "{\"friends\":["
                + "{\"userId\":\"a\",\"nickname\":\"가\",\"presence\":\"ONLINE\"},"
                + "{\"userId\":\"b\",\"nickname\":\"나\",\"presence\":\"IN_GAME\"},"
                + "{\"userId\":\"c\",\"nickname\":\"다\",\"presence\":\"OFFLINE\"}]}");

            var result = await new FriendGateway(client).ListFriendsAsync(CancellationToken.None);

            Assert.That(result.Ok, Is.True);
            Assert.That(result.Value[0].Presence, Is.EqualTo(FriendPresence.Online));
            Assert.That(result.Value[1].Presence, Is.EqualTo(FriendPresence.InGame));
            Assert.That(result.Value[2].Presence, Is.EqualTo(FriendPresence.Offline));
        }

        [Test]
        public async Task AnUnknownPresenceReadsAsOfflineRatherThanFailing()
        {
            var transport = new FakeTransport();
            var client = SignedIn(transport, out _);
            transport.Answer(200,
                "{\"friends\":[{\"userId\":\"a\",\"nickname\":\"가\",\"presence\":\"AWAY\"}]}");

            var result = await new FriendGateway(client).ListFriendsAsync(CancellationToken.None);

            Assert.That(result.Ok, Is.True);
            Assert.That(result.Value[0].Presence, Is.EqualTo(FriendPresence.Offline));
        }

        [Test]
        public async Task OneMalformedRowDoesNotDiscardTheRest()
        {
            var transport = new FakeTransport();
            var client = SignedIn(transport, out _);
            transport.Answer(200, "{\"friends\":["
                + "{\"userId\":\"\",\"nickname\":\"\",\"presence\":\"ONLINE\"},"
                + "{\"userId\":\"b\",\"nickname\":\"나\",\"presence\":\"ONLINE\"}]}");

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Skipped"));
            var result = await new FriendGateway(client).ListFriendsAsync(CancellationToken.None);

            Assert.That(result.Ok, Is.True);
            Assert.That(result.Value.Count, Is.EqualTo(1));
            Assert.That(result.Value[0].PlayerId, Is.EqualTo("b"));
        }

        [Test]
        public async Task RequestTimesAreReadAsUtc()
        {
            var transport = new FakeTransport();
            var client = SignedIn(transport, out _);
            transport.Answer(200, "{\"requests\":[{\"userId\":\"a\",\"nickname\":\"가\","
                + "\"requestedAt\":\"20260903142530\"}]}");

            var result = await new FriendGateway(client)
                .ListIncomingRequestsAsync(CancellationToken.None);

            Assert.That(result.Ok, Is.True);

            var requestedAt = result.Value[0].RequestedAtUtc;
            Assert.That(requestedAt.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(requestedAt, Is.EqualTo(new DateTime(2026, 9, 3, 14, 25, 30, DateTimeKind.Utc)));
        }

        [Test]
        public async Task AcceptedMeansFriendsAlready_NotPending()
        {
            var transport = new FakeTransport();
            var client = SignedIn(transport, out _);

            transport.Answer(201, "{\"status\":\"PENDING\"}");
            var pending = await new FriendGateway(client)
                .SendRequestAsync("other", CancellationToken.None);

            transport.Answer(200, "{\"status\":\"ACCEPTED\"}");
            var settled = await new FriendGateway(client)
                .SendRequestAsync("other", CancellationToken.None);

            Assert.That(pending.Value, Is.EqualTo(FriendRequestOutcome.Sent));
            Assert.That(settled.Value, Is.EqualTo(FriendRequestOutcome.BecameFriends));
        }

        [Test]
        public async Task SendingARequestNamesTheOtherPlayerById()
        {
            var transport = new FakeTransport();
            var client = SignedIn(transport, out _);
            transport.Answer(201, "{\"status\":\"PENDING\"}");

            await new FriendGateway(client).SendRequestAsync("other-1", CancellationToken.None);

            Assert.That(transport.LastCall.Method, Is.EqualTo(HttpMethod.Post));
            Assert.That(transport.LastCall.Url, Does.EndWith("/api/v1/friend-requests"));
            Assert.That(transport.LastCall.JsonBody, Is.EqualTo("{\"userId\":\"other-1\"}"));
        }

        [Test]
        public async Task OutOfARoom_TheRestReportOmitsTheSessionEntirely()
        {
            var transport = new FakeTransport();
            var client = SignedIn(transport, out _);
            transport.Answer(204, string.Empty);

            await new PresenceGateway(client, new FakeFrameSender())
                .ReportAsync(null, RoomSessionKind.Lobby, CancellationToken.None);

            // The server reads a present sessionId as being in a room, and
            // JsonUtility writes a null string as "". Sending the field at all
            // would report this player into a room with no name. The kind is
            // dropped with it: out of a room there is nothing to be either of.
            Assert.That(transport.LastCall.JsonBody, Is.EqualTo("{}"));
        }

        [Test]
        public async Task InARoom_TheRestReportCarriesTheRoomAndItsKind()
        {
            var transport = new FakeTransport();
            var client = SignedIn(transport, out _);
            var presence = new PresenceGateway(client, new FakeFrameSender());

            transport.Answer(204, string.Empty);
            await presence.ReportAsync("room-7", RoomSessionKind.Lobby, CancellationToken.None);
            Assert.That(transport.LastCall.JsonBody, Is.EqualTo("{\"sessionId\":\"room-7\",\"sessionKind\":\"LOBBY\"}"));

            // Same room, now a match. The room code cannot show it; only the kind can.
            transport.Answer(204, string.Empty);
            await presence.ReportAsync("room-7", RoomSessionKind.Match, CancellationToken.None);
            Assert.That(transport.LastCall.JsonBody, Is.EqualTo("{\"sessionId\":\"room-7\",\"sessionKind\":\"MATCH\"}"));
        }

        [Test]
        public async Task WhileTheLinkIsUp_TheReportGoesAsAFrameAndNoRequestIsMade()
        {
            var transport = new FakeTransport();
            var client = SignedIn(transport, out _);
            var frames = new FakeFrameSender { Connected = true };

            var result = await new PresenceGateway(client, frames)
                .ReportAsync("room-7", RoomSessionKind.Match, CancellationToken.None);

            Assert.That(result.Ok, Is.True);
            Assert.That(transport.Calls, Is.Empty);
            Assert.That(frames.Sent.Count, Is.EqualTo(1));
            Assert.That(frames.Sent[0], Is.EqualTo("{\"type\":\"PRESENCE\",\"sessionId\":\"room-7\",\"sessionKind\":\"MATCH\"}"));
        }

        [Test]
        public async Task OutOfARoom_TheFrameCarriesOnlyItsType()
        {
            var transport = new FakeTransport();
            var client = SignedIn(transport, out _);
            var frames = new FakeFrameSender { Connected = true };

            await new PresenceGateway(client, frames)
                .ReportAsync(null, RoomSessionKind.Lobby, CancellationToken.None);

            // The server reads a blank sessionId in a frame as out of a room, but
            // the shape is kept identical to the REST body so the two wires can
            // never disagree about what "out of a room" looks like.
            Assert.That(frames.Sent[0], Is.EqualTo("{\"type\":\"PRESENCE\"}"));
        }

        [Test]
        public async Task WhenTheLinkDrops_TheSameReportFallsBackToRest()
        {
            var transport = new FakeTransport();
            var client = SignedIn(transport, out _);
            var frames = new FakeFrameSender { Connected = true };
            var presence = new PresenceGateway(client, frames);
            await presence.ReportAsync("room-7", RoomSessionKind.Lobby, CancellationToken.None);

            frames.Connected = false;
            transport.Answer(204, string.Empty);
            var result = await presence.ReportAsync("room-7", RoomSessionKind.Match, CancellationToken.None);

            Assert.That(result.Ok, Is.True);
            Assert.That(frames.Sent.Count, Is.EqualTo(1), "nothing more went to the socket");
            Assert.That(transport.LastCall.Method, Is.EqualTo(HttpMethod.Put));
            Assert.That(transport.LastCall.JsonBody, Does.Contain("\"sessionKind\":\"MATCH\""));
        }

        [Test]
        public async Task InvitingAFriendWhoIsInARoom_ReadsAsTargetInGame()
        {
            var transport = new FakeTransport();
            var client = SignedIn(transport, out _);
            transport.Answer(409,
                "{\"code\":\"TARGET_IN_GAME\",\"message\":\"게임 중인 친구에게는 초대를 보낼 수 없습니다.\"}");

            var result = await new InviteGateway(client)
                .SendAsync("other-1", "7K2M9P", CancellationToken.None);

            // The server has refused this since the presence work; until now the
            // client read it as Unknown and told the player "처리하지 못했습니다".
            // Classified by code, not by the 409, which Conflict also uses.
            Assert.That(result.Ok, Is.False);
            Assert.That(result.Failure, Is.EqualTo(BackendFailure.TargetInGame));
        }

        [Test]
        public async Task ALobbyPresenceIsReadAsInLobby()
        {
            var transport = new FakeTransport();
            var client = SignedIn(transport, out _);
            transport.Answer(200,
                "{\"friends\":[{\"userId\":\"a\",\"nickname\":\"가\",\"presence\":\"IN_LOBBY\"}]}");

            var result = await new FriendGateway(client).ListFriendsAsync(CancellationToken.None);

            // Not folded into InGame and not dropped to Offline. The server tells
            // the lobby from the match so the screen can, and a friend waiting in
            // a lobby is online in every sense that matters to the list.
            Assert.That(result.Value[0].Presence, Is.EqualTo(FriendPresence.InLobby));
            Assert.That(result.Value[0].IsOnline, Is.True);
        }

        /// <summary>
        /// The notification socket as the presence gateway sees it: up or down,
        /// and a record of what was handed to it.
        /// </summary>
        private sealed class FakeFrameSender : INotificationFrameSender
        {
            public readonly List<string> Sent = new List<string>();

            public bool Connected { get; set; }

            public bool TrySend(string json)
            {
                if (!Connected)
                {
                    return false;
                }

                Sent.Add(json);
                return true;
            }
        }

        [Test]
        public async Task AnEmptyListIsNotAFailure()
        {
            var transport = new FakeTransport();
            var client = SignedIn(transport, out _);
            transport.Answer(200, "{\"friends\":[]}");

            var result = await new FriendGateway(client).ListFriendsAsync(CancellationToken.None);

            Assert.That(result.Ok, Is.True);
            Assert.That(result.Value, Is.Empty);
        }

        private static async UniTask<BackendFailure> FailureOf(long status, string body)
        {
            var transport = new FakeTransport();
            var client = SignedIn(transport, out _);
            transport.Answer(status, body);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("failed"));
            var result = await new FriendGateway(client).ListFriendsAsync(CancellationToken.None);
            return result.Failure;
        }

        private static async UniTask<BackendFailure> FailureOf(HttpOutcome outcome)
        {
            var transport = new FakeTransport();
            var client = SignedIn(transport, out _);
            transport.Fail(outcome);

            var result = await new FriendGateway(client).ListFriendsAsync(CancellationToken.None);
            return result.Failure;
        }

        [Test]
        public async Task SetSearchable_PutsTheFlagOnTheAccountsOwnPath()
        {
            // 화면이 부를 유일한 경로입니다. 오타가 나면 404 가 오는데, 클라이언트에는
            // "처리하지 못했습니다" 로만 보여서 무엇이 틀렸는지 드러나지 않습니다.
            var transport = new FakeTransport();
            transport.Answer(200, Account("나", false));
            var accounts = new AccountGateway(SignedIn(transport, out _));

            var result = await accounts.SetSearchableAsync(false, CancellationToken.None);

            Assert.That(result.Ok, Is.True);
            Assert.That(transport.LastCall.Method, Is.EqualTo(HttpMethod.Put));
            Assert.That(transport.LastCall.Url, Does.EndWith("/api/v1/accounts/me/searchable"));
            Assert.That(transport.LastCall.JsonBody, Does.Contain("\"searchable\":false"));
            Assert.That(Header(transport.LastCall, "X-User-Id"), Is.EqualTo(UserId));
        }

        [Test]
        public async Task TheAccount_CarriesWhetherSearchIsOn()
        {
            // 서버가 보내는데 클라이언트가 안 읽으면 체크박스가 늘 켜진 채로 그려집니다.
            // 껐다가 다시 들어오면 안 꺼진 것처럼 보이는 종류의 버그입니다.
            var transport = new FakeTransport();
            transport.Answer(200, Account("나", false));
            var accounts = new AccountGateway(SignedIn(transport, out _));

            var result = await accounts.RefreshAsync(CancellationToken.None);

            Assert.That(result.Value.Searchable, Is.False);
        }

        [Test]
        public async Task AnAccountThatIsSearchable_ReadsAsSuch()
        {
            // 위 테스트만 있으면 Searchable 을 늘 false 로 두어도 통과합니다.
            var transport = new FakeTransport();
            transport.Answer(200, Account("나", true));
            var accounts = new AccountGateway(SignedIn(transport, out _));

            var result = await accounts.RefreshAsync(CancellationToken.None);

            Assert.That(result.Value.Searchable, Is.True);
        }

        private static string Account(string nickname, bool searchable) =>
            "{\"userId\":\"" + UserId + "\",\"nickname\":\"" + nickname
            + "\",\"nicknameSet\":true,\"searchable\":" + (searchable ? "true" : "false")
            + ",\"createdAt\":\"20260101000000\"}";

        private static BackendClient Client(IHttpTransport transport, out BackendSession session)
        {
            session = new BackendSession(DeviceId);
            return new BackendClient(transport, new BackendEndpoint("http://localhost:8080"), session);
        }

        private static BackendClient SignedIn(IHttpTransport transport, out BackendSession session)
        {
            var client = Client(transport, out session);
            session.Adopt(UserId, AccountToken);
            return client;
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

        /// <summary>Answers whatever the test told it to, and records the call.</summary>
        private sealed class FakeTransport : IHttpTransport
        {
            private HttpCallResult next = HttpCallResult.Completed(200, "{}");

            public List<HttpCall> Calls { get; } = new List<HttpCall>();

            public HttpCall LastCall => Calls[Calls.Count - 1];

            public void Answer(long statusCode, string body)
            {
                next = HttpCallResult.Completed(statusCode, body);
            }

            public void Fail(HttpOutcome outcome)
            {
                next = HttpCallResult.Failed(outcome);
            }

            public UniTask<HttpCallResult> SendAsync(HttpCall call, CancellationToken cancellation)
            {
                Calls.Add(call);
                return UniTask.FromResult(next);
            }
        }
    }
}
