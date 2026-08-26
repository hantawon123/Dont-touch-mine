using System;
using TMPro;
using UnityEngine;

namespace Game.Client.Home
{
    public static class HomeUiFonts
    {
        private static TMP_FontAsset koreanFont;
        private static Sprite circleSprite;
        private static Sprite whiteSprite;
        private static Sprite roundedSprite;
        private static Sprite pillSprite;

        public static Sprite WhiteSprite
        {
            get
            {
                if (whiteSprite != null)
                {
                    return whiteSprite;
                }

                var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
                var pixels = new Color[16];
                for (var index = 0; index < pixels.Length; index++)
                {
                    pixels[index] = Color.white;
                }

                texture.SetPixels(pixels);
                texture.Apply(false, false);
                whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
                whiteSprite.hideFlags = HideFlags.HideAndDontSave;
                return whiteSprite;
            }
        }

        public static Sprite RoundedSprite
        {
            get
            {
                if (roundedSprite != null)
                {
                    return roundedSprite;
                }

                const int size = 64;
                const int radius = 16;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear
                };

                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        texture.SetPixel(x, y, IsInsideRoundedRect(x, y, size, radius)
                            ? Color.white
                            : Color.clear);
                    }
                }

                texture.Apply(false, false);
                roundedSprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, size, size),
                    new Vector2(0.5f, 0.5f),
                    100f,
                    0,
                    SpriteMeshType.FullRect,
                    new Vector4(radius, radius, radius, radius));
                roundedSprite.hideFlags = HideFlags.HideAndDontSave;
                return roundedSprite;
            }
        }

        public static Sprite PillSprite
        {
            get
            {
                if (pillSprite != null)
                {
                    return pillSprite;
                }

                const int size = 64;
                const int radius = 30;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear
                };

                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        texture.SetPixel(x, y, IsInsideRoundedRect(x, y, size, radius)
                            ? Color.white
                            : Color.clear);
                    }
                }

                texture.Apply(false, false);
                pillSprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, size, size),
                    new Vector2(0.5f, 0.5f),
                    100f,
                    0,
                    SpriteMeshType.FullRect,
                    new Vector4(radius, radius, radius, radius));
                pillSprite.hideFlags = HideFlags.HideAndDontSave;
                return pillSprite;
            }
        }

        public static Sprite CircleSprite
        {
            get
            {
                if (circleSprite != null)
                {
                    return circleSprite;
                }

                const int size = 64;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear
                };

                var center = (size - 1) * 0.5f;
                var radius = center - 1f;
                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        var dx = x - center;
                        var dy = y - center;
                        texture.SetPixel(x, y, (dx * dx) + (dy * dy) <= radius * radius
                            ? Color.white
                            : Color.clear);
                    }
                }

                texture.Apply(false, false);
                circleSprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, size, size),
                    new Vector2(0.5f, 0.5f),
                    100f);
                circleSprite.hideFlags = HideFlags.HideAndDontSave;
                return circleSprite;
            }
        }

        public static TMP_FontAsset Apply(TMP_FontAsset fontAsset = null)
        {
            if (koreanFont != null)
            {
                return koreanFont;
            }

            koreanFont = fontAsset != null ? fontAsset : TMP_Settings.defaultFontAsset;
            if (koreanFont == null)
            {
                throw new InvalidOperationException(
                    "Korean TMP font is missing. Assign Cafe24Ssurround SDF " +
                    "or set it as TMP Settings default font.");
            }

            return koreanFont;
        }

        private static bool IsInsideRoundedRect(int x, int y, int size, int radius)
        {
            var innerMin = radius;
            var innerMax = size - radius;
            if (x >= innerMin && x < innerMax)
            {
                return true;
            }

            if (y >= innerMin && y < innerMax)
            {
                return true;
            }

            var cornerX = x < innerMin ? innerMin : innerMax;
            var cornerY = y < innerMin ? innerMin : innerMax;
            var dx = x - cornerX;
            var dy = y - cornerY;
            return (dx * dx) + (dy * dy) <= radius * radius;
        }
    }
}
