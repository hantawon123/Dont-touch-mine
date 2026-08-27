using Game.Core.Match;
using TMPro;
using UnityEngine;

namespace Game.Client.Match
{
    public interface INetworkMatchHudView
    {
        void SetPhase(MatchPhase phase);
        void SetRemainingSeconds(double remainingSeconds);
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
        private GameObject destructionNoticeRoot;

        [SerializeField]
        private TMP_Text destructionNoticeText;

        [SerializeField]
        private RectTransform shredderMarker;

        [SerializeField]
        private Canvas rootCanvas;

        private void Awake()
        {
            if (rootCanvas == null)
            {
                rootCanvas = GetComponentInParent<Canvas>();
            }

            HideDestructionNotice();
            SetShredderMarker(default, false);
        }

        public void SetPhase(MatchPhase phase) => phaseView?.SetPhase(phase);

        public void SetRemainingSeconds(double remainingSeconds) =>
            timerView?.SetRemainingSeconds(remainingSeconds);

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
    }
}
