using System;
using System.Collections.Generic;
using Fusion;
using Game.Core.Lobby;
using Game.Core.Items;
using Game.Core.Match;
using Game.Core.Ports;
using Game.Core.Rooms;
using Game.Network.Players;
using Game.Network.Session;
using Game.Server.Match;
using UnityEngine;

namespace Game.Network.Match
{
    /// <summary>
    /// Decides whether a match may start, and reports the confirmed line-up.
    /// </summary>
    /// <remarks>
    /// Lives on the runner object for the same reason the roster does: the
    /// networked object that receives the request is spawned by Fusion and
    /// cannot be injected.
    /// <para>
    /// Every check runs on the authority. A client asking is only a request, so
    /// a peer that wrongly believes it is the host cannot start a match by
    /// skipping its own check.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed partial class MatchStarter : MonoBehaviour
    {
        private static readonly Vector3 ShredderEjectionLocalVelocity =
            new(0f, 1.5f, 4f);

        private readonly List<RoomParticipant> _room = new List<RoomParticipant>();
        private readonly List<MatchParticipant> _playing = new List<MatchParticipant>();
        private readonly InteractionAuthorityRules _interactionRules =
            new InteractionAuthorityRules();

        private IMatchStartSink _sink;
        private PlayerRoster _roster;

        /// <summary>
        /// Takes the room into the map once a match is confirmed. Held as an
        /// interface so that this stays a judge of whether a match may start and
        /// never learns what a scene is.
        /// </summary>
        private IMatchSceneDirector _sceneDirector;

        /// <summary>
        /// The room's object, remembered as it reports itself. Anything wanting
        /// to ask for a match needs a handle on it, and only this peer's copy of
        /// it can carry the request.
        /// </summary>
        private MatchSessionState _state;
        private MatchMigrationCheckpoint _checkpoint;
        private MatchSessionCoordinator _session;
        private IReadOnlyList<Pose> _shredderEjectionPoses = Array.Empty<Pose>();
        private bool _returningToLobby;
        private bool _lastPublishedStarted;
        private string[] _countdownParticipants;

        /// <summary>
        /// Backend accounts for <see cref="_countdownParticipants"/>, index for
        /// index. Captured together so the line-up confirmed at the end of the
        /// countdown is the one that was shown when it began.
        /// </summary>
        private string[] _countdownUserIds;
        public bool IsStartPending => HasValidState && _state.StartCountdownEndsAt > 0d;
        public double StartCountdownEndsAt => HasValidState ? _state.StartCountdownEndsAt : 0d;

        /// <summary>
        /// What the session listing was last told, so the same answer is not
        /// sent to the cloud on every replication.
        /// </summary>
        private bool _publishedRoomStatus;
        private MatchPhase _lastPublishedPhase = MatchPhase.Waiting;
        public bool HasStartedMatch =>
            HasValidState ? _state.IsStarted : _lastPublishedStarted;
        public MatchPhase CurrentPhase =>
            _session?.CurrentPhase ??
            (HasValidState ? _state.Phase : _lastPublishedPhase);
        internal IReadOnlyList<MatchParticipant> PlayingParticipants => _playing;

        private bool HasValidState =>
            _state != null && _state.Object != null && _state.Object.IsValid;

        public event Action<MatchStateSnapshot> MatchStateReceived;
        public event Action<LobbyChatMessage> LobbyChatReceived;
        public event Action<LobbyChatMessage> MatchChatReceived;
        public event Action<IReadOnlyList<MatchObjectStateSnapshot>> ObjectStatesReceived;
        public event Action<PlayerItemDestroyedEvent> ItemDestroyedReceived;
        public event Action<IReadOnlyList<PlayerItemStatusSnapshot>> PlayerItemStatusesReceived;
        public event Action<PlayerStunnedEvent> PlayerStunnedReceived;
        public event Action<ObjectThrownEvent> ObjectThrownReceived;
        public event Action<FinalWarningStartedEvent> FinalWarningReceived;
        public event Action<IReadOnlyList<bool>> ParticipantActivityReceived;
        public event Action<IReadOnlyList<PlayerInteractionStateSnapshot>>
            PlayerInteractionStatesReceived;
        public event Action<MatchResult> MatchResultReceived;
        public event Action<IReadOnlyList<MatchParticipant>> LineUpReceived;
        public event Action SimulationTick;
        public event Action<PlayerRef, string> LobbyKickRequested;
        public event Action<PlayerRef, PlaySettingsDraft> LobbySettingsRequested;
        public event Action<PlayerRef, RoomCreateRequest, string> RoomClaimRequested;
        public event Action<PlayerRef, string> LobbyNicknameRequested;
        public event Action<bool> RoomClaimAnswered;
        internal bool CanSendLobbyCommand => HasValidState;
        internal void ReceiveRoomClaim(PlayerRef source, RoomCreateRequest request, string nickname) => RoomClaimRequested?.Invoke(source, request, nickname);
        internal void ReceiveLobbyNickname(PlayerRef source, string nickname) => LobbyNicknameRequested?.Invoke(source, nickname);
        internal bool RequestLobbyNickname(string nickname)
        {
            if (!HasValidState) return false;
            _state.RPC_LobbyNickname(nickname);
            return true;
        }
        internal void ReceiveRoomClaimAnswer(bool accepted) => RoomClaimAnswered?.Invoke(accepted);
        internal void AnswerRoomClaim(PlayerRef source, bool accepted) => _state.RPC_RoomClaimAnswer(source, accepted);
        internal void RequestRoomClaim(RoomCreateRequest request, string nickname) => _state.RPC_ClaimRoom(
            request.Title, request.IsLocked, request.Password ?? string.Empty, request.MaxPlayers, request.MapId, request.IsPrivate, nickname);

        internal void ReceiveLobbyKick(PlayerRef source, string target) => LobbyKickRequested?.Invoke(source, target);
        internal void ReceiveLobbySettings(PlayerRef source, PlaySettingsDraft settings) => LobbySettingsRequested?.Invoke(source, settings);
        internal bool RequestLobbyKick(string target)
        {
            if (!HasValidState) return false;
            _state.RPC_RequestLobbyKick(target);
            return true;
        }
        internal bool RequestLobbySettings(int maxPlayers, int destructionLimit, string mapId, MatchRuleSettings rules, string title)
        {
            if (!HasValidState) return false;
            _state.RPC_RequestLobbySettings(maxPlayers, destructionLimit, mapId, rules.HidingDurationSeconds,
                rules.SearchingDurationMinutes, rules.SprintMultiplier, rules.StunHitCount, rules.CategoryId, title);
            return true;
        }

        public void Bind(
            IMatchStartSink sink,
            PlayerRoster roster,
            IMatchSceneDirector sceneDirector)
        {
            _sink = sink;
            _roster = roster;
            _sceneDirector = sceneDirector;
        }

        /// <summary>
        /// Starts a match if the room allows it.
        /// </summary>
        /// <remarks>
        /// The request carries Fusion's sender identity. Only the server decides,
        /// after checking the replicated lobby owner instead of its local player.
        /// </remarks>
        public void RequestStart(NetworkRunner runner)
        {
            if (runner == null || !runner.IsRunning)
            {
                return;
            }

            if (!HasValidState)
            {
                Debug.LogWarning("[Match] The room is not ready to start yet.");
                return;
            }

            _state.RPC_RequestMatchStart();
        }

        internal void ReceiveStartRequest(PlayerRef source)
        {
            if (!HasValidState || !_state.HasStateAuthority) return;
            var runner = _state.Runner;
            // The first player owns lobby management before their room claim
            // completes. That admission alone must not start an unclaimed room.
            if (runner.SessionInfo.Properties.TryGetValue(SessionPropertyKeys.AvailableServer, out var available) &&
                available.Isbool && (bool)available) return;
            if (!PlayerSpawner.IsRoomOwner(runner, source))
            {
                if (source.IsRealPlayer) _state.RPC_StartRefused(source, RoomStartResult.NotHost);
                return;
            }

            if (IsStartPending) return;
            var refusal = Evaluate(_state, runner);

            if (refusal != RoomStartResult.Started)
            {
                Debug.Log($"[Match] The match cannot start: {refusal}.");
                _state.RPC_StartRefused(source, refusal);
                return;
            }

            var state = _state;

            var participants = MatchParticipant.FromRoomParticipants(_room);
            var participantIds = new string[participants.Length];
            var participantUserIds = new string[participants.Length];
            for (var index = 0; index < participants.Length; index++)
            {
                participantIds[index] = participants[index].PlayerId;
                participantUserIds[index] = participants[index].UserId ?? string.Empty;
            }

            _countdownParticipants = participantIds;
            _countdownUserIds = participantUserIds;
            state.StartCountdownEndsAt = runner.SimulationTime + 10d;
        }

        private void AdvanceStartCountdown()
        {
            if (!IsStartPending) return;
            var runner = _state.Runner;
            if (!runner.IsServer) return;
            var valid = _countdownParticipants != null && Evaluate(_state, runner) == RoomStartResult.Started &&
                _room.Count == _countdownParticipants.Length;
            if (valid)
                for (var i = 0; i < _room.Count; i++)
                    if (_room[i].PlayerId != _countdownParticipants[i]) { valid = false; break; }
            if (!valid)
            {
                _state.StartCountdownEndsAt = 0d;
                _countdownParticipants = null;
                _countdownUserIds = null;
                return;
            }
            if (runner.SimulationTime < _state.StartCountdownEndsAt) return;
            var participantIds = _countdownParticipants;
            var participantUserIds = _countdownUserIds;
            _countdownParticipants = null;
            _countdownUserIds = null;
            _state.StartCountdownEndsAt = 0d;
            _state.Confirm(participantIds, participantUserIds);
            Debug.Log($"[Match] Started with {participantIds.Length} players.");

            // After the line-up is frozen, not before: the map replaces this
            // scene, and a load that began first could tear down the objects
            // this method is still reading.
            _sceneDirector?.EnterMatchScene(runner);
        }

        /// <summary>
        /// Reports the room's current answer. Called on every peer as the
        /// decision replicates, so presentation does not have to ask.
        /// </summary>
        public void Publish(MatchSessionState state)
        {
            if (state == null)
            {
                return;
            }

            _state = state;
            _lastPublishedStarted = state.IsStarted;
            PublishRoomStatus();

            _playing.Clear();

            if (_lastPublishedStarted)
            {
                var count = Mathf.Min(state.ParticipantCount, MatchSessionState.MaxParticipants);

                for (var index = 0; index < count; index++)
                {
                    // The position in the replicated array is the playerIndex.
                    // Seat numbers are not used here: they are reused as people
                    // come and go and can leave gaps.
                    _playing.Add(new MatchParticipant(
                        state.Participants.Get(index).ToString(),
                        index,
                        state.ParticipantUserIds.Get(index).ToString()));
                }
            }

            _sink?.MatchStarted(_playing);
            LineUpReceived?.Invoke(_playing);
        }

        /// <summary>
        /// Tells the lobby listing whether this room is playing, so the room
        /// browser can show it as one nobody can join.
        /// </summary>
        /// <remarks>
        /// A room is playing from the moment the host presses start, not from
        /// the moment the countdown ends: those ten seconds are part of the
        /// match starting, and a list that still says "waiting" through them
        /// invites someone into a room that is already on its way to the map.
        /// A countdown that gets called off puts the room back to waiting.
        /// <para>
        /// Only the authority writes it, and only when the answer changes: a
        /// session property update is a round trip to the cloud, and this is
        /// asked on every tick and every replication of the match state.
        /// </para>
        /// <para>
        /// A failed update is not worth failing a match start over. The room
        /// stays listed as waiting, someone tries to enter, and the session
        /// refuses them — which is the same outcome, reached less kindly.
        /// </para>
        /// </remarks>
        private void PublishRoomStatus()
        {
            var playing = HasStartedMatch || IsStartPending;

            if (_publishedRoomStatus == playing)
            {
                return;
            }

            var runner = _state != null ? _state.Runner : null;
            if (runner == null || !runner.IsServer || !runner.SessionInfo.IsValid)
            {
                return;
            }

            if (!runner.SessionInfo.UpdateCustomProperties(
                    SessionPropertyMapper.BuildRoomStatus(playing)))
            {
                Debug.LogWarning(
                    "[Match] Could not tell the lobby the room is "
                    + (playing ? "playing." : "waiting."));
                return;
            }

            _publishedRoomStatus = playing;
        }

        public void Publish(MatchMigrationCheckpoint checkpoint)
        {
            _checkpoint = checkpoint;
        }

        public bool TryPublishSnapshot(MatchStateSnapshot snapshot)
        {
            return _state != null && _state.TrySetSnapshot(snapshot);
        }

        public void PublishSnapshot(MatchStateSnapshot snapshot)
        {
            _lastPublishedPhase = snapshot.Phase;
            MatchStateReceived?.Invoke(snapshot);
        }

        internal void PublishSceneState()
        {
            if (_state != null && _state.Object != null && _state.Object.IsValid)
                _state.PublishSceneState();
        }

        public bool TrySetPlayerControls(int playerIndex, bool enabled)
        {
            if (!TryGetPlayingAvatar(playerIndex, out var avatar))
            {
                return false;
            }

            var motor = avatar.GetComponent<NetworkPlayerMotor>();
            return motor != null && motor.TrySetControlsEnabled(enabled);
        }

        public bool TrySetPlayerSprintMultiplier(int playerIndex, float multiplier)
        {
            if (!TryGetPlayingAvatar(playerIndex, out var avatar))
            {
                return false;
            }

            var motor = avatar.GetComponent<NetworkPlayerMotor>();
            return motor != null && motor.TrySetSprintMultiplier(multiplier);
        }

        public bool TryResetPlayerStamina(int playerIndex)
        {
            if (!TryGetPlayingAvatar(playerIndex, out var avatar))
            {
                return false;
            }

            var motor = avatar.GetComponent<NetworkPlayerMotor>();
            return motor != null && motor.TryResetStamina();
        }

        public bool TryTeleportPlayer(int playerIndex, Pose pose)
        {
            if (!TryGetPlayingAvatar(playerIndex, out var avatar))
            {
                Debug.LogWarning(
                    $"[PlayerTeleport] No avatar mapped to playerIndex={playerIndex}, " +
                    $"target={pose.position}.");
                return false;
            }

            var motor = avatar.GetComponent<NetworkPlayerMotor>();
            var teleported = motor != null && motor.TryTeleport(pose);
            Debug.Log(
                $"[PlayerTeleport] playerIndex={playerIndex}, " +
                $"playerId={_playing[playerIndex].PlayerId}, " +
                $"avatarOwner={avatar.Owner}, target={pose.position}, " +
                $"success={teleported}.");
            return teleported;
        }

        public bool TryInitializeAssignedItems(
            IReadOnlyList<PlayerItemAssignment> assignments)
        {
            if (_state == null || _session == null || assignments == null ||
                assignments.Count == 0)
            {
                return false;
            }

            for (var index = 0; index < assignments.Count; index++)
            {
                var assignment = assignments[index];
                var playerIndex = assignment.PlayerIndex;
                if (playerIndex < 0 ||
                    playerIndex >= _session.Assignments.Count ||
                    !string.Equals(
                        _session.Assignments[playerIndex].Item.ItemId,
                        assignment.Item.ItemId,
                        StringComparison.Ordinal) ||
                    !_state.CanHoldObject(assignment.Item.ItemId) ||
                    !_session.TryInitializeAssignedItem(playerIndex) ||
                    !_state.TrySetObjectHeld(assignment.Item.ItemId, playerIndex))
                {
                    return false;
                }
            }

            return true;
        }

        public bool TryPublishPlayerItemStatuses(
            IReadOnlyList<PlayerItemStatusSnapshot> statuses)
        {
            return _state != null && _state.TryPublishPlayerItemStatuses(statuses);
        }

        public void PublishObjectStates(IReadOnlyList<MatchObjectStateSnapshot> states)
        {
            ObjectStatesReceived?.Invoke(states);
        }

        public void PublishPlayerItemStatuses(
            IReadOnlyList<PlayerItemStatusSnapshot> statuses)
        {
            PlayerItemStatusesReceived?.Invoke(statuses);
        }

        public void PublishItemDestroyed(PlayerItemDestroyedEvent confirmedEvent)
        {
            ItemDestroyedReceived?.Invoke(confirmedEvent);
        }

        public void PublishPlayerStunned(PlayerStunnedEvent confirmedEvent)
        {
            PlayerStunnedReceived?.Invoke(confirmedEvent);
        }

        public void PublishObjectThrown(ObjectThrownEvent confirmedEvent)
        {
            ObjectThrownReceived?.Invoke(confirmedEvent);
        }

        public void PublishFinalWarning(FinalWarningStartedEvent confirmedEvent)
        {
            FinalWarningReceived?.Invoke(confirmedEvent);
        }

        public void PublishParticipantActivity(IReadOnlyList<bool> active)
        {
            ParticipantActivityReceived?.Invoke(active);
        }

        public void PublishPlayerInteractionStates(
            IReadOnlyList<PlayerInteractionStateSnapshot> states)
        {
            PlayerInteractionStatesReceived?.Invoke(states);
        }

        public void PublishMatchResult(MatchResult result)
        {
            MatchResultReceived?.Invoke(result);
        }

        public void PublishSimulationTick()
        {
            AdvanceStartCountdown();

            // The countdown is not part of the replicated line-up, so starting
            // or calling one off raises no line-up change for the listing to
            // ride along with. Asked here instead, where the countdown is run.
            PublishRoomStatus();
            SimulationTick?.Invoke();
            if (_session != null && _state != null)
            {
                for (var i = 0; i < _session.Assignments.Count; i++)
                    if (!_session.Players.IsActive(i)) _state.TrySetParticipantInactive(i);
                // Host migration suspended; retain the checkpoint component/schema but do not record it.
                // _checkpoint?.Capture(_session, _state, _roster);
            }
        }

        public MatchMigrationState CaptureMigrationState()
        {
            if (_state == null || !_state.IsStarted) return null;
            if (_checkpoint == null) throw new InvalidOperationException("Match migration checkpoint is missing.");
            return _checkpoint.Read(_state);
        }

        public void BindSession(
            MatchSessionCoordinator session,
            IReadOnlyList<Pose> shredderEjectionPoses)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (shredderEjectionPoses == null || shredderEjectionPoses.Count == 0)
            {
                throw new ArgumentException(
                    "At least one shredder ejection pose is required.",
                    nameof(shredderEjectionPoses));
            }

            UnbindSession();
            _returningToLobby = false;
            _session = session;
            _session.PlayerItemDestroyed += OnPlayerItemDestroyed;
            _session.PlayerStunned += OnPlayerStunned;
            _session.ObjectThrown += OnObjectThrown;
            _session.ObjectAutoReleased += OnObjectAutoReleased;
            _session.MapObjectEjected += OnMapObjectEjected;
            _session.FinalWarningStarted += OnFinalWarningStarted;
            _session.MatchEnded += OnMatchEnded;
            _shredderEjectionPoses = shredderEjectionPoses;

            if (_session.CurrentPhase != MatchPhase.Waiting)
            {
                // Restored combat timers must not be reset by the normal new-match initializer.
                for (var i = 0; i < _session.Assignments.Count; i++)
                {
                    var restored = _session.CaptureMigrationPlayer(i, default);
                    _state.TrySetStunEndsAt(i, restored.StunEndsAt);
                    _state.TrySetRemainingDestructionUses(i, restored.DestructionUses);
                    _state.TrySetHitCount(i, restored.HitCount);
                }
                return;
            }

            var remainingUses = new int[_session.Players.Players.Count];
            for (var playerIndex = 0;
                 playerIndex < remainingUses.Length;
                 playerIndex++)
            {
                remainingUses[playerIndex] =
                    _session.GetRemainingDestructionUses(playerIndex);
            }

            if (!_state.TryInitializePlayerInteractionStates(remainingUses))
            {
                throw new InvalidOperationException(
                    "The authority could not initialize player interaction state.");
            }
        }

