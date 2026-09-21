using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Client.Settings
{
    /// <summary>
    /// Which corners of a rectangle are rounded.
    /// </summary>
    /// <remarks>
    /// The settings design rounds one whole side — a tab down its left, a row
    /// down its right — which <see cref="Home.SquareCorner"/> cannot say, so
    /// the corners are named individually here.
    /// </remarks>
    [Flags]
    public enum RoundedCorners
    {
        None = 0,
        TopLeft = 1,
        TopRight = 2,
        BottomRight = 4,
        BottomLeft = 8,

        /// <summary>Both corners down one side, which is how this screen rounds a tab or a row.</summary>
        Left = TopLeft | BottomLeft,

        Right = TopRight | BottomRight,
        All = Left | Right
    }

    /// <summary>
    /// The two shapes the settings screen needs that the shared sprite cache
    /// does not have: a rectangle rounded at chosen corners, and a glow.
    /// </summary>
    /// <remarks>
    /// Generated and cached rather than imported, for the same reason
    /// <see cref="Home.HomeUiFonts.Rounded"/> is: a generated sprite costs a
    /// few kilobytes and no repository space, and cannot drift from
    /// <see cref="SettingsStyle"/> the way an exported PNG does.
    /// </remarks>
    public static class SettingsSprites
    {
        private static readonly Dictionary<int, Sprite> Rounded = new Dictionary<int, Sprite>();
        private static readonly Dictionary<int, Sprite> Glows = new Dictionary<int, Sprite>();
        private static Sprite exitGlyph;

        /// <summary>
        /// A filled rectangle rounded at <paramref name="corners"/>, nine-sliced
        /// so one sprite serves any size.
        /// </summary>
        public static Sprite RoundedRect(int radius, RoundedCorners corners)
        {
            var key = (radius * 16) + (int)corners;
            if (Rounded.TryGetValue(key, out var cached) && cached != null)
            {
                return cached;
            }

            var sprite = BuildRounded(radius, corners);
            Rounded[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// The soft halo a design tool draws as a drop shadow with no offset:
        /// the panel's own rounded outline pushed out by <paramref name="spread"/>,
        /// then faded to nothing over <paramref name="blur"/>.
        /// </summary>
        /// <remarks>
        /// Meant to be drawn on a rectangle that is the panel's grown by
        /// <see cref="GlowMargin"/> on every side, so the fully lit edge lands
        /// exactly <paramref name="spread"/> outside the panel. Nine-sliced,
        /// with the whole faded band inside the border, so stretching the
        /// middle never stretches the fade.
        /// </remarks>
        public static Sprite Glow(int radius, int spread, int blur)
        {
            var key = (radius * 10000) + (spread * 100) + blur;
            if (Glows.TryGetValue(key, out var cached) && cached != null)
            {
                return cached;
            }

            var sprite = BuildGlow(radius, spread, blur);
            Glows[key] = sprite;
            return sprite;
        }

        /// <summary>How far past the panel the glow's rectangle has to reach.</summary>
        public static float GlowMargin(int spread, int blur) => spread + blur;

        /// <summary>
        /// 나가기 표시 (S15P21D205-1086): 오른쪽이 열린 문틀과 그 밖으로 나가는 화살표.
        /// </summary>
        /// <remarks>
        /// 가져오지 않고 그립니다. 이 화면의 다른 도형들과 같은 이유입니다 - 몇 킬로바이트도
        /// 저장소를 쓰지 않고, 굵기나 비례를 바꿀 때 PNG 를 다시 내보낼 일이 없습니다.
        /// 흰색으로 그리므로 색은 쓰는 쪽의 <see cref="UnityEngine.UI.Image.color"/> 가 정합니다.
        /// </remarks>
        public static Sprite ExitGlyph()
        {
            if (exitGlyph != null)
            {
                return exitGlyph;
            }

            exitGlyph = BuildExitGlyph();
            return exitGlyph;
        }

        /// <summary>
        /// 선분 다섯 개로 그립니다. 문틀 세 변(오른쪽은 비웁니다 - 그 자리가 나가는 곳입니다)과
        /// 화살표의 대와 촉 둘입니다. 좌표는 디자인처럼 위에서 아래로 세고, 텍스처에 쓸 때
        /// 뒤집습니다.
        /// </summary>
        private static Sprite BuildExitGlyph()
        {
            const int Size = 64;
            const float HalfStroke = 2.6f;

            var strokes = new[]
            {
                new Vector4(12f, 10f, 12f, 54f),
                new Vector4(12f, 10f, 32f, 10f),
                new Vector4(12f, 54f, 32f, 54f),
                new Vector4(30f, 32f, 54f, 32f),
                new Vector4(44f, 22f, 54f, 32f),
                new Vector4(44f, 42f, 54f, 32f)
            };

            var texture = NewTexture(Size);
            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    var point = new Vector2(x + 0.5f, Size - (y + 0.5f));
                    var nearest = float.MaxValue;
                    foreach (var stroke in strokes)
                    {
                        nearest = Mathf.Min(nearest, DistanceToSegment(
                            point, new Vector2(stroke.x, stroke.y), new Vector2(stroke.z, stroke.w)));
                    }

                    var alpha = Mathf.Clamp01(HalfStroke + 0.5f - nearest);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply(false, false);
            var sprite = Sprite.Create(
                texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), 100f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static float DistanceToSegment(Vector2 point, Vector2 from, Vector2 to)
        {
            var span = to - from;
            var length = span.sqrMagnitude;
            if (length <= Mathf.Epsilon)
            {
                return Vector2.Distance(point, from);
            }

            var t = Mathf.Clamp01(Vector2.Dot(point - from, span) / length);
            return Vector2.Distance(point, from + (span * t));
        }

        private static Sprite BuildRounded(int radius, RoundedCorners corners)
        {
            var size = Mathf.Max((radius * 2) + 4, 8);
            var texture = NewTexture(size);
            var half = size * 0.5f;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    // Texture space counts up from the bottom, so the design's
                    // top-left corner is the low-x, high-y quadrant here. A
                    // quadrant whose corner is square is filled solid; the
                    // nine-slice takes each corner from the matching corner of
                    // this texture.
                    var right = x + 0.5f > half;
                    var top = y + 0.5f > half;
                    var coverage = IsRounded(corners, right, top)
                        ? Coverage(x, y, half, radius)
                        : 1f;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(coverage)));
                }
            }

            texture.Apply(false, false);
            return Slice(texture, size, Mathf.Min(radius + 1, (size / 2) - 1));
        }

        private static Sprite BuildGlow(int radius, int spread, int blur)
        {
            var margin = spread + blur;
            var shapeRadius = radius + spread;
            var size = ((shapeRadius + blur) * 2) + 4;
            var texture = NewTexture(size);
            var half = size * 0.5f;

            // The lit shape: the panel grown by the spread, sitting one blur
            // plus a pixel inside the texture's edge so the fade has room.
            var extent = half - blur - 1f - shapeRadius;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = Mathf.Abs(x + 0.5f - half) - extent;
                    var dy = Mathf.Abs(y + 0.5f - half) - extent;
                    var outside = new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dy, 0f)).magnitude;
                    var inside = Mathf.Min(Mathf.Max(dx, dy), 0f);
                    var distance = outside + inside - shapeRadius;

                    // Full inside the shape, then eased to nothing across the
                    // blur. Squared so it fades the way a blurred edge does:
                    // quickly at first, then trailing off.
                    var fade = distance <= 0f
                        ? 1f
                        : Mathf.Clamp01(1f - (distance / blur));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, fade * fade));
                }
            }

            texture.Apply(false, false);
            return Slice(texture, size, Mathf.Min(shapeRadius + margin + 1, (size / 2) - 1));
        }

        private static Texture2D NewTexture(int size) =>
            new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

        private static Sprite Slice(Texture2D texture, int size, int border)
        {
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

        private static bool IsRounded(RoundedCorners corners, bool right, bool top)
        {
            var corner = right
                ? (top ? RoundedCorners.TopRight : RoundedCorners.BottomRight)
                : (top ? RoundedCorners.TopLeft : RoundedCorners.BottomLeft);
            return (corners & corner) != 0;
        }

        /// <summary>
        /// How much of a pixel a rounded rectangle covers, from its signed
        /// distance, so the corners stay smooth once the nine-slice stretches
        /// them.
        /// </summary>
        private static float Coverage(int x, int y, float half, float radius)
        {
            var extent = half - radius;
            var dx = Mathf.Abs(x + 0.5f - half) - extent;
            var dy = Mathf.Abs(y + 0.5f - half) - extent;
            var outside = new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dy, 0f)).magnitude;
            var inside = Mathf.Min(Mathf.Max(dx, dy), 0f);
            return Mathf.Clamp01(0.5f - (outside + inside - radius));
        }
    }
}
