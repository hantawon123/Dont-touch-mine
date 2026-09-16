using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Client.Settings
{
    /// <summary>Preserves wheel distance while easing toward its destination.</summary>
    public sealed class SettingsScrollRect : ScrollRect
    {
        private RectTransform wheelContent;
        private Vector2 wheelTarget;
        private Vector2 lastPosition;
        private bool smoothing;
        private bool dragging;

        public override void OnScroll(PointerEventData eventData)
        {
            if (!IsActive() || content == null || dragging) return;

            if (wheelContent != content || content.anchoredPosition != lastPosition)
                StopMovement();

            var start = content.anchoredPosition;
            // Let ScrollRect calculate the normal wheel distance and clamp at edges.
            // Repeated ticks accumulate at the destination rather than losing distance.
            if (smoothing) content.anchoredPosition = wheelTarget;
            base.OnScroll(eventData);
            wheelTarget = content.anchoredPosition;
            content.anchoredPosition = start;
            lastPosition = start;
            wheelContent = content;
            smoothing = true;
            base.StopMovement();
        }

        protected override void LateUpdate()
        {
            if (smoothing)
            {
                // A scrollbar interaction or a page reset takes priority over the wheel.
                if (content == null || content != wheelContent ||
                    content.anchoredPosition != lastPosition)
                {
                    StopMovement();
                }
                else
                {
                    var blend = 1f - Mathf.Exp(-Time.unscaledDeltaTime /
                        SettingsStyle.Scroll.SmoothingTime);
                    var next = Vector2.Lerp(content.anchoredPosition, wheelTarget, blend);
                    if ((next - wheelTarget).sqrMagnitude < 0.01f)
                    {
                        next = wheelTarget;
                        smoothing = false;
                    }
                    SetContentAnchoredPosition(next);
                    base.StopMovement();
                }
            }

            base.LateUpdate();
            if (content != null) lastPosition = content.anchoredPosition;
        }

        public override void StopMovement()
        {
            smoothing = false;
            wheelContent = null;
            base.StopMovement();
        }

        public override void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                StopMovement();
                dragging = true;
            }
            base.OnBeginDrag(eventData);
        }

        public override void OnEndDrag(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) dragging = false;
            base.OnEndDrag(eventData);
        }

        protected override void OnDisable()
        {
            StopMovement();
            dragging = false;
            base.OnDisable();
        }
    }
}