        public bool UnbindSession(MatchSessionCoordinator session)
        {
            if (!ReferenceEquals(_session, session))
            {
                return false;
            }

            UnbindSession();
            return true;
        }

        public bool RequestHoldObject(string objectId)
        {
            if (_state == null || string.IsNullOrWhiteSpace(objectId))
            {
                return false;
            }

            _state.RPC_RequestHold(objectId.Trim());
            return true;
        }

        public bool RequestPhaseIntroReady(MatchPhase phase)
        {
            if (!HasValidState || !_state.IsStarted || _state.Phase != phase || _state.PhaseEndsAt != 0d ||
                (phase != MatchPhase.Hiding && phase != MatchPhase.Searching)) return false;
            _state.RPC_ConfirmPhaseIntroReady(phase);
            return true;
        }

        internal bool ConfirmPhaseIntroReady(PlayerRef source, MatchPhase phase) =>
            HasValidState && _state.Object.HasStateAuthority && TryGetPlayerIndex(source, out var index) &&
            _session.ConfirmPhaseIntroReady(index, phase);

        public bool RequestReleaseHeldObject(Pose pose)
        {
            if (_state == null)
            {
                return false;
            }

            _state.RPC_RequestRelease(pose.position, pose.rotation);
            return true;
        }

        public bool RequestDropHeldObject(Pose pose)
        {
            if (_state == null)
            {
                return false;
            }

            _state.RPC_RequestDrop(pose.position, pose.rotation);
            return true;
        }

