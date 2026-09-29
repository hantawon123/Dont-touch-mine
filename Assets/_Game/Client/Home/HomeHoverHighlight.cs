using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Client.Home
{
    /// <summary>
    /// Repaints a fill on hover, and shows a hairline while the pointer is
    /// over it or while what it opens is on screen.
    /// </summary>
    /// <remarks>
    /// The profile chip and the two icon buttons change two things at once on
    /// hover, and a <see cref="Selectable"/> colour tint only changes one: it
    /// multiplies a single graphic. Rather than tint the fill and leave the
    /// stroke to some second mechanism, both live here.
    /// <para>
    /// Exit is also raised when the object is disabled underneath the pointer,
    /// so a chip hidden while hovered does not come back lit.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class HomeHoverHighlight : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        private Image fill;
        private Image stroke;
        private Color normalColor;
        private Color hoverColor;

        /// <summary>
        /// Whether the pointer is on this. Remembered so that being given new
        /// colours mid-hover paints the state the pointer is actually in.
        /// </summary>
        /// <remarks>
        /// Without it, a control rebound while hovered goes flat until the
        /// pointer leaves and comes back — which is what a key plate does the
        /// moment it stops waiting for a press, with the pointer still on it.
        /// </remarks>
        private bool hovered;

        /// <summary>
        /// Whether what this button opens is on screen right now.
        /// </summary>
        /// <remarks>
        /// The hairline used to mean "the pointer is here", which left an open
        /// panel with nothing pointing back at the button that opened it: the
        /// only way to see which one was open was to go and hover it. It now
        /// means "this is where you are", and hover keeps borrowing it, so a
        /// button that is both reads the same as one that is only open.
        /// <para>
        /// The fill is deliberately left to hover alone. It is the pointer
        /// answering a movement, and holding it lit under an open panel would
        /// make the button look pressed rather than current.
        /// </para>
        /// </remarks>
        private bool selected;

        public void Bind(Image fillImage, Image strokeImage, Color normal, Color hover)
        {
            fill = fillImage;
            stroke = strokeImage;
            normalColor = normal;
            hoverColor = hover;
            Apply();
        }

        /// <summary>
        /// Marks this as the button whose panel is open, or no longer is.
        /// </summary>
        public void SetSelected(bool value)
        {
            selected = value;
            Apply();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = true;
            Apply();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            Apply();
        }

        /// <remarks>
        /// Only the pointer state is dropped. Selection outlives being hidden,
        /// because whether a panel is open is not something this object stops
        /// being told while it is off.
        /// </remarks>
        private void OnDisable()
        {
            hovered = false;
            Apply();
        }

        private void Apply()
        {
            if (fill != null)
            {
                fill.color = hovered ? hoverColor : normalColor;
            }

            if (stroke != null)
            {
                stroke.enabled = hovered || selected;
            }
        }
    }
}
