using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Rooms
{
    /// <summary>
    /// The drawing primitives the room browser is built from: rounded shapes and
    /// the two or three lines of setup every rect, image and label needs.
    /// </summary>
    /// <remarks>
    /// The mock-up rounds six different shapes to six different radii, so the
    /// sprites are generated per radius and cached rather than imported. A
    /// generated sprite costs a few kilobytes of memory and no repository space,
    /// and it cannot drift from <see cref="RoomBrowserStyle"/> the way an
    /// exported PNG does.
    /// <para>
    /// Everything here is marked <see cref="HideFlags.HideAndDontSave"/> because
    /// it belongs to the running screen and never to an asset file. That is also
    /// why this screen is built at runtime: a scene that serialised a reference
    /// to one of these would save a pointer to an object that is gone by the
    /// next play.
    /// </para>
    /// </remarks>
    public static class RoomBrowserUi
    {
        private static readonly Dictionary<int, Sprite> RoundedSprites =
            new Dictionary<int, Sprite>();

        private static readonly Dictionary<int, Sprite> OutlineSprites =
            new Dictionary<int, Sprite>();

        private static Sprite circleSprite;

        /// <summary>
        /// A filled rounded rectangle, nine-sliced so one sprite serves both a
        /// 40 pixel code cell and a 1110 pixel panel at the same radius.
        /// </summary>
        public static Sprite Rounded(int radius)
        {
            if (RoundedSprites.TryGetValue(radius, out var cached) && cached != null)
            {
                return cached;
            }

            var sprite = BuildRoundedSprite(radius, 0f);
            RoundedSprites[radius] = sprite;
            return sprite;
        }

        /// <summary>
        /// The border of a rounded rectangle, for the stroke a list row draws.
        /// Kept apart from the fill so a row can change one without the other.
        /// </summary>
        public static Sprite Outline(int radius, float thickness = 1f)
        {
            var key = (radius * 100) + Mathf.RoundToInt(thickness * 10f);
            if (OutlineSprites.TryGetValue(key, out var cached) && cached != null)
            {
                return cached;
            }

            var sprite = BuildRoundedSprite(radius, thickness);
            OutlineSprites[key] = sprite;
            return sprite;
        }

        public static Sprite Circle()
        {
            if (circleSprite != null)
            {
                return circleSprite;
            }

            const int size = 64;
            var texture = NewTexture(size);
            var centre = (size - 1) * 0.5f;
            var radius = centre - 1f;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x - centre;
                    var dy = y - centre;
                    var distance = Mathf.Sqrt((dx * dx) + (dy * dy)) - radius;
                    texture.SetPixel(
                        x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - distance)));
                }
            }

            texture.Apply(false, false);
            circleSprite = Sprite.Create(
                texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            circleSprite.hideFlags = HideFlags.HideAndDontSave;
            return circleSprite;
        }

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        public static Image CreateImage(
            string name, Transform parent, Color color, Sprite sprite = null)
        {
            var image = CreateRect(name, parent).gameObject.AddComponent<Image>();
            image.color = color;

            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Sliced;

                // The sprites are generated at one pixel per unit of radius, so
                // the slice must not be rescaled by the canvas reference PPU.
                image.pixelsPerUnitMultiplier = 1f;
            }

            return image;
        }

        public static TextMeshProUGUI CreateText(
            string name,
            Transform parent,
            TMP_FontAsset font,
            float size,
            Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.Left)
        {
            var text = CreateRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.richText = false;
            text.raycastTarget = false;

            // Every label on this screen sits in a column of its own. A long one
            // has to end, not fold into a second line and push the row's height
            // past what the list reserved for it.
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }

        /// <summary>
        /// Fills the parent, inset by the given number of pixels per side.
        /// </summary>
        public static RectTransform Stretch(
            this RectTransform rect,
            float left = 0f,
            float right = 0f,
            float top = 0f,
            float bottom = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            return rect;
        }

        /// <summary>
        /// Pins a fixed-size rect to one point of its parent, anchor and pivot
        /// together, so the offset reads as the margin from that corner.
        /// </summary>
        public static RectTransform Anchor(
            this RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        /// <summary>
        /// Builds the rounded rectangle. A <paramref name="thickness"/> above
        /// zero hollows it out, leaving a border that many pixels wide.
        /// </summary>
        /// <remarks>
        /// Alpha comes from a signed distance rather than an inside test, so the
        /// corners stay smooth once the nine-slice stretches them. A thresholded
        /// corner shows its steps at panel size.
        /// </remarks>
        private static Sprite BuildRoundedSprite(int radius, float thickness)
        {
            var size = Mathf.Max((radius * 2) + 4, 8);
            var texture = NewTexture(size);
            var half = size * 0.5f;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var coverage = Coverage(x, y, half, radius, 0f);
                    if (thickness > 0f)
                    {
                        coverage -= Coverage(x, y, half, radius, thickness);
                    }

                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(coverage)));
                }
            }

            texture.Apply(false, false);

            // One pixel past the radius, so the stretched middle never eats into
            // a corner.
            var border = Mathf.Min(radius + 1, (size / 2) - 1);
            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static float Coverage(int x, int y, float half, float radius, float inset)
        {
            var effectiveRadius = Mathf.Max(radius - inset, 0f);
            var extent = half - inset - effectiveRadius;
            var dx = Mathf.Abs(x + 0.5f - half) - extent;
            var dy = Mathf.Abs(y + 0.5f - half) - extent;
            var outside = new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dy, 0f)).magnitude;
            var inside = Mathf.Min(Mathf.Max(dx, dy), 0f);
            return Mathf.Clamp01(0.5f - (outside + inside - effectiveRadius));
        }

        private static Texture2D NewTexture(int size)
        {
            return new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
        }
    }
}
