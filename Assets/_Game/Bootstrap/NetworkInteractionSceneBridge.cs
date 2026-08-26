using System;
using System.Collections.Generic;
using Game.Client.Combat;
using Game.Client.Interactions;
using Game.Core.Lobby;
using Game.Core.Match;
using Game.Network.Match;
using Game.Network.Players;
using Game.Network.Session;
using UnityEngine;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>
    /// Connects the existing client interaction components to authority-confirmed
    /// Fusion state without putting a Fusion dependency in Game.Client.
    /// </summary>
    public sealed class NetworkInteractionSceneBridge :
        IPlayerInteractionCommands,
        IStartable,
        ITickable,
        IDisposable
    {
        private readonly NetworkRunnerService network;
        private readonly RoomBrowserSystem room;
        private readonly Dictionary<string, CarryableItem> items =
            new(StringComparer.Ordinal);
        private readonly Dictionary<int, PlayerInteractor> interactors = new();
        private readonly Dictionary<int, PlayerCombatant> combatants = new();
        private readonly Dictionary<string, int> appliedVersions =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> settlingVersions =
            new(StringComparer.Ordinal);
        private readonly List<string> settledObjectIds = new();

        private MatchObjectStateSnapshot[] objectStates =
            Array.Empty<MatchObjectStateSnapshot>();
        private PlayerInteractionStateSnapshot[] playerStates =
            Array.Empty<PlayerInteractionStateSnapshot>();
        private string assignedItemId;
        private bool assignmentHoldRequested;
        private bool standaloneActorsDisabled;

        public NetworkInteractionSceneBridge(
            NetworkRunnerService network,
            RoomBrowserSystem room)
        {
            this.network = network ?? throw new ArgumentNullException(nameof(network));
            this.room = room ?? throw new ArgumentNullException(nameof(room));
        }

        public void Start()
        {
            network.ItemAssignmentReceived += OnItemAssignmentReceived;
            network.ObjectStatesReceived += OnObjectStatesReceived;
            network.PlayerInteractionStatesReceived += OnPlayerStatesReceived;
            RefreshItems();
        }

        public void Dispose()
        {
            network.ItemAssignmentReceived -= OnItemAssignmentReceived;
            network.ObjectStatesReceived -= OnObjectStatesReceived;
            network.PlayerInteractionStatesReceived -= OnPlayerStatesReceived;
        }

        public void Tick()
        {
            if (!network.IsRunning || network.IsBrowsingLobby)
            {
                return;
            }

            DisableStandaloneActors();
            RefreshPlayers();
            TryApplyAssignment();
            ApplyObjectStates();
            ConfirmSettledObjects();
            ApplyPlayerStates();
        }

        public bool RequestHold(string objectId) => network.RequestHoldObject(objectId);

        public bool RequestRelease(Pose pose) => network.RequestReleaseHeldObject(pose);

        public bool RequestThrow(Pose pose, Vector3 initialVelocity) =>
            network.RequestThrowHeldObject(pose, initialVelocity);

        public bool RequestHit(int targetPlayerIndex) =>
            network.RequestHitPlayer(targetPlayerIndex);

        public bool RequestUseShredder() => network.RequestUseShredder();

        private void RefreshItems()
        {
            items.Clear();
            foreach (var item in UnityEngine.Object.FindObjectsByType<CarryableItem>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (!items.TryAdd(item.ObjectId, item))
                {
                    Debug.LogError(
                        $"[Match] Duplicate carryable object id '{item.ObjectId}'.",
                        item);
                }
            }
        }

        private void RefreshPlayers()
        {
            interactors.Clear();
            combatants.Clear();
            var participants = room.MatchParticipants.CurrentValue;
            if (participants.Count == 0)
            {
                return;
            }

            foreach (var avatar in UnityEngine.Object.FindObjectsByType<PlayerAvatar>(
                         FindObjectsInactive.Exclude,
                         FindObjectsSortMode.None))
            {
                var playerId = PlayerRegistry.IdOf(avatar.Owner);
                var playerIndex = IndexOf(participants, playerId);
                if (playerIndex < 0)
                {
                    continue;
                }

                var motor = avatar.GetComponent<NetworkPlayerMotor>();
                var acceptsLocalInput = avatar.IsOwner &&
                                        motor != null &&
                                        motor.ControlsEnabled;

                var interactor = avatar.GetComponent<PlayerInteractor>();
                if (interactor != null)
                {
                    interactor.BindCommands(acceptsLocalInput ? this : null);
                    interactor.enabled = acceptsLocalInput;
                    interactors[playerIndex] = interactor;

                    var placement = avatar.GetComponent<ItemPlacementController>();
                    if (placement != null)
                    {
                        placement.enabled = acceptsLocalInput;
                    }
                }

                var combatant = avatar.GetComponent<PlayerCombatant>();
                if (combatant != null)
                {
                    combatant.ConfigureNetworkPlayer(playerIndex, acceptsLocalInput);
                    combatants[playerIndex] = combatant;
                }
            }
        }

        private void DisableStandaloneActors()
        {
            if (standaloneActorsDisabled)
            {
                return;
            }

            standaloneActorsDisabled = true;
            foreach (var interactor in UnityEngine.Object.FindObjectsByType<PlayerInteractor>(
                         FindObjectsInactive.Exclude,
                         FindObjectsSortMode.None))
            {
                if (interactor.GetComponent<PlayerAvatar>() == null)
                {
                    interactor.gameObject.SetActive(false);
                }
            }
        }

        private void TryApplyAssignment()
        {
            if (assignmentHoldRequested || string.IsNullOrEmpty(assignedItemId) ||
                room.LocalPlayerIndex < 0)
            {
                return;
            }

            if (!items.TryGetValue(assignedItemId, out var item) || item == null)
            {
                RefreshItems();
                if (!items.TryGetValue(assignedItemId, out item) || item == null)
                {
                    return;
                }
            }

            item.AssignToPlayer(room.LocalPlayerIndex);
            assignmentHoldRequested = network.RequestHoldObject(assignedItemId);
        }

        private void ApplyObjectStates()
        {
            for (var index = 0; index < objectStates.Length; index++)
            {
                var state = objectStates[index];
                if (appliedVersions.TryGetValue(state.ObjectId, out var version) &&
                    version >= state.Version)
                {
                    continue;
                }

                if (!items.TryGetValue(state.ObjectId, out var item) || item == null)
                {
                    continue;
                }

                if (state.IsDestroyed)
                {
                    settlingVersions.Remove(state.ObjectId);
                    ForgetItem(item);
                    appliedVersions[state.ObjectId] = state.Version;
                    items.Remove(state.ObjectId);
                    UnityEngine.Object.Destroy(item.gameObject);
                    continue;
                }

                if (state.HolderPlayerIndex >= 0)
                {
                    settlingVersions.Remove(state.ObjectId);
                    if (!interactors.TryGetValue(
                            state.HolderPlayerIndex,
                            out var holder))
                    {
                        continue;
                    }

                    ForgetItem(item);
                    if (!holder.ApplyConfirmedPickup(item))
                    {
                        continue;
                    }
                }
                else
                {
                    ForgetItem(item);
                    if (state.IsPhysicsActive)
                    {
                        item.OnReleased(state.Pose, state.InitialVelocity);
                        if (network.IsServer)
                        {
                            settlingVersions[state.ObjectId] = state.Version;
                        }
                    }
                    else
                    {
                        settlingVersions.Remove(state.ObjectId);
                        item.OnSettled(state.Pose, network.IsServer);
                    }
                }

                appliedVersions[state.ObjectId] = state.Version;
            }
        }

        private void ConfirmSettledObjects()
        {
            if (!network.IsServer || settlingVersions.Count == 0)
            {
                return;
            }

            settledObjectIds.Clear();
            foreach (var pair in settlingVersions)
            {
                if (!items.TryGetValue(pair.Key, out var item) || item == null)
                {
                    settledObjectIds.Add(pair.Key);
                    continue;
                }

                if (!item.TryGetSettledPose(out var pose))
                {
                    continue;
                }

                network.TryConfirmObjectSettled(pair.Key, pose, pair.Value);
                settledObjectIds.Add(pair.Key);
            }

            foreach (var objectId in settledObjectIds)
            {
                settlingVersions.Remove(objectId);
            }
        }

        private void ApplyPlayerStates()
        {
            if (!network.IsRunning)
            {
                return;
            }

            var now = network.ServerTime;
            for (var index = 0; index < playerStates.Length; index++)
            {
                var state = playerStates[index];
                if (combatants.TryGetValue(state.PlayerIndex, out var combatant))
                {
                    combatant.SetNetworkStunned(state.IsStunned(now));
                }
            }
        }

        private void ForgetItem(CarryableItem item)
        {
            foreach (var interactor in interactors.Values)
            {
                interactor.ForgetConfirmedItem(item);
            }
        }

        private void OnItemAssignmentReceived(string itemId)
        {
            assignedItemId = string.IsNullOrWhiteSpace(itemId) ? null : itemId.Trim();
            assignmentHoldRequested = false;
        }

        private void OnObjectStatesReceived(
            IReadOnlyList<MatchObjectStateSnapshot> states)
        {
            objectStates = states == null
                ? Array.Empty<MatchObjectStateSnapshot>()
                : Copy(states);
        }

        private void OnPlayerStatesReceived(
            IReadOnlyList<PlayerInteractionStateSnapshot> states)
        {
            if (states == null)
            {
                playerStates = Array.Empty<PlayerInteractionStateSnapshot>();
                return;
            }

            playerStates = new PlayerInteractionStateSnapshot[states.Count];
            for (var index = 0; index < states.Count; index++)
            {
                playerStates[index] = states[index];
            }
        }

        private static MatchObjectStateSnapshot[] Copy(
            IReadOnlyList<MatchObjectStateSnapshot> states)
        {
            var copy = new MatchObjectStateSnapshot[states.Count];
            for (var index = 0; index < states.Count; index++)
            {
                copy[index] = states[index];
            }

            return copy;
        }

        private static int IndexOf(
            IReadOnlyList<MatchParticipant> participants,
            string playerId)
        {
            for (var index = 0; index < participants.Count; index++)
            {
                if (string.Equals(
                        participants[index].PlayerId,
                        playerId,
                        StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
        }
    }
}