        public bool RequestThrowHeldObject(Pose pose, Vector3 initialVelocity)
        {
            if (_state == null)
            {
                return false;
            }

            _state.RPC_RequestThrow(pose.position, pose.rotation, initialVelocity);
            return true;
        }

        public bool RequestCompleteHidingTurn()
        {
            if (!HasValidState || _state.Phase != MatchPhase.Hiding) return false;
            _state.RPC_RequestCompleteHidingTurn();
            return true;
        }

        public bool TryCompleteHidingTurn(PlayerRef source)
        {
            return TryGetPlayerIndex(source, out var playerIndex) &&
                _session.TryCompleteHidingTurn(playerIndex, ServerTime);
        }

        public bool RequestHitPlayer(int targetPlayerIndex)
        {
            if (_state == null)
            {
                return false;
            }

            _state.RPC_RequestHit(targetPlayerIndex);
            return true;
        }

        public bool RequestUseShredder()
        {
            if (_state == null)
            {
                return false;
            }

            _state.RPC_RequestShredder();
            return true;
        }

        public bool RequestReturnToLobby()
        {
            if (_state == null || _returningToLobby)
            {
                return false;
            }

            // The host must return the actual outcome, not merely report that an
            // RPC was sent. Result has already unloaded/unbound the match session.
            if (_state.Object != null && _state.Object.HasStateAuthority)
                return ReturnToLobby();
            _state.RPC_RequestReturnToLobby();
            return true;
        }

