using Game.Client.Home;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Lobby
{
    /// <summary>
    /// Read-only map and category cards in the lobby's top-left, lined up with
    /// the chat input. The map is a preview stacked over its name; the
    /// category option sits in a white inner card on the same-sized outer card.
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
        public const float Padding = 0f;
        public const float ContentSpacing = 22f;
        public const float MapRowPadding = 18f;
        public const float MapNameSpacing = 22f;
        public const float LabelHeight = 68f;
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
        public static readonly Color CategoryInnerFill = Color.white;
        public static readonly Color CategoryValueColor = Color.black;

        public static float MapCardHeight =>
            MapRowPadding + MapPreviewSize.y + MapNameSpacing + LabelHeight + MapRowPadding;

        public static float CategoryCardHeight => MapRowPadding + LabelHeight + MapRowPadding;

        public static float MapRowHeight => MapCardHeight;

        public static float PanelHeight =>
            MapCardHeight + ContentSpacing + CategoryCardHeight;

        public static float PlayerListTopOffset => MarginTop + PanelHeight + PlayerListGap;

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
                && transform.Find("CategoryRow/CategoryCaption") == null
                && transform.Find("CategoryRow/CategoryValueCard") != null
                && transform.Find("CategoryRow/Divider") == null;
        }

        private void CacheRefs()
        {
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
            PlaceTopRow((RectTransform)mapName.transform.parent, 0f, MapCardHeight);

            categoryValue = CreateCategoryCard();
            PlaceTopRow(
                (RectTransform)categoryValue.transform.parent,
                MapCardHeight + ContentSpacing,
                CategoryCardHeight);
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
            var inner = CreateRect("CategoryValueCard", row);
            inner.anchorMin = Vector2.zero;
            inner.anchorMax = Vector2.one;
            inner.offsetMin = new Vector2(InnerCardInset, InnerCardInset);
            inner.offsetMax = new Vector2(-InnerCardInset, -InnerCardInset);
            var innerImage = inner.gameObject.AddComponent<Image>();
            innerImage.sprite = HomeUiFonts.Rounded(InnerCardRadius);
            innerImage.type = Image.Type.Sliced;
            innerImage.color = CategoryInnerFill;
            innerImage.raycastTarget = false;

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
            return label;
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
            var inner = transform.Find("CategoryRow/CategoryValueCard")?.GetComponent<Image>();
            if (inner != null)
            {
                inner.color = CategoryInnerFill;
            }

            ApplyLabelFace(mapName, FontSize, Color.white);
            ApplyLabelFace(categoryValue, FontSize, CategoryValueColor);
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
