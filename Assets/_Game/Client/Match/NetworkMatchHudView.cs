using Game.Core.Match;
using System.Collections.Generic;
using Game.Client.Interactions;
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
        void SetRemainingDestructionUses(int remainingUses);
        void ShowDestructionNotice(string message);
        void HideDestructionNotice();
        void SetShredderMarker(Vector2 screenPosition, bool visible);
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

        [SerializeField]
        private GameObject destructionNoticeRoot;

        [SerializeField]
        private TMP_Text destructionNoticeText;

        [SerializeField]
        private RectTransform shredderMarker;

        [SerializeField]
        private Canvas rootCanvas;

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
                    (destructionNoticeRoot != null && graphic.transform.IsChildOf(destructionNoticeRoot.transform)))
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

        private void RefreshPlayerStatus()
        {
            if (assignedItemText == null)
            {
                return;
            }

            var hasItem = assignedItemDisplayName != null;
            var hasUses = remainingDestructionUses >= 0;
            assignedItemText.gameObject.SetActive(hasItem || hasUses);
            assignedItemText.text = hasItem && hasUses
                ? $"내 물건: {assignedItemDisplayName}\n파쇄기: {remainingDestructionUses}회"
                : hasItem
                    ? $"내 물건: {assignedItemDisplayName}"
                    : hasUses
                        ? $"파쇄기: {remainingDestructionUses}회"
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
