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
        private const float MissingItemGraceSeconds = 3f;

        /// <summary>플레이어별로 손에 붙여 둔 전시용 복제본 (S15P21D205-1087).</summary>
        private readonly Dictionary<int, GameObject> shownItems = new();

        /// <summary>들기 자세를 못 박아 둔 아바타. 무대를 내려올 때 돌려준다 (S15P21D205-1087).</summary>
        private readonly HashSet<PlayerAnimationDriver> carryForced = new();

        /// <summary>빈손으로 남은 이유를 이미 알린 플레이어 (S15P21D205-1087).</summary>
        private readonly HashSet<int> missingItemReported = new();

        /// <summary>
        /// 이 시각 전의 실패는 알리지 않는다. 아바타·물건 상태가 도착하기 전에도 한 번은 돌기 때문에,
        /// 곧 성공할 시도를 경고로 남기면 진짜 빈손과 구분할 수 없다.
        /// </summary>
        private float missingItemReportAfter;

        private MatchChatView chat;
        private MatchChatBubbleView bubbles;
        private bool chatChromeApplied;

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
            missingItemReportAfter = Time.unscaledTime + MissingItemGraceSeconds;
            stage.ShowCamera();
            LockLocalInteraction();
            ShowLocalBody();
            HideCarriedItems();
            ShowOwnItems();
            ShowChat();
        }

        public void Tick()
        {
            if (!stage.IsWired) return;
            if (lockedInteractor == null) LockLocalInteraction();
            if (bodyShownRig == null) ShowLocalBody();
            HideCarriedItems();
            ShowOwnItems();
            ShowChat();
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

            foreach (var copy in shownItems.Values)
                if (copy != null) UnityEngine.Object.Destroy(copy);
            shownItems.Clear();

            // 들기 자세는 무대 위에서만 못 박은 것이다. 로비로 돌아가는 아바타는 제 상태를 따른다.
            foreach (var driver in carryForced)
                if (driver != null) driver.SetCarryOverride(false);
            carryForced.Clear();
            missingItemReported.Clear();

            // 채팅과 말풍선은 매치 씬의 것이라 결과 씬보다 오래 산다. 빌려 쓴 상태를 돌려준다.
            if (chat != null) chat.SetKeepChromeVisible(false);
            chat = null;
            chatChromeApplied = false;
            if (bubbles != null) bubbles.PinCamera(null);
            bubbles = null;
        }

        /// <summary>
        /// 각자 <b>원래 자기 물건</b>을 손에 들려 준다 (S15P21D205-1087).
        /// </summary>
        /// <remarks>
        /// 들려 주는 것은 복제본이고 원본은 <see cref="HideCarriedItems"/> 가 숨긴 그대로 둔다.
        /// 결과가 확정된 뒤에 진짜 물건을 옮기면 권위가 쥐고 있는 소유·물리 상태를 건드리게 되고,
        /// 하이라이트 복원과 분석 기록이 그 위에서 돈다. 여기서 바꾸는 것은 보이는 것뿐이다.
        /// <para>
        /// 누구 물건인지는 <see cref="NetworkRunnerService.LatestPlayerItemStatuses"/> 가 소유자
        /// 인덱스 순으로 들고 있다. <see cref="CarryableItem.AssignToPlayer"/> 는 자기 것만 표시하므로
        /// 남의 물건을 찾는 데는 쓸 수 없다.
        /// </para>
        /// <para>
        /// 파괴된 물건도 그대로 들려 준다. 유치장에 선 사람이 빈손이면 "무엇을 잃었는지"가 화면에서
        /// 사라진다.
        /// </para>
        /// </remarks>
        private void ShowOwnItems()
        {
            if (!result.HasMatchResult) return;
            var participants = room.MatchParticipants.CurrentValue;
            if (participants == null || participants.Count == 0) return;
            if (shownItems.Count >= participants.Count) return;

            var statuses = network.LatestPlayerItemStatuses;
            if (statuses == null || statuses.Count == 0) return;

            var placements = EndingStageLayout.Assign(
                participants,
                result.LastWinnerPlayerIndices,
                stage.EscapeSlotCount,
                stage.ArrestSlotCount);

            Dictionary<string, PlayerAvatar> avatars = null;
            Dictionary<string, CarryableItem> items = null;
            DestroyedItemArchive archive = null;
            var archiveSearched = false;
            foreach (var placement in placements)
            {
                if (shownItems.ContainsKey(placement.PlayerIndex)) continue;
                if (placement.PlayerIndex < 0 || placement.PlayerIndex >= statuses.Count)
                {
                    ReportMissingItem(placement.PlayerIndex, "물건 상태에 자리가 없다");
                    continue;
                }

                var itemId = statuses[placement.PlayerIndex].ItemId;
                if (string.IsNullOrEmpty(itemId))
                {
                    ReportMissingItem(placement.PlayerIndex, "배정된 물건 id가 비어 있다");
                    continue;
                }

                avatars ??= FindAvatars();
                if (!avatars.TryGetValue(placement.PlayerId, out var avatar))
                {
                    ReportMissingItem(placement.PlayerIndex, $"아바타를 찾지 못했다 (item={itemId})");
                    continue;
                }

                var holdPoint = avatar.GetComponent<PlayerInteractor>()?.HoldPoint;
                if (holdPoint == null)
                {
                    ReportMissingItem(placement.PlayerIndex, $"HoldPoint가 없다 (item={itemId})");
                    continue;
                }

                items ??= FindItems();
                GameObject sourceObject = null;
                if (items.TryGetValue(itemId, out var source) && source != null)
                {
                    sourceObject = source.gameObject;
                }
                else
                {
                    // 파괴된 물건은 씬에서 지워졌으므로 파괴 직전에 맡겨 둔 겉모습을 쓴다.
                    if (!archiveSearched)
                    {
                        archiveSearched = true;
                        archive = UnityEngine.Object.FindFirstObjectByType<DestroyedItemArchive>(
                            FindObjectsInactive.Include);
                    }

                    if (archive != null && archive.TryGetVisual(itemId, out var kept)) sourceObject = kept;
                }

                if (sourceObject == null)
                {
                    ReportMissingItem(placement.PlayerIndex,
                        $"씬에도 파괴 보관소에도 없다 (item={itemId}, 파괴됨={statuses[placement.PlayerIndex].IsDestroyed})");
                    continue;
                }

                var copy = ItemDisplayCopy.Create(sourceObject, holdPoint, itemId + " (Ending)");
                if (copy == null) continue;
                copy.SetActive(true);
                shownItems[placement.PlayerIndex] = copy;
                ShowCarryPose(avatar);
            }
        }

        /// <summary>
        /// 손에 든 것이 없어도 들기 자세를 유지시킨다 (S15P21D205-1087).
        /// </summary>
        /// <remarks>
        /// 복제본은 애니메이션이 아는 물건이 아니다. 로컬은 진짜 물건을 잊게 만들었고
        /// (<see cref="HideCarriedItems"/>), 원격은 결과 씬에서 들기 상태를 끈 채로 온다.
        /// 그대로 두면 물건은 손에 있는데 팔만 내려간다.
        /// </remarks>
        private void ShowCarryPose(PlayerAvatar avatar)
        {
            var driver = avatar.GetComponent<PlayerAnimationDriver>();
            if (driver == null || !carryForced.Add(driver)) return;
            driver.SetCarryOverride(true);
        }

        /// <summary>
        /// 누구를 왜 빈손으로 세웠는지 플레이어당 한 번 남긴다 (S15P21D205-1087).
        /// </summary>
        /// <remarks>
        /// 손에 붙이는 일은 매 틱 다시 시도하므로, 경고 없이 두면 "그 사람만 빈손"인 판이
        /// 무엇 때문이었는지 나중에 알 길이 없다. 한 번만 남겨 로그를 채우지 않는다.
        /// </remarks>
        private void ReportMissingItem(int playerIndex, string reason)
        {
            if (Time.unscaledTime < missingItemReportAfter) return;
            if (!missingItemReported.Add(playerIndex)) return;
            Debug.LogWarning($"[Ending] Player {playerIndex} stands empty-handed: {reason}.");
        }

        private static Dictionary<string, CarryableItem> FindItems()
        {
            var map = new Dictionary<string, CarryableItem>(StringComparer.Ordinal);
            // 파괴된 물건은 꺼져 있을 수 있는데 그것도 들려 주므로 꺼진 것까지 찾는다.
            foreach (var item in UnityEngine.Object.FindObjectsByType<CarryableItem>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var id = item.ObjectId;
                if (!string.IsNullOrEmpty(id)) map.TryAdd(id, item);
            }
            return map;
        }

        /// <summary>
        /// 유치장에서도 이야기할 수 있게 한다 (S15P21D205-1087).
        /// </summary>
        /// <remarks>
        /// 채팅은 매치 씬의 것이고 그 모드는 단계 스냅샷이 정한다. 결과 화면의 주인은 이 무대이므로
        /// 여기서 직접 켜 둔다 - 하이라이트가 화이트리스트 밖 그래픽을 전부 껐다 켜는 길을 지나오기
        /// 때문에, 스냅샷이 한 번 더 오기를 기다리지 않는다.
        /// <para>
        /// 말풍선은 아바타 머리 위의 월드 캔버스라 무대 카메라를 보게 못 박는다.
        /// </para>
        /// </remarks>
        private void ShowChat()
        {
            chat ??= UnityEngine.Object.FindFirstObjectByType<MatchChatView>(FindObjectsInactive.Include);
            if (chat != null)
            {
                var wasHidden = !chat.gameObject.activeSelf;
                chat.SetMode(MatchChatHudMode.Full);

                // 크롬은 한 번만 켠다. 켜는 쪽이 레이아웃을 다시 그리므로 매 틱 부를 일이 아니다.
                // 누가 채팅을 껐다 켜면 그때 다시 걸어 준다.
                if (!chatChromeApplied || wasHidden)
                {
                    chat.SetKeepChromeVisible(true);
                    chatChromeApplied = true;
                }
            }

            bubbles ??= UnityEngine.Object.FindFirstObjectByType<MatchChatBubbleView>(
                FindObjectsInactive.Include);
            if (bubbles != null && stage.StageCamera != null)
            {
                bubbles.PinCamera(stage.StageCamera);
            }
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
