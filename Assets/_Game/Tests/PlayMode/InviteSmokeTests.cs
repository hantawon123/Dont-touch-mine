using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Backend;
using Game.Core.Backend;
using Game.Core.Home;
using Game.Core.Ports;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// Drives the invite gateway against a real server.
    /// </summary>
    /// <remarks>
    /// Skipped when no backend is listening. See <see cref="BackendSmokeTests"/>
    /// for how to start one.
    /// </remarks>
    public sealed class InviteSmokeTests
    {
        private const string LocalBackend = "http://localhost:8080";
        private const string Room = "7K2M9P";

        [UnityTest]
        public IEnumerator AnInvitation_ReachesTheFriendWithItsRoomCode() =>
            UniTask.ToCoroutine(async () =>
            {
                var transport = new UnityWebRequestTransport();
                await SkipUnlessListening(transport);

                var run = Guid.NewGuid().ToString("N").Substring(0, 6);
                var host = new Player(transport, run + "a");
                var guest = new Player(transport, run + "b");

                try
                {
                    await host.SignInAsync();
                    await guest.SignInAsync();
                    await Befriend(host, guest, run);

                    var sent = await host.Invites.SendAsync(guest.UserId, Room, Token);
                    Assert.That(sent.Ok, Is.True, "send");

                    var inbox = await guest.Invites.ListAsync(Token);
                    Assert.That(inbox.Ok, Is.True, "inbox");
                    Assert.That(inbox.Value.Count, Is.EqualTo(1));
                    Assert.That(inbox.Value[0].PlayerId, Is.EqualTo(host.UserId));
                    Assert.That(inbox.Value[0].Nickname, Is.EqualTo(host.Nickname));
                    Assert.That(inbox.Value[0].RoomCode, Is.EqualTo(Room));

                    // The same nine hour bug the friend requests have.
                    Assert.That(inbox.Value[0].InvitedAtUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
                    Assert.That(
                        DateTime.UtcNow - inbox.Value[0].InvitedAtUtc,
                        Is.LessThan(TimeSpan.FromMinutes(5)),
                        "the invite time was not read as UTC");

                    // The sender does not see their own invitation.
                    var mine = await host.Invites.ListAsync(Token);
                    Assert.That(mine.Value, Is.Empty);

                    var declined = await guest.Invites.DeclineAsync(host.UserId, Token);
                    Assert.That(declined.Ok, Is.True, "decline");

                    var afterwards = await guest.Invites.ListAsync(Token);
                    Assert.That(afterwards.Value, Is.Empty);
                }
                finally
                {
                    await host.DeleteAsync();
                    await guest.DeleteAsync();
                }
            });

        [UnityTest]
        public IEnumerator TheServersRefusals_LandOnTheFailuresThisClientExpects() =>
            UniTask.ToCoroutine(async () =>
            {
                var transport = new UnityWebRequestTransport();
                await SkipUnlessListening(transport);

                var run = Guid.NewGuid().ToString("N").Substring(0, 6);
                var host = new Player(transport, run + "a");
                var stranger = new Player(transport, run + "b");

                try
                {
                    await host.SignInAsync();
                    await stranger.SignInAsync();

                    // Not friends yet: the room code must not travel.
                    var toStranger = await host.Invites.SendAsync(stranger.UserId, Room, Token);
                    Assert.That(toStranger.Failure, Is.EqualTo(BackendFailure.NotFriends));

                    await Befriend(host, stranger, run);

                    // I, O, U and L are not in the client's alphabet.
                    var confusable = await host.Invites.SendAsync(stranger.UserId, "7K2M9I", Token);
                    Assert.That(confusable.Failure, Is.EqualTo(BackendFailure.InvalidRequest));

                    var tooShort = await host.Invites.SendAsync(stranger.UserId, "7K2M9", Token);
                    Assert.That(tooShort.Failure, Is.EqualTo(BackendFailure.InvalidRequest));

                    // Clearing one that was never there is the ordinary case.
                    var nothing = await stranger.Invites.DeclineAsync(host.UserId, Token);
                    Assert.That(nothing.Ok, Is.True);
                }
                finally
                {
                    await host.DeleteAsync();
                    await stranger.DeleteAsync();
                }
            });

        private static CancellationToken Token => CancellationToken.None;

        private static async UniTask Befriend(Player one, Player two, string run)
        {
            await one.Commands.SearchAsync(run, one.Commands.FriendIds(), Token);
            await one.Commands.SendRequestAsync(two.UserId, Token);
            await two.Commands.AcceptRequestAsync(one.UserId, Token);
        }

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

        private sealed class Player
        {
            private readonly BackendClient client;
            private readonly AccountGateway accounts;

            public Player(IHttpTransport transport, string nickname)
            {
                var session = new BackendSession(Guid.NewGuid().ToString("N"));
                client = new BackendClient(transport, new BackendEndpoint(LocalBackend), session);
                accounts = new AccountGateway(client);
                Commands = new FriendUiCommands(
                    new FriendGateway(client),
                    new FriendListSystem(),
                    new FriendSearchSystem());
                Invites = new InviteGateway(client);
                Nickname = nickname;
            }

            public FriendUiCommands Commands { get; }

            public IInviteGateway Invites { get; }

            public string Nickname { get; }

            public string UserId => client.Session.UserId;

            public async UniTask SignInAsync()
            {
                Assert.That((await accounts.SignInAsync(Token)).Ok, Is.True, "sign in");
                Assert.That((await accounts.RenameAsync(Nickname, Token)).Ok, Is.True, Nickname);
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
