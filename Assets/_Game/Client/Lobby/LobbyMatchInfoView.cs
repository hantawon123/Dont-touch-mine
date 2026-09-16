using Game.Client.Home;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Lobby
{
    /// <summary>
    /// Read-only category and map cards in the lobby's top-left, lined up with
    /// the chat input. Each choice is a rounded box with a preview and a name.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LobbyMatchInfoView : MonoBehaviour
    {
        public const string RootName = "MatchInfo";
        public const float MarginTop = 60f;

        /// <summary>Same left inset as <see cref="Match.MatchChatView.Margin"/>.</summary>
        public const float MarginLeft = 24f;
        public const float Width = 320f;
        public const float FontSize = 18f;
        public const float Padding = 16f;
        public const float ContentSpacing = 12f;
        public const float MapRowPadding = 10f;
        public const float MapNameSpacing = 12f;
        public const float PlayerListGap = 24f;
        public const int PanelRadius = 16;
        public const int MapRowRadius = 12;
        public const int MapPreviewRadius = 8;
        public static readonly Vector2 MapPreviewSize = new Vector2(100f, 75f);

        public static readonly Color PanelFill = new Color(11f / 255f, 16f / 255f, 24f / 255f, 0.8f);
        public static readonly Color MapRowFill = new Color(1f, 1f, 1f, 0.14f);
        public static readonly Color MapPreviewFill = PlaySettingsStyle.Palette.MapPreview;

        public static float MapRowHeight => MapPreviewSize.y + (MapRowPadding * 2f);

        public static float PanelHeight =>
            Padding + MapRowHeight + ContentSpacing + MapRowHeight + Padding;

        public static float PlayerListTopOffset => MarginTop + PanelHeight + PlayerListGap;

        private TextMeshProUGUI categoryValue;
        private TextMeshProUGUI mapName;
        private Image categoryPreviewPhoto;
        private Image mapPreviewPhoto;

        public string CategoryLabel => categoryValue != null ? categoryValue.text : string.Empty;

        public string MapLabel => mapName != null ? mapName.text : string.Empty;

        /// <summary>지금 보이는 맵 사진. 사진이 없는 맵·랜덤이면 null.</summary>
        public Sprite MapPreviewSprite =>
            mapPreviewPhoto != null && mapPreviewPhoto.gameObject.activeSelf ? mapPreviewPhoto.sprite : null;

        /// <summary>지금 보이는 카테고리 사진. 사진이 없으면 null.</summary>
        public Sprite CategoryPreviewSprite =>
            categoryPreviewPhoto != null && categoryPreviewPhoto.gameObject.activeSelf
                ? categoryPreviewPhoto.sprite
                : null;

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
            SetInfo(categoryLabel, mapLabel, mapPreview, categoryPreview: null);
        }

        /// <param name="mapPreview">맵 사진. null이면 기존 단색 상자만 보인다.</param>
        /// <param name="categoryPreview">카테고리 사진. null이면 기존 단색 상자만 보인다.</param>
        public void SetInfo(
            string categoryLabel,
            string mapLabel,
            Sprite mapPreview,
            Sprite categoryPreview)
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
            MapPreviewSprites.Apply(categoryPreviewPhoto, categoryPreview);
        }

        private void Awake()
        {
            EnsureLayout();
        }

        private void EnsureLayout()
        {
            CacheRefs();
            if (HasBoxedLayout())
            {
                PlacePanel();
                return;
            }

            BuildLayout();
        }

        private bool HasBoxedLayout()
        {
            return categoryValue != null
                && mapName != null
                && transform.Find("CategoryRow/CategoryPreview") != null
                && transform.Find("MapRow/MapPreview") != null;
        }

        private void CacheRefs()
        {
            categoryValue = transform.Find("CategoryRow/CategoryValue")?.GetComponent<TextMeshProUGUI>();
            mapName = transform.Find("MapRow/MapName")?.GetComponent<TextMeshProUGUI>();
            categoryPreviewPhoto = FindPreviewPhoto("CategoryRow/CategoryPreview");
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
            categoryPreviewPhoto = null;
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

            categoryValue = CreateBoxedRow(
                "CategoryRow",
                "CategoryPreview",
                "CategoryValue",
                PlaySettingsCategoryCatalog.Default.Label,
                out categoryPreviewPhoto);
            PlaceTopRow((RectTransform)categoryValue.transform.parent, Padding, MapRowHeight);

            mapName = CreateBoxedRow(
                "MapRow",
                "MapPreview",
                "MapName",
                PlaySettingsMapCatalog.Default.Label,
                out mapPreviewPhoto);
            PlaceFillRow(
                (RectTransform)mapName.transform.parent,
                new Vector2(Padding, Padding),
                new Vector2(-Padding, -(Padding + MapRowHeight + ContentSpacing)));
        }

        private TextMeshProUGUI CreateBoxedRow(
            string rowName,
            string previewName,
            string labelName,
            string defaultLabel,
            out Image previewPhoto)
        {
            var row = CreateRect(rowName, (RectTransform)transform);
            var rowImage = row.gameObject.AddComponent<Image>();
            rowImage.sprite = HomeUiFonts.Rounded(MapRowRadius);
            rowImage.type = Image.Type.Sliced;
            rowImage.color = MapRowFill;
            rowImage.raycastTarget = false;

            var preview = CreateRect(previewName, row);
            preview.anchorMin = preview.anchorMax = new Vector2(0f, 0.5f);
            preview.pivot = new Vector2(0f, 0.5f);
            preview.sizeDelta = MapPreviewSize;
            preview.anchoredPosition = new Vector2(MapRowPadding, 0f);
            var previewImage = preview.gameObject.AddComponent<Image>();
            previewImage.sprite = HomeUiFonts.Rounded(MapPreviewRadius);
            previewImage.type = Image.Type.Sliced;
            previewImage.color = MapPreviewFill;
            previewImage.raycastTarget = false;
            previewPhoto = MapPreviewSprites.FindPhoto(previewImage) ?? MapPreviewSprites.AttachPhoto(previewImage);

            var label = CreateLabel(
                row,
                labelName,
                defaultLabel,
                HomeUiFonts.ApplyRegular(),
                TextAlignmentOptions.MidlineLeft);
            var labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(
                MapRowPadding + MapPreviewSize.x + MapNameSpacing,
                0f);
            labelRect.offsetMax = new Vector2(-MapRowPadding, 0f);
            return label;
        }

        private static void PlaceTopRow(RectTransform row, float top, float height)
        {
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.offsetMin = new Vector2(Padding, -(top + height));
            row.offsetMax = new Vector2(-Padding, -top);
        }

        private static void PlaceFillRow(RectTransform row, Vector2 offsetMin, Vector2 offsetMax)
        {
            row.anchorMin = Vector2.zero;
            row.anchorMax = Vector2.one;
            row.pivot = new Vector2(0.5f, 0.5f);
            row.offsetMin = offsetMin;
            row.offsetMax = offsetMax;
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
            TextAlignmentOptions alignment)
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
            label.fontSize = FontSize;
            label.fontStyle = FontStyles.Normal;
            label.color = Color.white;
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
