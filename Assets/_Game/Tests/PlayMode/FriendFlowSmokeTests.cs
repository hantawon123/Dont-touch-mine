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
    /// Drives the friend screen's own commands against a real server.
    /// </summary>
    /// <remarks>
    /// <see cref="BackendSmokeTests"/> proves the gateways reach the server.
    /// This proves the layer the screen actually calls does the right things
    /// with the answers: which list a result lands in, when a row stops saying
    /// it is waiting, and what happens when two players ask each other at once.
    /// <para>
    /// Skipped when no backend is listening. See <see cref="BackendSmokeTests"/>
    /// for how to start one.
    /// </para>
    /// </remarks>
    public sealed class FriendFlowSmokeTests
    {
        private const string LocalBackend = "http://localhost:8080";

        [UnityTest]
        public IEnumerator TheFriendPanelsCommands_AddAFriendAgainstALocalBackend() =>
            UniTask.ToCoroutine(async () =>
            {
                var transport = new UnityWebRequestTransport();
                await SkipUnlessListening(transport);

                var run = Guid.NewGuid().ToString("N").Substring(0, 6);
                var one = new Screen(transport, run + "a");
                var two = new Screen(transport, run + "b");

                try
                {
                    await one.SignInAsync();
                    await two.SignInAsync();

                    // What the search box does: ask the server, then narrow to
                    // people who are not already friends.
                    Assert.That(
                        await one.Commands.SearchAsync(run, one.Commands.FriendIds(), Token),
                        Is.EqualTo(BackendFailure.None));
                    Assert.That(one.Search.Results.Count, Is.EqualTo(1));
                    Assert.That(one.Search.Results[0].PlayerId, Is.EqualTo(two.UserId));
                    Assert.That(one.Search.Results[0].IsPending, Is.False);

                    Assert.That(
                        await one.Commands.SendRequestAsync(two.UserId, Token),
                        Is.EqualTo(BackendFailure.None));
                    Assert.That(one.Search.Results[0].IsPending, Is.True, "the row is waiting");

                    var incoming = await two.Commands.ListIncomingRequestsAsync(Token);
                    Assert.That(incoming.Ok, Is.True);
                    Assert.That(incoming.Value.Count, Is.EqualTo(1));
                    Assert.That(incoming.Value[0].PlayerId, Is.EqualTo(one.UserId));

                    // Accepting refreshes the accepter's own list as part of the
                    // command, so the panel does not need a second call.
                    Assert.That(
                        await two.Commands.AcceptRequestAsync(one.UserId, Token),
                        Is.EqualTo(BackendFailure.None));
                    Assert.That(two.Friends.OfflineFriends.Count, Is.EqualTo(1));
                    Assert.That(two.Friends.OfflineFriends[0].PlayerId, Is.EqualTo(one.UserId));

                    Assert.That(
                        await one.Commands.RefreshFriendsAsync(Token),
                        Is.EqualTo(BackendFailure.None));
                    Assert.That(one.Friends.OfflineFriends.Count, Is.EqualTo(1));

                    // Searching again no longer offers to befriend them.
                    await one.Commands.SearchAsync(run, one.Commands.FriendIds(), Token);
                    Assert.That(one.Search.Results, Is.Empty);
                }
                finally
                {
                    await one.DeleteAsync();
                    await two.DeleteAsync();
                }
            });

        [UnityTest]
        public IEnumerator TwoPlayersAskingAtOnce_BecomeFriendsWithoutAPendingRow() =>
            UniTask.ToCoroutine(async () =>
            {
                var transport = new UnityWebRequestTransport();
                await SkipUnlessListening(transport);

                var run = Guid.NewGuid().ToString("N").Substring(0, 6);
                var one = new Screen(transport, run + "a");
                var two = new Screen(transport, run + "b");

                try
                {
                    await one.SignInAsync();
                    await two.SignInAsync();

                    await one.Commands.SearchAsync(run, one.Commands.FriendIds(), Token);
                    await one.Commands.SendRequestAsync(two.UserId, Token);

                    await two.Commands.SearchAsync(run, two.Commands.FriendIds(), Token);
                    var failure = await two.Commands.SendRequestAsync(one.UserId, Token);

                    Assert.That(failure, Is.EqualTo(BackendFailure.None));

                    // The server settled it rather than leaving a second request
                    // pending, and the command noticed and reloaded the list.
                    Assert.That(two.Friends.OfflineFriends.Count, Is.EqualTo(1));
                    Assert.That(two.Search.Results, Is.Empty.Or.Property("Count").EqualTo(0));
                }
                finally
                {
                    await one.DeleteAsync();
                    await two.DeleteAsync();
                }
            });

        [UnityTest]
        public IEnumerator ARequestToNobody_ClearsTheRowItMarkedWaiting() =>
            UniTask.ToCoroutine(async () =>
            {
                var transport = new UnityWebRequestTransport();
                await SkipUnlessListening(transport);

                var run = Guid.NewGuid().ToString("N").Substring(0, 6);
                var one = new Screen(transport, run + "a");
                var two = new Screen(transport, run + "b");

                try
                {
                    await one.SignInAsync();
                    await two.SignInAsync();
                    await one.Commands.SearchAsync(run, one.Commands.FriendIds(), Token);

                    // Held before the delete. Deleting clears that screen's
                    // session, so afterwards it no longer knows its own id.
                    var goneUserId = two.UserId;

                    // Deleted after the search, so the row is on screen and the
                    // account behind it is gone: the request cannot succeed.
                    await two.DeleteAsync();

                    var failure = await one.Commands.SendRequestAsync(goneUserId, Token);

                    Assert.That(failure, Is.EqualTo(BackendFailure.TargetNotFound));
                    Assert.That(
                        one.Search.Results[0].IsPending, Is.False,
                        "a row cannot be left waiting for an answer that cannot come");
                }
                finally
                {
                    await one.DeleteAsync();
                    await two.DeleteAsync();
                }
            });

        private static CancellationToken Token => CancellationToken.None;

        private static async UniTask SkipUnlessListening(IHttpTransport transport)
        {
            var health = await transport.SendAsync(
                new HttpCall(HttpMethod.Get, LocalBackend + "/actuator/health", null, null, 2),
                CancellationToken.None);

            if (health.Outcome != HttpOutcome.Completed || health.StatusCode != 200)
            {
                Assert.Ignore(
                    "No backend on " + LocalBackend
                    + ". Start one in backend/: docker compose -f compose.local.yml up -d, then ./gradlew bootRun.");
            }
        }

        /// <summary>One player's screen: their account and the systems it binds.</summary>
        private sealed class Screen
        {
            private readonly BackendClient client;
            private readonly AccountGateway accounts;

            public Screen(IHttpTransport transport, string nickname)
            {
                var session = new BackendSession(Guid.NewGuid().ToString("N"));
                client = new BackendClient(transport, new BackendEndpoint(LocalBackend), session);
                accounts = new AccountGateway(client);
                Friends = new FriendListSystem();
                Search = new FriendSearchSystem();
                Commands = new FriendUiCommands(
                    new FriendGateway(client), new BlockGateway(client), Friends, Search);
                Nickname = nickname;
            }

            public FriendListSystem Friends { get; }

            public FriendSearchSystem Search { get; }

            public FriendUiCommands Commands { get; }

            public string Nickname { get; }

            public string UserId => client.Session.UserId;

            public async UniTask SignInAsync()
            {
                var joined = await accounts.SignInAsync(Token);
                Assert.That(joined.Ok, Is.True, "sign in");

                var renamed = await accounts.RenameAsync(Nickname, Token);
                Assert.That(renamed.Ok, Is.True, "rename to " + Nickname);
            }

            public async UniTask DeleteAsync()
            {
                if (client.Session.SignedIn)
                {
                    await accounts.DeleteAccountAsync(Token);
                }
            }
        }
    }
}