        public bool RequestLobbyChat(string text)
        {
            if (_state == null || string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            _state.RPC_RequestLobbyChat(LobbyChatMessage.ClampText(text.Trim()));
            return true;
        }

        /// <summary>Requests a chat message for the frozen match line-up.</summary>
        public bool RequestMatchChat(string text)
        {
            if (_state == null || !_state.IsStarted || string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            _state.RPC_RequestMatchChat(LobbyChatMessage.ClampText(text.Trim()));
            return true;
        }

        public bool TryRelayLobbyChat(PlayerRef source, string text)
        {
            if (_state == null || _state.Object == null ||
                !_state.Object.HasStateAuthority || _roster == null ||
                string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            if (!source.IsRealPlayer && _state.Runner.IsServer)
            {
                source = _state.Runner.LocalPlayer;
            }

            var playerId = PlayerRegistry.IdOf(source);
            _room.Clear();
            _roster.Capture(_room);
            for (var index = 0; index < _room.Count; index++)
            {
                var participant = _room[index];
                if (!string.Equals(
                        participant.PlayerId,
                        playerId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                var nickname = string.IsNullOrEmpty(participant.Nickname)
                    ? participant.PlayerId
                    : participant.Nickname;
                _state.RPC_NotifyLobbyChat(
                    participant.PlayerId,
                    nickname,
                    LobbyChatMessage.ClampText(text.Trim()));
                return true;
            }

            return false;
        }

        public void PublishLobbyChat(LobbyChatMessage message)
        {
            LobbyChatReceived?.Invoke(message);
        }

        public bool TryRelayMatchChat(PlayerRef source, string text)
        {
            if (_state == null || !_state.IsStarted || _state.Object == null ||
                !_state.Object.HasStateAuthority || string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            if (!source.IsRealPlayer && _state.Runner.IsServer)
            {
                source = _state.Runner.LocalPlayer;
            }

            var playerId = PlayerRegistry.IdOf(source);
            for (var index = 0; index < _playing.Count; index++)
            {
                var participant = _playing[index];
                if (!string.Equals(participant.PlayerId, playerId, StringComparison.Ordinal) ||
                    participant.PlayerIndex < 0 ||
                    participant.PlayerIndex >= _state.ParticipantCount ||
                    !_state.ParticipantActive.Get(participant.PlayerIndex))
                {
                    continue;
                }

                _state.RPC_NotifyMatchChat(
                    participant.PlayerId,
                    ResolveNickname(participant.PlayerId),
                    LobbyChatMessage.ClampText(text.Trim()));
                return true;
            }

            return false;
        }

        public void PublishMatchChat(LobbyChatMessage message)
        {
            MatchChatReceived?.Invoke(message);
        }

        internal static bool IsMatchChatParticipant(
            IReadOnlyList<MatchParticipant> playing,
            string playerId)
        {
            if (playing == null || string.IsNullOrWhiteSpace(playerId))
            {
                return false;
            }

            for (var index = 0; index < playing.Count; index++)
            {
                if (string.Equals(playing[index].PlayerId, playerId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private string ResolveNickname(string playerId)
        {
            if (_roster == null)
            {
                return playerId;
            }

            _room.Clear();
            _roster.Capture(_room);
            for (var index = 0; index < _room.Count; index++)
            {
                if (string.Equals(_room[index].PlayerId, playerId, StringComparison.Ordinal))
                {
                    return string.IsNullOrEmpty(_room[index].Nickname)
                        ? playerId
                        : _room[index].Nickname;
                }
            }

            return playerId;
        }

        public bool TryHoldObject(PlayerRef source, string objectId)
        {
            if (IsLobby) return TryHoldLobbyObject(source, objectId);
            if (!TryGetPlayerIndex(source, out var playerIndex) ||
                !TryGetPlayerPose(playerIndex, out var playerPose) ||
                !_state.CanHoldObject(objectId) ||
                !IsObjectWithinReach(playerIndex, objectId, playerPose.position) ||
                !_state.CanTrackObject(objectId) ||
                !_session.TryHoldObject(playerIndex, objectId, ServerTime))
            {
                return false;
            }

            return _state.TrySetObjectHeld(objectId, playerIndex);
        }

        public bool TryReleaseHeldObject(PlayerRef source, Pose pose)
        {
            if (IsLobby) return TryReleaseLobbyObject(source, pose, default, false);
            if (!TryGetPlayerIndex(source, out var playerIndex) ||
                !TryGetPlayerPose(playerIndex, out var playerPose) ||
                !_interactionRules.IsValidRelease(playerPose, pose) ||
                !_session.TryGetHeldObjectId(playerIndex, out var objectId) ||
                !_state.CanTrackObject(objectId) ||
                !_session.TryReleaseHeldObject(playerIndex, pose, ServerTime))
            {
                return false;
            }

            return _state.TrySetObjectReleased(objectId, pose);
        }

        public bool TryDropHeldObject(PlayerRef source, Pose pose)
        {
            if (IsLobby) return TryReleaseLobbyObject(source, pose, default, false);
            if (!TryGetPlayerIndex(source, out var playerIndex) ||
                !TryGetPlayerPose(playerIndex, out var playerPose) ||
                !_interactionRules.IsValidRelease(playerPose, pose) ||
                !_session.TryGetHeldObjectId(playerIndex, out var objectId) ||
                !_state.CanTrackObject(objectId) ||
                !_session.TryDropHeldObject(playerIndex, pose, ServerTime))
            {
                return false;
            }

            return _state.TrySetObjectReleased(objectId, pose);
        }

        public bool TryThrowHeldObject(
            PlayerRef source,
            Pose pose,
            Vector3 initialVelocity)
        {
            if (IsLobby) return TryReleaseLobbyObject(source, pose, initialVelocity, true);
            if (!TryGetPlayerIndex(source, out var playerIndex) ||
                !TryGetPlayerPose(playerIndex, out var playerPose) ||
                !_interactionRules.IsValidThrow(
                    playerPose,
                    pose,
                    initialVelocity) ||
                !_session.TryGetHeldObjectId(playerIndex, out var objectId) ||
                !_state.CanTrackObject(objectId) ||
                !_session.TryThrowHeldObject(
                    playerIndex,
                    pose,
                    initialVelocity,
                    ServerTime))
            {
                return false;
            }

            return _state.TrySetObjectReleased(objectId, pose, initialVelocity);
        }

        public (int HitsReceived, int Stuns) GetCombatTotals(int playerIndex) =>
            _session?.GetCombatTotals(playerIndex) ?? default;

        public bool TryConfirmObjectPhysicsPose(string objectId, Pose pose, Vector3 velocity,
            bool moving, int expectedVersion)
        {
            if (_state == null || (IsLobby && lobbyObjects == null) || (!IsLobby && (_session == null ||
                !_session.TryGetObjectPose(objectId, out _)))) return false;
            if (!_state.TrySetObjectPhysicsPose(objectId, pose, velocity, moving, expectedVersion)) return false;
            return IsLobby ? lobbyObjects != null && lobbyObjects.TrySetPose(objectId, pose)
                : _session.TryConfirmReleasedObjectPose(objectId, pose);
        }

        public bool TryConfirmObjectSettled(
            string objectId,
            Pose pose,
            int expectedVersion)
        {
            if (IsLobby)
                return lobbyObjects != null && _state.TrySetObjectSettled(objectId, pose, expectedVersion) &&
                    lobbyObjects.TrySetPose(objectId, pose);
            if (_state == null || _session == null ||
                !_session.TryGetObjectPose(objectId, out _) ||
                !_state.TrySetObjectSettled(objectId, pose, expectedVersion))
            {
                return false;
            }

            if (!_session.TryConfirmReleasedObjectPose(objectId, pose))
            {
                throw new InvalidOperationException(
                    $"The settled pose could not be stored for '{objectId}'.");
            }

            return true;
        }

        public bool TryHitPlayer(PlayerRef source, int targetPlayerIndex)
        {
            if (!TryGetPlayerIndex(source, out var attackerPlayerIndex) ||
                !TryGetPlayingAvatar(attackerPlayerIndex, out var attacker) ||
                !attacker.TryGetComponent<NetworkPlayerMotor>(out var motor) ||
                !motor.ControlsEnabled ||
                motor.Posture == Game.Core.Players.PlayerPosture.Prone ||
                targetPlayerIndex < 0 ||
                targetPlayerIndex >= _session.Players.Players.Count)
            {
                return false;
            }

            if (!TryGetPlayerPose(attackerPlayerIndex, out var attackerPose) ||
                !TryGetPlayerPose(targetPlayerIndex, out var targetPose) ||
                !_interactionRules.IsWithinInteractionDistance(
                    attackerPose.position,
                    targetPose.position))
            {
                return false;
            }

            _session.TryGetHeldObjectId(targetPlayerIndex, out var droppedObjectId);
            if (droppedObjectId != null && !_state.CanTrackObject(droppedObjectId))
            {
                return false;
            }

            var result = _session.RegisterHit(
                attackerPlayerIndex,
                targetPlayerIndex,
                targetPose.position,
                ServerTime);
            if (result != Game.Core.Players.HitResult.Ignored)
            {
                PublishHitCount(targetPlayerIndex);
            }

            if (result == Game.Core.Players.HitResult.Stunned && droppedObjectId != null)
            {
                _state.TrySetObjectReleased(
                    droppedObjectId,
                    new Pose(targetPose.position, Quaternion.identity));
            }

            return result != Game.Core.Players.HitResult.Ignored;
        }

        public bool TryUseShredder(PlayerRef source)
        {
            if (!TryGetPlayerIndex(source, out var playerIndex) ||
                !TryGetPlayerPose(playerIndex, out var playerPose) ||
                !TrySelectShredderEjectionPose(
                    _shredderEjectionPoses,
                    playerPose.position,
                    _interactionRules.IsWithinInteractionDistance,
                    out var ejectionPose))
            {
                return false;
            }

            var now = ServerTime;
            if (!_session.TryGetHeldObjectId(playerIndex, out var objectId))
            {
                return false;
            }

            if (!_state.CanTrackObject(objectId))
            {
                return false;
            }

            if (_session.TryDestroyHeldPlayerItem(playerIndex, now))
            {
                PublishRemainingDestructionUses(playerIndex);
                return _state.TrySetObjectDestroyed(objectId);
            }

            if (!_session.TryUseShredderOnHeldMapObject(
                    playerIndex,
                    ejectionPose,
                    now))
            {
                return false;
            }

            PublishRemainingDestructionUses(playerIndex);
            return _state.TrySetObjectPendingEjection(
                objectId,
                ejectionPose);
        }

        /// <summary>
        /// 플레이어가 상호작용 거리 안에 있는 파쇄기 중 가장 가까운 것의 튕김 지점을 고른다.
        /// 파쇄기가 여러 대인 맵에서 클라이언트가 어느 파쇄기를 눌렀는지 RPC로 보내지 않아도
        /// 서버가 같은 판단을 내릴 수 있다(상호작용 거리 안에 두 대가 겹치지 않는다는 배치 전제).
        /// </summary>
        internal static bool TrySelectShredderEjectionPose(
            IReadOnlyList<Pose> ejectionPoses,
            Vector3 playerPosition,
            Func<Vector3, Vector3, bool> isWithinInteractionDistance,
            out Pose selected)
        {
            selected = default;
            if (ejectionPoses == null || isWithinInteractionDistance == null)
            {
                return false;
            }

            var found = false;
            var bestDistance = float.MaxValue;
            for (var index = 0; index < ejectionPoses.Count; index++)
            {
                var candidate = ejectionPoses[index];
                if (!isWithinInteractionDistance(playerPosition, candidate.position))
                {
                    continue;
                }

                var distance = (candidate.position - playerPosition).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    selected = candidate;
                    found = true;
                }
            }

            return found;
        }

        public bool TryHandlePlayerLeft(PlayerRef player)
        {
            if (IsLobby) return ReleaseDepartedLobbyObject(player);
            // Result has unloaded the scene-owned session; the replicated roster remains.
            if (_session == null && _state != null && _state.Phase == MatchPhase.Result && player.IsRealPlayer)
            {
                var leavingId = PlayerRegistry.IdOf(player);
                foreach (var participant in _playing)
                    if (participant.PlayerId == leavingId)
                        return _state.TrySetParticipantInactive(participant.PlayerIndex);
                return false;
            }
            if (!TryGetPlayerIndex(player, out var playerIndex))
            {
                return false;
            }

            var playerId = PlayerRegistry.IdOf(player);
            var lastKnownPose = Pose.identity;
            if (_roster == null || !_roster.TryGetPose(playerId, out lastKnownPose))
            {
                Debug.LogWarning(
                    $"[Match] No last pose was found for leaving player {playerId}.");
            }

            if (!_session.TryHandlePlayerLeft(playerIndex, lastKnownPose, ServerTime))
            {
                return false;
            }

            _state.TrySetParticipantInactive(playerIndex);
            return true;
        }

        public bool TryReturnToLobby(PlayerRef source)
        {
            if (_state == null || !source.IsRealPlayer ||
                !IsReturnParticipant(_playing, PlayerRegistry.IdOf(source)))
            {
                return false;
            }

            return ReturnToLobby();
        }

        public bool TryCompleteHighlightViewing(PlayerRef source, Pose lobbyPose)
        {
            if (_state == null || _session == null ||
                _session.CurrentPhase != MatchPhase.Highlight ||
                !TryGetPlayerIndex(source, out var playerIndex) ||
                !TryTeleportPlayer(playerIndex, lobbyPose))
            {
                return false;
            }

            return TrySetPlayerControls(playerIndex, true);
        }

        internal static bool IsReturnParticipant(IReadOnlyList<MatchParticipant> playing, string playerId)
        {
            foreach (var participant in playing)
                if (participant.PlayerId == playerId) return true;
            return false;
        }

        private bool ReturnToLobby()
        {
            // Loading Result unloads the match scene and unbinds its session.
            // The authority's replicated phase remains valid until rematch reset.
            if (_returningToLobby || _sceneDirector == null || _state == null ||
                (_session?.CurrentPhase ?? _state.Phase) != MatchPhase.Result)
            {
                return false;
            }

            if (!_sceneDirector.EnterLobbyScene(_state.Runner))
            {
                return false;
            }

            // The reset below clears the player-index mapping. Restore controls
            // while every match participant can still be resolved to an avatar.
            for (var playerIndex = 0; playerIndex < _playing.Count; playerIndex++)
            {
                TrySetPlayerControls(playerIndex, true);
            }

            _returningToLobby = true;
            if (!_state.TryResetForRematch())
            {
                throw new InvalidOperationException(
                    "The authority could not reset the completed match state.");
            }

            return true;
        }

        private double ServerTime => _state.Runner.SimulationTime;

        private void OnPlayerItemDestroyed(PlayerItemDestroyedEvent confirmedEvent)
        {
            _state?.RPC_NotifyItemDestroyed(
                confirmedEvent.DestroyerPlayerIndex,
                confirmedEvent.ItemId,
                confirmedEvent.DestroyedAt);
        }

        private void OnPlayerStunned(PlayerStunnedEvent confirmedEvent)
        {
            _state?.TrySetStunEndsAt(
                confirmedEvent.TargetPlayerIndex,
                confirmedEvent.StunEndsAt);
            _state?.RPC_NotifyPlayerStunned(
                confirmedEvent.AttackerPlayerIndex,
                confirmedEvent.TargetPlayerIndex,
                confirmedEvent.DroppedObjectId ?? string.Empty,
                confirmedEvent.StunnedAt,
                confirmedEvent.StunEndsAt);
        }

        private void OnObjectThrown(ObjectThrownEvent confirmedEvent)
        {
            _state?.RPC_NotifyObjectThrown(
                confirmedEvent.PlayerIndex,
                confirmedEvent.ObjectId,
                confirmedEvent.ReleasePose.position,
                confirmedEvent.ReleasePose.rotation,
                confirmedEvent.InitialVelocity,
                confirmedEvent.ThrownAt);
        }

        private void OnObjectAutoReleased(ObjectAutoReleasedEvent confirmedEvent)
        {
            _state?.TrySetObjectReleased(
                confirmedEvent.ObjectId,
                confirmedEvent.Pose);
        }

        private void OnMapObjectEjected(MapObjectEjectedEvent confirmedEvent)
        {
            if (_state == null || !_state.TrySetObjectReleased(
                    confirmedEvent.ObjectId,
                    confirmedEvent.Pose,
                    CalculateShredderEjectionVelocity(confirmedEvent.Pose.rotation)))
            {
                throw new InvalidOperationException(
                    $"The shredder could not eject '{confirmedEvent.ObjectId}'.");
            }
        }

        private void OnFinalWarningStarted(FinalWarningStartedEvent confirmedEvent)
        {
            _state?.RPC_NotifyFinalWarning(
                confirmedEvent.StartedAt,
                confirmedEvent.EndsAt);
        }

        private void OnMatchEnded(MatchResult result)
        {
            if (_state?.TrySetResult(result) == true &&
                result.EndReason == MatchEndReason.LastPlayerStanding)
            {
                ReturnToLobby();
            }
        }

        private void PublishRemainingDestructionUses(int playerIndex)
        {
            _state?.TrySetRemainingDestructionUses(
                playerIndex,
                _session.GetRemainingDestructionUses(playerIndex));
        }

        private void PublishHitCount(int playerIndex)
        {
            _state?.TrySetHitCount(playerIndex, _session.GetHitCount(playerIndex));
        }

        private void UnbindSession()
        {
            if (_session == null)
            {
                return;
            }

            _session.PlayerItemDestroyed -= OnPlayerItemDestroyed;
            _session.PlayerStunned -= OnPlayerStunned;
            _session.ObjectThrown -= OnObjectThrown;
            _session.ObjectAutoReleased -= OnObjectAutoReleased;
            _session.MapObjectEjected -= OnMapObjectEjected;
            _session.FinalWarningStarted -= OnFinalWarningStarted;
            _session.MatchEnded -= OnMatchEnded;
            _session = null;
        }

        internal static Vector3 CalculateShredderEjectionVelocity(Quaternion rotation) =>
            rotation * ShredderEjectionLocalVelocity;

        internal bool TryGetPlayerIndex(PlayerRef source, out int playerIndex)
        {
            if (_session == null || _state == null || _state.Runner == null)
            {
                playerIndex = -1;
                return false;
            }

            if (!source.IsRealPlayer && _state.Runner.IsServer)
            {
                source = _state.Runner.LocalPlayer;
            }

            playerIndex = -1;
            return source.IsRealPlayer &&
                   _session.Players.TryGetPlayerIndex(
                       PlayerRegistry.IdOf(source),
                       out playerIndex);
        }

        private bool TryGetPlayerPose(int playerIndex, out Pose pose)
        {
            if (_session != null &&
                _roster != null &&
                playerIndex >= 0 &&
                playerIndex < _session.Players.Players.Count)
            {
                var player = _session.Players.GetPlayer(playerIndex);
                return _roster.TryGetPose(player.PlayerId, out pose);
            }

            pose = default;
            return false;
        }

        private bool TryGetPlayingAvatar(int playerIndex, out PlayerAvatar avatar)
        {
            if (_state == null || _state.Object == null ||
                !_state.Object.HasStateAuthority || _roster == null ||
                playerIndex < 0 || playerIndex >= _playing.Count)
            {
                avatar = null;
                return false;
            }

            return _roster.TryGetAvatar(_playing[playerIndex].PlayerId, out avatar);
        }

        private bool IsObjectWithinReach(
            int playerIndex,
            string objectId,
            Vector3 playerPosition)
        {
            if (_session.TryGetObjectPose(objectId, out var objectPose))
            {
                return _interactionRules.IsWithinInteractionDistance(
                    playerPosition,
                    objectPose.position);
            }

            // Before its hiding turn placement, the assigned item is treated as
            // already being in its owner's hand.
            return playerIndex >= 0 &&
                   playerIndex < _session.Assignments.Count &&
                   string.Equals(
                       _session.Assignments[playerIndex].Item.ItemId,
                       objectId,
                       StringComparison.Ordinal) &&
                   !_session.TryGetItemPlacement(playerIndex, out _);
        }

        /// <summary>Reports a refusal on the peer that asked.</summary>
        public void Refused(RoomStartResult reason)
        {
            _sink?.MatchStartRefused(reason);
        }

        /// <summary>Forgets the room. The line-up goes with the session.</summary>
        public void Clear()
        {
            lobbyObjects = null;
            _state = null;
            _lastPublishedStarted = false;

            // The next room starts out listed as waiting, so that is what this
            // has to believe was last published.
            _publishedRoomStatus = false;
            _lastPublishedPhase = MatchPhase.Waiting;
            UnbindSession();
            _shredderEjectionPoses = Array.Empty<Pose>();
            _returningToLobby = false;
            _playing.Clear();
            _room.Clear();
            _sink?.MatchStarted(_playing);
            LineUpReceived?.Invoke(_playing);
        }

        /// <summary>
        /// Fills <see cref="_room"/> with the line-up if the match may start,
        /// and says why not otherwise.
        /// </summary>
        private RoomStartResult Evaluate(MatchSessionState state, NetworkRunner runner)
        {
            if (state.IsStarted)
            {
                return RoomStartResult.AlreadyStarted;
            }

            _room.Clear();
            _roster?.Capture(_room);

            if (_room.Count < RoomSettings.MinMatchPlayerCount)
            {
                return RoomStartResult.NotEnoughPlayers;
            }

            // Someone can be in the room before their character exists. Starting
            // then would leave a hole where the match rules expect a position for
            // every player, so it waits rather than starting short.
            var inRoom = 0;
            foreach (var _ in runner.ActivePlayers)
            {
                inRoom++;
            }

            if (inRoom != _room.Count)
            {
                Debug.Log(
                    $"[Match] {inRoom} in the room but {_room.Count} characters " +
                    "exist. Waiting for everyone to appear.");

                return RoomStartResult.NotEnoughPlayers;
            }

            return RoomStartResult.Started;
        }
    }
}
