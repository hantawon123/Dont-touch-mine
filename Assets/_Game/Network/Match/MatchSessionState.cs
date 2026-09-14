using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using Fusion;
using Game.Core.Lobby;
using Game.Core.Match;
using Game.Core.Rooms;
using Game.Server.Items;
using Game.Server.Match;
using UnityEngine;

namespace Game.Network.Match
{
    public readonly struct PlayerInteractionStateSnapshot
    {
        public PlayerInteractionStateSnapshot(
            int playerIndex,
            double stunEndsAt,
            int remainingDestructionUses,
            int hitCount = 0)
        {
            if (playerIndex < 0 ||
                double.IsNaN(stunEndsAt) ||
                double.IsInfinity(stunEndsAt) ||
                stunEndsAt < 0d ||
                remainingDestructionUses < 0 ||
                hitCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(playerIndex));
            }

            PlayerIndex = playerIndex;
            StunEndsAt = stunEndsAt;
            RemainingDestructionUses = remainingDestructionUses;
            HitCount = hitCount;
        }

        public int PlayerIndex { get; }
        public double StunEndsAt { get; }
        public int RemainingDestructionUses { get; }
        public int HitCount { get; }
        public bool IsStunned(double serverTime) => serverTime < StunEndsAt;
    }

    public readonly struct MatchObjectStateSnapshot
    {
        public MatchObjectStateSnapshot(
            string objectId,
            int holderPlayerIndex,
            Pose pose,
            Vector3 initialVelocity,
            bool isDestroyed,
            int version,
            bool isPhysicsActive = false,
            bool isPendingEjection = false)
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
            IsPhysicsActive = isPhysicsActive;
            IsPendingEjection = isPendingEjection;
        }

        public string ObjectId { get; }
        public int HolderPlayerIndex { get; }
        public Pose Pose { get; }
        public Vector3 InitialVelocity { get; }
        public bool IsDestroyed { get; }
        public int Version { get; }
        public bool IsPhysicsActive { get; }
        public bool IsPendingEjection { get; }
    }

    internal struct ReplicatedObjectState : INetworkStruct
    {
        private const int DestroyedFlag = 1 << 0;
        private const int PhysicsActiveFlag = 1 << 1;
        private const int PendingEjectionFlag = 1 << 2;

        public NetworkString<_16> ObjectId;
        public int HolderPlayerIndex;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 InitialVelocity;
        public int Flags;
        public int Version;

        public bool IsDestroyed
        {
            readonly get => (Flags & DestroyedFlag) != 0;
            set => Flags = value ? Flags | DestroyedFlag : Flags & ~DestroyedFlag;
        }

        public bool IsPhysicsActive
        {
            readonly get => (Flags & PhysicsActiveFlag) != 0;
            set => Flags = value ? Flags | PhysicsActiveFlag : Flags & ~PhysicsActiveFlag;
        }

        public bool IsPendingEjection
        {
            readonly get => (Flags & PendingEjectionFlag) != 0;
            set => Flags = value ? Flags | PendingEjectionFlag : Flags & ~PendingEjectionFlag;
        }
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

        // Covers every carryable in Playground with room for later map props.
        // Fusion reserves this state capacity up front, so keep it close to the
        // real map maximum instead of treating it as an unbounded collection.
        public const int MaxReplicatedObjects = 256;
        public const int MaxObjectIdLength = 16;

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

        /// <summary>
        /// The backend account behind each entry of <see cref="Participants"/>,
        /// at the same index. Empty for a player who did not sign in.
        /// </summary>
        /// <remarks>
        /// Beside the line-up rather than looked up from the avatars, because a
        /// participant who leaves takes their avatar with them and the match
        /// still has to say whose actions those were. Replicated state, so a
        /// late joiner and the next host read it without being told.
        /// <para>
        /// 64 because the server's public id is a 36-character UUID and Fusion's
        /// fixed sizes are powers of two. Six of these add 1.5 KB to a state
        /// object that lives on a 64 KB heap page.
        /// </para>
        /// </remarks>
        [Networked, Capacity(MaxParticipants)]
        public NetworkArray<NetworkString<_64>> ParticipantUserIds => default;

        [Networked, Capacity(MaxParticipants)]
        public NetworkArray<NetworkBool> ParticipantActive => default;

        [Networked]
        public int ParticipantActivityRevision { get; set; }

        [Networked, Capacity(MaxParticipants)]
        public NetworkArray<double> StunEndsAt => default;

        [Networked, Capacity(MaxParticipants)]
        public NetworkArray<int> RemainingDestructionUses => default;

        [Networked, Capacity(MaxParticipants)]
        public NetworkArray<int> HitCounts => default;

        [Networked]
        public int PlayerInteractionStateRevision { get; set; }

        [Networked]
        public MatchPhase Phase { get; set; }

        [Networked]
        public double PhaseEndsAt { get; set; }

        [Networked]
        public double StartCountdownEndsAt { get; set; }

        [Networked]
        public int ObjectStateCount { get; set; }

        [Networked, Capacity(MaxReplicatedObjects)]
        internal NetworkArray<ReplicatedObjectState> ObjectStates => default;

        [Networked]
        public int ObjectStateRevision { get; set; }

        [Networked]
        public NetworkBool HasResult { get; set; }

        [Networked]
        public MatchEndReason ResultEndReason { get; set; }

        [Networked]
        public double ResultEndedAt { get; set; }

        [Networked]
        public int WinnerCount { get; set; }

        [Networked, Capacity(MaxParticipants)]
        public NetworkArray<int> WinnerPlayerIndices => default;

        private bool _publishedStarted;
        private int _publishedCount = -1;
        private MatchPhase _publishedPhase = (MatchPhase)(-1);
        private double _publishedPhaseEndsAt = -1d;
        private int _publishedObjectStateRevision = -1;
        private int _publishedParticipantActivityRevision = -1;
        private int _publishedPlayerInteractionStateRevision = -1;
        private bool _publishedHasResult;

        public override void Spawned()
        {
            PublishLineUp();
            PublishSceneState();
        }

        internal void PublishSceneState()
        {
            PublishSnapshot();
            PublishObjectStates();
            PublishParticipantActivity();
            PublishPlayerInteractionStates();
            PublishResult();
        }

        public override void FixedUpdateNetwork()
        {
            if (Object.HasStateAuthority)
            {
                StarterOf(Runner)?.PublishSimulationTick();
            }
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

            if (_publishedParticipantActivityRevision != ParticipantActivityRevision)
            {
                PublishParticipantActivity();
            }

            if (_publishedPlayerInteractionStateRevision !=
                PlayerInteractionStateRevision)
            {
                PublishPlayerInteractionStates();
            }

            if (_publishedHasResult != HasResult)
            {
                PublishResult();
            }
        }

        /// <summary>
        /// Writes the confirmed line-up. Authority only; everyone else receives
        /// it through replication.
        /// </summary>
        /// <param name="participantUserIds">
        /// One per participant, at the same index, or null to record none.
        /// A length that disagrees with <paramref name="participantIds"/> is
        /// refused loudly: the two arrays are read side by side, and a silent
        /// shift would credit one player's actions to another.
        /// </param>
        public void Confirm(string[] participantIds, string[] participantUserIds = null)
        {
            if (participantUserIds != null && participantUserIds.Length != participantIds.Length)
            {
                throw new ArgumentException(
                    "Participant user ids must line up with participant ids.",
                    nameof(participantUserIds));
            }

            ClearObjectStates();
            var count = Mathf.Min(participantIds.Length, MaxParticipants);

            for (var index = 0; index < count; index++)
            {
                Participants.Set(index, participantIds[index]);
                ParticipantUserIds.Set(index, participantUserIds?[index] ?? string.Empty);
                ParticipantActive.Set(index, true);
            }

            ParticipantCount = count;
            ParticipantActivityRevision++;
            IsStarted = true;
        }

        public bool TryResetForRematch()
        {
            if (Object == null || !Object.HasStateAuthority || !IsStarted)
            {
                return false;
            }

            for (var index = 0; index < ParticipantCount; index++)
            {
                Participants.Set(index, default);
                ParticipantUserIds.Set(index, default);
                ParticipantActive.Set(index, false);
                StunEndsAt.Set(index, 0d);
                RemainingDestructionUses.Set(index, 0);
                HitCounts.Set(index, 0);
            }

            for (var index = 0; index < WinnerCount; index++)
            {
                WinnerPlayerIndices.Set(index, 0);
            }

            for (var index = 0; index < ObjectStateCount; index++)
            {
                ObjectStates.Set(index, default);
            }

            ObjectStateCount = 0;
            ParticipantCount = 0;
            ParticipantActivityRevision++;
            PlayerInteractionStateRevision++;
            Phase = MatchPhase.Waiting;
            PhaseEndsAt = 0d;
            StartCountdownEndsAt = 0d;
            ObjectStateRevision++;
            HasResult = false;
            ResultEndReason = default;
            ResultEndedAt = 0d;
            WinnerCount = 0;
            IsStarted = false;
            return true;
        }

        private void ClearObjectStates()
        {
            for (var i = 0; i < ObjectStateCount; i++) ObjectStates.Set(i, default);
            ObjectStateCount = 0;
            ObjectStateRevision++;
        }

        public bool TryResetLobbyObjects(IReadOnlyList<WorldObjectState> objects)
        {
            if (Object == null || !Object.HasStateAuthority || IsStarted ||
                objects == null || objects.Count > MaxReplicatedObjects) return false;
            foreach (var item in objects) if (!IsValidObjectId(item.ObjectId)) return false;
            ClearObjectStates();
            return TryResetWorldObjects(objects);
        }

        public bool TrySetParticipantInactive(int playerIndex)
        {
            if (Object == null || !Object.HasStateAuthority ||
                playerIndex < 0 || playerIndex >= ParticipantCount ||
                !ParticipantActive.Get(playerIndex))
            {
                return false;
            }

            ParticipantActive.Set(playerIndex, false);
            ParticipantActivityRevision++;
            return true;
        }

        public bool TryInitializePlayerInteractionStates(
            IReadOnlyList<int> remainingDestructionUses)
        {
            if (Object == null ||
                !Object.HasStateAuthority ||
                remainingDestructionUses == null ||
                remainingDestructionUses.Count != ParticipantCount)
            {
                return false;
            }

            for (var playerIndex = 0;
                 playerIndex < remainingDestructionUses.Count;
                 playerIndex++)
            {
                if (remainingDestructionUses[playerIndex] < 0)
                {
                    return false;
                }
            }

            for (var playerIndex = 0;
                 playerIndex < remainingDestructionUses.Count;
                 playerIndex++)
            {
                StunEndsAt.Set(playerIndex, 0d);
                RemainingDestructionUses.Set(
                    playerIndex,
                    remainingDestructionUses[playerIndex]);
                HitCounts.Set(playerIndex, 0);
            }

            PlayerInteractionStateRevision++;
            return true;
        }

        public bool TrySetStunEndsAt(int playerIndex, double stunEndsAt)
        {
            if (!CanWritePlayerInteractionState(playerIndex) ||
                double.IsNaN(stunEndsAt) ||
                double.IsInfinity(stunEndsAt) ||
                stunEndsAt < 0d)
            {
                return false;
            }

            StunEndsAt.Set(playerIndex, stunEndsAt);
            PlayerInteractionStateRevision++;
            return true;
        }

        public bool TrySetRemainingDestructionUses(
            int playerIndex,
            int remainingUses)
        {
            if (!CanWritePlayerInteractionState(playerIndex) || remainingUses < 0)
            {
                return false;
            }

            RemainingDestructionUses.Set(playerIndex, remainingUses);
            PlayerInteractionStateRevision++;
            return true;
        }

        public bool TrySetHitCount(int playerIndex, int hitCount)
        {
            if (!CanWritePlayerInteractionState(playerIndex) || hitCount < 0)
            {
                return false;
            }

            HitCounts.Set(playerIndex, hitCount);
            PlayerInteractionStateRevision++;
            return true;
        }

        public bool TrySetResult(MatchResult result)
        {
            if (Object == null || !Object.HasStateAuthority || HasResult ||
                result.WinnerPlayerIndices.Count > MaxParticipants)
            {
                return false;
            }

            for (var index = 0; index < result.WinnerPlayerIndices.Count; index++)
            {
                var winnerPlayerIndex = result.WinnerPlayerIndices[index];
                if (winnerPlayerIndex < 0 || winnerPlayerIndex >= ParticipantCount)
                {
                    return false;
                }

                WinnerPlayerIndices.Set(index, winnerPlayerIndex);
            }

            ResultEndReason = result.EndReason;
            ResultEndedAt = result.EndedAt;
            WinnerCount = result.WinnerPlayerIndices.Count;
            HasResult = true;
            return true;
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

        public bool TrySetObjectHeld(string objectId, int holderPlayerIndex)
        {
            if (holderPlayerIndex < 0 || !TryGetWritableState(objectId, out var key, out var state))
            {
                return false;
            }

            state.HolderPlayerIndex = holderPlayerIndex;
            state.InitialVelocity = default;
            state.IsPhysicsActive = false;
            state.IsPendingEjection = false;
            return WriteObjectState(key, state);
        }

        public bool CanTrackObject(string objectId)
        {
            if (Object == null || !Object.HasStateAuthority ||
                string.IsNullOrWhiteSpace(objectId))
            {
                return false;
            }

            return IsValidObjectId(objectId) &&
                   (TryFindObjectState(objectId, out _, out _) ||
                    ObjectStateCount < MaxReplicatedObjects);
        }

        public bool CanHoldObject(string objectId)
        {
            if (Object == null || !Object.HasStateAuthority ||
                string.IsNullOrWhiteSpace(objectId))
            {
                return false;
            }

            return IsValidObjectId(objectId) &&
                   (!TryFindObjectState(objectId, out _, out var state) ||
                   state.HolderPlayerIndex < 0 &&
                   !state.IsDestroyed &&
                   !state.IsPendingEjection);
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
            state.IsPhysicsActive = true;
            state.IsPendingEjection = false;
            return WriteObjectState(key, state);
        }

        public bool TrySetObjectPendingEjection(string objectId, Pose pose)
        {
            if (!TryGetWritableState(objectId, out var key, out var state))
            {
                return false;
            }

            state.HolderPlayerIndex = -1;
            state.Position = pose.position;
            state.Rotation = pose.rotation;
            state.InitialVelocity = default;
            state.IsDestroyed = false;
            state.IsPhysicsActive = false;
            state.IsPendingEjection = true;
            return WriteObjectState(key, state);
        }

        public bool TrySetObjectPhysicsPose(string objectId, Pose pose, Vector3 velocity,
            bool moving, int expectedVersion)
        {
            if (!IsFinite(pose.position) || !IsFinite(pose.rotation) || !IsFinite(velocity) ||
                Object == null || !Object.HasStateAuthority ||
                !TryFindObjectState(objectId, out var key, out var state) ||
                state.Version != expectedVersion || state.HolderPlayerIndex >= 0 ||
                state.IsDestroyed || state.IsPendingEjection) return false;
            state.Position = pose.position;
            state.Rotation = pose.rotation;
            state.InitialVelocity = velocity;
            state.IsPhysicsActive = moving;
            return WriteObjectState(key, state);
        }

        public bool TrySetObjectSettled(
            string objectId,
            Pose pose,
            int expectedVersion)
        {
            if (expectedVersion < 0 ||
                !IsFinite(pose.position) ||
                !IsFinite(pose.rotation) ||
                !TryGetWritableState(objectId, out var key, out var state) ||
                state.Version != expectedVersion ||
                state.HolderPlayerIndex >= 0 ||
                state.IsDestroyed ||
                state.IsPendingEjection ||
                !state.IsPhysicsActive)
            {
                return false;
            }

            state.Position = pose.position;
            state.Rotation = pose.rotation;
            state.InitialVelocity = default;
            state.IsPhysicsActive = false;
            state.IsPendingEjection = false;
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
            state.IsPhysicsActive = false;
            state.IsPendingEjection = false;
            return WriteObjectState(key, state);
        }

        public bool TryPublishPlayerItemStatuses(
            IReadOnlyList<PlayerItemStatusSnapshot> statuses)
        {
            if (Object == null || !Object.HasStateAuthority ||
                statuses == null)
            {
                return false;
            }

            if (!TrySerializePlayerItemStatuses(statuses, out var payload))
            {
                return false;
            }

            RPC_NotifyPlayerItemStatuses(payload);
            return true;
        }

        public bool TryResetWorldObjects(IReadOnlyList<WorldObjectState> states)
        {
            if (Object == null || !Object.HasStateAuthority || states == null)
            {
                return false;
            }

            var newObjectIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var worldObject in states)
            {
                if (!IsValidObjectId(worldObject.ObjectId))
                {
                    return false;
                }

                var objectId = worldObject.ObjectId.Trim();
                if (!TryFindObjectState(objectId, out _, out _))
                {
                    newObjectIds.Add(objectId);
                }
            }

            if (ObjectStateCount + newObjectIds.Count > MaxReplicatedObjects)
            {
                return false;
            }

            foreach (var worldObject in states)
            {
                TryGetWritableState(worldObject.ObjectId, out var index, out var state);
                state.HolderPlayerIndex = -1;
                state.Position = worldObject.Pose.position;
                state.Rotation = worldObject.Pose.rotation;
                state.InitialVelocity = default;
                state.IsDestroyed = false;
                state.IsPhysicsActive = false;
                state.IsPendingEjection = false;
                WriteObjectState(index, state);
            }

            return true;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsHostPlayer)]
        public void RPC_RequestMatchStart(RpcInfo info = default) => StarterOf(Runner)?.ReceiveStartRequest(info.Source);

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_ClaimRoom(string title, bool locked, string password, int maxPlayers, string mapId,
            bool isPrivate, string nickname, RpcInfo info = default)
        {
            if (!info.Source.IsRealPlayer || title == null || title.Length > 128 ||
                password == null || password.Length > 128 || mapId == null || mapId.Length > 64 ||
                nickname == null || nickname.Length > 32) return;
            StarterOf(Runner)?.ReceiveRoomClaim(info.Source,
                new RoomCreateRequest(title, locked, password, maxPlayers, mapId, isPrivate), nickname);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsHostPlayer)]
        public void RPC_LobbyNickname(string nickname, RpcInfo info = default)
        {
            if (nickname == null || nickname.Length > 32) return;
            StarterOf(Runner)?.ReceiveLobbyNickname(info.Source, nickname);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_RoomClaimAnswer([RpcTarget] PlayerRef target, bool accepted) => StarterOf(Runner)?.ReceiveRoomClaimAnswer(accepted);

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_StartRefused([RpcTarget] PlayerRef target, RoomStartResult reason) => StarterOf(Runner)?.Refused(reason);

        [Rpc(RpcSources.All, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsHostPlayer)]
        public void RPC_RequestLobbyKick(string target, RpcInfo info = default)
        {
            if (target == null || target.Length > 32) return;
            StarterOf(Runner)?.ReceiveLobbyKick(info.Source, target);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsHostPlayer)]
        public void RPC_RequestLobbySettings(int maxPlayers, int destructionLimit, string mapId,
            int hidingSeconds, int searchingMinutes, float sprintMultiplier, int stunHits,
            string categoryId, string title, RpcInfo info = default)
        {
            if (mapId == null || mapId.Length > 64 || categoryId == null || categoryId.Length > 64 ||
                (title != null && !RoomSettings.IsValidTitle(title)) ||
                !MatchRuleSettings.TryCreate(hidingSeconds, searchingMinutes, sprintMultiplier, stunHits,
                    categoryId, out var rules, out _)) return;
            StarterOf(Runner)?.ReceiveLobbySettings(info.Source,
                new PlaySettingsDraft(title, string.Empty, false, string.Empty, maxPlayers, destructionLimit, mapId, rules));
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_RequestCompleteHidingTurn(RpcInfo info = default)
        {
            StarterOf(Runner)?.TryCompleteHidingTurn(info.Source);
        }
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_RequestHold(string objectId, RpcInfo info = default)
        {
            StarterOf(Runner)?.TryHoldObject(info.Source, objectId);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsHostPlayer)]
        public void RPC_RequestRelease(
            Vector3 position,
            Quaternion rotation,
            RpcInfo info = default)
        {
            if (StarterOf(Runner)?.TryReleaseHeldObject(
                info.Source,
                new Pose(position, rotation)) != true)
                RPC_InteractionRejected(info.Source, "release");
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsHostPlayer)]
        public void RPC_RequestDrop(
            Vector3 position,
            Quaternion rotation,
            RpcInfo info = default)
        {
            if (StarterOf(Runner)?.TryDropHeldObject(
                info.Source,
                new Pose(position, rotation)) != true)
                RPC_InteractionRejected(info.Source, "drop");
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsHostPlayer)]
        public void RPC_RequestThrow(
            Vector3 position,
            Quaternion rotation,
            Vector3 initialVelocity,
            RpcInfo info = default)
        {
            if (StarterOf(Runner)?.TryThrowHeldObject(
                info.Source,
                new Pose(position, rotation),
                initialVelocity) != true)
                RPC_InteractionRejected(info.Source, "throw");
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_InteractionRejected([RpcTarget] PlayerRef target, string action)
        {
            // Re-publish the replicated authority snapshot; never clear ownership locally.
            PublishObjectStates();
            Debug.LogWarning($"[Interaction] {action} rejected by authority; refreshed item state.");
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_RequestHit(int targetPlayerIndex, RpcInfo info = default)
        {
            StarterOf(Runner)?.TryHitPlayer(info.Source, targetPlayerIndex);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_ConfirmPhaseIntroReady(MatchPhase phase, RpcInfo info = default)
        {
            StarterOf(Runner)?.ConfirmPhaseIntroReady(info.Source, phase);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_RequestShredder(RpcInfo info = default)
        {
            StarterOf(Runner)?.TryUseShredder(info.Source);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_RequestReturnToLobby(RpcInfo info = default)
        {
            StarterOf(Runner)?.TryReturnToLobby(info.Source);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_RequestLobbyChat(string text, RpcInfo info = default)
        {
            StarterOf(Runner)?.TryRelayLobbyChat(info.Source, text);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_RequestMatchChat(string text, RpcInfo info = default)
        {
            StarterOf(Runner)?.TryRelayMatchChat(info.Source, text);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_NotifyPlayerItemStatuses(byte[] payload)
        {
            if (!TryDeserializePlayerItemStatuses(payload, out var statuses))
            {
                return;
            }

            StarterOf(Runner)?.PublishPlayerItemStatuses(statuses);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_NotifyLobbyChat(
            string senderId,
            string senderName,
            string text)
        {
            if (string.IsNullOrWhiteSpace(senderId) ||
                string.IsNullOrWhiteSpace(senderName) ||
                string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            StarterOf(Runner)?.PublishLobbyChat(
                new LobbyChatMessage(senderId, senderName, text));
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_NotifyMatchChat(
            string senderId,
            string senderName,
            string text)
        {
            if (string.IsNullOrWhiteSpace(senderId) ||
                string.IsNullOrWhiteSpace(senderName) ||
                string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            StarterOf(Runner)?.PublishMatchChat(
                new LobbyChatMessage(senderId, senderName, text));
        }

        private static bool TrySerializePlayerItemStatuses(
            IReadOnlyList<PlayerItemStatusSnapshot> statuses,
            out byte[] payload)
        {
            payload = null;

            if (statuses == null || statuses.Count > ushort.MaxValue)
            {
                return false;
            }

            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.UTF8, true);

            writer.Write((ushort)statuses.Count);
            for (var index = 0; index < statuses.Count; index++)
            {
                var status = statuses[index];
                if (string.IsNullOrWhiteSpace(status.ItemId))
                {
                    return false;
                }

                var itemId = status.ItemId.Trim();
                var itemIdBytes = Encoding.UTF8.GetBytes(itemId);
                if (itemIdBytes.Length > MaxObjectIdLength ||
                    itemIdBytes.Length > byte.MaxValue)
                {
                    return false;
                }

                writer.Write((byte)itemIdBytes.Length);
                writer.Write(itemIdBytes);
                writer.Write(status.IsDestroyed);
            }

            payload = stream.ToArray();
            return true;
        }

        private static bool TryDeserializePlayerItemStatuses(
            byte[] payload,
            out PlayerItemStatusSnapshot[] statuses)
        {
            statuses = null;
            if (payload == null || payload.Length == 0)
            {
                return false;
            }

            using var stream = new MemoryStream(payload);
            using var reader = new BinaryReader(stream, Encoding.UTF8, true);

            ushort count;
            try
            {
                count = reader.ReadUInt16();
                statuses = new PlayerItemStatusSnapshot[count];
                for (var index = 0; index < count; index++)
                {
                    var idLength = reader.ReadByte();
                    if (idLength == 0 || idLength > MaxObjectIdLength ||
                        reader.BaseStream.Position + idLength > stream.Length)
                    {
                        return false;
                    }

                    var itemId = Encoding.UTF8.GetString(reader.ReadBytes(idLength));
                    var isDestroyed = reader.ReadBoolean();
                    statuses[index] = new PlayerItemStatusSnapshot(itemId, isDestroyed);
                }
            }
            catch (Exception)
            {
                statuses = null;
                return false;
            }

            if (reader.BaseStream.Position != reader.BaseStream.Length)
            {
                return false;
            }

            return true;
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_NotifyItemDestroyed(
            int destroyerPlayerIndex,
            string itemId,
            double destroyedAt)
        {
            StarterOf(Runner)?.PublishItemDestroyed(
                new PlayerItemDestroyedEvent(
                    destroyerPlayerIndex,
                    itemId,
                    destroyedAt));
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_NotifyPlayerStunned(
            int attackerPlayerIndex,
            int targetPlayerIndex,
            string droppedObjectId,
            double stunnedAt,
            double stunEndsAt)
        {
            StarterOf(Runner)?.PublishPlayerStunned(
                new PlayerStunnedEvent(
                    attackerPlayerIndex,
                    targetPlayerIndex,
                    string.IsNullOrEmpty(droppedObjectId) ? null : droppedObjectId,
                    stunnedAt,
                    stunEndsAt));
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_NotifyObjectThrown(
            int playerIndex,
            string objectId,
            Vector3 position,
            Quaternion rotation,
            Vector3 initialVelocity,
            double thrownAt)
        {
            StarterOf(Runner)?.PublishObjectThrown(
                new ObjectThrownEvent(
                    playerIndex,
                    objectId,
                    new Pose(position, rotation),
                    initialVelocity,
                    thrownAt));
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_NotifyFinalWarning(double startedAt, double endsAt)
        {
            StarterOf(Runner)?.PublishFinalWarning(
                new FinalWarningStartedEvent(startedAt, endsAt));
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
            var count = Mathf.Min(ObjectStateCount, MaxReplicatedObjects);
            var snapshots = new MatchObjectStateSnapshot[count];
            for (var index = 0; index < count; index++)
            {
                var state = ObjectStates.Get(index);
                snapshots[index] = new MatchObjectStateSnapshot(
                    state.ObjectId.ToString(),
                    state.HolderPlayerIndex,
                    new Pose(state.Position, state.Rotation),
                    state.InitialVelocity,
                    state.IsDestroyed,
                    state.Version,
                    state.IsPhysicsActive,
                    state.IsPendingEjection);
            }

            StarterOf(Runner)?.PublishObjectStates(snapshots);
        }

        private void PublishParticipantActivity()
        {
            _publishedParticipantActivityRevision = ParticipantActivityRevision;
            var count = Mathf.Min(ParticipantCount, MaxParticipants);
            var active = new bool[count];
            for (var playerIndex = 0; playerIndex < count; playerIndex++)
            {
                active[playerIndex] = ParticipantActive.Get(playerIndex);
            }

            StarterOf(Runner)?.PublishParticipantActivity(active);
        }

        private void PublishPlayerInteractionStates()
        {
            _publishedPlayerInteractionStateRevision =
                PlayerInteractionStateRevision;
            var count = Mathf.Min(ParticipantCount, MaxParticipants);
            var snapshots = new PlayerInteractionStateSnapshot[count];
            for (var playerIndex = 0; playerIndex < count; playerIndex++)
            {
                snapshots[playerIndex] = new PlayerInteractionStateSnapshot(
                    playerIndex,
                    StunEndsAt.Get(playerIndex),
                    RemainingDestructionUses.Get(playerIndex),
                    HitCounts.Get(playerIndex));
            }

            StarterOf(Runner)?.PublishPlayerInteractionStates(snapshots);
        }

        private void PublishResult()
        {
            _publishedHasResult = HasResult;
            if (!HasResult)
            {
                return;
            }

            var count = Mathf.Min(WinnerCount, MaxParticipants);
            var winners = new int[count];
            for (var index = 0; index < count; index++)
            {
                winners[index] = WinnerPlayerIndices.Get(index);
            }

            StarterOf(Runner)?.PublishMatchResult(
                new MatchResult(ResultEndReason, ResultEndedAt, winners));
        }

        private bool TryGetWritableState(
            string objectId,
            out int index,
            out ReplicatedObjectState state)
        {
            index = -1;
            state = default;
            if (!CanTrackObject(objectId))
            {
                return false;
            }

            if (!TryFindObjectState(objectId, out index, out state))
            {
                index = ObjectStateCount++;
                state.ObjectId = objectId.Trim();
                state.HolderPlayerIndex = -1;
                state.Rotation = Quaternion.identity;
            }

            return true;
        }

        private bool CanWritePlayerInteractionState(int playerIndex)
        {
            return Object != null &&
                   Object.HasStateAuthority &&
                   playerIndex >= 0 &&
                   playerIndex < ParticipantCount;
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);

        private static bool IsFinite(Quaternion value) =>
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z) &&
            float.IsFinite(value.w);

        private bool WriteObjectState(int index, ReplicatedObjectState state)
        {
            state.Version++;
            ObjectStates.Set(index, state);
            ObjectStateRevision++;
            return true;
        }

        private bool TryFindObjectState(
            string objectId,
            out int index,
            out ReplicatedObjectState state)
        {
            index = -1;
            state = default;
            if (!IsValidObjectId(objectId))
            {
                return false;
            }

            NetworkString<_16> key = objectId.Trim();
            var count = Mathf.Min(ObjectStateCount, MaxReplicatedObjects);
            for (var candidate = 0; candidate < count; candidate++)
            {
                var current = ObjectStates.Get(candidate);
                if (!current.ObjectId.Equals(key))
                {
                    continue;
                }

                index = candidate;
                state = current;
                return true;
            }

            return false;
        }

        private static bool IsValidObjectId(string objectId)
        {
            return !string.IsNullOrWhiteSpace(objectId) &&
                   objectId.Trim().Length <= MaxObjectIdLength;
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
