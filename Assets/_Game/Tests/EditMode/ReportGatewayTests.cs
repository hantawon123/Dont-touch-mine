using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Backend;
using Game.Core.Backend;
using Game.Core.Ports;
using NUnit.Framework;

namespace Game.Architecture.Tests
{
    /// <summary>
    /// What the report gateway puts on the wire, checked without a server.
    /// </summary>
    /// <remarks>
    /// The smoke test covers the same ground against a real backend, but it
    /// skips itself when nothing is listening — which is every CI run. Without
    /// this file the gateway would be untested wherever it matters most: no
    /// screen calls it yet, so a wrong field name would sit there until someone
    /// wired up a button.
    /// </remarks>
    public sealed class ReportGatewayTests
    {
        private const string UserId = "user-1";

        [Test]
        public async Task AReport_GoesToTheReportsEndpointAsTheCaller()
        {
            var transport = new FakeTransport();
            transport.Answer(201, string.Empty);
            var reports = new ReportGateway(SignedIn(transport));

            var sent = await reports.ReportAsync(
                "other", ReportReason.Abuse, "욕설", CancellationToken.None);

            Assert.That(sent.Ok, Is.True);
            Assert.That(transport.LastCall.Method, Is.EqualTo(HttpMethod.Post));
            Assert.That(transport.LastCall.Url, Does.EndWith("/api/v1/reports"));
            Assert.That(Header(transport.LastCall, "X-User-Id"), Is.EqualTo(UserId));
        }

        [Test]
        public async Task TheBody_CarriesTheFieldNamesTheServerReads()
        {
            // The names are a contract with a server that cannot tell us it
            // stopped recognising them — a mistyped field is simply absent, and
            // an absent reason is a 400 the player reads as "it did not work".
            var transport = new FakeTransport();
            transport.Answer(201, string.Empty);
            var reports = new ReportGateway(SignedIn(transport));

            await reports.ReportAsync(
                "other", ReportReason.Cheating, "핵을 씁니다", CancellationToken.None);

            Assert.That(transport.LastCall.JsonBody, Does.Contain("\"userId\":\"other\""));
            Assert.That(transport.LastCall.JsonBody, Does.Contain("\"reason\":\"CHEATING\""));
            Assert.That(transport.LastCall.JsonBody, Does.Contain("\"memo\":\"핵을 씁니다\""));
        }

        [Test]
        public async Task EveryReason_HasItsOwnServerName()
        {
            // Two reasons sharing a name would file half the reports under the
            // wrong heading, and a value falling through the mapping would throw
            // in the player's hands. Both are caught by walking the enum rather
            // than listing the cases again here, which would only agree with a
            // copy of the table.
            var transport = new FakeTransport();
            transport.Answer(201, string.Empty);
            var reports = new ReportGateway(SignedIn(transport));

            var seen = new List<string>();
            foreach (ReportReason reason in Enum.GetValues(typeof(ReportReason)))
            {
                await reports.ReportAsync("other", reason, null, CancellationToken.None);

                var body = transport.LastCall.JsonBody;
                var start = body.IndexOf("\"reason\":\"", StringComparison.Ordinal) + 10;
                var wire = body.Substring(start, body.IndexOf('"', start) - start);

                Assert.That(wire, Is.Not.Empty, reason.ToString());
                Assert.That(wire, Is.EqualTo(wire.ToUpperInvariant()), reason.ToString());
                Assert.That(seen, Does.Not.Contain(wire), reason + " repeats a name");
                seen.Add(wire);
            }

            Assert.That(seen.Count, Is.EqualTo(Enum.GetValues(typeof(ReportReason)).Length));
        }

        [Test]
        public async Task ANoteLeftOut_IsSentAsAnEmptyString()
        {
            // JsonUtility has no way to omit a field, so a null note travels as
            // "". That is fine only because the server reads "" and absent the
            // same way; this pins the assumption rather than leaving it in a
            // comment.
            var transport = new FakeTransport();
            transport.Answer(201, string.Empty);
            var reports = new ReportGateway(SignedIn(transport));

            await reports.ReportAsync("other", ReportReason.Spam, null, CancellationToken.None);

            Assert.That(transport.LastCall.JsonBody, Does.Contain("\"memo\":\"\""));
        }

        [Test]
        public async Task ARefusedReport_KeepsTheServersReason()
        {
            var transport = new FakeTransport();
            transport.Answer(400, "{\"code\":\"INVALID_REQUEST\",\"message\":\"...\"}");
            var reports = new ReportGateway(SignedIn(transport));

            var sent = await reports.ReportAsync(
                "other", ReportReason.Other, new string('가', 201), CancellationToken.None);

            Assert.That(sent.Ok, Is.False);
            Assert.That(sent.Failure, Is.EqualTo(BackendFailure.InvalidRequest));
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
