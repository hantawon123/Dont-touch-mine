using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Backend;
using Game.Core.Backend;
using Game.Core.Ports;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// Drives the report gateway against a real server.
    /// </summary>
    /// <remarks>
    /// This carries more weight than a smoke test usually does. Nothing on
    /// screen calls this gateway yet, so a round trip against a real server is
    /// the only thing that shows the reason names, the field names and the
    /// length limit actually agree with the server. Unit tests against a fake
    /// would agree with themselves.
    /// <para>
    /// Skipped when no backend is listening. See <see cref="BackendSmokeTests"/>
    /// for how to start one.
    /// </para>
    /// </remarks>
    public sealed class ReportSmokeTests
    {
        private const string LocalBackend = "http://localhost:8080";

        [UnityTest]
        public IEnumerator EveryReason_IsAcceptedByTheServer() =>
            UniTask.ToCoroutine(async () =>
            {
                // The names travel as strings. One typo in the mapping is a
                // reason that can never be filed, and nothing local would say so.
                var transport = new UnityWebRequestTransport();
                await SkipUnlessListening(transport);

                var run = Guid.NewGuid().ToString("N").Substring(0, 6);
                var reporter = new Player(transport, run + "a");
                var target = new Player(transport, run + "b");

                try
                {
                    await reporter.SignInAsync();
                    await target.SignInAsync();

                    foreach (ReportReason reason in Enum.GetValues(typeof(ReportReason)))
                    {
                        var sent = await reporter.Reports.ReportAsync(
                            target.UserId, reason, "스모크 테스트", Token);

                        Assert.That(sent.Ok, Is.True, reason.ToString());
                    }
                }
                finally
                {
                    await reporter.DeleteAsync();
                    await target.DeleteAsync();
                }
            });

        [UnityTest]
        public IEnumerator AReport_ChangesNothingForTheTarget() =>
            UniTask.ToCoroutine(async () =>
            {
                // The one thing a screen must not get wrong. This replaced
                // blocking and looks like it, but the person reported is not
                // hidden and not cut off.
                var transport = new UnityWebRequestTransport();
                await SkipUnlessListening(transport);

                var run = Guid.NewGuid().ToString("N").Substring(0, 6);
                var reporter = new Player(transport, run + "a");
                var target = new Player(transport, run + "b");

                try
                {
                    await reporter.SignInAsync();
                    await target.SignInAsync();

                    var sent = await reporter.Reports.ReportAsync(
                        target.UserId, ReportReason.Abuse, null, Token);
                    Assert.That(sent.Ok, Is.True, "report");

                    var found = await reporter.Friends.SearchAsync(target.Nickname, Token);
                    Assert.That(found.Ok, Is.True, "search");
                    Assert.That(
                        found.Value.Count,
                        Is.EqualTo(1),
                        "a reported player still turns up in search");

                    var asked = await reporter.Friends.SendRequestAsync(target.UserId, Token);
                    Assert.That(
                        asked.Ok,
                        Is.True,
                        "a reported player can still be sent a friend request");
                }
                finally
                {
                    await reporter.DeleteAsync();
                    await target.DeleteAsync();
                }
            });

        [UnityTest]
        public IEnumerator AnOverlongNote_IsRefusedAsInvalid() =>
            UniTask.ToCoroutine(async () =>
            {
                // 200 is the server's limit. Pinning it here means a change on
                // that side shows up as a failing test rather than as a report
                // the player thinks was filed.
                var transport = new UnityWebRequestTransport();
                await SkipUnlessListening(transport);

                var run = Guid.NewGuid().ToString("N").Substring(0, 6);
                var reporter = new Player(transport, run + "a");
                var target = new Player(transport, run + "b");

                try
                {
                    await reporter.SignInAsync();
                    await target.SignInAsync();

                    var atTheLimit = await reporter.Reports.ReportAsync(
                        target.UserId, ReportReason.Other, new string('가', 200), Token);
                    Assert.That(atTheLimit.Ok, Is.True, "200 characters should be accepted");

                    var overIt = await reporter.Reports.ReportAsync(
                        target.UserId, ReportReason.Other, new string('가', 201), Token);
                    Assert.That(overIt.Ok, Is.False, "201 characters should be refused");
                    Assert.That(overIt.Failure, Is.EqualTo(BackendFailure.InvalidRequest));
                }
                finally
                {
                    await reporter.DeleteAsync();
                    await target.DeleteAsync();
                }
            });

        [UnityTest]
        public IEnumerator ReportingYourself_ReadsAsAMissingTarget() =>
            UniTask.ToCoroutine(async () =>
            {
                var transport = new UnityWebRequestTransport();
                await SkipUnlessListening(transport);

                var me = new Player(transport, Guid.NewGuid().ToString("N").Substring(0, 6) + "a");

                try
                {
                    await me.SignInAsync();

                    var sent = await me.Reports.ReportAsync(
                        me.UserId, ReportReason.Abuse, null, Token);

                    Assert.That(sent.Ok, Is.False);
                    Assert.That(sent.Failure, Is.EqualTo(BackendFailure.TargetNotFound));
                }
                finally
                {
                    await me.DeleteAsync();
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

        private sealed class Player
        {
            private readonly BackendClient client;
            private readonly AccountGateway accounts;

            public Player(IHttpTransport transport, string nickname)
            {
                var session = new BackendSession(Guid.NewGuid().ToString("N"));
                client = new BackendClient(transport, new BackendEndpoint(LocalBackend), session);
                accounts = new AccountGateway(client);
                Friends = new FriendGateway(client);
                Reports = new ReportGateway(client);
                Nickname = nickname;
            }

            public IFriendGateway Friends { get; }

            public IReportGateway Reports { get; }

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
