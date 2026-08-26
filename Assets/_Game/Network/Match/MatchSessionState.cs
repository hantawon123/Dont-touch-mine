using System;
using System.Collections.Generic;
using Fusion;
using Game.Core.Lobby;
using Game.Core.Match;
using UnityEngine;

namespace Game.Network.Match
{
    public readonly struct MatchObjectStateSnapshot
    {
        public MatchObjectStateSnapshot(
            string objectId,
            int holderPlayerIndex,
            Pose pose,
            Vector3 initialVelocity,
            bool isDestroyed,
            int version)
        {
            if (string.IsNullOrWhiteSpace(objectId))
            {
                throw new ArgumentException("Object id is required.", nameof(objectId));
            }

            if (holderPlayerIndex < -1 || version < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(holderPlayerIndex));
            }

            ObjectId = objectId.Trim();
            HolderPlayerIndex = holderPlayerIndex;
            Pose = pose;
            InitialVelocity = initialVelocity;
            IsDestroyed = isDestroyed;
            Version = version;
        }

        public string ObjectId { get; }
        public int HolderPlayerIndex { get; }
        public Pose Pose { get; }
        public Vector3 InitialVelocity { get; }
        public bool IsDestroyed { get; }
        public int Version { get; }
    }

    internal struct ReplicatedObjectState : INetworkStruct
    {
        public int HolderPlayerIndex;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 InitialVelocity;
        public NetworkBool IsDestroyed;
        public int Version;
    }

    /// <summary>
    /// The room's shared answer to "has the match started, and who is playing".
    /// One of these exists per room, spawned by the authority when the session
    /// opens.
    /// </summary>
    /// <remarks>
    /// Separate from the characters because it outlives any one of them and
    /// belongs to the room rather than a player. Holding it on a character would
    /// tie the match to whoever happened to spawn first.
    /// <para>
    /// The roster is replicated rather than worked out per peer. Every peer can
    /// see the seats, but they would each freeze them at a slightly different
    /// moment, and a match where two players believe they are the same
    /// <c>playerIndex</c> is unrecoverable.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class MatchSessionState : NetworkBehaviour
    {
        /// <summary>Largest room the rules allow, so the array never resizes.</summary>
        public const int MaxParticipants = RoomSettings.MaxPlayerCount;

        // ponytail: 64 covers the MVP carryables; raise this if a map exceeds it.
        public const int MaxReplicatedObjects = 64;

        [Networked]
        public bool IsStarted { get; set; }

        /// <summary>
        /// How much of <see cref="Participants"/> is in use. The array is a
        /// fixed size, so its length says nothing about the room.
        /// </summary>
        [Networked]
        public int ParticipantCount { get; set; }

        /// <summary>
        /// Player ids in play order. The position in this array is the
        /// <c>playerIndex</c> the match rules use, and it never moves once the
        /// match has started.
        /// </summary>
        /// <remarks>
        /// Not the seat number. Seats are reused as people come and go and can
        /// have gaps, so seat 5 may well be index 2.
        /// </remarks>
        [Networked, Capacity(MaxParticipants)]
        public NetworkArray<NetworkString<_16>> Participants => default;

        [Networked]
        public MatchPhase Phase { get; set; }

        [Networked]
        public double PhaseEndsAt { get; set; }

        [Networked, Capacity(MaxReplicatedObjects)]
        internal NetworkDictionary<NetworkString<_64>, ReplicatedObjectState>
            ObjectStates => default;

        [Networked]
        public int ObjectStateRevision { get; set; }

        private bool _publishedStarted;
        private int _publishedCount = -1;
        private MatchPhase _publishedPhase = (MatchPhase)(-1);
        private double _publishedPhaseEndsAt = -1d;
        private int _publishedObjectStateRevision = -1;

        public override void Spawned()
        {
            PublishLineUp();
            PublishSnapshot();
            PublishObjectStates();
        }

        /// <summary>
        /// Watches for the authority's decision arriving. Compared against what
        /// was last reported rather than using a change detector, because the
        /// two fields that matter are cheap to compare and the ids behind them
        /// never change after the match starts.
        /// </summary>
        public override void Render()
        {
            if (_publishedStarted != IsStarted || _publishedCount != ParticipantCount)
            {
                PublishLineUp();
            }

            if (_publishedPhase != Phase || _publishedPhaseEndsAt != PhaseEndsAt)
            {
                PublishSnapshot();
            }

            if (_publishedObjectStateRevision != ObjectStateRevision)
            {
                PublishObjectStates();
            }
        }

        /// <summary>
        /// Writes the confirmed line-up. Authority only; everyone else receives
        /// it through replication.
        /// </summary>
        public void Confirm(string[] participantIds)
        {
            var count = Mathf.Min(participantIds.Length, MaxParticipants);

            for (var index = 0; index < count; index++)
            {
                Participants.Set(index, participantIds[index]);
            }

            ParticipantCount = count;
            IsStarted = true;
        }

        public bool TrySetSnapshot(MatchStateSnapshot snapshot)
        {
            if (Object == null || !Object.HasStateAuthority)
            {
                return false;
            }

            Phase = snapshot.Phase;
            PhaseEndsAt = snapshot.PhaseEndsAt;
            return true;
        }

        public bool TrySendItemAssignment(PlayerRef target, string itemId)
        {
            if (Object == null || !Object.HasStateAuthority ||
                !target.IsRealPlayer || string.IsNullOrWhiteSpace(itemId))
            {
                return false;
            }

            RPC_AssignItem(target, itemId.Trim());
            return true;
        }

        public bool TrySetObjectHeld(string objectId, int holderPlayerIndex)
        {
            if (holderPlayerIndex < 0 || !TryGetWritableState(objectId, out var key, out var state))
            {
                return false;
            }

            state.HolderPlayerIndex = holderPlayerIndex;
            state.InitialVelocity = default;
            return WriteObjectState(key, state);
        }

        public bool CanTrackObject(string objectId)
        {
            if (Object == null || !Object.HasStateAuthority ||
                string.IsNullOrWhiteSpace(objectId))
            {
                return false;
            }

            NetworkString<_64> key = objectId.Trim();
            return ObjectStates.ContainsKey(key) ||
                   ObjectStates.Count < MaxReplicatedObjects;
        }

        public bool TrySetObjectReleased(
            string objectId,
            Pose pose,
            Vector3 initialVelocity = default)
        {
            if (!TryGetWritableState(objectId, out var key, out var state))
            {
                return false;
            }

            state.HolderPlayerIndex = -1;
            state.Position = pose.position;
            state.Rotation = pose.rotation;
            state.InitialVelocity = initialVelocity;
            return WriteObjectState(key, state);
        }

        public bool TrySetObjectDestroyed(string objectId)
        {
            if (!TryGetWritableState(objectId, out var key, out var state))
            {
                return false;
            }

            state.HolderPlayerIndex = -1;
            state.InitialVelocity = default;
            state.IsDestroyed = true;
            return WriteObjectState(key, state);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_AssignItem([RpcTarget] PlayerRef target, string itemId)
        {
            StarterOf(Runner)?.PublishItemAssignment(itemId);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_RequestHold(string objectId, RpcInfo info = default)
        {
            StarterOf(Runner)?.TryHoldObject(info.Source, objectId);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_RequestRelease(
            Vector3 position,
            Quaternion rotation,
            RpcInfo info = default)
        {
            StarterOf(Runner)?.TryReleaseHeldObject(
                info.Source,
                new Pose(position, rotation));
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_RequestThrow(
            Vector3 position,
            Quaternion rotation,
            Vector3 initialVelocity,
            RpcInfo info = default)
        {
            StarterOf(Runner)?.TryThrowHeldObject(
                info.Source,
                new Pose(position, rotation),
                initialVelocity);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_RequestHit(int targetPlayerIndex, RpcInfo info = default)
        {
            StarterOf(Runner)?.TryHitPlayer(info.Source, targetPlayerIndex);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_RequestShredder(RpcInfo info = default)
        {
            StarterOf(Runner)?.TryUseShredder(info.Source);
        }

        /// <summary>
        /// Reports the room's answer to whoever is listening on this peer.
        /// </summary>
        private void PublishLineUp()
        {
            _publishedStarted = IsStarted;
            _publishedCount = ParticipantCount;

            StarterOf(Runner)?.Publish(this);
        }

        private void PublishSnapshot()
        {
            _publishedPhase = Phase;
            _publishedPhaseEndsAt = PhaseEndsAt;

            StarterOf(Runner)?.PublishSnapshot(new MatchStateSnapshot(Phase, PhaseEndsAt));
        }

        private void PublishObjectStates()
        {
            _publishedObjectStateRevision = ObjectStateRevision;
            var states = ObjectStates;
            var snapshots = new MatchObjectStateSnapshot[states.Count];
            var index = 0;

            foreach (var pair in states)
            {
                var state = pair.Value;
                snapshots[index++] = new MatchObjectStateSnapshot(
                    pair.Key.ToString(),
                    state.HolderPlayerIndex,
                    new Pose(state.Position, state.Rotation),
                    state.InitialVelocity,
                    state.IsDestroyed,
                    state.Version);
            }

            StarterOf(Runner)?.PublishObjectStates(snapshots);
        }

        private bool TryGetWritableState(
            string objectId,
            out NetworkString<_64> key,
            out ReplicatedObjectState state)
        {
            key = default;
            state = default;
            if (!CanTrackObject(objectId))
            {
                return false;
            }

            key = objectId.Trim();
            if (!ObjectStates.TryGet(key, out state))
            {
                state.HolderPlayerIndex = -1;
                state.Rotation = Quaternion.identity;
            }

            return true;
        }

        private bool WriteObjectState(
            NetworkString<_64> key,
            ReplicatedObjectState state)
        {
            state.Version++;
            var states = ObjectStates;
            states.Set(key, state);
            ObjectStateRevision++;
            return true;
        }

        /// <summary>
        /// The starter sits on the runner object, which is the one place a
        /// Fusion-spawned object can reach without being injected.
        /// </summary>
        private static MatchStarter StarterOf(NetworkRunner runner)
        {
            return runner == null ? null : runner.GetComponent<MatchStarter>();
        }
    }
}
