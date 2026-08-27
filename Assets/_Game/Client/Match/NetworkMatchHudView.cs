using Game.Core.Match;
using TMPro;
using UnityEngine;

namespace Game.Client.Match
{
    public interface INetworkMatchHudView
    {
        /// <inheritdoc cref="IMatchPhaseView.SetPhase"/>
        void SetPhase(MatchPhase phase, string hidingPlayerName);
        void SetRemainingSeconds(double remainingSeconds);
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

        private void Awake()
        {
            if (rootCanvas == null)
            {
                rootCanvas = GetComponentInParent<Canvas>();
            }

            HideDestructionNotice();
            SetShredderMarker(default, false);
            SetAssignedItem(null);
        }

        public void SetPhase(MatchPhase phase, string hidingPlayerName) =>
            phaseView?.SetPhase(phase, hidingPlayerName);

        public void SetRemainingSeconds(double remainingSeconds) =>
            timerView?.SetRemainingSeconds(remainingSeconds);

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
