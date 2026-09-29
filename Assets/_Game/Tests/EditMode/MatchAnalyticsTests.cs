using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Backend;
using Game.Bootstrap;
using Game.Core.Lobby;
using Game.Core.Match;
using Game.Core.Ports;
using Game.Core.Rooms;
using Game.Network.Match;
using Game.Server.Match;
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

        // The recorder's gating decides whether a match is collected at all, and a
        // dedicated server reaches it by a different route than a player host does.
        // Both routes are covered here (S15P21D205-1021).

        [Test]
        public void DedicatedServerHost_RecordsTheMatch_WithoutEnteringThroughTheRoomBrowser()
        {
            using var room = new RoomBrowserSystem();
            room.MatchStarted(LineUp());
            var source = new FakeAnalyticsSource { IsServer = true, IsDedicatedServer = true };
            Assert.That(room.IsInRoom.CurrentValue, Is.False,
                "precondition: a dedicated server never records a room entry");

            var stored = RunMatch(source, room);

            Assert.That(stored, Is.Not.Empty, "the server collected nothing");
            Assert.That(stored[0], Does.Contain("\"eventName\":\"match_start\"")
                .And.Contain("\"eventName\":\"position_sample\"")
                .And.Contain("\"eventName\":\"player_result\"")
                .And.Contain("\"eventName\":\"match_end\""));
        }

        [Test]
        public void PlayerHost_StillRecordsTheMatch()
        {
            using var room = new RoomBrowserSystem();
            Enter(room, "7K2M9P");
            room.MatchStarted(LineUp());
            var source = new FakeAnalyticsSource { IsServer = true, IsDedicatedServer = false };

            Assert.That(RunMatch(source, room), Is.Not.Empty);
        }

        [Test]
        public void Client_RecordsNothing_EvenInsideARoom()
        {
            using var room = new RoomBrowserSystem();
            Enter(room, "7K2M9P");
            room.MatchStarted(LineUp());
            var source = new FakeAnalyticsSource { IsServer = false, IsDedicatedServer = false };

            Assert.That(RunMatch(source, room), Is.Empty, "only the authority collects");
        }

        [Test]
        public void RoomCode_ComesFromTheSession_NotTheRoomBrowser()
        {
            using var room = new RoomBrowserSystem();
            room.MatchStarted(LineUp());
            var source = new FakeAnalyticsSource
            {
                IsServer = true, IsDedicatedServer = true, RoomCode = "DEV001"
            };
            Assert.That(room.RoomCode.CurrentValue, Is.Null,
                "precondition: the server's room browser has no code");

            Assert.That(RunMatch(source, room)[0], Does.Contain("\"roomCode\":\"DEV001\""));
        }

        /// <summary>
        /// Plays one short match and returns what the outbox kept. The transport
        /// cancels every send so the file stays on disk for the assertions.
        /// </summary>
        private static string[] RunMatch(FakeAnalyticsSource source, RoomBrowserSystem room)
        {
            var path = Path.Combine(Path.GetTempPath(), "analytics-test-" + Guid.NewGuid());
            var upload = new MatchAnalyticsUpload(
                new FakeTransport { Cancel = true }, new BackendEndpoint("http://localhost"), path);
            var recorder = new MatchAnalyticsRecorder(source, room, upload);
            try
            {
                recorder.Start();
                source.RaisePhase(MatchPhase.Hiding);
                recorder.Tick();
                source.RaiseObjects(Array.Empty<MatchObjectStateSnapshot>());
                source.ServerTime += 1d;
                recorder.Tick();
                source.RaiseResult();
                return Directory.Exists(path)
                    ? Array.ConvertAll(Directory.GetFiles(path, "*.jsonl"), File.ReadAllText)
                    : Array.Empty<string>();
            }
            finally
            {
                recorder.Dispose();
                if (Directory.Exists(path)) Directory.Delete(path, true);
            }
        }

        private static MatchParticipant[] LineUp() =>
            new[] { new MatchParticipant("P1", 0, "11111111-1111-1111-1111-111111111111") };

        /// <summary>Goes through the commands, the way the screen does, so the room records the entry itself.</summary>
        private static void Enter(RoomBrowserSystem room, string code)
        {
            new RoomUiCommands(new OpeningBrowser(code), room)
                .EnterByCodeAsync(code, null, CancellationToken.None).GetAwaiter().GetResult();
            Assert.That(room.IsInRoom.CurrentValue, Is.True, "precondition: the room was entered");
        }

        private sealed class OpeningBrowser : IRoomBrowser
        {
            private readonly string code;
            public OpeningBrowser(string code) => this.code = code;

            public UniTask<RoomEntryFailure> RefreshAsync(CancellationToken cancellation) =>
                UniTask.FromResult(RoomEntryFailure.None);

            public UniTask<RoomEntryResult> CreateAsync(RoomCreateRequest request, CancellationToken cancellation) =>
                UniTask.FromResult(RoomEntryResult.Opened(code));

            public UniTask<RoomEntryResult> EnterAsync(RoomId room, string password, CancellationToken cancellation) =>
                UniTask.FromResult(RoomEntryResult.Opened(code));

            public UniTask<RoomEntryResult> EnterByCodeAsync(string roomCode, string password, CancellationToken cancellation) =>
                UniTask.FromResult(RoomEntryResult.Opened(code));

            public UniTask LeaveAsync(CancellationToken cancellation) => UniTask.CompletedTask;
        }

        private sealed class FakeAnalyticsSource : IMatchAnalyticsSource
        {
            public event Action<MatchStateSnapshot> MatchStateReceived;
            public event Action<MatchResult> MatchResultReceived;
            public event Action<IReadOnlyList<MatchObjectStateSnapshot>> ObjectStatesReceived;

            public bool IsRuntimeReady { get; set; } = true;
            public bool IsServer { get; set; }
            public bool IsDedicatedServer { get; set; }
            public string RoomCode { get; set; } = "7K2M9P";
            public double ServerTime { get; set; } = 100d;
            public string AnalyticsMapId => "supermarket";
            public MatchRuleSettings MatchRules => MatchRuleSettings.Default;
            public int DestructionLimit => 3;
            public MatchMigrationState MatchMigration => null;
            public IReadOnlyList<PlayerItemStatusSnapshot> LatestPlayerItemStatuses { get; } =
                Array.Empty<PlayerItemStatusSnapshot>();

            public bool TryGetPlayerPose(string playerId, out Pose pose)
            {
                pose = Pose.identity;
                return true;
            }

            public bool TryGetPlayerReplayState(string playerId, out NetworkPlayerReplayState state)
            {
                state = default;
                return false;
            }

            public (int HitsReceived, int Stuns) GetCombatTotals(int playerIndex) => default;

            public void RaisePhase(MatchPhase phase) =>
                MatchStateReceived?.Invoke(new MatchStateSnapshot(phase, ServerTime + 60d));

            public void RaiseObjects(IReadOnlyList<MatchObjectStateSnapshot> states) =>
                ObjectStatesReceived?.Invoke(states);

            public void RaiseResult() =>
                MatchResultReceived?.Invoke(new MatchResult(MatchEndReason.TimeExpired, ServerTime, new[] { 0 }));
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
