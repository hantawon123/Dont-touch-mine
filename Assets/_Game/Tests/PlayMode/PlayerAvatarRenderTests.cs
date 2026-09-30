#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using Game.Core.Ports;
using Game.Core.Rooms;
using Game.Network.Players;
using NUnit.Framework;
using UnityEditor;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.TestTools;
using Assert = NUnit.Framework.Assert;

namespace Game.Tests.PlayMode
{
    public sealed class PlayerAvatarRenderTests
    {
        NetworkRunner runner;

        [UnityTest]
        public IEnumerator StableRenderAndRosterChanges()
        {
            runner = new GameObject("Avatar render regression").AddComponent<NetworkRunner>();
            runner.gameObject.AddComponent<Photon.Voice.Unity.VoiceConnection>().enabled = false;
            var sink = new Sink();
            var roster = runner.gameObject.AddComponent<PlayerRoster>();
            roster.Bind(sink);
            var start = runner.StartGame(new StartGameArgs { GameMode = GameMode.Single });
            var deadline = Time.realtimeSinceStartup + 30f;
            while (!start.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(start.IsCompleted && start.Result.Ok, Is.True);
            var prefab = AssetDatabase.LoadAssetAtPath<NetworkObject>("Assets/_Game/Content/Prefabs/NetworkedPlayer.prefab");
            var first = runner.Spawn(prefab, inputAuthority: runner.LocalPlayer);
            var avatar = first.GetComponent<PlayerAvatar>();
            avatar.Nickname = "검증😀사용자";
            avatar.UserId = "12345678-1234-1234-1234-123456789abc";
            avatar.Render();
            Assert.That(sink.Rows[0].Nickname, Is.EqualTo("검증😀사용자"));
            Assert.That(sink.Rows[0].UserId, Is.EqualTo("12345678-1234-1234-1234-123456789abc"));
            for (var i = 0; i < 100; i++) avatar.Render();
            var publishes = sink.Publishes;
            var watch = new System.Diagnostics.Stopwatch();
            using var allocations = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GC.Alloc", 1,
                ProfilerRecorderOptions.SumAllSamplesInFrame | ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
            GC.KeepAlive(new byte[512]);
            allocations.Stop();
            Assert.That(allocations.Count, Is.GreaterThan(0), "Allocation recorder positive control must work.");
            Assert.That(allocations.GetSample(0).Count, Is.GreaterThan(0));
            allocations.Reset(); allocations.Start();
            for (var i = 0; i < 10000; i++) runner.GetComponent<Game.Network.Voice.VoiceRig>();
            allocations.Stop();
            var editorLookupAllocations = allocations.Count == 0 ? 0 : allocations.GetSample(0).Count;
            TestContext.WriteLine("EDITOR_MISSING_VOICE_RIG_LOOKUP allocations=" + editorLookupAllocations);
            allocations.Reset();
            allocations.Start();
            watch.Start();
            for (var i = 0; i < 10000; i++) avatar.Render();
            watch.Stop();
            allocations.Stop();
            var allocationCount = allocations.Count == 0 ? 0 : allocations.GetSample(0).Count;
            Assert.That(sink.Publishes, Is.EqualTo(publishes), "Unchanged frames must not republish the roster.");
            TestContext.WriteLine($"STABLE_RENDER calls=10000 allocations={allocationCount} elapsedTicks={watch.ElapsedTicks} frequency={System.Diagnostics.Stopwatch.Frequency}");

            avatar.Nickname = "변경😀";
            avatar.UserId = "abcdefab-1234-1234-1234-123456789abc";
            avatar.Render();
            Assert.That(sink.Publishes, Is.EqualTo(++publishes));
            Assert.That(sink.Rows[0].Nickname, Is.EqualTo("변경😀"));
            Assert.That(sink.Rows[0].UserId, Is.EqualTo("abcdefab-1234-1234-1234-123456789abc"));
            avatar.UserId = "account-only-changed";
            avatar.Render();
            Assert.That(sink.Publishes, Is.EqualTo(++publishes));
            Assert.That(sink.Rows[0].Nickname, Is.EqualTo("변경😀"));
            Assert.That(sink.Rows[0].UserId, Is.EqualTo("account-only-changed"));
            avatar.IsHost = true; avatar.Render();
            Assert.That(sink.Publishes, Is.EqualTo(++publishes));
            Assert.That(sink.Rows[0].IsHost, Is.True);
            avatar.IsMuted = true; avatar.Render();
            Assert.That(sink.Publishes, Is.EqualTo(++publishes));
            Assert.That(sink.Rows[0].IsMuted, Is.True);
            avatar.IsListening = false; avatar.Render();
            Assert.That(sink.Publishes, Is.EqualTo(++publishes));
            Assert.That(sink.Rows[0].IsListening, Is.False);
            avatar.Nickname = ""; avatar.UserId = ""; avatar.Render();
            Assert.That(sink.Publishes, Is.EqualTo(++publishes));
            Assert.That(sink.Rows[0].Nickname, Is.Empty);
            Assert.That(sink.Rows[0].UserId, Is.Empty);

            var later = runner.Spawn(prefab, inputAuthority: PlayerRef.FromIndex(1), onBeforeSpawned: (_, obj) =>
            {
                var joined = obj.GetComponent<PlayerAvatar>();
                joined.Seat = 1; joined.Nickname = "후발😀"; joined.UserId = "late-account";
            });
            Assert.That(sink.Rows.Count, Is.EqualTo(2));
            Assert.That(sink.Rows[1].Nickname, Is.EqualTo("후발😀"));
            Assert.That(sink.Rows[1].UserId, Is.EqualTo("late-account"));
            publishes = sink.Publishes;
            later.GetComponent<PlayerAvatar>().Render();
            Assert.That(sink.Publishes, Is.EqualTo(publishes), "Spawned must seed the same cache used by Render.");
            runner.Despawn(first);
            Assert.That(sink.Rows.Count, Is.EqualTo(1));
            Assert.That(sink.Rows[0].UserId, Is.EqualTo("late-account"));
            runner.Despawn(later);
            Assert.That(sink.Rows, Is.Empty);
            // This Editor fixture has no VoiceRig: Unity's missing-component lookup
            // allocates an Editor wrapper. Compare against that measured control.
            Assert.That(allocationCount, Is.EqualTo(editorLookupAllocations),
                "Stable names and account IDs must add no allocations beyond the Editor lookup control.");
        }

        [UnityTearDown]
        public IEnumerator Shutdown()
        {
            if (runner == null) yield break;
            var shutdown = runner.Shutdown();
            while (!shutdown.IsCompleted) yield return null;
            if (runner != null) UnityEngine.Object.Destroy(runner.gameObject);
            runner = null;
        }

        sealed class Sink : IRoomParticipantSink
        {
            public int Publishes;
            public readonly List<RoomParticipant> Rows = new();
            public void SetParticipants(IReadOnlyList<RoomParticipant> participants)
            {
                Publishes++; Rows.Clear();
                foreach (var row in participants) Rows.Add(row);
            }
            public void SetLocalPlayer(string playerId) { }
        }
    }
}
#endif
