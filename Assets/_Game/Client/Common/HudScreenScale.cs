using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Common
{
    /// <summary>
    /// One scale for lobby and in-game HUD: designed at 1920×1080, then grown
    /// or shrunk with the window so a 1280×720 and a 2560×1440 stay the same
    /// proportions.
    /// </summary>
    public static class HudScreenScale
    {
        public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        /// <summary>
        /// How large the HUD is at the design resolution. 1 is the mock-up
        /// size, which reads too large on a full 1920×1080 view.
        /// </summary>
        public const float OverallSize = 0.8f;

        /// <summary>
        /// Halfway between width and height. Every shipped resolution is 16:9,
        /// so both sides agree; a free-aspect Game view still keeps the HUD
        /// from stretching.
        /// </summary>
        public const float WidthOrHeight = 0.5f;

        public static Vector2 ScaledReference => ReferenceResolution / OverallSize;

        public static void Apply(CanvasScaler scaler)
        {
            if (scaler == null)
            {
                return;
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ScaledReference;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = WidthOrHeight;
        }

        public static CanvasScaler Ensure(GameObject root)
        {
            if (root == null)
            {
                return null;
            }

            var scaler = root.GetComponent<CanvasScaler>() ?? root.AddComponent<CanvasScaler>();
            Apply(scaler);
            return scaler;
        }

        public static CanvasScaler EnsureOn(Canvas canvas)
        {
            return canvas == null ? null : Ensure(canvas.gameObject);
        }
    }
}
