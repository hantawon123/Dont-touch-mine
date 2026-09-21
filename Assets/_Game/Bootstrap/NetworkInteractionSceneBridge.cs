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
        private bool suspendedForHighlights;
        private readonly Dictionary<string, CarryableItem> items =
            new(StringComparer.Ordinal);
        private readonly Dictionary<int, PlayerInteractor> interactors = new();
        private readonly Dictionary<int, PlayerCombatant> combatants = new();
        // 붙이기 실패를 물건별로 한 번만 경고하기 위한 기록(성공하면 지운다)
        private readonly HashSet<string> attachWarnings = new();
        // Objects the client last saw enter the shredder's pending-ejection state, so the
        // later physics-released state (no current holder) is known to be a shredder eject
        // rather than a genuine player throw, which shares the same replicated fields.
        private readonly HashSet<string> pendingShredderEjectionIds = new(StringComparer.Ordinal);
        private AudioClip shredderFeedClip;
        private AudioClip shredderRunClip;
        private AudioClip shredderEjectClip;
        private AudioClip shredderSuccessClip;
        private double nextAssignmentItemScanAt;
        private bool objectStatesReceived;
        private readonly HashSet<string> replicatedIds = new();
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

        internal static bool ShouldShowInteractionHud(
            bool introBlocked,
            bool highlightInProgress) =>
            !introBlocked && !highlightInProgress;

        private IReadOnlyCollection<CarryableItem> sceneItems;
        public void BindSceneItems(IReadOnlyCollection<CarryableItem> value) => sceneItems = value;

        public void Start()
        {
            startedAt = Time.realtimeSinceStartupAsDouble;
            if (!lobbyMode) UnityEngine.Rendering.RenderPipelineManager.endCameraRendering += OnGameplayCameraRendered;
            network.ItemAssignmentReceived += OnItemAssignmentReceived;
            network.ObjectStatesReceived += OnObjectStatesReceived;
            network.PlayerInteractionStatesReceived += OnPlayerStatesReceived;
#if !UNITY_SERVER
            shredderFeedClip = Resources.Load<AudioClip>(ShredderInteractable.FeedResource);
            shredderRunClip = Resources.Load<AudioClip>(ShredderInteractable.RunResource);
            shredderEjectClip = Resources.Load<AudioClip>(ShredderInteractable.EjectResource);
            shredderSuccessClip = Resources.Load<AudioClip>(ShredderInteractable.SuccessResource);
#endif
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

        private void PlayShredderClip(AudioClip clip, Vector3 position)
        {
            ShredderInteractable.PlaySpatial(clip, position);
        }

        internal void SuspendForHighlights()
        {
            if (lobbyMode || suspendedForHighlights) return;
            suspendedForHighlights = true;
            // Replay uses separate visual copies. The retained live map must neither
            // simulate falling props nor re-enable their colliders through state updates.
            foreach (var item in items.Values)
            {
                if (item == null) continue;
                item.enabled = false;
                if (item.TryGetComponent<Rigidbody>(out var body))
                {
                    body.isKinematic = true;
                    body.detectCollisions = false;
                }
                foreach (var collider in item.GetComponentsInChildren<Collider>(true))
                    collider.enabled = false;
            }
        }

        public void Tick()
        {
            if (suspendedForHighlights) return;
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

        /// <summary>
        /// 이 씬의 물건 목록을 갱신한다. 기존 등록은 유지하고(파괴된 것만 정리) 새 물건만 더한다.
        /// </summary>
        /// <remarks>
        /// 들고 있는 물건은 플레이어 오브젝트(다른 씬) 아래에 붙어 있어 <c>gameObject.scene</c>이 이 씬이 아니다.
        /// 예전처럼 목록을 비우고 현재 씬 소속만 다시 담으면 들고 있는 물건이 목록에서 빠지고, 그 뒤 그 물건의
        /// 상태 변화(놓임·파괴)가 전부 무시된다. 그러면 이 클라이언트는 그 플레이어가 "이미 놓은 물건을 계속 들고
        /// 있다"고 믿어 이후 그 플레이어가 집는 모든 물건을 붙이지 못한다(팀 테스트 2026-09-17: 물건이 머리 위에
        /// 남고 놓기가 안 되던 버그). 그래서 소속 판단은 <see cref="CarryableItem.OwningScene"/>로 한다.
        /// </remarks>
        private void RefreshItems()
        {
            var stale = new List<string>();
            foreach (var pair in items)
            {
                if (pair.Value == null) stale.Add(pair.Key);
            }

            foreach (var key in stale) items.Remove(key);

            var candidates = sceneItems ?? UnityEngine.Object.FindObjectsByType<CarryableItem>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var item in candidates)
            {
                if (item == null) continue;
                if (sceneItems == null && item.OwningScene != scene && item.gameObject.scene != scene) continue;
                if (items.TryGetValue(item.ObjectId, out var existing))
                {
                    if (ReferenceEquals(existing, item)) continue;
                    if (existing != null)
                    {
                        Debug.LogError(
                            $"[Match] Duplicate carryable object id '{item.ObjectId}'.",
                            item);
                        continue;
                    }

                    items[item.ObjectId] = item;
                    continue;
                }

                items.Add(item.ObjectId, item);
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
                    interactor.SetHudVisible(ShouldShowInteractionHud(
                        introBlocked,
                        network.IsHighlightInProgress));
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
                    combatant.ConfigureNetworkPlayer(playerIndex, acceptsLocalInput, avatar.IsOwner);
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
                // 배정 물건이 아직 없으면 목록을 다시 훑되, 매 틱 씬 전체를 뒤지지는 않는다.
                if (Time.unscaledTimeAsDouble < nextAssignmentItemScanAt) return;
                nextAssignmentItemScanAt = Time.unscaledTimeAsDouble + 1d;
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
            if (suspendedForHighlights || network.IsResultSceneLoaded) return;

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
                    var firstEjection = pendingShredderEjectionIds.Add(state.ObjectId);
                    if (firstEjection)
                    {
                        PlayShredderClip(shredderFeedClip, item.transform.position);
                        PlayShredderClip(shredderRunClip, item.transform.position);
                    }
                    item.OnStored(state.Pose);
                    appliedVersions[state.ObjectId] = state.Version;
                    continue;
                }
                else if (state.IsDestroyed)
                {
                    ForgetItem(item);
                    pendingShredderEjectionIds.Remove(state.ObjectId);
                    ShredderInteractable.PlayGlobal(shredderSuccessClip);
                    if (ReferenceEquals(highlightedAssignment, item))
                    {
                        highlightedAssignment = null;
                    }

                    appliedVersions[state.ObjectId] = state.Version;
                    items.Remove(state.ObjectId);
                    // 엔딩 유치장이 잃어버린 물건을 손에 들려 주므로, 지우기 전에 겉모습을 맡긴다
                    // (S15P21D205-1087). 맡기는 것은 스크립트를 지운 복제본이다.
                    DestroyedItemArchive.Ensure(item.OwningScene).Archive(state.ObjectId, item.gameObject);
                    UnityEngine.Object.Destroy(item.gameObject);
                    continue;
                }

                if (state.HolderPlayerIndex >= 0)
                {
                    if (!interactors.TryGetValue(
                            state.HolderPlayerIndex,
                            out var holder))
                    {
                        if (attachWarnings.Add(state.ObjectId))
                            Debug.LogWarning(
                                $"[Interaction] '{state.ObjectId}' is held by player {state.HolderPlayerIndex} on authority " +
                                $"but that player has no avatar here (known indices: {string.Join(",", interactors.Keys)}).");
                        continue;
                    }

                    if (holder.CarriedItem == item && item.IsCarried)
                    {
                        appliedVersions[state.ObjectId] = state.Version;
                        attachWarnings.Remove(state.ObjectId);
                        continue;
                    }

                    ForgetItem(item);
                    if (!holder.ApplyConfirmedPickup(item))
                    {
                        if (attachWarnings.Add(state.ObjectId))
                            Debug.LogWarning(
                                $"[Interaction] could not attach '{state.ObjectId}' to player {state.HolderPlayerIndex}: " +
                                $"already carrying '{holder.CarriedItem?.ObjectId}'.");
                        continue;
                    }

                    attachWarnings.Remove(state.ObjectId);
                }
                else
                {
                    // Play the throw only after the authority actually releases the held object.
                    // A shredder eject reaches this same "released, no holder" state, so a
                    // pending-ejection id marks it as a spit-out rather than a player throw.
                    if (state.IsPhysicsActive && state.InitialVelocity.sqrMagnitude > 0f)
                    {
                        if (pendingShredderEjectionIds.Remove(state.ObjectId))
                        {
                            PlayShredderClip(shredderEjectClip, item.transform.position);
                        }
                        else
                        {
                            foreach (var holder in interactors.Values)
                                if (holder != null)
                                    holder.PlayConfirmedThrow(item);
                        }
                    }
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

            DetachItemsMissingFromAuthority();
        }

        /// <summary>
        /// 복제 배열에 더는 없는 물건을 누가 들고 있으면 손에서 뗀다. 매치가 끝나 호스트가 배열을 비웠거나 상태가
        /// 사라진 경우, 예전엔 그 물건이 플레이어에 붙은 채 남아 로비까지 따라왔다.
        /// </summary>
        private void DetachItemsMissingFromAuthority()
        {
            if (!objectStatesReceived) return;
            replicatedIds.Clear();
            for (var index = 0; index < objectStates.Length; index++) replicatedIds.Add(objectStates[index].ObjectId);

            foreach (var interactor in interactors.Values)
            {
                var carried = interactor != null ? interactor.CarriedItem : null;
                if (carried == null || replicatedIds.Contains(carried.ObjectId)) continue;
                Debug.LogWarning(
                    $"[Interaction] '{carried.ObjectId}' is carried by {interactor.name} but authority no longer tracks it; detaching.");
                interactor.ForgetConfirmedItem(carried);
                carried.OnNetworkPose(new Pose(carried.transform.position, carried.transform.rotation));
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

        /// <summary>
        /// 매치 씬을 떠날 때 손에 남은 물건을 모두 지운다. 들고 있던 물건은 플레이어(씬을 넘어 살아남는
        /// 네트워크 오브젝트) 아래에 붙어 있어, 여기서 지우지 않으면 로비까지 따라온다. 등록된 물건뿐 아니라
        /// 각 플레이어의 CarriedItem과 HoldPoint 아래에 남은 것도 함께 정리한다.
        /// </summary>
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

            foreach (var interactor in interactors.Values)
            {
                if (interactor == null) continue;
                var carried = interactor.CarriedItem;
                if (carried != null)
                {
                    interactor.ForgetConfirmedItem(carried);
                    UnityEngine.Object.Destroy(carried.gameObject);
                }

                var holdPoint = interactor.HoldPoint;
                if (holdPoint == null) continue;
                foreach (var stray in holdPoint.GetComponentsInChildren<CarryableItem>(true))
                {
                    if (stray != null) UnityEngine.Object.Destroy(stray.gameObject);
                }
            }
        }

        private void OnItemAssignmentReceived(string itemId)
        {
            if (lobbyMode || suspendedForHighlights) return;
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
            if (suspendedForHighlights) return;
            objectStatesReceived = true;
            objectStates = states == null
                ? Array.Empty<MatchObjectStateSnapshot>()
                : Copy(states);
        }

        private void OnPlayerStatesReceived(
            IReadOnlyList<PlayerInteractionStateSnapshot> states)
        {
            if (suspendedForHighlights) return;
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
