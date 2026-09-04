using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Backend;
using Game.Core.Backend;
using Game.Core.Home;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// Drives the backend layer against a real server over real HTTP.
    /// </summary>
    /// <remarks>
    /// The EditMode tests hand <see cref="BackendClient"/> a fake transport, so
    /// everything above the wire is checked and nothing on it is: whether
    /// UnityWebRequest is driven correctly, whether the shapes this client
    /// expects are the shapes the server sends, and whether a status the server
    /// really returns lands on the failure this client really maps it to.
    /// <para>
    /// Skipped rather than failed when no server is listening, so a teammate who
    /// has not started one does not see a red test they did not break. Start one
    /// with <c>docker compose -f compose.local.yml up -d</c> and
    /// <c>./gradlew bootRun</c> in <c>backend/</c>.
    /// </para>
    /// <para>
    /// Every account this creates is deleted again, so a local database does not
    /// fill up with test players.
    /// </para>
    /// </remarks>
    public sealed class BackendSmokeTests
    {
        private const string LocalBackend = "http://localhost:8080";

        [UnityTest]
        public IEnumerator AddingAFriend_RoundTripsAgainstALocalBackend() =>
            UniTask.ToCoroutine(async () =>
            {
                var transport = new UnityWebRequestTransport();
                await SkipUnlessListening(transport);

                // Two accounts on one machine, which is the thing a single
                // PlayerPrefs-backed device id cannot do.
                var run = Guid.NewGuid().ToString("N").Substring(0, 6);
                var one = new Player(transport, run + "a");
                var two = new Player(transport, run + "b");

                try
                {
                    var joined = await one.SignInAsync();
                    Assert.That(joined.Ok, Is.True, "sign in");

                    // The server invents a nickname and says it did.
                    Assert.That(joined.Value.NicknameSet, Is.False);
                    Assert.That(joined.Value.UserId, Is.Not.Empty);

                    await one.RenameAsync();
                    await two.SignInAsync();
                    await two.RenameAsync();

                    // Issuing again returns the same account rather than a new
                    // one, which is what lets the client call this every launch.
                    var again = await one.SignInAsync();
                    Assert.That(again.Value.UserId, Is.EqualTo(one.UserId));
                    Assert.That(again.Value.NicknameSet, Is.True, "the rename stuck");

                    var found = await one.Friends.SearchAsync(run, CancellationToken.None);
                    Assert.That(found.Ok, Is.True, "search");
                    Assert.That(found.Value.Count, Is.EqualTo(1), "search excludes the caller");
                    Assert.That(found.Value[0].PlayerId, Is.EqualTo(two.UserId));
                    Assert.That(found.Value[0].Nickname, Is.EqualTo(two.Nickname));

                    var sent = await one.Friends.SendRequestAsync(
                        two.UserId, CancellationToken.None);
                    Assert.That(sent.Ok, Is.True, "send request");
                    Assert.That(sent.Value, Is.EqualTo(FriendRequestOutcome.Sent));

                    var incoming = await two.Friends.ListIncomingRequestsAsync(
                        CancellationToken.None);
                    Assert.That(incoming.Ok, Is.True, "incoming requests");
                    Assert.That(incoming.Value.Count, Is.EqualTo(1));
                    Assert.That(incoming.Value[0].PlayerId, Is.EqualTo(one.UserId));

                    // The moment of the nine hour bug: a time read in this
                    // machine's zone would be hours away from now.
                    var age = DateTime.UtcNow - incoming.Value[0].RequestedAtUtc;
                    Assert.That(incoming.Value[0].RequestedAtUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
                    Assert.That(
                        age, Is.LessThan(TimeSpan.FromMinutes(5)),
                        "the request time was not read as UTC");

                    var accepted = await two.Friends.AcceptRequestAsync(
                        one.UserId, CancellationToken.None);
                    Assert.That(accepted.Ok, Is.True, "accept");

                    var friends = await one.Friends.ListFriendsAsync(CancellationToken.None);
                    Assert.That(friends.Ok, Is.True, "friend list");
                    Assert.That(friends.Value.Count, Is.EqualTo(1));
                    Assert.That(friends.Value[0].PlayerId, Is.EqualTo(two.UserId));

                    // Nobody has sent a heartbeat yet.
                    Assert.That(friends.Value[0].Presence, Is.EqualTo(FriendPresence.Offline));

                    await Reports(two, "room-smoke");
                    Assert.That(
                        await PresenceOfTheOtherSeenBy(one), Is.EqualTo(FriendPresence.InGame),
                        "a heartbeat naming a room reads as in-game");

                    await Reports(two, null);
                    Assert.That(
                        await PresenceOfTheOtherSeenBy(one), Is.EqualTo(FriendPresence.Online),
                        "a heartbeat with no room reads as online, not in-game with an empty one");

                    var wentOffline = await two.Presence.GoOfflineAsync(CancellationToken.None);
                    Assert.That(wentOffline.Ok, Is.True, "go offline");
                    Assert.That(
                        await PresenceOfTheOtherSeenBy(one), Is.EqualTo(FriendPresence.Offline));

                    var removed = await one.Friends.RemoveFriendAsync(
                        two.UserId, CancellationToken.None);
                    Assert.That(removed.Ok, Is.True, "unfriend");

                    var empty = await two.Friends.ListFriendsAsync(CancellationToken.None);
                    Assert.That(empty.Value, Is.Empty, "unfriending is seen from both sides");
                }
                finally
                {
                    await one.DeleteAsync();
                    await two.DeleteAsync();
                }
            });

        [UnityTest]
        public IEnumerator TheServersRefusals_LandOnTheFailuresThisClientExpects() =>
            UniTask.ToCoroutine(async () =>
            {
                var transport = new UnityWebRequestTransport();
                await SkipUnlessListening(transport);

                var run = Guid.NewGuid().ToString("N").Substring(0, 6);
                var alone = new Player(transport, run + "c");
                var rival = new Player(transport, run + "d");

                try
                {
                    await alone.SignInAsync();
                    await alone.RenameAsync();
                    await rival.SignInAsync();

                    var atSelf = await alone.Friends.SendRequestAsync(
                        alone.UserId, CancellationToken.None);
                    Assert.That(atSelf.Failure, Is.EqualTo(BackendFailure.SelfRequest));

                    var atNobody = await alone.Friends.SendRequestAsync(
                        Guid.NewGuid().ToString(), CancellationToken.None);
                    Assert.That(atNobody.Failure, Is.EqualTo(BackendFailure.TargetNotFound));

                    var notFriends = await alone.Friends.RemoveFriendAsync(
                        Guid.NewGuid().ToString(), CancellationToken.None);
                    Assert.That(notFriends.Failure, Is.EqualTo(BackendFailure.TargetNotFound));

                    // One character is below the server's minimum, and the
                    // client sends it rather than deciding on its own.
                    var tooShort = await alone.Friends.SearchAsync("a", CancellationToken.None);
                    Assert.That(tooShort.Failure, Is.EqualTo(BackendFailure.InvalidRequest));

                    // Someone else's name is refused. Keeping your own is not a
                    // rename at all and the server lets it through, so taking a
                    // name is the only way to see this code.
                    var stealing = await rival.Accounts.RenameAsync(
                        alone.Nickname, CancellationToken.None);
                    Assert.That(stealing.Failure, Is.EqualTo(BackendFailure.NicknameTaken));

                    var keepingItsOwn = await alone.Accounts.RenameAsync(
                        alone.Nickname, CancellationToken.None);
                    Assert.That(keepingItsOwn.Ok, Is.True);

                    // Nothing above ever saw the account's credential, and the
                    // one call that sends it is the one that ends the account.
                    var deleted = await alone.Accounts.DeleteAccountAsync(CancellationToken.None);
                    Assert.That(deleted.Ok, Is.True, "delete");

                    var afterwards = await alone.Friends.ListFriendsAsync(CancellationToken.None);
                    Assert.That(
                        afterwards.Failure, Is.EqualTo(BackendFailure.NotSignedIn),
                        "deleting clears the session instead of leaving a dead id behind");
                }
                finally
                {
                    await alone.DeleteAsync();
                    await rival.DeleteAsync();
                }
            });

        [UnityTest]
        public IEnumerator AServerThatIsNotThere_ReadsAsOfflineRatherThanHanging() =>
            UniTask.ToCoroutine(async () =>
            {
                // Port 1 is reserved and nothing listens on it, so this is a
                // connection failure rather than a slow one.
                var session = new BackendSession(Guid.NewGuid().ToString("N"));
                session.Adopt("nobody");
                var client = new BackendClient(
                    new UnityWebRequestTransport(),
                    new BackendEndpoint("http://127.0.0.1:1", 2),
                    session);

                var result = await new FriendGateway(client)
                    .ListFriendsAsync(CancellationToken.None);

                Assert.That(result.Ok, Is.False);
                Assert.That(
                    result.Failure,
                    Is.EqualTo(BackendFailure.Offline).Or.EqualTo(BackendFailure.Timeout));
            });

        private static async UniTask SkipUnlessListening(IHttpTransport transport)
        {
            var health = await transport.SendAsync(
                new HttpCall(
                    HttpMethod.Get, LocalBackend + "/actuator/health", null, null, 2),
                CancellationToken.None);

            if (health.Outcome != HttpOutcome.Completed || health.StatusCode != 200)
            {
                Assert.Ignore(
                    "No backend on " + LocalBackend
                    + ". Start one in backend/: docker compose -f compose.local.yml up -d, then ./gradlew bootRun.");
            }
        }

        private static async UniTask Reports(Player player, string sessionId)
        {
            var reported = await player.Presence.ReportAsync(sessionId, CancellationToken.None);
            Assert.That(reported.Ok, Is.True, "heartbeat");
        }

        private static async UniTask<FriendPresence> PresenceOfTheOtherSeenBy(Player player)
        {
            var friends = await player.Friends.ListFriendsAsync(CancellationToken.None);
            Assert.That(friends.Ok, Is.True, "friend list");
            Assert.That(friends.Value.Count, Is.EqualTo(1));
            return friends.Value[0].Presence;
        }

        /// <summary>One client, with its own device and its own account.</summary>
        private sealed class Player
        {
            private readonly BackendClient client;

            public Player(IHttpTransport transport, string tag)
            {
                var session = new BackendSession(Guid.NewGuid().ToString("N"));
                client = new BackendClient(transport, new BackendEndpoint(LocalBackend), session);
                Accounts = new AccountGateway(client);
                Friends = new FriendGateway(client);
                Presence = new PresenceGateway(client);

                // Letters and digits only, inside the server's twelve, and
                // starting with the run's tag: the server matches a prefix, not
                // a substring, so a name with anything in front of it would not
                // come back from a search for that tag.
                Nickname = tag;
            }

            public AccountGateway Accounts { get; }

            public FriendGateway Friends { get; }

            public PresenceGateway Presence { get; }

            public string Nickname { get; }

            public string UserId => client.Session.UserId;

            public UniTask<BackendResult<AccountSnapshot>> SignInAsync() =>
                Accounts.SignInAsync(CancellationToken.None);

            public async UniTask RenameAsync()
            {
                var renamed = await Accounts.RenameAsync(Nickname, CancellationToken.None);
                Assert.That(renamed.Ok, Is.True, "rename to " + Nickname);
                Assert.That(renamed.Value.Nickname, Is.EqualTo(Nickname));
                Assert.That(renamed.Value.NicknameSet, Is.True);
            }

            /// <summary>
            /// Leaves nothing behind. Silent when there is no account, so it can
            /// run in a finally block after the test already deleted one.
            /// </summary>
            public async UniTask DeleteAsync()
            {
                if (client.Session.SignedIn)
                {
                    await Accounts.DeleteAccountAsync(CancellationToken.None);
                }
            }
        }
    }
}
