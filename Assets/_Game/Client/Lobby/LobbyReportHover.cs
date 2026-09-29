using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Client.Lobby
{
    /// <summary>
    /// Lights the participant row on hover. A right-click opens the report
    /// button above it; leaving the row and the button closes it.
    /// </summary>
    /// <remarks>
    /// The button sits above the row with a gap, and Unity fires exit on the
    /// row before enter on the button. Hiding on that exit would take the
    /// button away while the pointer is still travelling to it, so the close
    /// waits a frame — long enough for the button (or the bridge under the
    /// gap) to claim the pointer.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class LobbyReportHover : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerClickHandler
    {
        private static LobbyReportHover open;
        private Image fill;
        private GameObject tooltip;
        private Color hoverColor;
        private int hoverCount;
        private Coroutine hideRoutine;

        public void Bind(Image background, GameObject tooltipRoot, Color hover)
        {
            fill = background;
            tooltip = tooltipRoot;
            hoverColor = hover;
            AttachRelays(tooltipRoot);
            ApplyHover();
            HideTooltip();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hoverCount++;
            CancelHide();
            ApplyHover();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hoverCount = Mathf.Max(0, hoverCount - 1);
            ApplyHover();
            if (hoverCount == 0)
            {
                RequestHideTooltip();
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.button == PointerEventData.InputButton.Right)
            {
                ShowTooltip();
            }
        }

        public void ShowTooltip()
        {
            if (open != null && open != this)
            {
                open.HideTooltip();
            }

            open = this;
            CancelHide();
            if (tooltip != null)
            {
                tooltip.SetActive(true);
            }
        }

        public void HideTooltip()
        {
            CancelHide();
            if (open == this)
            {
                open = null;
            }

            if (tooltip != null)
            {
                tooltip.SetActive(false);
            }
        }

        /// <summary>
        /// Closes the button if the pointer never arrived on it. Tests call
        /// this in place of the next-frame wait.
        /// </summary>
        public void HideIfStillLeft()
        {
            hideRoutine = null;
            if (hoverCount == 0)
            {
                HideTooltip();
            }
        }

        private void OnDisable()
        {
            hoverCount = 0;
            ApplyHover();
            HideTooltip();
        }

        private void RequestHideTooltip()
        {
            if (!isActiveAndEnabled || !Application.isPlaying)
            {
                return;
            }

            if (hideRoutine == null)
            {
                hideRoutine = StartCoroutine(HideTooltipNextFrame());
            }
        }

        private IEnumerator HideTooltipNextFrame()
        {
            yield return null;
            HideIfStillLeft();
        }

        private void CancelHide()
        {
            if (hideRoutine != null)
            {
                StopCoroutine(hideRoutine);
                hideRoutine = null;
            }
        }

        private void ApplyHover()
        {
            if (fill != null)
            {
                fill.color = hoverCount > 0 ? hoverColor : Color.clear;
            }
        }

        private void AttachRelays(GameObject tooltipRoot)
        {
            if (tooltipRoot == null)
            {
                return;
            }

            var graphics = tooltipRoot.GetComponentsInChildren<Graphic>(true);
            for (var index = 0; index < graphics.Length; index++)
            {
                var target = graphics[index].gameObject;
                var relay = target.GetComponent<Relay>();
                if (relay == null)
                {
                    relay = target.AddComponent<Relay>();
                }

                relay.Owner = this;
            }
        }

        private sealed class Relay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            public LobbyReportHover Owner;

            public void OnPointerEnter(PointerEventData eventData) =>
                Owner?.OnPointerEnter(eventData);

            public void OnPointerExit(PointerEventData eventData) =>
                Owner?.OnPointerExit(eventData);
        }
    }
}
