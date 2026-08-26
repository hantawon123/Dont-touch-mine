using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Game.Client.Home
{
    public static class HomeUiFonts
    {
        private static TMP_FontAsset koreanFont;
        private static Sprite circleSprite;
        private static Sprite whiteSprite;

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

        public static TMP_FontAsset Apply(Font sourceFont)
        {
            if (koreanFont != null)
            {
                return koreanFont;
            }

            if (sourceFont == null)
            {
                throw new InvalidOperationException(
                    "Cafe24 Ssurround font must be assigned. Expected at Assets/_Game/Content/Fonts/Cafe24Ssurround-v2.0.ttf.");
            }

            koreanFont = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                90,
                9,
                GlyphRenderMode.SDFAA,
                1024,
                1024,
                AtlasPopulationMode.Dynamic,
                true);
            if (koreanFont == null)
            {
                throw new InvalidOperationException(
                    "Failed to create TMP font from Cafe24 Ssurround. Enable Include Font Data on the font importer.");
            }

            koreanFont.hideFlags = HideFlags.HideAndDontSave;
            koreanFont.name = "Cafe24Ssurround SDF";
            TMP_Settings.defaultFontAsset = koreanFont;
            var fallbacks = TMP_Settings.fallbackFontAssets;
            if (fallbacks == null)
            {
                TMP_Settings.fallbackFontAssets = new List<TMP_FontAsset> { koreanFont };
            }
            else if (!fallbacks.Contains(koreanFont))
            {
                fallbacks.Insert(0, koreanFont);
            }

            return koreanFont;
        }
    }
}
