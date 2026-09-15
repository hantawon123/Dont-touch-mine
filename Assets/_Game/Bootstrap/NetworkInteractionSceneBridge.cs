using System;
using System.Collections.Generic;
using Game.Client.Combat;
using Game.Client.Cameras;
using Game.Client.Interactions;
using Game.Client.Players;
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
        private readonly bool lobbyMode;
        private readonly UnityEngine.SceneManagement.Scene scene;
        private bool lobbyReady;
        private readonly Dictionary<string, CarryableItem> items =
            new(StringComparer.Ordinal);
        private readonly Dictionary<int, PlayerInteractor> interactors = new();
        private readonly Dictionary<int, PlayerCombatant> combatants = new();
        private readonly Dictionary<string, int> appliedVersions =
            new(StringComparer.Ordinal);

        private MatchObjectStateSnapshot[] objectStates =
            Array.Empty<MatchObjectStateSnapshot>();
        private PlayerInteractionStateSnapshot[] playerStates =
            Array.Empty<PlayerInteractionStateSnapshot>();
        private string assignedItemId;
        private double nextAssignmentRequestAt;
        private double nextHeldStateCheckAt;
        private CarryableItem highlightedAssignment;
        private PlayerCameraController cameraRig;
        private Transform cameraTarget;
        private bool standaloneActorsDisabled;
        private bool readinessReported;
        private bool gameplayFramePresented;
        private bool initialStatesApplied;
        public bool IsLocalPresentationReady => gameplayFramePresented &&
            cameraRig != null && cameraRig.isActiveAndEnabled && cameraTarget != null &&
            initialStatesApplied && network.IsSceneLoadComplete && network.IsSimulationCaughtUp;
        private double startedAt;
        private double cameraDiagnosticAt = double.PositiveInfinity;

        public NetworkInteractionSceneBridge(
            NetworkRunnerService network,
            RoomBrowserSystem room,
            bool lobbyMode,
            UnityEngine.SceneManagement.Scene scene)
        {
            this.network = network ?? throw new ArgumentNullException(nameof(network));
            this.room = room ?? throw new ArgumentNullException(nameof(room));
            this.lobbyMode = lobbyMode;
            this.scene = scene;
        }

        private Func<bool> presentationBlocksInput;
        public void BindPresentationInput(Func<bool> blocksInput) => presentationBlocksInput = blocksInput;

        private IReadOnlyCollection<CarryableItem> sceneItems;
        public void BindSceneItems(IReadOnlyCollection<CarryableItem> value) => sceneItems = value;

        public void Start()
        {
            startedAt = Time.realtimeSinceStartupAsDouble;
            if (!lobbyMode) UnityEngine.Rendering.RenderPipelineManager.endCameraRendering += OnGameplayCameraRendered;
            network.ItemAssignmentReceived += OnItemAssignmentReceived;
            network.ObjectStatesReceived += OnObjectStatesReceived;
            network.PlayerInteractionStatesReceived += OnPlayerStatesReceived;
            RefreshItems();
        }

        public void Dispose()
        {
            UnityEngine.Rendering.RenderPipelineManager.endCameraRendering -= OnGameplayCameraRendered;
            network.ItemAssignmentReceived -= OnItemAssignmentReceived;
            network.ObjectStatesReceived -= OnObjectStatesReceived;
            network.PlayerInteractionStatesReceived -= OnPlayerStatesReceived;
            foreach (var interactor in interactors.Values)
            {
                if (interactor == null) continue;
                var motor = interactor.GetComponent<NetworkPlayerMotor>();
                if (motor != null) motor.LocalPresentationInputBlocked = false;
                interactor.SetHudVisible(true);
            }
            DestroyCarriedSceneItems();
            SetHighlightedAssignment(null);
        }

        public void Tick()
        {
            if (Time.realtimeSinceStartupAsDouble >= cameraDiagnosticAt)
            {
                cameraDiagnosticAt = double.PositiveInfinity;
                MatchTransitionDiagnostics.Dump("playground-camera-settled");
            }
            if (!network.IsRuntimeReady || network.IsBrowsingLobby)
            {
                return;
            }

            if (lobbyMode)
            {
                if (!network.IsWaitingForMatch) return;
                if (!lobbyReady)
                {
                    RefreshItems();
                    if (network.IsServer)
                    {
                        var initial = new List<Game.Server.Items.WorldObjectState>(items.Count);
                        foreach (var item in items.Values)
                            initial.Add(new Game.Server.Items.WorldObjectState(item.ObjectId,
                                new Pose(item.transform.position, item.transform.rotation)));
                        if (!network.ConfigureLobbyObjects(initial)) return;
                    }
                    lobbyReady = true;
                    network.PublishInteractionState();
                }
                RefreshPlayers();
                ApplyObjectStates();
                PublishPhysicsObjects();
                return;
            }

            if (network.IsWaitingForMatch || network.IsLocalHighlightComplete) return;

            // Request after scene subscribers are installed; retry until an assignment actually arrives.
            // The host only resends assignments already published for this sender's current match.
            var now = Time.unscaledTimeAsDouble;
            if (assignedItemId == null && room.LocalPlayerIndex >= 0 && now >= nextAssignmentRequestAt)
            {
                nextAssignmentRequestAt = now + 1d;
                network.RequestItemAssignment();
            }
            DisableStandaloneActors();
            RefreshPlayers();
            ApplyAssignmentOwner();
            ApplyObjectStates();
            PublishPhysicsObjects();
            ApplyPlayerStates();
            initialStatesApplied = readinessReported && assignedItemId != null;
        }

        public bool RequestHold(string objectId) => network.RequestHoldObject(objectId);

        public bool RequestDrop(Pose pose) => network.RequestDropHeldObject(pose);

        public bool RequestRelease(Pose pose) => network.RequestReleaseHeldObject(pose);

        public bool RequestThrow(Pose pose, Vector3 initialVelocity) =>
            network.RequestThrowHeldObject(pose, initialVelocity);

        public bool RequestHit(int targetPlayerIndex) =>
            network.RequestHitPlayer(targetPlayerIndex);

        public bool RequestUseShredder() => network.RequestUseShredder();

        private void RefreshItems()
        {
            items.Clear();
            var candidates = sceneItems ?? UnityEngine.Object.FindObjectsByType<CarryableItem>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var item in candidates)
            {
                if (item == null || (sceneItems == null && item.gameObject.scene != scene)) continue;
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
            if (!lobbyMode && participants.Count == 0)
            {
                return;
            }

            var avatars = network.PlayerAvatars;
            for (var avatarIndex = 0; avatarIndex < avatars.Count; avatarIndex++)
            {
                var avatar = avatars[avatarIndex];
                if (avatar == null || !avatar.isActiveAndEnabled || avatar.Object == null || !avatar.Object.IsValid)
                {
                    continue;
                }

                var playerId = PlayerRegistry.IdOf(avatar.Owner);
                var playerIndex = lobbyMode ? avatar.Seat : IndexOf(participants, playerId);
                if (playerIndex < 0)
                {
                    continue;
                }

                var motor = avatar.GetComponent<NetworkPlayerMotor>();
                if (!lobbyMode && avatar.IsOwner && motor != null && motor.IsScenePlacementReady)
                {
                    BindLocalCamera(avatar.transform);
                }

                var introBlocked = presentationBlocksInput?.Invoke() == true;
                if (avatar.IsOwner && motor != null) motor.LocalPresentationInputBlocked = introBlocked;
                var acceptsLocalInput = !introBlocked && avatar.IsOwner &&
                                        motor != null &&
                                        motor.ControlsEnabled;

                if (motor != null)
                {
                    avatar.GetComponent<PlayerMovement>()?.ApplyNetworkPosture(
                        motor.Posture);
                    avatar.GetComponent<PlayerAnimationDriver>()?.ApplyNetworkState(
                        motor.AnimationSpeed,
                        motor.AnimationGrounded,
                        motor.AttackSequence,
                        new Vector2(motor.AnimationMoveX, motor.AnimationMoveZ),
                        motor.AnimationCarrying && !network.IsResultSceneLoaded);
                }

                var interactor = avatar.GetComponent<PlayerInteractor>();
                if (interactor != null)
                {
                    // A disabled network avatar must never fall back to standalone item mutation.
                    interactor.BindCommands(this);
                    interactor.enabled = acceptsLocalInput;
                    if (!acceptsLocalInput) interactor.RefreshHoldPoint();
                    interactor.SetHudVisible(!introBlocked);
                    interactors[playerIndex] = interactor;

                    var placement = avatar.GetComponent<ItemPlacementController>();
                    if (placement != null)
                    {
                        placement.enabled = acceptsLocalInput;
                    }
                }

                var combatant = avatar.GetComponent<PlayerCombatant>();
                if (!lobbyMode && combatant != null)
                {
                    combatant.ConfigureNetworkPlayer(playerIndex, acceptsLocalInput);
                    combatants[playerIndex] = combatant;
                }
            }
        }

        private void OnGameplayCameraRendered(UnityEngine.Rendering.ScriptableRenderContext context, Camera camera)
        {
            // Render behind the cover before dismissing it; waiting for it to close
            // here would make actual loading readiness circular.
            if (!network.IsSceneLoadComplete || !initialStatesApplied)
            {
                gameplayFramePresented = false;
                return;
            }
            if (gameplayFramePresented || !readinessReported || cameraTarget == null ||
                camera == null || camera.gameObject.scene != scene || camera.targetTexture != null ||
                !camera.CompareTag("MainCamera")) return;
            gameplayFramePresented = true;
            Debug.Log($"[SceneTiming] Gameplay frame presented: scene={scene.name}, elapsed={Time.realtimeSinceStartupAsDouble - startedAt:F3}s.");
        }

        private void BindLocalCamera(Transform target)
        {
            if (target == null || (cameraRig != null && ReferenceEquals(cameraTarget, target)))
            {
                return;
            }

            if (cameraRig == null)
                cameraRig = UnityEngine.Object.FindFirstObjectByType<PlayerCameraController>(FindObjectsInactive.Include);
            if (cameraRig == null)
            {
                return;
            }

            var preserveView = !ReferenceEquals(cameraTarget, null);
            cameraTarget = target;
            gameplayFramePresented = false;
            cameraRig.SetFollowTarget(target, preserveView);
            if (!preserveView) cameraRig.SetCursorCaptureEnabled(true);
            if (!readinessReported)
            {
                readinessReported = true;
                cameraDiagnosticAt = Time.realtimeSinceStartupAsDouble + 2d;
                MatchTransitionDiagnostics.Dump("playground-camera-bound");
                Debug.Log(
                    $"[SceneTiming] Playground local player ready, " +
                    $"elapsedSinceBridgeStart={Time.realtimeSinceStartupAsDouble - startedAt:F3}s.");
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

        private void ApplyAssignmentOwner()
        {
            if (string.IsNullOrEmpty(assignedItemId) ||
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
            SetHighlightedAssignment(item);
        }

        private void ApplyObjectStates()
        {
            // Result presentation detaches held items. Frozen match snapshots must not reattach them.
            if (network.IsResultSceneLoaded) return;

            var checkHeldState = Time.unscaledTimeAsDouble >= nextHeldStateCheckAt;
            if (checkHeldState) nextHeldStateCheckAt = Time.unscaledTimeAsDouble + 0.5d;
            for (var index = 0; index < objectStates.Length; index++)
            {
                var state = objectStates[index];
                if (!items.TryGetValue(state.ObjectId, out var item) || item == null)
                {
                    continue;
                }

                if (appliedVersions.TryGetValue(state.ObjectId, out var version) &&
                    (version > state.Version ||
                     (version == state.Version && (!checkHeldState || IsHeldStateAligned(state, item)))))
                {
                    continue;
                }

                if (state.IsPendingEjection)
                {
                    ForgetItem(item);
                    item.OnStored(state.Pose);
                    appliedVersions[state.ObjectId] = state.Version;
                    continue;
                }
                else if (state.IsDestroyed)
                {
                    ForgetItem(item);
                    if (ReferenceEquals(highlightedAssignment, item))
                    {
                        highlightedAssignment = null;
                    }

                    appliedVersions[state.ObjectId] = state.Version;
                    items.Remove(state.ObjectId);
                    UnityEngine.Object.Destroy(item.gameObject);
                    continue;
                }

                if (state.HolderPlayerIndex >= 0)
                {
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
                    // Play the throw only after the authority actually releases the held object.
                    if (state.IsPhysicsActive && state.InitialVelocity.sqrMagnitude > 0f)
                        foreach (var holder in interactors.Values)
                            if (holder != null && holder.CarriedItem == item)
                                holder.GetComponent<PlayerAnimationDriver>()?.PlayThrow();
                    ForgetItem(item);
                    if (!network.IsServer)
                    {
                        item.OnNetworkPose(state.Pose);
                    }
                    else if (state.IsPhysicsActive)
                    {
                        item.OnReleased(state.Pose, state.InitialVelocity);
                    }
                    else
                    {
                        item.OnSettled(state.Pose, true);
                    }
                }

                appliedVersions[state.ObjectId] = state.Version;
            }
        }

        private bool IsHeldStateAligned(MatchObjectStateSnapshot state, CarryableItem item)
        {
            var held = false;
            foreach (var pair in interactors)
            {
                if (pair.Value.CarriedItem != item) continue;
                if (pair.Key != state.HolderPlayerIndex) return false;
                held = true;
            }
            return state.HolderPlayerIndex >= 0 ? held && item.IsCarried : !held && !item.IsCarried;
        }

        private double nextPhysicsPublishAt;
        private void PublishPhysicsObjects()
        {
            if (!network.IsServer || Time.unscaledTimeAsDouble < nextPhysicsPublishAt) return;
            nextPhysicsPublishAt = Time.unscaledTimeAsDouble + 0.1d;
            foreach (var state in objectStates)
            {
                if (state.HolderPlayerIndex >= 0 || state.IsDestroyed || state.IsPendingEjection ||
                    !items.TryGetValue(state.ObjectId, out var item) || item == null ||
                    !item.TryGetPhysicsPose(out var pose, out var velocity, out var moving)) continue;
                if (!moving && !state.IsPhysicsActive &&
                    Vector3.SqrMagnitude(pose.position - state.Pose.position) < 0.000001f &&
                    Quaternion.Angle(pose.rotation, state.Pose.rotation) < 0.1f) continue;
                if (network.TryConfirmObjectPhysicsPose(state.ObjectId, pose, velocity, moving, state.Version))
                    appliedVersions[state.ObjectId] = state.Version + 1;
            }
        }

        private void ApplyPlayerStates()
        {
            if (!network.IsRuntimeReady)
            {
                return;
            }

            var now = network.ServerTime;
            for (var index = 0; index < playerStates.Length; index++)
            {
                var state = playerStates[index];
                if (combatants.TryGetValue(state.PlayerIndex, out var combatant))
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (combatant.IsStunned != state.IsStunned(now))
                    {
                        var driver = combatant.GetComponent<PlayerAnimationDriver>();
                        Debug.Log($"[QA-Stun] apply scene={scene.name} player={state.PlayerIndex} now={now:F3} end={state.StunEndsAt:F3} stunned={state.IsStunned(now)} driverEnabled={driver != null && driver.isActiveAndEnabled} actor={combatant.GetInstanceID()}");
                    }
#endif
                    combatant.SetNetworkStunned(state.IsStunned(now));
                    combatant.SetNetworkHitCount(state.HitCount);
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

        private void DestroyCarriedSceneItems()
        {
            foreach (var item in items.Values)
            {
                if (item == null || !item.IsCarried)
                {
                    continue;
                }

                ForgetItem(item);
                UnityEngine.Object.Destroy(item.gameObject);
            }
        }

        private void OnItemAssignmentReceived(string itemId)
        {
            if (lobbyMode) return;
            assignedItemId = string.IsNullOrWhiteSpace(itemId) ? null : itemId.Trim();
            SetHighlightedAssignment(
                assignedItemId != null && items.TryGetValue(assignedItemId, out var item)
                    ? item
                    : null);
        }

        private void SetHighlightedAssignment(CarryableItem item)
        {
            if (ReferenceEquals(highlightedAssignment, item))
            {
                return;
            }

            if (highlightedAssignment != null)
            {
                highlightedAssignment.SetAssignedHighlight(false);
            }

            highlightedAssignment = item;
            if (highlightedAssignment != null)
            {
                highlightedAssignment.SetAssignedHighlight(true);
            }
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
