using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Client.Settings
{
    /// <summary>
    /// Paints one tab for the three states it has: picked, under the pointer,
    /// and neither.
    /// </summary>
    /// <remarks>
    /// A tab answers a pointer with its plate and its lettering together, and
    /// <see cref="Home.HomeHoverHighlight"/> can only repaint images — so the
    /// pair is handled here rather than by tinting the plate and leaving the
    /// word to some second mechanism.
    /// <para>
    /// Which tab is picked is the presenter's to decide, so this holds that as
    /// a flag and lets it win over a hover: the tab the player is already on
    /// should not change under the pointer.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    internal sealed class SettingsTabHover : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        private Image plate;
        private TMP_Text label;
        private bool isSelected;
        private bool isHovered;

        public void Bind(Image tabPlate, TMP_Text tabLabel)
        {
            plate = tabPlate;
            label = tabLabel;
            Apply();
        }

        public void SetSelected(bool selected)
        {
            isSelected = selected;
            Apply();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;
            Apply();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            Apply();
        }

        /// <summary>
        /// A tab hidden while the pointer is on it must not come back lit: the
        /// exit is never delivered to a disabled object.
        /// </summary>
        private void OnDisable()
        {
            isHovered = false;
            Apply();
        }

        private void Apply()
        {
            if (plate != null)
            {
                plate.color = isSelected
                    ? SettingsStyle.Palette.TabSelectedFill
                    : isHovered
                        ? SettingsStyle.Palette.HoverFill
                        : SettingsStyle.Palette.IdleFill;
            }

            if (label != null)
            {
                label.color = isSelected
                    ? SettingsStyle.Palette.TabSelectedLabel
                    : isHovered
                        ? SettingsStyle.Palette.TabHoverLabel
                        : SettingsStyle.Palette.TabIdleLabel;
            }
        }
    }
}
