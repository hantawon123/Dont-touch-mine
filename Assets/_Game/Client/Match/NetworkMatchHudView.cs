using System;
using System.Collections.Generic;
using Game.Client.Home;
using Game.Client.Interactions;
using Game.Core.Lobby;
using Game.Core.Match;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Match
{
    public interface INetworkMatchHudView
    {
        /// <inheritdoc cref="IMatchPhaseView.SetPhase"/>
        void SetPhase(MatchPhase phase, string hidingPlayerName);
        void SetRemainingSeconds(double remainingSeconds);
        void SetEndCountdown(double remainingSeconds);
        void SetEndResult(string headline, string subtitle);
        void SetHighlightHud(bool visible, string subtitle, IReadOnlyList<float> barFills);
        void SetAssignedItem(string displayName);
        void SetPlayerItemStatuses(IReadOnlyList<PlayerItemStatusSnapshot> statuses);
        void SetDestroyedItems(
            int playerCount,
            IReadOnlyList<PlayerItemStatusSnapshot> statuses,
            string localItemId = null,
            IReadOnlyList<string> destroyedItemIdsInOrder = null);
        void SetRemainingDestructionUses(int remainingUses);
        void ShowDestructionNotice(string message);
        void HideDestructionNotice();
        void SetShredderMarker(Vector2 screenPosition, bool visible);
        void ShowHidingIntro(string itemDisplayName, string itemId);
        void HideHidingIntro();
        void ShowSearchingIntro(string itemDisplayName);
        void HideSearchingIntro();
        bool IsPhaseIntroPresented(MatchPhase phase);
        void ShowHidingTurnStart(double remainingSeconds, string bannerText = null);
        void HideHidingTurnStart();
        void SetHidingTurnStartSeconds(double remainingSeconds);
        void ShowHidingActiveHud(double remainingSeconds, bool showTopPrompt, bool showCompleteGuide);
        void HideHidingActiveHud();
        void SetHidingActiveHudSeconds(double remainingSeconds);
        void ShowHidingWaitHud(
            int completedCount,
            int totalCount,
            string hidingPlayerName,
            IReadOnlyList<HidingWaitPlayer> players,
            bool showNextTurnNotice,
            double remainingSeconds,
            double turnDurationSeconds);
        void HideHidingWaitHud();
        void ShowVitals(float stamina, float maxStamina, int hits, int maxHits, bool exhausted);
        void HideVitals();
        void SetTopHudVisible(bool visible);
        void SetMatchChatVisible(bool visible);
        void SetMatchChatMode(MatchChatHudMode mode);
        void SetPlayerStatusVisible(bool visible);
    }

    /// <summary>
    /// Scene-owned references for the in-game HUD. Layout remains a scene/UI concern;
    /// the presenter only sends display values here.
    /// </summary>
    public sealed class NetworkMatchHudView : MonoBehaviour, INetworkMatchHudView
    {
        public const float DestructionUsesFontSize = 30f;
        [SerializeField]
        private MatchPhaseView phaseView;

        [SerializeField]
        private MatchTimerView timerView;

        [SerializeField]
        private HighlightHudView highlightHudView;

        [SerializeField]
        private TMP_Text assignedItemText;

        [SerializeField, Tooltip("Optional public item status text. Leave empty until the scene UI is laid out.")]
        private TMP_Text playerItemStatusesText;

        [SerializeField]
        private GameObject destructionNoticeRoot;

        [SerializeField]
        private TMP_Text destructionNoticeText;

        [SerializeField]
        private RectTransform shredderMarker;

        [SerializeField]
        private Canvas rootCanvas;

        [SerializeField]
        private HidingIntroView hidingIntroView;

        [SerializeField]
        private SearchingIntroView searchingIntroView;

        [SerializeField]
        private HidingTurnStartView hidingTurnStartView;

        [SerializeField]
        private HidingActiveHudView hidingActiveHudView;

        [SerializeField]
        private HidingWaitHudView hidingWaitHudView;

        [SerializeField]
        private KeySettingGuideView keySettingGuideView;

        [SerializeField]
        private MatchVitalsHudView vitalsHudView;

        [SerializeField]
        private DestroyedItemsHudView destroyedItemsHudView;

        [SerializeField]
        private MatchUrgencyBorderView urgencyBorderView;

        private MatchPhase currentPhase;
        private double lastRemainingSeconds = 999d;
        private int remainingDestructionUses = -1;
        private int destroyedItemPlayerCount;
        private IReadOnlyList<PlayerItemStatusSnapshot> destroyedItemStatuses =
            Array.Empty<PlayerItemStatusSnapshot>();
        private string destroyedItemLocalId;
        private IReadOnlyList<string> destroyedItemOrder = Array.Empty<string>();
        private bool playerStatusVisible = true;
        private bool highlightOnly;
        private bool hidingPresentation, searchingPresentation;
        public bool HasEssentialPresentation => hidingPresentation || searchingPresentation || highlightOnly || showEndCountdown;
        private bool showEndCountdown;
        private readonly Dictionary<Graphic, bool> hiddenGraphics = new();
        private readonly Dictionary<PlayerInteractor, bool> hiddenCrosshairs = new();

        private void Awake()
        {
            if (rootCanvas == null)
            {
                rootCanvas = GetComponentInParent<Canvas>();
            }

            HideDestructionNotice();
            SetShredderMarker(default, false);
            SetHighlightHud(false, null, Array.Empty<float>());
            SetAssignedItem(null);
            SetPlayerItemStatuses(Array.Empty<PlayerItemStatusSnapshot>());
            EnsureHidingIntro();
            HideHidingIntro();
            EnsureSearchingIntro();
            HideSearchingIntro();
            EnsureHidingTurnStart();
            HideHidingTurnStart();
            EnsureHidingActiveHud();
            HideHidingActiveHud();
            EnsureHidingWaitHud();
            HideHidingWaitHud();
            EnsureVitalsHud();
            HideVitals();
            EnsureDestroyedItemsHud();
            destroyedItemsHudView?.Hide();
            EnsureUrgencyBorder();
            urgencyBorderView?.Hide();
            EnsureDestructionUsesText();
            EnsureHighlightHud();
            HideVoiceButton();
            RefreshKeyGuide(MatchPhase.Waiting);
        }

        public void SetPhase(MatchPhase phase, string hidingPlayerName)
        {
            currentPhase = phase;
            SetHighlightOnly(phase == MatchPhase.Highlight);
            if (phase != MatchPhase.Highlight)
            {
                SetHighlightHud(false, null, Array.Empty<float>());
            }

            phaseView?.SetPhase(phase, hidingPlayerName);
            RefreshKeyGuide(phase);
            RefreshUrgencyBorder();
            ApplyDestroyedItems();
        }

        private void SetHighlightOnly(bool value)
        {
            if (highlightOnly == value) return;
            highlightOnly = value;
            if (!value)
            {
                RestoreHud();
                return;
            }
            // Hide presentation components, not HUD objects/presenters. Notices
            // must keep receiving events and updating at their original position.
            foreach (var graphic in FindObjectsByType<Graphic>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (graphic.gameObject.scene != gameObject.scene ||
                    (highlightHudView != null && graphic.transform.IsChildOf(highlightHudView.transform)) ||
                    (destructionNoticeRoot != null && graphic.transform.IsChildOf(destructionNoticeRoot.transform)) ||
                    (hidingIntroView != null && graphic.transform.IsChildOf(hidingIntroView.transform)) ||
                    (searchingIntroView != null && graphic.transform.IsChildOf(searchingIntroView.transform)) ||
                    (hidingTurnStartView != null && graphic.transform.IsChildOf(hidingTurnStartView.transform)) ||
                    (hidingActiveHudView != null && graphic.transform.IsChildOf(hidingActiveHudView.transform)) ||
                    (hidingWaitHudView != null && graphic.transform.IsChildOf(hidingWaitHudView.transform)) ||
                    (vitalsHudView != null && graphic.transform.IsChildOf(vitalsHudView.transform)) ||
                    (keySettingGuideView != null && graphic.transform.IsChildOf(keySettingGuideView.transform)) ||
                    (urgencyBorderView != null && graphic.transform.IsChildOf(urgencyBorderView.transform)))
                    continue;
                hiddenGraphics[graphic] = graphic.enabled;
                graphic.enabled = false;
            }
            foreach (var interactor in FindObjectsByType<PlayerInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                hiddenCrosshairs[interactor] = interactor.PresentationHudVisible;
                interactor.SetHudVisible(false);
            }
        }

        private void LateUpdate()
        {
            if (!highlightOnly) return;
            foreach (var pair in hiddenGraphics)
                if (pair.Key != null)
                    pair.Key.enabled = showEndCountdown && timerView != null &&
                        pair.Key.transform.IsChildOf(timerView.transform) && pair.Value;
        }

        public void SetEndCountdown(double remainingSeconds)
        {
            showEndCountdown = remainingSeconds > 0d;
            if (showEndCountdown)
            {
                timerView?.SetRemainingSeconds(remainingSeconds);
            }
            else
            {
                timerView?.ClearResult();
            }

            timerView?.SetHintVisible(!showEndCountdown);
            LateUpdate();
        }

        public void SetEndResult(string headline, string subtitle)
        {
            showEndCountdown = true;
            timerView?.SetResult(headline, subtitle);
            urgencyBorderView?.Hide();
            LateUpdate();
        }

        private void RestoreHud()
        {
            foreach (var pair in hiddenGraphics)
                if (pair.Key != null) pair.Key.enabled = pair.Value;
            foreach (var pair in hiddenCrosshairs)
                if (pair.Key != null) pair.Key.SetHudVisible(pair.Value);
            hiddenGraphics.Clear();
            hiddenCrosshairs.Clear();
        }

        private void OnDestroy() => RestoreHud();

        public void SetRemainingSeconds(double remainingSeconds)
        {
            lastRemainingSeconds = remainingSeconds;
            timerView?.SetRemainingSeconds(remainingSeconds);
            RefreshUrgencyBorder();
        }

        public void SetHighlightHud(bool visible, string subtitle, IReadOnlyList<float> barFills)
        {
            EnsureHighlightHud();
            if (highlightHudView == null)
            {
                return;
            }

            if (!visible)
            {
                highlightHudView.Hide();
                return;
            }

            highlightHudView.Show(subtitle, barFills ?? Array.Empty<float>());
        }

        public void SetAssignedItem(string displayName)
        {
        }

        public void SetPlayerItemStatuses(IReadOnlyList<PlayerItemStatusSnapshot> statuses)
        {
            if (playerItemStatusesText != null)
            {
                playerItemStatusesText.gameObject.SetActive(false);
            }

            SetDestroyedItems(
                statuses == null ? 0 : statuses.Count,
                statuses,
                destroyedItemLocalId,
                destroyedItemOrder);
        }

        public void SetDestroyedItems(
            int playerCount,
            IReadOnlyList<PlayerItemStatusSnapshot> statuses,
            string localItemId = null,
            IReadOnlyList<string> destroyedItemIdsInOrder = null)
        {
            destroyedItemPlayerCount = playerCount;
            destroyedItemStatuses = statuses ?? Array.Empty<PlayerItemStatusSnapshot>();
            destroyedItemLocalId = localItemId;
            destroyedItemOrder = destroyedItemIdsInOrder ?? Array.Empty<string>();
            ApplyDestroyedItems();
        }

        public void SetRemainingDestructionUses(int remainingUses)
        {
            remainingDestructionUses = remainingUses;
            RefreshPlayerStatus();
        }

        public void ShowDestructionNotice(string message)
        {
            if (destructionNoticeText != null)
            {
                ApplyPaperlogy(destructionNoticeText);
                destructionNoticeText.text = message ?? string.Empty;
            }

            if (destructionNoticeRoot != null)
            {
                destructionNoticeRoot.SetActive(true);
            }
        }

        public void HideDestructionNotice()
        {
            if (destructionNoticeRoot != null)
            {
                destructionNoticeRoot.SetActive(false);
            }
        }

        public void SetShredderMarker(Vector2 screenPosition, bool visible)
        {
            if (shredderMarker == null)
            {
                return;
            }

            shredderMarker.gameObject.SetActive(visible);
            if (!visible)
            {
                return;
            }

            var parent = shredderMarker.parent as RectTransform;
            var camera = rootCanvas != null &&
                         rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? rootCanvas.worldCamera
                : null;

            if (parent != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parent,
                    screenPosition,
                    camera,
                    out var localPoint))
            {
                shredderMarker.anchoredPosition = localPoint;
            }
        }

        public void ShowHidingIntro(string itemDisplayName, string itemId)
        {
            EnsureHidingIntro();
            hidingPresentation = true;
            hidingIntroView?.Show(itemDisplayName, itemId);
        }

        public void HideHidingIntro()
        {
            hidingPresentation = false;
            hidingIntroView?.Hide();
        }

        public void ShowSearchingIntro(string itemDisplayName)
        {
            EnsureSearchingIntro();
            searchingPresentation = true;
            searchingIntroView?.Show(itemDisplayName);
        }

        public void HideSearchingIntro()
        {
            searchingPresentation = false;
            searchingIntroView?.Hide();
        }

        public bool IsPhaseIntroPresented(MatchPhase phase) => phase switch
        {
            MatchPhase.Hiding => hidingIntroView != null && hidingIntroView.IsPresented,
            MatchPhase.Searching => searchingIntroView != null && searchingIntroView.IsPresented,
            _ => false
        };

        public void ShowHidingTurnStart(double remainingSeconds, string bannerText = null)
        {
            EnsureHidingTurnStart();
            SetTopHudVisible(false);
            hidingTurnStartView?.Show(remainingSeconds, bannerText);
        }

        public void HideHidingTurnStart()
        {
            hidingTurnStartView?.Hide();
        }

        public void ShowHidingActiveHud(double remainingSeconds, bool showTopPrompt, bool showCompleteGuide)
        {
            EnsureHidingActiveHud();
            hidingActiveHudView?.Show(remainingSeconds, showTopPrompt, showCompleteGuide);
        }

        public void HideHidingActiveHud()
        {
            hidingActiveHudView?.Hide();
        }

        public void SetHidingActiveHudSeconds(double remainingSeconds)
        {
            hidingActiveHudView?.SetRemainingSeconds(remainingSeconds);
        }

        public void ShowHidingWaitHud(
            int completedCount,
            int totalCount,
            string hidingPlayerName,
            IReadOnlyList<HidingWaitPlayer> players,
            bool showNextTurnNotice,
            double remainingSeconds,
            double turnDurationSeconds)
        {
            EnsureHidingWaitHud();
            hidingWaitHudView?.Show(
                completedCount,
                totalCount,
                hidingPlayerName,
                players,
                showNextTurnNotice,
                remainingSeconds,
                turnDurationSeconds);
        }

        public void HideHidingWaitHud()
        {
            hidingWaitHudView?.Hide();
        }

        public void ShowVitals(float stamina, float maxStamina, int hits, int maxHits, bool exhausted)
        {
            EnsureVitalsHud();
            vitalsHudView?.Show(stamina, maxStamina, hits, maxHits, exhausted);
        }

        public void HideVitals()
        {
            vitalsHudView?.Hide();
        }

        public void SetTopHudVisible(bool visible)
        {
            if (phaseView != null)
            {
                phaseView.gameObject.SetActive(visible);
            }

            if (timerView != null)
            {
                timerView.gameObject.SetActive(visible);
            }
        }

        public void SetHidingTurnStartSeconds(double remainingSeconds)
        {
            hidingTurnStartView?.SetRemainingSeconds(remainingSeconds);
        }

        public void SetMatchChatVisible(bool visible)
        {
            SetMatchChatMode(visible ? MatchChatHudMode.Full : MatchChatHudMode.Hidden);
        }

        public void SetMatchChatMode(MatchChatHudMode mode)
        {
            var chat = GetComponentInParent<Canvas>()?.GetComponentInChildren<MatchChatView>(true);
            if (chat != null)
            {
                chat.SetMode(mode);
            }
        }

        public void SetPlayerStatusVisible(bool visible)
        {
            playerStatusVisible = visible;
            RefreshPlayerStatus();
            ApplyDestroyedItems();
        }

        private void ApplyDestroyedItems()
        {
            EnsureDestroyedItemsHud();
            if (destroyedItemsHudView == null)
            {
                return;
            }

            if (destroyedItemPlayerCount <= 0 || !ShowsDestroyedItems(currentPhase))
            {
                destroyedItemsHudView.Hide();
                return;
            }

            destroyedItemsHudView.Show(
                destroyedItemPlayerCount,
                destroyedItemStatuses,
                destroyedItemLocalId,
                destroyedItemOrder);
        }

        private static bool ShowsDestroyedItems(MatchPhase phase)
        {
            return phase == MatchPhase.Searching;
        }

        private void RefreshUrgencyBorder()
        {
            EnsureUrgencyBorder();
            if (urgencyBorderView == null)
            {
                return;
            }

            if (currentPhase == MatchPhase.Searching && MatchTimerView.IsWarning(lastRemainingSeconds))
            {
                urgencyBorderView.Show();
                return;
            }

            urgencyBorderView.Hide();
        }

        private void EnsureUrgencyBorder()
        {
            if (urgencyBorderView == null)
            {
                urgencyBorderView = GetComponentInChildren<MatchUrgencyBorderView>(true);
            }

            if (urgencyBorderView == null)
            {
                urgencyBorderView = MatchUrgencyBorderView.Create(transform);
            }
        }

        private void EnsureHidingIntro()
        {
            if (hidingIntroView == null)
            {
                hidingIntroView = GetComponentInChildren<HidingIntroView>(true);
            }

            if (hidingIntroView == null)
            {
                hidingIntroView = HidingIntroView.Create(transform);
            }
        }

        private void EnsureSearchingIntro()
        {
            if (searchingIntroView == null)
            {
                searchingIntroView = GetComponentInChildren<SearchingIntroView>(true);
            }

            if (searchingIntroView == null)
            {
                searchingIntroView = SearchingIntroView.Create(transform);
            }
        }

        private void EnsureHidingTurnStart()
        {
            if (hidingTurnStartView == null)
            {
                hidingTurnStartView = GetComponentInChildren<HidingTurnStartView>(true);
            }

            if (hidingTurnStartView == null)
            {
                hidingTurnStartView = HidingTurnStartView.Create(transform);
            }
        }

        private void EnsureHidingActiveHud()
        {
            if (hidingActiveHudView == null)
            {
                hidingActiveHudView = GetComponentInChildren<HidingActiveHudView>(true);
            }

            if (hidingActiveHudView == null)
            {
                hidingActiveHudView = HidingActiveHudView.Create(transform);
            }
        }

        private void EnsureHidingWaitHud()
        {
            if (hidingWaitHudView == null)
            {
                hidingWaitHudView = GetComponentInChildren<HidingWaitHudView>(true);
            }

            if (hidingWaitHudView == null)
            {
                hidingWaitHudView = HidingWaitHudView.Create(transform);
            }
        }

        private void EnsureVitalsHud()
        {
            if (vitalsHudView == null)
            {
                vitalsHudView = GetComponentInChildren<MatchVitalsHudView>(true);
            }

            if (vitalsHudView == null)
            {
                vitalsHudView = MatchVitalsHudView.Create(transform);
            }
        }

        private void EnsureDestroyedItemsHud()
        {
            if (destroyedItemsHudView == null)
            {
                destroyedItemsHudView = GetComponentInChildren<DestroyedItemsHudView>(true);
            }

            if (destroyedItemsHudView == null)
            {
                destroyedItemsHudView = DestroyedItemsHudView.Create(transform);
            }
        }

        private void RefreshKeyGuide(MatchPhase phase)
        {
            EnsureKeySettingGuide();
            keySettingGuideView?.SetVisible(ShowsKeySettingGuide(phase));
        }

        private static bool ShowsKeySettingGuide(MatchPhase phase)
        {
            return phase == MatchPhase.Hiding || phase == MatchPhase.Searching;
        }

        private void EnsureKeySettingGuide()
        {
            if (keySettingGuideView == null)
            {
                keySettingGuideView = GetComponentInChildren<KeySettingGuideView>(true);
            }

            if (keySettingGuideView == null)
            {
                keySettingGuideView = KeySettingGuideView.Ensure(transform);
            }
        }

        private void HideVoiceButton()
        {
            var slot = transform.Find("VoiceButton");
            if (slot != null)
            {
                slot.gameObject.SetActive(false);
            }
        }

        private void EnsureDestructionUsesText()
        {
            if (assignedItemText == null)
            {
                assignedItemText = transform.Find("AssignedItemText")?.GetComponent<TMP_Text>();
            }

            if (assignedItemText == null)
            {
                var gameObject = new GameObject(
                    "AssignedItemText",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(TextMeshProUGUI));
                gameObject.transform.SetParent(transform, false);
                assignedItemText = gameObject.GetComponent<TextMeshProUGUI>();
                assignedItemText.color = Color.white;
                assignedItemText.raycastTarget = false;
            }

            ApplyDestructionUsesStyle(assignedItemText);
        }

        private void EnsureHighlightHud()
        {
            StripLegacyHighlightTitle();
            if (highlightHudView == null)
            {
                highlightHudView = GetComponentInChildren<HighlightHudView>(true);
            }

            if (highlightHudView == null)
            {
                highlightHudView = HighlightHudView.Create(transform);
            }
        }

        private void StripLegacyHighlightTitle()
        {
            var leftover = transform.Find("HighlightTitleText");
            if (leftover != null)
            {
                leftover.gameObject.SetActive(false);
            }
        }

        private static void ApplyPaperlogy(TMP_Text text)
        {
            var font = HomeUiFonts.Apply();
            if (text == null || font == null || text.font == font)
            {
                return;
            }

            text.font = font;
            text.fontSharedMaterial = font.material;
        }

        public static void ApplyDestructionUsesStyle(TMP_Text text)
        {
            if (text == null)
            {
                return;
            }

            text.font = HomeUiFonts.Apply();
            text.fontSize = DestructionUsesFontSize;
            text.alignment = TextAlignmentOptions.TopRight;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-36f, -36f);
            rect.sizeDelta = new Vector2(360f, 48f);
        }

        private void RefreshPlayerStatus()
        {
            EnsureDestructionUsesText();
            if (assignedItemText == null)
            {
                return;
            }

            var hasUses = remainingDestructionUses >= 0;
            var uses = remainingDestructionUses == PlaySettingsDraft.UnlimitedDestructionUses
                ? "무한"
                : $"{remainingDestructionUses}회";
            assignedItemText.gameObject.SetActive(playerStatusVisible && hasUses);
            assignedItemText.text = hasUses ? $"파쇄기: {uses}" : string.Empty;
        }
    }
}
