using System;
using System.Collections.Generic;
using Game.Client.Cameras;
using Game.Client.Interactions;
using Game.Client.Match;
using Game.Client.Players;
using Game.Core.Lobby;
using Game.Core.Match;
using Game.Network.Players;
using Game.Network.Session;
using UnityEngine;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>
    /// 결과 화면 동안 플레이어를 유치장 무대로 옮긴다. 탈출한 사람은 철창 앞에서 철창을 보고,
    /// 체포된 사람은 철창 안에서 카메라를 본다. 실제 아바타를 옮기므로 철창·벽 콜라이더가
    /// 그대로 가두고, 걸어 다니는 모습이 고정 카메라에 보인다.
    /// </summary>
    /// <remarks>
    /// Only the authority moves anyone: it teleports each avatar through the
    /// same path the hiding phase uses, and Fusion carries the new positions to
    /// every peer. Clients just switch to the stage camera and drop the text
    /// screen's opaque backdrop. Movement stays free on purpose, so being locked
    /// in is something the loser can feel; only item interaction is blocked.
    /// </remarks>
    public sealed class EndingStagePresenter : IStartable, ITickable, IDisposable
    {
        private readonly NetworkResultLobbyReturnController result;
        private readonly NetworkRunnerService network;
        private readonly RoomBrowserSystem room;
        private readonly EndingStage stage;
        private readonly IResultView view;
        private bool backdropHidden;
        private PlayerInteractor lockedInteractor;
        private ItemPlacementController lockedPlacement;
        private PlayerMovement stagedMovement;
        private PlayerCameraController bodyShownRig;
        private readonly HashSet<int> itemHiddenFor = new();
        private readonly List<Renderer> hiddenItemRenderers = new();

        public EndingStagePresenter(
            NetworkResultLobbyReturnController result,
            NetworkRunnerService network,
            RoomBrowserSystem room,
            EndingStage stage,
            IResultView view)
        {
            this.result = result ?? throw new ArgumentNullException(nameof(result));
            this.network = network ?? throw new ArgumentNullException(nameof(network));
            this.room = room ?? throw new ArgumentNullException(nameof(room));
            this.stage = stage ?? throw new ArgumentNullException(nameof(stage));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
        }

        public void Start()
        {
            if (!stage.IsWired)
            {
                Debug.LogWarning("[Ending] EndingStage is not wired; the result screen keeps the text-only view.", stage);
                return;
            }

            // The text screen was built to sit over the match with an opaque
            // backdrop. With a stage behind it, the backdrop would hide the stage.
            view.SetBackdropVisible(false);
            backdropHidden = true;
            stage.ShowCamera();
            LockLocalInteraction();
            ShowLocalBody();
            HideCarriedItems();
        }

        public void Tick()
        {
            if (!stage.IsWired) return;
            if (lockedInteractor == null) LockLocalInteraction();
            if (bodyShownRig == null) ShowLocalBody();
            HideCarriedItems();
        }

        /// <remarks>
        /// In first person the rig hides the player's own body (shadows only).
        /// The stage camera is a different camera looking at that body, so the
        /// winner would show up as a floating item. Force the body on for the
        /// duration and hand the choice back afterwards.
        /// </remarks>
        private void ShowLocalBody()
        {
            var rig = UnityEngine.Object.FindFirstObjectByType<PlayerCameraController>(FindObjectsInactive.Include);
            if (rig == null) return;
            rig.SetBodyVisibleOverride(true);
            bodyShownRig = rig;
        }

        public void Dispose()
        {
            stage.HideCamera();
            if (backdropHidden) view.SetBackdropVisible(true);
            UnlockLocalInteraction();
            if (bodyShownRig != null) bodyShownRig.SetBodyVisibleOverride(false);
            bodyShownRig = null;
            foreach (var renderer in hiddenItemRenderers)
                if (renderer != null) renderer.forceRenderingOff = false;
            hiddenItemRenderers.Clear();
            itemHiddenFor.Clear();
        }

        /// <remarks>
        /// 결과 무대에서는 승패와 관계없이 소지 물건을 숨긴다. 매치의 소유권은 유지하고
        /// 표시만 복원하므로 결과·하이라이트 전환이 게임 판정을 변경하지 않는다.
        /// 하이라이트 복원이 늦게 도착해도 매 틱 숨김 상태를 유지한다.
        /// </remarks>
        private void HideCarriedItems()
        {
            foreach (var renderer in hiddenItemRenderers)
                if (renderer != null) renderer.forceRenderingOff = true;
            if (!result.HasMatchResult) return;
            var participants = room.MatchParticipants.CurrentValue;
            if (participants == null || participants.Count == 0) return;
            if (itemHiddenFor.Count >= participants.Count) return;

            var placements = EndingStageLayout.Assign(
                participants,
                result.LastWinnerPlayerIndices,
                stage.EscapeSlotCount,
                stage.ArrestSlotCount);

            Dictionary<string, PlayerAvatar> avatars = null;
            foreach (var placement in placements)
            {
                if (itemHiddenFor.Contains(placement.PlayerIndex)) continue;
                avatars ??= FindAvatars();
                if (!avatars.TryGetValue(placement.PlayerId, out var avatar)) continue;
                var interactor = avatar.GetComponent<PlayerInteractor>();
                var item = interactor != null ? interactor.CarriedItem : null;
                if (item == null)
                {
                    // 아직 들고 있는 물건이 동기화되지 않았을 수 있으니 다음 틱에 다시 본다.
                    // 결과가 확정된 뒤에는 새로 집을 수 없으므로 잠시만 기다리면 된다.
                    continue;
                }

                foreach (var renderer in item.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.forceRenderingOff = true;
                    hiddenItemRenderers.Add(renderer);
                }
                // Hidden geometry alone leaves the interactor choosing throw instead of attack.
                interactor.ForgetConfirmedItem(item);
                itemHiddenFor.Add(placement.PlayerIndex);
            }
        }

        private static Dictionary<string, PlayerAvatar> FindAvatars()
        {
            var map = new Dictionary<string, PlayerAvatar>(StringComparer.Ordinal);
            foreach (var avatar in UnityEngine.Object.FindObjectsByType<PlayerAvatar>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                var id = avatar.PlayerId;
                if (!string.IsNullOrEmpty(id)) map.TryAdd(id, avatar);
            }
            return map;
        }

        /// <remarks>
        /// Picking things up or aiming at objects makes no sense on the stage,
        /// and a stray F would grab the match's items from a distance. Walking
        /// is left alone so the cell actually holds the player.
        /// </remarks>
        private void LockLocalInteraction()
        {
            foreach (var avatar in UnityEngine.Object.FindObjectsByType<PlayerAvatar>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!avatar.IsOwner) continue;
                lockedInteractor = avatar.GetComponent<PlayerInteractor>();
                if (lockedInteractor != null) lockedInteractor.IsInputLocked = true;
                // 던지기는 상호작용 잠금에 포함된다. 배치 모드(우클릭)는 별도 컨트롤러라 따로 막는다.
                lockedPlacement = avatar.GetComponent<ItemPlacementController>();
                if (lockedPlacement != null) lockedPlacement.IsInputLocked = true;

                // WASD는 고정 무대 카메라 기준(W = 화면 안쪽), 몸은 이동 방향을 향한다.
                stagedMovement = avatar.GetComponent<PlayerMovement>();
                if (stagedMovement != null && stage.StageCamera != null)
                {
                    stagedMovement.SetStageControl(stage.StageCamera.transform);
                }
                return;
            }
        }

        private void UnlockLocalInteraction()
        {
            if (lockedInteractor != null) lockedInteractor.IsInputLocked = false;
            lockedInteractor = null;
            if (lockedPlacement != null) lockedPlacement.IsInputLocked = false;
            lockedPlacement = null;
            if (stagedMovement != null) stagedMovement.ClearStageControl();
            stagedMovement = null;
        }
    }

    // Scene-scoped authority work must run even when the dedicated server skips UI.
    public sealed class EndingStagePlacementController : ITickable
    {
        private readonly NetworkResultLobbyReturnController result;
        private readonly NetworkRunnerService network;
        private readonly RoomBrowserSystem room;
        private readonly EndingStage stage;
        private readonly HashSet<int> teleported = new();
        private bool staged;

        public EndingStagePlacementController(NetworkResultLobbyReturnController result,
            NetworkRunnerService network, RoomBrowserSystem room, EndingStage stage)
        {
            this.result = result;
            this.network = network;
            this.room = room;
            this.stage = stage;
        }

        public void Tick()
        {
            if (staged || !network.IsServer || !network.IsResultSceneLoaded ||
                !network.IsSceneLoadComplete || !result.HasMatchResult) return;
            var participants = room.MatchParticipants.CurrentValue;
            if (participants == null || participants.Count == 0) return;

            var placements = EndingStageLayout.Assign(
                participants,
                result.LastWinnerPlayerIndices,
                stage.EscapeSlotCount,
                stage.ArrestSlotCount);

            foreach (var placement in placements)
            {
                if (teleported.Contains(placement.PlayerIndex)) continue;
                var slot = stage.Slot(placement.Escaped, placement.Slot);
                if (slot == null) continue;
                if (network.TryTeleportPlayer(placement.PlayerIndex, new Pose(slot.position, slot.rotation)))
                {
                    teleported.Add(placement.PlayerIndex);
                }
            }

            staged = teleported.Count >= placements.Count;
            if (staged)
            {
                Debug.Log($"[Ending] Staged {teleported.Count} of {participants.Count} players " +
                          $"(escaped {CountEscaped(placements)}).");
            }
        }

        private static int CountEscaped(IReadOnlyList<EndingStagePlacement> placements)
        {
            var count = 0;
            foreach (var p in placements) if (p.Escaped) count++;
            return count;
        }

    }
}
