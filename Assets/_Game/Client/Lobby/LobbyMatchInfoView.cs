using Game.Client.Home;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Lobby
{
    /// <summary>
    /// Read-only map and category cards in the lobby's top-left, lined up with
    /// the chat input. The map is a preview stacked over its name; the
    /// category shows a title on the black card and the option in a translucent
    /// white inner card underneath.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LobbyMatchInfoView : MonoBehaviour
    {
        public const string RootName = "MatchInfo";
        public const float MarginTop = 60f;

        /// <summary>Same left inset as <see cref="Match.MatchChatView.Margin"/>.</summary>
        public const float MarginLeft = 24f;

        /// <summary>Previous card width 320, scaled 1.8x to match the stacked preview.</summary>
        public const float Width = 576f;
        public const float FontSize = 32f;
        public const float CaptionFontSize = 24f;
        public const string CategoryCaption = "카테고리";
        public const float Padding = 0f;
        public const float ContentSpacing = 22f;
        public const float MapRowPadding = 18f;
        public const float MapNameSpacing = 22f;
        public const float LabelHeight = 68f;
        public const float CaptionHeight = 36f;
        public const float CaptionGap = 8f;
        public const float InnerCardInset = 12f;
        public const float PlayerListGap = 24f;
        public const int PanelRadius = 16;
        public const int MapRowRadius = 22;
        public const int MapPreviewRadius = 14;
        public const int InnerCardRadius = 16;
        public static readonly Vector2 MapPreviewSize = new Vector2(540f, 405f);

        public static readonly Color PanelFill = Color.clear;
        public static readonly Color MapRowFill = Color.black;
        public static readonly Color MapPreviewFill = PlaySettingsStyle.Palette.MapPreview;
        public static readonly Color CategoryInnerFill = new Color(1f, 1f, 1f, 0.7f);
        public static readonly Color CategoryValueColor = Color.black;

        public static float MapCardHeight =>
            MapRowPadding + MapPreviewSize.y + MapNameSpacing + LabelHeight + MapRowPadding;

        public static float InnerCardTop => MapRowPadding + CaptionHeight + CaptionGap;

        public static float CategoryCardHeight =>
            InnerCardTop + LabelHeight + InnerCardInset;

        public static float MapRowHeight => MapCardHeight;

        public static float PanelHeight =>
            MapCardHeight + ContentSpacing + CategoryCardHeight;

        public static float PlayerListTopOffset => MarginTop + PanelHeight + PlayerListGap;

        private TextMeshProUGUI categoryCaption;
        private TextMeshProUGUI categoryValue;
        private TextMeshProUGUI mapName;
        private Image mapPreviewPhoto;

        public string CategoryLabel => categoryValue != null ? categoryValue.text : string.Empty;

        public string MapLabel => mapName != null ? mapName.text : string.Empty;

        /// <summary>지금 보이는 맵 사진. 사진이 없는 맵·랜덤이면 null.</summary>
        public Sprite MapPreviewSprite =>
            mapPreviewPhoto != null && mapPreviewPhoto.gameObject.activeSelf ? mapPreviewPhoto.sprite : null;

        public static LobbyMatchInfoView Create(Transform parent)
        {
            var root = new GameObject(RootName, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var view = root.AddComponent<LobbyMatchInfoView>();
            view.EnsureLayout();
            view.transform.SetAsLastSibling();
            return view;
        }

        public static LobbyMatchInfoView Ensure(Transform parent)
        {
            if (parent == null)
            {
                return null;
            }

            var existing = parent.Find(RootName);
            if (existing == null)
            {
                return Create(parent);
            }

            var view = existing.GetComponent<LobbyMatchInfoView>();
            if (view == null)
            {
                view = existing.gameObject.AddComponent<LobbyMatchInfoView>();
            }

            view.EnsureLayout();
            view.transform.SetAsLastSibling();
            return view;
        }

        public void SetInfo(string categoryLabel, string mapLabel)
        {
            SetInfo(categoryLabel, mapLabel, mapPreview: null);
        }

        /// <param name="mapPreview">맵 사진. null이면 기존 단색 상자만 보인다.</param>
        public void SetInfo(string categoryLabel, string mapLabel, Sprite mapPreview)
        {
            EnsureLayout();
            if (categoryValue != null)
            {
                categoryValue.text = categoryLabel ?? string.Empty;
            }

            if (mapName != null)
            {
                mapName.text = mapLabel ?? string.Empty;
            }

            MapPreviewSprites.Apply(mapPreviewPhoto, mapPreview);
        }

        private void Awake()
        {
            EnsureLayout();
        }

        private void EnsureLayout()
        {
            CacheRefs();
            if (HasStackedLayout())
            {
                PlacePanel();
                PlaceCards();
                ApplyChrome();
                return;
            }

            BuildLayout();
        }

        private bool HasStackedLayout()
        {
            var preview = transform.Find("MapRow/MapPreview") as RectTransform;
            return categoryValue != null
                && mapName != null
                && preview != null
                && preview.sizeDelta == MapPreviewSize
                && transform.Find("CategoryRow/CategoryPreview") == null
                && transform.Find("CategoryRow/CategoryCaption") != null
                && transform.Find("CategoryRow/CategoryValueCard/Fill") != null
                && transform.Find("CategoryRow/Divider") == null;
        }

        private void CacheRefs()
        {
            categoryCaption = transform.Find("CategoryRow/CategoryCaption")?.GetComponent<TextMeshProUGUI>();
            categoryValue = transform.Find("CategoryRow/CategoryValueCard/CategoryValue")
                ?.GetComponent<TextMeshProUGUI>()
                ?? transform.Find("CategoryRow/CategoryValue")?.GetComponent<TextMeshProUGUI>();
            mapName = transform.Find("MapRow/MapName")?.GetComponent<TextMeshProUGUI>();
            mapPreviewPhoto = FindPreviewPhoto("MapRow/MapPreview");
        }

        private Image FindPreviewPhoto(string path)
        {
            var preview = transform.Find(path)?.GetComponent<Image>();
            return preview != null
                ? MapPreviewSprites.FindPhoto(preview) ?? MapPreviewSprites.AttachPhoto(preview)
                : null;
        }

        private void BuildLayout()
        {
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }

            categoryCaption = null;
            categoryValue = null;
            mapName = null;
            mapPreviewPhoto = null;

            var panelImage = gameObject.GetComponent<Image>();
            if (panelImage == null)
            {
                panelImage = gameObject.AddComponent<Image>();
            }

            panelImage.sprite = HomeUiFonts.Rounded(PanelRadius);
            panelImage.type = Image.Type.Sliced;
            panelImage.color = PanelFill;
            panelImage.raycastTarget = false;
            PlacePanel();

            mapName = CreateMapCard(out mapPreviewPhoto);
            categoryValue = CreateCategoryCard();
            PlaceCards();
        }

        private TextMeshProUGUI CreateMapCard(out Image previewPhoto)
        {
            var row = CreateCardRow("MapRow");

            var preview = CreateRect("MapPreview", row);
            preview.anchorMin = preview.anchorMax = new Vector2(0.5f, 1f);
            preview.pivot = new Vector2(0.5f, 1f);
            preview.sizeDelta = MapPreviewSize;
            preview.anchoredPosition = new Vector2(0f, -MapRowPadding);
            var previewImage = preview.gameObject.AddComponent<Image>();
            previewImage.sprite = HomeUiFonts.Rounded(MapPreviewRadius);
            previewImage.type = Image.Type.Sliced;
            previewImage.color = MapPreviewFill;
            previewImage.raycastTarget = false;
            previewPhoto = MapPreviewSprites.FindPhoto(previewImage) ?? MapPreviewSprites.AttachPhoto(previewImage);

            var label = CreateLabel(
                row,
                "MapName",
                PlaySettingsMapCatalog.Default.Label,
                HomeUiFonts.Apply(),
                TextAlignmentOptions.Center);
            var labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(MapRowPadding, MapRowPadding);
            labelRect.offsetMax = new Vector2(
                -MapRowPadding,
                -(MapRowPadding + MapPreviewSize.y + MapNameSpacing));
            return label;
        }

        private TextMeshProUGUI CreateCategoryCard()
        {
            var row = CreateCardRow("CategoryRow");
            var caption = CreateLabel(
                row,
                "CategoryCaption",
                CategoryCaption,
                HomeUiFonts.Apply(),
                TextAlignmentOptions.Center,
                CaptionFontSize,
                Color.white);
            PlaceTopBand(caption.rectTransform, MapRowPadding, CaptionHeight, MapRowPadding);

            var inner = CreateRect("CategoryValueCard", row);
            var maskImage = inner.gameObject.AddComponent<Image>();
            maskImage.sprite = HomeUiFonts.Rounded(InnerCardRadius);
            maskImage.type = Image.Type.Sliced;
            maskImage.color = Color.white;
            maskImage.raycastTarget = false;
            var mask = inner.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            var fill = CreateRect("Fill", inner);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            var fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.sprite = HomeUiFonts.WhiteSprite;
            fillImage.type = Image.Type.Simple;
            fillImage.raycastTarget = false;
            ApplyInnerFill(fillImage);

            var label = CreateLabel(
                inner,
                "CategoryValue",
                PlaySettingsCategoryCatalog.Default.Label,
                HomeUiFonts.Apply(),
                TextAlignmentOptions.Center,
                FontSize,
                CategoryValueColor);
            var labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            categoryCaption = caption;
            return label;
        }

        private void PlaceCards()
        {
            var mapRow = transform.Find("MapRow") as RectTransform;
            if (mapRow != null)
            {
                PlaceTopRow(mapRow, 0f, MapCardHeight);
            }

            var categoryRow = transform.Find("CategoryRow") as RectTransform;
            if (categoryRow != null)
            {
                PlaceTopRow(categoryRow, MapCardHeight + ContentSpacing, CategoryCardHeight);
            }

            var captionRect = transform.Find("CategoryRow/CategoryCaption") as RectTransform;
            if (captionRect != null)
            {
                PlaceTopBand(captionRect, MapRowPadding, CaptionHeight, MapRowPadding);
            }

            var inner = transform.Find("CategoryRow/CategoryValueCard") as RectTransform;
            if (inner == null)
            {
                return;
            }

            inner.anchorMin = Vector2.zero;
            inner.anchorMax = Vector2.one;
            inner.pivot = new Vector2(0.5f, 0.5f);
            inner.offsetMin = new Vector2(InnerCardInset, InnerCardInset);
            inner.offsetMax = new Vector2(-InnerCardInset, -InnerCardTop);
        }

        private RectTransform CreateCardRow(string rowName)
        {
            var row = CreateRect(rowName, (RectTransform)transform);
            var rowImage = row.gameObject.AddComponent<Image>();
            rowImage.sprite = HomeUiFonts.Rounded(MapRowRadius);
            rowImage.type = Image.Type.Sliced;
            rowImage.color = MapRowFill;
            rowImage.raycastTarget = false;
            return row;
        }

        private void ApplyChrome()
        {
            ApplyCardFill("MapRow");
            ApplyCardFill("CategoryRow");
            ApplyInnerFill(
                transform.Find("CategoryRow/CategoryValueCard/Fill")?.GetComponent<Image>());

            ApplyLabelFace(categoryCaption, CaptionFontSize, Color.white);
            ApplyLabelFace(mapName, FontSize, Color.white);
            ApplyLabelFace(categoryValue, FontSize, CategoryValueColor);
        }

        private static void ApplyInnerFill(Image image)
        {
            if (image == null)
            {
                return;
            }

            image.color = CategoryInnerFill;
            image.canvasRenderer.SetColor(CategoryInnerFill);
            image.canvasRenderer.SetAlpha(CategoryInnerFill.a);
            image.canvasRenderer.cullTransparentMesh = false;
        }

        private void ApplyCardFill(string path)
        {
            var image = transform.Find(path)?.GetComponent<Image>();
            if (image != null)
            {
                image.color = MapRowFill;
            }
        }

        private static void ApplyLabelFace(TextMeshProUGUI label, float fontSize, Color color)
        {
            if (label == null)
            {
                return;
            }

            var font = HomeUiFonts.Apply();
            if (font != null)
            {
                label.font = font;
                if (font.material != null)
                {
                    label.fontSharedMaterial = font.material;
                }
            }

            label.fontSize = fontSize;
            label.color = color;
        }

        private static void PlaceTopRow(RectTransform row, float top, float height)
        {
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.offsetMin = new Vector2(Padding, -(top + height));
            row.offsetMax = new Vector2(-Padding, -top);
        }

        private static void PlaceTopBand(RectTransform rect, float top, float height, float inset)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(inset, -(top + height));
            rect.offsetMax = new Vector2(-inset, -top);
        }

        private void PlacePanel()
        {
            var rect = (RectTransform)transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(MarginLeft, -MarginTop);
            rect.sizeDelta = new Vector2(Width, PanelHeight);
        }

        private static TextMeshProUGUI CreateLabel(
            RectTransform parent,
            string name,
            string text,
            TMP_FontAsset font,
            TextAlignmentOptions alignment,
            float fontSize = FontSize,
            Color? color = null)
        {
            var rect = CreateRect(name, parent);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null)
            {
                label.font = font;
                if (font.material != null)
                {
                    label.fontSharedMaterial = font.material;
                }
            }

            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = FontStyles.Normal;
            label.color = color ?? Color.white;
            label.alignment = alignment;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            return label;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }
    }
}
