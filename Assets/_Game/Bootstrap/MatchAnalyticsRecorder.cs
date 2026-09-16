using System;
using System.Collections.Generic;
using Game.Backend;
using Game.Core.Lobby;
using Game.Core.Match;
using Game.Network.Match;
using Game.Network.Session;
using Game.Server.Match;
using UnityEngine;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>Only the host samples the confirmed match. Independent of highlight recording.</summary>
    public sealed class MatchAnalyticsRecorder : IStartable, ITickable, IDisposable
    {
        private readonly NetworkRunnerService network;
        private readonly RoomBrowserSystem room;
        private readonly MatchAnalyticsUpload upload;
        private readonly Dictionary<string, MatchObjectStateSnapshot> objects = new();
        private IReadOnlyList<MatchObjectStateSnapshot> latestObjects;
        private MatchAnalyticsBuffer buffer;
        private MatchParticipant[] players;
        private MatchPhase phase = MatchPhase.Waiting;
        private bool ended, partial;
        private string roomCode, mapId;
        private double lastTime;

        public MatchAnalyticsRecorder(NetworkRunnerService network, RoomBrowserSystem room, MatchAnalyticsUpload upload)
        {
            this.network = network;
            this.room = room;
            this.upload = upload;
        }

        public void Start()
        {
            network.MatchStateReceived += OnPhase;
            network.MatchResultReceived += OnResult;
            network.ObjectStatesReceived += OnObjects;
            upload.Retry();
        }

        public void Tick()
        {
            if (!network.IsRuntimeReady || !room.IsInRoom.CurrentValue)
            {
                if (buffer != null) Finish("Interrupted", true);
                phase = MatchPhase.Waiting;
                ended = false;
                latestObjects = null;
                return;
            }
            if (!network.IsServer)
            {
                if (buffer != null) Finish("Interrupted", true);
                return;
            }
            if (ended) return;
            if (phase != MatchPhase.Hiding && phase != MatchPhase.Searching) return;
            lastTime = network.ServerTime;
            if (buffer == null)
            {
                var lineUp = room.MatchParticipants.CurrentValue;
                if (lineUp.Count == 0) return;
                players = new MatchParticipant[lineUp.Count];
                for (var i = 0; i < players.Length; i++) players[i] = lineUp[i];
                roomCode = room.RoomCode.CurrentValue;
                mapId = network.AnalyticsMapId;
                partial = phase != MatchPhase.Hiding || network.MatchMigration != null;
                buffer = new MatchAnalyticsBuffer(lastTime);
                var rules = network.MatchRules;
                Add("match_start", new MatchAnalyticsParams {
                    player_count = players.Length, hide_sec = rules.HidingDurationSeconds,
                    seek_sec = rules.SearchingDurationSeconds, sprint_multiplier = rules.SprintMultiplier,
                    stun_hits = rules.StunHitCount, destroy_limit = network.DestructionLimit,
                    build_ver = Application.version, partial = partial });
                Add("phase_change", new MatchAnalyticsParams { from = "Waiting", to = phase.ToString() });
            }
            if (!buffer.IsSampleDue(lastTime)) return;
            objects.Clear();
            if (latestObjects != null)
                foreach (var state in latestObjects) objects[state.ObjectId] = state;
            foreach (var player in players)
            {
                if (!network.TryGetPlayerPose(player.PlayerId, out var pose)) continue;
                var data = new MatchAnalyticsParams { seat = player.PlayerIndex, rotation_y = pose.rotation.eulerAngles.y };
                (data.total_hits_received, data.total_stuns) = network.GetCombatTotals(player.PlayerIndex);
                if (network.TryGetPlayerReplayState(player.PlayerId, out var action))
                {
                    data.posture = action.Posture.ToString();
                    data.grounded = action.Grounded;
                    data.attack_sequence = action.AttackSequence;
                }
                var statuses = network.LatestPlayerItemStatuses;
                if (player.PlayerIndex >= 0 && player.PlayerIndex < statuses.Count)
                {
                    data.item_id = statuses[player.PlayerIndex].ItemId;
                    data.item_destroyed = statuses[player.PlayerIndex].IsDestroyed;
                    if (data.item_id != null && objects.TryGetValue(data.item_id, out var item))
                    {
                        data.item_known = true;
                        data.holder_seat = item.HolderPlayerIndex;
                        data.item_x = item.Pose.position.x; data.item_y = item.Pose.position.y; data.item_z = item.Pose.position.z;
                        data.item_in_motion = item.IsPhysicsActive || item.HolderPlayerIndex >= 0;
                    }
                }
                Add("position_sample", data, player, pose.position);
            }
        }

        private void OnPhase(MatchStateSnapshot snapshot)
        {
            if (snapshot.Phase == phase) return;
            var previous = phase;
            phase = snapshot.Phase;
            if (buffer != null)
            {
                lastTime = network.ServerTime;
                Add("phase_change", new MatchAnalyticsParams { from = previous.ToString(), to = phase.ToString() });
            }
            if (phase == MatchPhase.Waiting)
            {
                if (buffer != null) Finish("Interrupted", true);
                ended = false;
                objects.Clear();
                latestObjects = null;
            }
        }

        private void OnObjects(IReadOnlyList<MatchObjectStateSnapshot> states)
        {
            latestObjects = states;
        }

        private void OnResult(MatchResult result)
        {
            if (buffer == null || !network.IsServer) return;
            lastTime = result.EndedAt;
            foreach (var player in players)
            {
                var won = false;
                foreach (var winner in result.WinnerPlayerIndices) if (winner == player.PlayerIndex) won = true;
                network.TryGetPlayerPose(player.PlayerId, out var pose);
                var data = new MatchAnalyticsParams { seat = player.PlayerIndex, result = won ? "Winner" : "Loser" };
                (data.total_hits_received, data.total_stuns) = network.GetCombatTotals(player.PlayerIndex);
                Add("player_result", data, player, pose.position);
            }
            Finish(result.EndReason.ToString(), partial);
        }

        private void Finish(string reason, bool incomplete)
        {
            Add("match_end", new MatchAnalyticsParams { end_reason = reason, partial = incomplete,
                expected_events = buffer.Count + 1, dropped_samples = buffer.DroppedSamples });
            var completed = buffer;
            buffer = null;
            ended = true;
            upload.Store(completed);
        }

        private void Add(string name, MatchAnalyticsParams data, MatchParticipant? player = null, Vector3 position = default)
        {
            var user = player?.UserId;
            buffer.Add(new MatchAnalyticsEvent { eventName = name, phase = phase.ToString(), roomCode = roomCode,
                mapId = mapId, userPublicId = Guid.TryParse(user, out _) ? user : null,
                posX = position.x, posY = position.y, posZ = position.z, @params = data }, lastTime);
        }

        public void Dispose()
        {
            network.MatchStateReceived -= OnPhase;
            network.MatchResultReceived -= OnResult;
            network.ObjectStatesReceived -= OnObjects;
            // An abrupt browser/process close cannot guarantee this callback or HTTP completion.
            if (buffer != null) Finish("Interrupted", true);
            upload.Dispose();
        }
    }
}
