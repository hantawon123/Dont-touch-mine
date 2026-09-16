using System;
using System.Collections.Generic;
using Game.Client.Home;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Match
{
    public interface IHighlightHudView
    {
        void Show(string subtitle, IReadOnlyList<float> barFills);
        void Hide();
        void SetSubtitle(string subtitle);
        void SetBarFills(IReadOnlyList<float> barFills);
    }

    /// <summary>
    /// Highlight playback HUD: centered title, scene/nickname, clip bars, and skip keys.
    /// Input wiring belongs to the playback controller; this view only paints.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class HighlightHudView : MonoBehaviour, IHighlightHudView
    {
        public const string RootName = "HighlightHud";
        public const string TitleText = "HIGHLIGHT";
        public const string SkipAction = "건너뛰기";
        public const string SkipKey = "Space";
        public const string SkipAllAction = "전체 건너뛰기";
        public const string SkipAllKey = "Tab";
        public const int BarCount = 3;
        public const float TitleFontSize = 45f;
        public const float SubtitleFontSize = 28f;
        public const float TopPadding = 20f;
        public const float HeaderWidth = 920f;
        public const float HeaderHeight = 148f;
        public const float TitleHeight = 56f;
        public const float SubtitleHeight = 40f;
        public const float BarRowOffset = 108f;
        public const float BarWidth = 132f;
        public const float BarHeight = 10f;
        public const float BarGap = 8f;
        public const int BarCornerRadius = 5;
        public const float MarginBottom = 48f;
        public const float SkipPanelWidth = 280f;
        public const float SkipPanelHeight = 112f;
        public const float SkipRowHeight = 40f;

        public static readonly Color BarFillColor = new Color(1f, 0.54f, 0.24f, 1f);
        public static readonly Color BarTrackColor = new Color(1f, 1f, 1f, 0.28f);
        public static readonly float[] EmptyFills = { 0f, 0f, 0f };

        [SerializeField]
        private TMP_Text titleText;

        [SerializeField]
        private TMP_Text subtitleText;

        [SerializeField]
        private GameObject header;

        [SerializeField]
        private GameObject skipGuide;

        [SerializeField]
        private RectTransform[] barFills;

        [SerializeField]
        [Tooltip("Shows the highlight HUD in the editor Game view without entering Play.")]
        private bool previewOnAwake;

        private bool shown;
        private int visibleBarCount = BarCount;

        public static HighlightHudView Create(Transform parent)
        {
            var rootObject = new GameObject(RootName, typeof(RectTransform));
            rootObject.transform.SetParent(parent, false);
            Stretch((RectTransform)rootObject.transform);
            return rootObject.AddComponent<HighlightHudView>();
        }

        public static void WriteFills(
            float[] destination,
            IReadOnlyList<double> durations,
            int currentIndex,
            double currentElapsed)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            var clipCount = durations == null ? 0 : durations.Count;
            for (var index = 0; index < destination.Length; index++)
            {
                if (index >= clipCount)
                {
                    destination[index] = 0f;
                    continue;
                }

                if (currentIndex < 0 || index > currentIndex)
                {
                    destination[index] = 0f;
                    continue;
                }

                if (index < currentIndex)
                {
                    destination[index] = 1f;
                    continue;
                }

                var duration = durations[index];
                destination[index] = duration <= 0d
                    ? 1f
                    : Mathf.Clamp01((float)(currentElapsed / duration));
            }
        }

        public static int VisibleBarCount(int clipCount)
        {
            return Mathf.Clamp(clipCount, 0, BarCount);
        }

        public static float[] FillsFor(
            IReadOnlyList<double> durations,
            int currentIndex,
            double currentElapsed)
        {
            var fills = new float[VisibleBarCount(durations == null ? 0 : durations.Count)];
            WriteFills(fills, durations, currentIndex, currentElapsed);
            return fills;
        }

        private void Awake()
        {
            EnsureLayout();
            if (previewOnAwake && !shown)
            {
                Show("FIRST BLOOD : 닉네임", new[] { 0.4f, 0f, 0f });
                return;
            }

            if (!shown)
            {
                Hide();
            }
        }

        public void Show(string subtitle, IReadOnlyList<float> fills)
        {
            var wasShown = shown;
            shown = true;
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            EnsureLayout();
            if (!wasShown)
            {
                ApplyStyle();
            }

            SetSubtitle(subtitle);
            SetBarFills(fills);
            SetSectionsVisible(true);
        }

        public void Hide()
        {
            shown = false;
            SetSectionsVisible(false);
        }

        public void SetSubtitle(string subtitle)
        {
            if (subtitleText == null)
            {
                return;
            }

            var visible = !string.IsNullOrWhiteSpace(subtitle);
            subtitleText.text = visible ? subtitle.Trim() : string.Empty;
        }

        public void SetBarFills(IReadOnlyList<float> fills)
        {
            EnsureLayout();
            ApplyBarLayout(VisibleBarCount(fills == null ? 0 : fills.Count));
            if (barFills == null)
            {
                return;
            }

            var visibleCount = VisibleBarCount(fills == null ? 0 : fills.Count);
            for (var index = 0; index < barFills.Length; index++)
            {
                var fill = barFills[index];
                var bar = fill != null ? fill.parent as RectTransform : null;
                if (bar != null)
                {
                    bar.gameObject.SetActive(index < visibleCount);
                }

                if (fill == null || index >= visibleCount)
                {
                    continue;
                }

                var amount = Mathf.Clamp01(fills[index]);
                fill.anchorMin = Vector2.zero;
                fill.anchorMax = new Vector2(amount, 1f);
                fill.offsetMin = Vector2.zero;
                fill.offsetMax = Vector2.zero;
                fill.gameObject.SetActive(amount > 0.001f);
            }
        }

        private void SetSectionsVisible(bool visible)
        {
            if (cctvOverlay != null) cctvOverlay.gameObject.SetActive(visible);
            if (header != null)
            {
                header.SetActive(visible);
            }

            if (skipGuide != null)
            {
                skipGuide.SetActive(visible);
            }
        }

        private void EnsureLayout()
        {
            EnsureCctvLayout();
            var rect = transform as RectTransform;
            if (rect != null)
            {
                Stretch(rect);
            }

            if (transform.name != RootName)
            {
                transform.name = RootName;
            }

            if (transform.Find("Header") == null)
            {
                BuildLayout();
            }

            if (header == null)
            {
                header = transform.Find("Header")?.gameObject;
            }

            if (titleText == null)
            {
                titleText = transform.Find("Header/Title")?.GetComponent<TMP_Text>();
            }

            if (subtitleText == null)
            {
                subtitleText = transform.Find("Header/Subtitle")?.GetComponent<TMP_Text>();
            }

            if (skipGuide == null)
            {
                skipGuide = transform.Find("SkipGuide")?.gameObject;
            }

            EnsureBars();
            ApplyHeaderLayout();
            ApplySkipLayout();
        }

        private void BuildLayout()
        {
            header = CreateRect(transform, "Header").gameObject;
            titleText = CreateText(header.transform, "Title", TitleText, TitleFontSize);
            subtitleText = CreateText(header.transform, "Subtitle", string.Empty, SubtitleFontSize);
            BuildBars(header.transform);
            skipGuide = CreateRect(transform, "SkipGuide").gameObject;
            BuildSkipRow(skipGuide.transform, 0, SkipAction, SkipKey);
            BuildSkipRow(skipGuide.transform, 1, SkipAllAction, SkipAllKey);
            ApplyHeaderLayout();
            ApplySkipLayout();
        }

        private void BuildBars(Transform parent)
        {
            var row = CreateRect(parent, "Bars");
            barFills = new RectTransform[BarCount];
            for (var index = 0; index < BarCount; index++)
            {
                var bar = CreateImage(
                    row,
                    $"Bar{index}",
                    BarTrackColor,
                    HomeUiFonts.Rounded(BarCornerRadius));
                bar.type = Image.Type.Sliced;
                var fill = CreateImage(
                    bar.transform,
                    "Fill",
                    BarFillColor,
                    HomeUiFonts.Rounded(BarCornerRadius));
                fill.type = Image.Type.Sliced;
                barFills[index] = fill.rectTransform;
            }
        }

        private void EnsureBars()
        {
            if (barFills != null && barFills.Length == BarCount)
            {
                var complete = true;
                for (var index = 0; index < barFills.Length; index++)
                {
                    if (barFills[index] == null)
                    {
                        complete = false;
                        break;
                    }
                }

                if (complete)
                {
                    return;
                }
            }

            barFills = new RectTransform[BarCount];
            for (var index = 0; index < BarCount; index++)
            {
                barFills[index] = transform.Find($"Header/Bars/Bar{index}/Fill") as RectTransform;
            }
        }

        private void ApplyHeaderLayout()
        {
            if (header != null)
            {
                header.transform.SetAsFirstSibling();
                Place(
                    header.GetComponent<RectTransform>(),
                    new Vector2(0.5f, 1f),
                    new Vector2(0f, -TopPadding),
                    new Vector2(HeaderWidth, HeaderHeight),
                    new Vector2(0.5f, 1f));
            }

            if (titleText != null)
            {
                Place(
                    titleText.rectTransform,
                    new Vector2(0.5f, 1f),
                    Vector2.zero,
                    new Vector2(HeaderWidth, TitleHeight),
                    new Vector2(0.5f, 1f));
            }

            if (subtitleText != null)
            {
                Place(
                    subtitleText.rectTransform,
                    new Vector2(0.5f, 1f),
                    new Vector2(0f, -TitleHeight),
                    new Vector2(HeaderWidth, SubtitleHeight),
                    new Vector2(0.5f, 1f));
            }

            ApplyBarLayout(visibleBarCount);
        }

        private void ApplyBarLayout(int count)
        {
            visibleBarCount = VisibleBarCount(count);
            var bars = transform.Find("Header/Bars") as RectTransform;
            if (bars == null)
            {
                return;
            }

            var shownCount = Mathf.Max(1, visibleBarCount);
            var rowWidth = (BarWidth * shownCount) + (BarGap * (shownCount - 1));
            Place(
                bars,
                new Vector2(0.5f, 1f),
                new Vector2(0f, -BarRowOffset),
                new Vector2(rowWidth, BarHeight),
                new Vector2(0.5f, 1f));
            bars.gameObject.SetActive(visibleBarCount > 0);
            for (var index = 0; index < BarCount; index++)
            {
                var bar = bars.Find($"Bar{index}") as RectTransform;
                if (bar == null)
                {
                    continue;
                }

                bar.gameObject.SetActive(index < visibleBarCount);
                if (index >= visibleBarCount)
                {
                    continue;
                }

                var x = visibleBarCount == 1
                    ? 0f
                    : -rowWidth * 0.5f + (BarWidth * 0.5f) + (index * (BarWidth + BarGap));
                Place(
                    bar,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(x, 0f),
                    new Vector2(BarWidth, BarHeight));
            }
        }

        private void BuildSkipRow(Transform parent, int index, string action, string key)
        {
            var row = CreateRect(parent, $"Row{index}");
            var actionText = CreateText(
                row,
                "Action",
                action,
                KeySettingGuideView.ActionFontSize,
                HomeUiFonts.ApplyLight());
            actionText.alignment = TextAlignmentOptions.MidlineRight;
            var chip = CreateImage(
                row,
                "Key",
                HidingActiveHudView.KeyChipColor,
                HidingActiveHudView.KeyChipSprite);
            chip.type = Image.Type.Sliced;
            var keyLabel = CreateText(
                chip.transform,
                "Label",
                key,
                HidingActiveHudView.KeyChipFontSize,
                HomeUiFonts.ApplyLight());
            Stretch(keyLabel.rectTransform);
        }

        private void ApplySkipLayout()
        {
            if (skipGuide == null)
            {
                return;
            }

            Place(
                skipGuide.GetComponent<RectTransform>(),
                new Vector2(1f, 0f),
                new Vector2(-KeySettingGuideView.MarginRight, MarginBottom),
                new Vector2(SkipPanelWidth, SkipPanelHeight),
                new Vector2(1f, 0f));

            ApplySkipRow(0, SkipAction, SkipKey);
            ApplySkipRow(1, SkipAllAction, SkipAllKey);
        }

        private void ApplySkipRow(int index, string action, string key)
        {
            var row = skipGuide.transform.Find($"Row{index}") as RectTransform;
            if (row == null)
            {
                return;
            }

            Place(
                row,
                new Vector2(1f, 1f),
                new Vector2(-SkipPanelWidth * 0.5f, -24f - (index * KeySettingGuideView.RowStep)),
                new Vector2(SkipPanelWidth, SkipRowHeight));

            var chip = row.Find("Key") as RectTransform;
            var keyLabel = row.Find("Key/Label")?.GetComponent<TMP_Text>();
            if (keyLabel != null)
            {
                keyLabel.text = key;
                keyLabel.font = HomeUiFonts.ApplyLight();
                keyLabel.fontSize = HidingActiveHudView.KeyChipFontSize;
                keyLabel.fontStyle = FontStyles.Normal;
                keyLabel.color = Color.white;
                keyLabel.textWrappingMode = TextWrappingModes.NoWrap;
                keyLabel.overflowMode = TextOverflowModes.Overflow;
                keyLabel.ForceMeshUpdate();
            }

            if (chip != null)
            {
                var chipImage = chip.GetComponent<Image>();
                if (chipImage != null)
                {
                    chipImage.color = HidingActiveHudView.KeyChipColor;
                    chipImage.sprite = HidingActiveHudView.KeyChipSprite;
                    chipImage.type = Image.Type.Sliced;
                    chipImage.pixelsPerUnitMultiplier = 1f;
                }

                var width = HidingActiveHudView.KeyChipWidth;
                if (keyLabel != null)
                {
                    width = HidingActiveHudView.MeasureKeyChipWidth(keyLabel.text, keyLabel.preferredWidth);
                }

                Place(
                    chip,
                    new Vector2(1f, 0.5f),
                    Vector2.zero,
                    new Vector2(width, HidingActiveHudView.KeyChipHeight),
                    new Vector2(1f, 0.5f));

                var actionText = row.Find("Action")?.GetComponent<TMP_Text>();
                if (actionText != null)
                {
                    actionText.text = action;
                    actionText.font = HomeUiFonts.ApplyLight();
                    actionText.fontSize = KeySettingGuideView.ActionFontSize;
                    actionText.fontStyle = FontStyles.Normal;
                    actionText.color = Color.white;
                    actionText.alignment = TextAlignmentOptions.MidlineRight;
                    Place(
                        actionText.rectTransform,
                        new Vector2(1f, 0.5f),
                        new Vector2(-(width + 8f), 0f),
                        new Vector2(220f, HidingActiveHudView.KeyChipHeight),
                        new Vector2(1f, 0.5f));
                }
            }
        }

        private void ApplyStyle()
        {
            var font = HomeUiFonts.Apply();
            if (titleText != null)
            {
                titleText.font = font;
                titleText.fontSize = TitleFontSize;
                titleText.fontStyle = FontStyles.Normal;
                titleText.color = Color.white;
                titleText.alignment = TextAlignmentOptions.Center;
                titleText.text = TitleText;
            }

            if (subtitleText != null)
            {
                subtitleText.font = font;
                subtitleText.fontSize = SubtitleFontSize;
                subtitleText.fontStyle = FontStyles.Normal;
                subtitleText.color = Color.white;
                subtitleText.alignment = TextAlignmentOptions.Center;
            }

            ApplyHeaderLayout();
            ApplySkipLayout();
        }

        private static RectTransform CreateRect(Transform parent, string name)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject.GetComponent<RectTransform>();
        }

        private static Image CreateImage(Transform parent, string name, Color color, Sprite sprite)
        {
            var gameObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            gameObject.transform.SetParent(parent, false);
            var image = gameObject.GetComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text CreateText(
            Transform parent,
            string name,
            string content,
            float fontSize,
            TMP_FontAsset font = null)
        {
            var gameObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            gameObject.transform.SetParent(parent, false);
            var text = gameObject.GetComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            text.richText = true;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.font = font != null ? font : HomeUiFonts.Apply();
            text.fontStyle = FontStyles.Normal;
            return text;
        }

        private static void Place(
            RectTransform rect,
            Vector2 anchor,
            Vector2 anchoredPosition,
            Vector2 size,
            Vector2? pivot = null)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rect.anchoredPosition3D = new Vector3(anchoredPosition.x, anchoredPosition.y, 0f);
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect, float padding = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
        }
    }
}
