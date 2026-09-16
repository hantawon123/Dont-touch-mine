using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Backend;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Architecture.Tests
{
    public sealed class MatchAnalyticsTests
    {
        [Test]
        public void Samples_SerializeCumulativeCombatTotalsWithoutMutatingPreviousSeconds()
        {
            var buffer = new MatchAnalyticsBuffer(0);
            var data = new MatchAnalyticsParams { seat = 1, total_hits_received = 3, total_stuns = 1 };
            buffer.Add(new MatchAnalyticsEvent { eventName = "position_sample", @params = data }, 1);
            data.total_hits_received = 6; data.total_stuns = 2;
            buffer.Add(new MatchAnalyticsEvent { eventName = "position_sample", @params = data }, 2);
            var samples = buffer.Snapshot();
            Assert.That(samples[0], Does.Contain("\"total_hits_received\":3").And.Contain("\"total_stuns\":1"));
            Assert.That(samples[1], Does.Contain("\"total_hits_received\":6").And.Contain("\"total_stuns\":2"));
        }

        [Test]
        public void Sampling_OncePerSecondWithoutCatchUp()
        {
            var buffer = new MatchAnalyticsBuffer(10);
            Assert.That(buffer.IsSampleDue(10), Is.True);
            Assert.That(buffer.IsSampleDue(10.1), Is.False);
            Assert.That(buffer.IsSampleDue(11), Is.True);
            Assert.That(buffer.IsSampleDue(20), Is.True);
            Assert.That(buffer.IsSampleDue(20.1), Is.False);
        }

        [Test]
        public void Serialization_PreservesRetryIdentityAndNullableUser()
        {
            var buffer = new MatchAnalyticsBuffer(10);
            buffer.Add(new MatchAnalyticsEvent { eventName = "position_sample", phase = "Searching" }, 11);
            var json = buffer.Snapshot()[0];
            Assert.That(json, Does.Contain("\"userPublicId\":null"));
            Assert.That(json, Does.Contain("\"params\":{"));
            Assert.That(json, Does.Contain("\"matchTimeMs\":1000"));
            Assert.That(buffer.Snapshot()[0], Is.EqualTo(json));
        }

        [UnityTest]
        public IEnumerator Upload_OnlyAfterStore_SplitsAndRetainsUnacceptedMatch()
        {
            var path = Path.Combine(Path.GetTempPath(), "analytics-test-" + Guid.NewGuid());
            var transport = new FakeTransport();
            using var upload = new MatchAnalyticsUpload(transport, new BackendEndpoint("http://localhost"), path);
            try
            {
                var buffer = new MatchAnalyticsBuffer(0);
                for (var i = 0; i < 51; i++) buffer.Add(new MatchAnalyticsEvent { eventName = "position_sample" }, i);
                Assert.That(transport.Bodies, Is.Empty);
                upload.Store(buffer);
                var deadline = DateTime.UtcNow.AddSeconds(10);
                while (upload.IsSending && DateTime.UtcNow < deadline) yield return null;
                Assert.That(upload.IsSending, Is.False);
                Assert.That(transport.Bodies.Count, Is.EqualTo(2));
                Assert.That(transport.Bodies[0].Split(new[] { "\"eventName\"" }, StringSplitOptions.None).Length - 1, Is.EqualTo(50));
                Assert.That(Directory.GetFiles(path, "*.jsonl"), Is.Empty);
                transport.Cancel = true;
                upload.Store(buffer);
                Assert.That(Directory.GetFiles(path, "*.jsonl").Length, Is.EqualTo(1));
                transport.Cancel = false;
                upload.Retry();
                while (upload.IsSending && DateTime.UtcNow < deadline) yield return null;
                Assert.That(transport.Bodies[0], Is.EqualTo(transport.Bodies[2]));
                Assert.That(Directory.GetFiles(path, "*.jsonl"), Is.Empty);
            }
            finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
        }

        [UnityTest]
        public IEnumerator RejectedMatch_DoesNotBlockFollowingMatch()
        {
            var path = Path.Combine(Path.GetTempPath(), "analytics-test-" + Guid.NewGuid());
            var transport = new FakeTransport { Status = 400 };
            using var upload = new MatchAnalyticsUpload(transport, new BackendEndpoint("http://localhost"), path);
            try
            {
                var rejected = new MatchAnalyticsBuffer(0);
                rejected.Add(new MatchAnalyticsEvent { eventName = "match_end" }, 1);
                LogAssert.Expect(LogType.Warning, "[Analytics] Batch rejected (400); quarantined locally.");
                upload.Store(rejected);
                Assert.That(Directory.GetFiles(path, "*.rejected").Length, Is.EqualTo(1));
                transport.Status = 202;
                var next = new MatchAnalyticsBuffer(0);
                next.Add(new MatchAnalyticsEvent { eventName = "match_end" }, 1);
                upload.Store(next);
                var deadline = DateTime.UtcNow.AddSeconds(10);
                while (upload.IsSending && DateTime.UtcNow < deadline) yield return null;
                Assert.That(upload.IsSending, Is.False);
                Assert.That(transport.Bodies.Count, Is.EqualTo(2));
                Assert.That(Directory.GetFiles(path, "*.jsonl"), Is.Empty);
            }
            finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
        }

        [UnityTest]
        public IEnumerator MatchStoredDuringUpload_IsAlsoSent()
        {
            var path = Path.Combine(Path.GetTempPath(), "analytics-test-" + Guid.NewGuid());
            var transport = new FakeTransport();
            using var upload = new MatchAnalyticsUpload(transport, new BackendEndpoint("http://localhost"), path);
            try
            {
                for (var i = 0; i < 2; i++)
                {
                    var match = new MatchAnalyticsBuffer(0);
                    match.Add(new MatchAnalyticsEvent { eventName = "match_end" }, 1);
                    upload.Store(match);
                }
                var deadline = DateTime.UtcNow.AddSeconds(10);
                while (upload.IsSending && DateTime.UtcNow < deadline) yield return null;
                Assert.That(upload.IsSending, Is.False);
                Assert.That(transport.Bodies.Count, Is.EqualTo(2));
                Assert.That(Directory.GetFiles(path, "*.jsonl"), Is.Empty);
            }
            finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
        }

        private sealed class FakeTransport : IHttpTransport
        {
            public readonly List<string> Bodies = new();
            public bool Cancel;
            public int Status = 202;
            public UniTask<HttpCallResult> SendAsync(HttpCall call, CancellationToken cancellation)
            {
                Bodies.Add(call.JsonBody);
                return UniTask.FromResult(Cancel ? HttpCallResult.Failed(HttpOutcome.Cancelled) : HttpCallResult.Completed(Status, ""));
            }
        }
    }
}
