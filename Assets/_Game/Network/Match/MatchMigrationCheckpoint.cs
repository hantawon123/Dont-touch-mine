using System;
using Fusion;
using Game.Core.Lobby;
using Game.Core.Match;
using Game.Network.Players;
using Game.Server.Match;
using UnityEngine;

namespace Game.Network.Match
{
    internal struct MigrationPose : INetworkStruct
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public MigrationPose(Pose pose) { Position = pose.position; Rotation = pose.rotation; }
        public readonly Pose ToPose() => new(Position, Rotation);
    }

    internal struct MigrationPlayer : INetworkStruct
    {
        public NetworkString<_16> ItemId;
        public MigrationPose Pose, HidingSpawn, SearchingSpawn, Placement;
        public NetworkBool Active, CompletedTurn, HasPlacement, AutoPlaced;
        public int HitCount, DestructionUses;
        public double StunEndsAt, InvulnerableEndsAt;

        public static MigrationPlayer From(MatchMigrationPlayer value) => new()
        {
            ItemId = value.ItemId, Pose = new(value.Pose), HidingSpawn = new(value.HidingSpawn),
            SearchingSpawn = new(value.SearchingSpawn), Placement = new(value.Placement),
            Active = value.Active, CompletedTurn = value.CompletedHidingTurn,
            HasPlacement = value.HasPlacement, AutoPlaced = value.AutoPlaced,
            HitCount = value.HitCount, DestructionUses = value.DestructionUses,
            StunEndsAt = value.StunEndsAt, InvulnerableEndsAt = value.InvulnerableEndsAt,
        };

        public readonly MatchMigrationPlayer ToState(string playerId) => new()
        {
            PlayerId = playerId, ItemId = ItemId.ToString(), Pose = Pose.ToPose(),
            HidingSpawn = HidingSpawn.ToPose(), SearchingSpawn = SearchingSpawn.ToPose(),
            Placement = Placement.ToPose(), Active = Active, CompletedHidingTurn = CompletedTurn,
            HasPlacement = HasPlacement, AutoPlaced = AutoPlaced, HitCount = HitCount,
            DestructionUses = DestructionUses, StunEndsAt = StunEndsAt, InvulnerableEndsAt = InvulnerableEndsAt,
        };
    }

    internal struct MigrationEjection : INetworkStruct
    {
        public double EndsAt;
        public MigrationPose Pose;
    }

    // Included in the authoritative Fusion snapshot, not in ordinary client replication:
    // assignments must remain private until this peer actually becomes the authority.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MatchSessionState))]
    public sealed class MatchMigrationCheckpoint : NetworkBehaviour
    {
        [Networked] public int PlayerCount { get; set; }
        [Networked] public double CapturedAt { get; set; }
        [Networked, Capacity(MatchSessionState.MaxParticipants)]
        internal NetworkArray<MigrationPlayer> Players => default;
        [Networked, Capacity(MatchSessionState.MaxReplicatedObjects)]
        internal NetworkArray<MigrationEjection> Ejections => default;

        public override void Spawned()
        {
            if (Object.HasStateAuthority) ReplicateToAll(false);
        }

        internal void Capture(MatchSessionCoordinator session, MatchSessionState state, PlayerRoster roster)
        {
            if (!Object.HasStateAuthority || session == null) return;
            for (var i = 0; i < session.Assignments.Count; i++)
            {
                var pose = Players.Get(i).Pose.ToPose();
                if (roster != null && roster.TryGetPose(session.Players.GetPlayer(i).PlayerId, out var current))
                    pose = current;
                Players.Set(i, MigrationPlayer.From(session.CaptureMigrationPlayer(i, pose)));
            }
            for (var i = 0; i < state.ObjectStateCount; i++)
            {
                session.TryGetPendingEjection(state.ObjectStates.Get(i).ObjectId.ToString(), out var at, out var pose);
                Ejections.Set(i, new MigrationEjection { EndsAt = at, Pose = new MigrationPose(pose) });
            }
            PlayerCount = session.Assignments.Count;
            CapturedAt = Runner.SimulationTime;
        }

        internal MatchMigrationState Read(MatchSessionState state)
        {
            if (!Object.HasStateAuthority || PlayerCount != state.ParticipantCount || PlayerCount < RoomSettings.MinMatchPlayerCount ||
                PlayerCount > MatchSessionState.MaxParticipants || state.ObjectStateCount < 0 ||
                state.ObjectStateCount > MatchSessionState.MaxReplicatedObjects)
                throw new InvalidOperationException("The host snapshot has no valid match checkpoint.");
            var snapshot = new MatchMigrationState
            {
                CapturedAt = CapturedAt, Phase = new MatchStateSnapshot(state.Phase, state.PhaseEndsAt),
                Players = new MatchMigrationPlayer[PlayerCount],
                Objects = new MatchMigrationObject[state.ObjectStateCount],
            };
            for (var i = 0; i < PlayerCount; i++)
            {
                snapshot.Players[i] = Players.Get(i).ToState(state.Participants.Get(i).ToString());
                snapshot.Players[i].Active = state.ParticipantActive.Get(i);
            }
            for (var i = 0; i < snapshot.Objects.Length; i++)
            {
                var item = state.ObjectStates.Get(i);
                var ejection = Ejections.Get(i);
                snapshot.Objects[i] = new MatchMigrationObject
                {
                    ObjectId = item.ObjectId.ToString(), Pose = new Pose(item.Position, item.Rotation),
                    Holder = item.HolderPlayerIndex, Destroyed = item.IsDestroyed,
                    PendingEjection = item.IsPendingEjection, EjectsAt = ejection.EndsAt,
                    EjectionPose = ejection.Pose.ToPose(),
                };
            }
            if (state.HasResult)
            {
                var winners = new int[state.WinnerCount];
                for (var i = 0; i < winners.Length; i++) winners[i] = state.WinnerPlayerIndices.Get(i);
                snapshot.Result = new MatchResult(state.ResultEndReason, state.ResultEndedAt, winners);
            }
            return snapshot;
        }
    }
}
