using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Client.Home
{
    /// <summary>
    /// Grows a menu label slightly while the pointer is on it, and settles it
    /// back when the pointer leaves.
    /// </summary>
    /// <remarks>
    /// The colour tint alone told the player which line they were on, but told
    /// them flatly: the menu answered without moving. The size does the same job
    /// with a bit of life in it, over the same eighth of a second the tint fades
    /// in, so the two read as one response rather than two.
    /// <para>
    /// The label's rect is pivoted at its leading edge, so it grows away from
    /// that edge and the column of lines stays aligned. The growth is a
    /// transform scale rather than a larger font: the glyphs are rebuilt on
    /// every font-size change, and a line that re-fits itself each frame ripples
    /// as it grows.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class HomeLabelPop : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        private RectTransform target;
        private float hoverScale = 1f;
        private float seconds = 0.08f;
        private bool hovered;

        /// <summary>Where the growth has got to, between 1 and the hover scale.</summary>
        private float current = 1f;

        public void Bind(RectTransform rect, float scale, float duration)
        {
            target = rect;
            hoverScale = scale;
            seconds = Mathf.Max(duration, 0.0001f);
            current = 1f;
            Apply();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!CanHover())
            {
                return;
            }

            hovered = true;
        }

        public void OnPointerExit(PointerEventData eventData) => hovered = false;

        /// <summary>
        /// Moves one step towards whichever size the pointer asks for. Driven by
        /// <c>Update</c>; public so the easing can be stepped by hand in a test,
        /// which never runs a frame.
        /// </summary>
        public void Advance(float deltaSeconds)
        {
            var wanted = hovered ? hoverScale : 1f;
            if (Mathf.Approximately(current, wanted))
            {
                return;
            }

            // Constant speed rather than a proportional ease, so leaving takes
            // as long as arriving and a fast pass over the menu cannot leave a
            // line stranded part-grown.
            var step = Mathf.Abs(hoverScale - 1f) / seconds * Mathf.Max(deltaSeconds, 0f);
            current = Mathf.MoveTowards(current, wanted, step);
            Apply();
        }

        private bool CanHover()
        {
            var selectable = GetComponent<UnityEngine.UI.Selectable>();
            return selectable == null || selectable.IsInteractable();
        }

        private void Update()
        {
            // Unscaled: Home does not pause, but a menu that stopped responding
            // because something else set the timescale would read as a freeze.
            Advance(Time.unscaledDeltaTime);
        }

        /// <summary>
        /// Drops the pointer and returns the label to its own size at once.
        /// </summary>
        /// <remarks>
        /// Public for the same reason <see cref="Advance"/> is: an edit-mode
        /// test runs no lifecycle callbacks, so <c>OnDisable</c> never fires
        /// there and the behaviour would otherwise go unchecked.
        /// </remarks>
        public void Release()
        {
            hovered = false;
            current = 1f;
            Apply();
        }

        /// <remarks>
        /// A label hidden under the pointer comes back its own size rather than
        /// still grown, since no exit is raised for an object switched off.
        /// </remarks>
        private void OnDisable() => Release();

        private void Apply()
        {
            if (target != null)
            {
                target.localScale = new Vector3(current, current, 1f);
            }
        }
    }
}
