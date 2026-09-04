using System;
using System.Collections.Generic;
using System.Text;
using Game.Client.Interactions;
using Game.Core.Items;
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
        void SetHighlightTitle(string title);
        void SetAssignedItem(string displayName);
        void SetPlayerItemStatuses(IReadOnlyList<PlayerItemStatusSnapshot> statuses);
        void SetRemainingDestructionUses(int remainingUses);
        void ShowDestructionNotice(string message);
        void HideDestructionNotice();
        void SetShredderMarker(Vector2 screenPosition, bool visible);
        void ShowHidingIntro(string itemDisplayName, string itemId);
        void HideHidingIntro();
    }

    /// <summary>
    /// Scene-owned references for the in-game HUD. Layout remains a scene/UI concern;
    /// the presenter only sends display values here.
    /// </summary>
    public sealed class NetworkMatchHudView : MonoBehaviour, INetworkMatchHudView
    {
        [SerializeField]
        private MatchPhaseView phaseView;

        [SerializeField]
        private MatchTimerView timerView;

        [SerializeField]
        private TMP_Text highlightTitleText;

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
        private HidingTurnStartView hidingTurnStartView;

        private string assignedItemDisplayName;
        private int remainingDestructionUses = -1;
        private bool highlightOnly;
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
            SetHighlightTitle(null);
            SetAssignedItem(null);
            SetPlayerItemStatuses(Array.Empty<PlayerItemStatusSnapshot>());
            EnsureHidingIntro();
            HideHidingIntro();
            EnsureHidingTurnStart();
        }

        public void SetPhase(MatchPhase phase, string hidingPlayerName)
        {
            SetHighlightOnly(phase == MatchPhase.Highlight);
            phaseView?.SetPhase(phase, hidingPlayerName);
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
                    (highlightTitleText != null && graphic.transform.IsChildOf(highlightTitleText.transform)) ||
                    (destructionNoticeRoot != null && graphic.transform.IsChildOf(destructionNoticeRoot.transform)) ||
                    (hidingIntroView != null && graphic.transform.IsChildOf(hidingIntroView.transform)) ||
                    (hidingTurnStartView != null && graphic.transform.IsChildOf(hidingTurnStartView.transform)))
                    continue;
                hiddenGraphics[graphic] = graphic.enabled;
                graphic.enabled = false;
            }
            foreach (var interactor in FindObjectsByType<PlayerInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                hiddenCrosshairs[interactor] = interactor.HudVisible;
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
            if (showEndCountdown) timerView?.SetRemainingSeconds(remainingSeconds);
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

        public void SetRemainingSeconds(double remainingSeconds) =>
            timerView?.SetRemainingSeconds(remainingSeconds);

        public void SetHighlightTitle(string title)
        {
            if (highlightTitleText == null)
            {
                return;
            }

            var visible = !string.IsNullOrWhiteSpace(title);
            highlightTitleText.text = visible ? title.Trim() : string.Empty;
            highlightTitleText.gameObject.SetActive(visible);
        }

        public void SetAssignedItem(string displayName)
        {
            assignedItemDisplayName = string.IsNullOrWhiteSpace(displayName)
                ? null
                : displayName.Trim();
            RefreshPlayerStatus();
        }

        public void SetPlayerItemStatuses(IReadOnlyList<PlayerItemStatusSnapshot> statuses)
        {
            if (playerItemStatusesText == null)
            {
                return;
            }

            if (statuses == null || statuses.Count == 0)
            {
                playerItemStatusesText.text = string.Empty;
                playerItemStatusesText.gameObject.SetActive(false);
                return;
            }

            var builder = new StringBuilder();
            for (var index = 0; index < statuses.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append('\n');
                }

                var status = statuses[index];
                builder.Append(ItemCatalog.DisplayNameOf(status.ItemId));
                builder.Append(": ");
                builder.Append(status.IsDestroyed ? "파괴됨" : "정상");
            }

            playerItemStatusesText.text = builder.ToString();
            playerItemStatusesText.gameObject.SetActive(true);
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
            hidingIntroView?.Show(itemDisplayName, itemId);
        }

        public void HideHidingIntro()
        {
            hidingIntroView?.Hide();
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

        private void RefreshPlayerStatus()
        {
            if (assignedItemText == null)
            {
                return;
            }

            var hasItem = assignedItemDisplayName != null;
            var hasUses = remainingDestructionUses >= 0;
            var uses = remainingDestructionUses == PlaySettingsDraft.UnlimitedDestructionUses
                ? "무한"
                : $"{remainingDestructionUses}회";
            assignedItemText.gameObject.SetActive(hasItem || hasUses);
            assignedItemText.text = hasItem && hasUses
                ? $"내 물건: {assignedItemDisplayName}\n파쇄기: {uses}"
                : hasItem
                    ? $"내 물건: {assignedItemDisplayName}"
                    : hasUses
                        ? $"파쇄기: {uses}"
                        : string.Empty;

            if (hasItem && hasUses)
            {
                var size = assignedItemText.rectTransform.sizeDelta;
                size.y = Mathf.Max(size.y, 96f);
                assignedItemText.rectTransform.sizeDelta = size;
            }
        }
    }
}
