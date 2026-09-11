using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace Game.Client.Home
{
    /// <summary>
    /// Which corner a rounded rectangle leaves square, for the panels that butt
    /// up against the control that opened them.
    /// </summary>
    public enum SquareCorner
    {
        None,
        TopRight,
        BottomRight
    }

    public static class HomeUiFonts
    {
        private static TMP_FontAsset koreanFont;
        private static Sprite circleSprite;
        private static Sprite whiteSprite;
        private static Sprite roundedSprite;
        private static Sprite pillSprite;

        private static readonly Dictionary<int, Sprite> RoundedSprites =
            new Dictionary<int, Sprite>();

        private static readonly Dictionary<int, Sprite> OutlineSprites =
            new Dictionary<int, Sprite>();

        /// <summary>
        /// A filled rounded rectangle at the given radius, nine-sliced so one
        /// sprite serves both a 60 pixel button and a 380 pixel chip.
        /// </summary>
        /// <remarks>
        /// Generated per radius and cached rather than imported: a generated
        /// sprite costs a few kilobytes and no repository space, and it cannot
        /// drift from <see cref="HomeStyle"/> the way an exported PNG does.
        /// </remarks>
        public static Sprite Rounded(int radius, SquareCorner squareCorner = SquareCorner.None)
        {
            var key = (radius * 10) + (int)squareCorner;
            if (RoundedSprites.TryGetValue(key, out var cached) && cached != null)
            {
                return cached;
            }

            var sprite = BuildRoundedSprite(radius, 0f, squareCorner);
            RoundedSprites[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// The border of a rounded rectangle, for the hairline a hovered chip
        /// draws. Kept apart from the fill so one can change without the other.
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

        /// <summary>
        /// Builds the rounded rectangle. A <paramref name="thickness"/> above
        /// zero hollows it out, leaving a border that many pixels wide.
        /// </summary>
        /// <remarks>
        /// Alpha comes from a signed distance rather than an inside test, so the
        /// corners stay smooth once the nine-slice stretches them. A thresholded
        /// corner shows its steps at chip size.
        /// </remarks>
        private static Sprite BuildRoundedSprite(
            int radius, float thickness, SquareCorner squareCorner = SquareCorner.None)
        {
            var size = Mathf.Max((radius * 2) + 4, 8);
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var half = size * 0.5f;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    // Texture space counts up from the bottom, so the design's
                    // bottom-right corner is the low-y, high-x quadrant here.
                    // Filling a quadrant solid leaves that corner square while
                    // the other three keep their radius, and the nine-slice
                    // takes each corner from the matching corner of this
                    // texture.
                    var isSquareCorner = IsSquare(squareCorner, x + 0.5f > half, y + 0.5f > half);
                    var coverage = isSquareCorner
                        ? 1f
                        : RoundedCoverage(x, y, half, radius, 0f);
                    if (thickness > 0f && !isSquareCorner)
                    {
                        coverage -= RoundedCoverage(x, y, half, radius, thickness);
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

        private static bool IsSquare(SquareCorner corner, bool right, bool top)
        {
            switch (corner)
            {
                case SquareCorner.TopRight:
                    return right && top;

                case SquareCorner.BottomRight:
                    return right && !top;

                default:
                    return false;
            }
        }

        private static float RoundedCoverage(int x, int y, float half, float radius, float inset)
        {
            var effectiveRadius = Mathf.Max(radius - inset, 0f);
            var extent = half - inset - effectiveRadius;
            var dx = Mathf.Abs(x + 0.5f - half) - extent;
            var dy = Mathf.Abs(y + 0.5f - half) - extent;
            var outside = new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dy, 0f)).magnitude;
            var inside = Mathf.Min(Mathf.Max(dx, dy), 0f);
            return Mathf.Clamp01(0.5f - (outside + inside - effectiveRadius));
        }

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

                roundedSprite = CreateRoundedSprite(256, 64, 400f);
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

        private const string ExtraBoldResource = "Fonts/Paperlogy-8ExtraBold";
        private const string SemiBoldResource = "Fonts/Paperlogy-6SemiBold";
        private const string LightResource = "Fonts/Paperlogy-3Light";
        private const string RegularResource = "Fonts/Paperlogy-4Regular";
        private const string MediumResource = "Fonts/Paperlogy-5Medium";
        private const string BoldResource = "Fonts/Paperlogy-7Bold";
        private const string BlackResource = "Fonts/Paperlogy-9Black";
        private static TMP_FontAsset koreanLightFont;
        private static TMP_FontAsset koreanRegularFont;
        private static TMP_FontAsset koreanMediumFont;
        private static TMP_FontAsset koreanBoldFont;
        private static TMP_FontAsset koreanBlackFont;
        private static TMP_FontAsset koreanExtraBoldFont;
        private static Font legacyFont;

        public static TMP_FontAsset Apply(TMP_FontAsset fontAsset = null)
        {
            if (IsWeight(koreanFont, "SemiBold"))
            {
                return koreanFont;
            }

            if (IsWeight(fontAsset, "SemiBold"))
            {
                koreanFont = fontAsset;
                return koreanFont;
            }

            try
            {
                koreanFont = LoadKorean(SemiBoldResource, null);
            }
            catch (InvalidOperationException)
            {
                koreanFont = null;
            }

            if (IsWeight(koreanFont, "SemiBold"))
            {
                return koreanFont;
            }

            var source = Resources.Load<Font>(SemiBoldResource) ?? LoadEditorFont(SemiBoldResource);
            var created = CreateRuntimeKorean(source);
            if (IsUsable(created))
            {
                koreanFont = created;
                return koreanFont;
            }

            if (IsUsable(koreanFont))
            {
                return koreanFont;
            }

            throw new InvalidOperationException(
                "Korean TMP font is missing. Add Paperlogy under " +
                "Assets/_Game/Content/Resources/Fonts.");
        }

        public static TMP_FontAsset ApplyLight(TMP_FontAsset fontAsset = null)
        {
            if (koreanLightFont != null)
            {
                return koreanLightFont;
            }

            try
            {
                koreanLightFont = LoadKorean(LightResource, fontAsset);
                return koreanLightFont;
            }
            catch (Exception)
            {
                try
                {
                    koreanLightFont = ApplyRegular(fontAsset);
                    return koreanLightFont;
                }
                catch (Exception)
                {
                    koreanLightFont = Apply(fontAsset);
                    return koreanLightFont;
                }
            }
        }

        public static TMP_FontAsset ApplyRegular(TMP_FontAsset fontAsset = null)
        {
            return koreanRegularFont ??= LoadKorean(RegularResource, fontAsset);
        }

        public static TMP_FontAsset ApplyMedium(TMP_FontAsset fontAsset = null)
        {
            if (koreanMediumFont != null)
            {
                return koreanMediumFont;
            }

            try
            {
                koreanMediumFont = LoadKorean(MediumResource, fontAsset);
                return koreanMediumFont;
            }
            catch (InvalidOperationException)
            {
                return Apply(fontAsset);
            }
        }

        public static TMP_FontAsset ApplyBold(TMP_FontAsset fontAsset = null)
        {
            if (koreanBoldFont != null)
            {
                return koreanBoldFont;
            }

            try
            {
                koreanBoldFont = LoadKorean(BoldResource, fontAsset);
                return koreanBoldFont;
            }
            catch (InvalidOperationException)
            {
                return Apply(fontAsset);
            }
        }

        public static TMP_FontAsset ApplyBlack(TMP_FontAsset fontAsset = null)
        {
            if (koreanBlackFont != null)
            {
                return koreanBlackFont;
            }

            try
            {
                koreanBlackFont = LoadKorean(BlackResource, fontAsset);
                return koreanBlackFont;
            }
            catch (InvalidOperationException)
            {
                return Apply(fontAsset);
            }
        }

        public static TMP_FontAsset ApplyExtraBold(TMP_FontAsset fontAsset = null)
        {
            if (koreanExtraBoldFont != null)
            {
                return koreanExtraBoldFont;
            }

            try
            {
                koreanExtraBoldFont = LoadKorean(ExtraBoldResource, fontAsset);
                return koreanExtraBoldFont;
            }
            catch (InvalidOperationException)
            {
                return ApplyBlack(fontAsset);
            }
        }

        public static Font Legacy()
        {
            if (legacyFont != null)
            {
                return legacyFont;
            }

            legacyFont = Resources.Load<Font>(SemiBoldResource)
                ?? Resources.Load<Font>(RegularResource);
            return legacyFont;
        }

        public static void ApplyLegacy(Transform root)
        {
            var font = Legacy();
            if (font == null || root == null)
            {
                return;
            }

            var texts = root.GetComponentsInChildren<Text>(true);
            for (var index = 0; index < texts.Length; index++)
            {
                if (texts[index] != null)
                {
                    texts[index].font = font;
                }
            }

            var inputs = root.GetComponentsInChildren<InputField>(true);
            for (var index = 0; index < inputs.Length; index++)
            {
                var input = inputs[index];
                if (input == null)
                {
                    continue;
                }

                if (input.textComponent != null)
                {
                    input.textComponent.font = font;
                }

                if (input.placeholder is Text placeholder)
                {
                    placeholder.font = font;
                }
            }
        }

        public static void ApplyTmp(Transform root)
        {
            var font = Apply();
            if (font == null || root == null)
            {
                return;
            }

            var texts = root.GetComponentsInChildren<TMP_Text>(true);
            for (var index = 0; index < texts.Length; index++)
            {
                var text = texts[index];
                if (text != null && text.font != font)
                {
                    text.font = font;
                    text.fontSharedMaterial = font.material;
                }
            }

            var inputs = root.GetComponentsInChildren<TMP_InputField>(true);
            for (var index = 0; index < inputs.Length; index++)
            {
                var input = inputs[index];
                if (input == null)
                {
                    continue;
                }

                input.fontAsset = font;
                if (input.textComponent != null && input.textComponent.font != font)
                {
                    input.textComponent.font = font;
                    input.textComponent.fontSharedMaterial = font.material;
                }

                if (input.placeholder is TMP_Text placeholder && placeholder.font != font)
                {
                    placeholder.font = font;
                    placeholder.fontSharedMaterial = font.material;
                }
            }
        }

        private static TMP_FontAsset LoadKorean(string resourcePath, TMP_FontAsset fontAsset)
        {
            if (IsUsable(fontAsset))
            {
                return fontAsset;
            }

            var baked = Resources.Load<TMP_FontAsset>(resourcePath + " SDF")
                ?? LoadEditorFontAsset(resourcePath + " SDF");
            if (baked != null)
            {
                EnsureRuntimeMaterial(baked);
            }

            if (IsUsable(baked))
            {
                return baked;
            }

            var source = Resources.Load<Font>(resourcePath) ?? LoadEditorFont(resourcePath);
            var loaded = CreateRuntimeKorean(source);
            if (IsUsable(loaded))
            {
                return loaded;
            }

            throw new InvalidOperationException(
                "Korean TMP font is missing. Add Paperlogy under " +
                "Assets/_Game/Content/Resources/Fonts.");
        }

        private static bool IsUsable(TMP_FontAsset font)
        {
            return font != null && font.material != null;
        }

        private static bool IsWeight(TMP_FontAsset font, string weight)
        {
            return IsUsable(font) &&
                   font.name.IndexOf(weight, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static TMP_FontAsset LoadEditorFontAsset(string resourcePath)
        {
#if UNITY_EDITOR
            var fileName = EditorFontFileName(resourcePath);
            return string.IsNullOrEmpty(fileName)
                ? null
                : UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                    $"Assets/_Game/Content/Fonts/{fileName}.asset");
#else
            return null;
#endif
        }

        private static Font LoadEditorFont(string resourcePath)
        {
#if UNITY_EDITOR
            var fileName = EditorFontFileName(resourcePath);
            return string.IsNullOrEmpty(fileName)
                ? null
                : UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(
                    $"Assets/_Game/Content/Fonts/{fileName}.ttf");
#else
            return null;
#endif
        }

        private static string EditorFontFileName(string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath))
            {
                return null;
            }

            return resourcePath.StartsWith("Fonts/", StringComparison.Ordinal)
                ? resourcePath.Substring("Fonts/".Length)
                : resourcePath;
        }

        public static TMP_FontAsset CreateRuntimeKorean(Font source, bool prewarmKorean = false)
        {
            if (source == null)
            {
                return null;
            }

            var loaded = TMP_FontAsset.CreateFontAsset(
                source,
                36,
                5,
                GlyphRenderMode.SDFAA,
                2048,
                2048,
                AtlasPopulationMode.Dynamic,
                enableMultiAtlasSupport: true);
            if (loaded == null)
            {
                return null;
            }

            loaded.hideFlags = HideFlags.HideAndDontSave;
            if (string.IsNullOrEmpty(loaded.name) ||
                loaded.name.IndexOf("Paperlogy", StringComparison.OrdinalIgnoreCase) < 0)
            {
                loaded.name = source.name + " SDF";
            }

            EnsureRuntimeMaterial(loaded);
            if (loaded.material == null)
            {
                return null;
            }

            loaded.TryAddCharacters(
                "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ .,!?:;-_~/()[]");
            if (prewarmKorean)
            {
                var glyphs = Resources.Load<TextAsset>("Fonts/KoreanGlyphs");
#if UNITY_EDITOR
                if (glyphs == null)
                {
                    glyphs = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(
                        "Assets/_Game/Editor/FontAtlasCharacterSet.txt");
                }
#endif
                if (glyphs != null && !string.IsNullOrEmpty(glyphs.text))
                {
                    loaded.TryAddCharacters(
                        glyphs.text.Replace("\r", string.Empty).Replace("\n", string.Empty));
                }
            }

            return loaded;
        }

        private static void EnsureRuntimeMaterial(TMP_FontAsset font)
        {
            if (font == null || font.material != null)
            {
                return;
            }

            var shader = Shader.Find("TextMeshPro/Distance Field")
                ?? Shader.Find("TextMeshPro/Mobile/Distance Field");
            if (shader == null)
            {
                return;
            }

            var atlas = font.atlasTextures != null && font.atlasTextures.Length > 0
                ? font.atlasTextures[0]
                : null;
            var material = new Material(shader)
            {
                name = font.name + " Material",
                hideFlags = HideFlags.HideAndDontSave
            };
            if (atlas != null)
            {
                material.SetTexture("_MainTex", atlas);
            }

            font.material = material;
        }

        private static Sprite CreateRoundedSprite(int size, int radius, float pixelsPerUnit)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    texture.SetPixel(x, y, CoverageRoundedRect(x, y, size, radius));
                }
            }

            texture.Apply(false, false);
            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit,
                0,
                SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static Color CoverageRoundedRect(int x, int y, int size, int radius)
        {
            var half = size * 0.5f;
            var extent = half - radius;
            var dx = Mathf.Abs(x + 0.5f - half) - extent;
            var dy = Mathf.Abs(y + 0.5f - half) - extent;
            var outside = Mathf.Sqrt(
                (Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f)) +
                (Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f)));
            var distance = outside + Mathf.Min(Mathf.Max(dx, dy), 0f) - radius;
            var alpha = Mathf.Clamp01(0.5f - distance);
            return new Color(1f, 1f, 1f, alpha);
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
