using Game.Client.Home;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Lobby
{
    /// <summary>
    /// Read-only category and map card in the lobby's top-left, lined up with
    /// the chat input.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LobbyMatchInfoView : MonoBehaviour
    {
        public const string RootName = "MatchInfo";
        public const string CategoryCaption = "카테고리";
        public const float MarginTop = 60f;

        /// <summary>Same left inset as <see cref="Match.MatchChatView.Margin"/>.</summary>
        public const float MarginLeft = 24f;
        public const float Width = 320f;
        public const float FontSize = 18f;
        public const float Padding = 16f;
        public const float CategoryRowHeight = 28f;
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
            Padding + CategoryRowHeight + ContentSpacing + MapRowHeight + Padding;

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
            if (categoryCaption != null && categoryValue != null && mapName != null)
            {
                PlacePanel();
                return;
            }

            CacheRefs();
            if (categoryCaption != null && categoryValue != null && mapName != null)
            {
                PlacePanel();
                return;
            }

            BuildLayout();
        }

        private void CacheRefs()
        {
            categoryCaption = transform.Find("CategoryRow/CategoryCaption")?.GetComponent<TextMeshProUGUI>();
            categoryValue = transform.Find("CategoryRow/CategoryValue")?.GetComponent<TextMeshProUGUI>();
            mapName = transform.Find("MapRow/MapName")?.GetComponent<TextMeshProUGUI>();
            // 이미 만들어진 카드(사진 자식이 없던 예전 레이아웃 포함)에도 사진 자리를 붙인다.
            var preview = transform.Find("MapRow/MapPreview")?.GetComponent<Image>();
            mapPreviewPhoto = preview != null
                ? MapPreviewSprites.FindPhoto(preview) ?? MapPreviewSprites.AttachPhoto(preview)
                : null;
        }

        private void BuildLayout()
        {
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                if (Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }

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

            var categoryRow = CreateRect("CategoryRow", (RectTransform)transform);
            categoryRow.anchorMin = new Vector2(0f, 1f);
            categoryRow.anchorMax = new Vector2(1f, 1f);
            categoryRow.pivot = new Vector2(0.5f, 1f);
            categoryRow.offsetMin = new Vector2(Padding, -(Padding + CategoryRowHeight));
            categoryRow.offsetMax = new Vector2(-Padding, -Padding);

            categoryCaption = CreateLabel(
                categoryRow,
                "CategoryCaption",
                CategoryCaption,
                HomeUiFonts.ApplyRegular(),
                TextAlignmentOptions.MidlineLeft);
            Stretch(categoryCaption.rectTransform);
            categoryCaption.rectTransform.offsetMax = new Vector2(-80f, 0f);

            categoryValue = CreateLabel(
                categoryRow,
                "CategoryValue",
                PlaySettingsCategoryCatalog.Default.Label,
                HomeUiFonts.Apply(),
                TextAlignmentOptions.MidlineRight);
            Stretch(categoryValue.rectTransform);
            categoryValue.rectTransform.offsetMin = new Vector2(80f, 0f);

            var mapRow = CreateRect("MapRow", (RectTransform)transform);
            mapRow.anchorMin = Vector2.zero;
            mapRow.anchorMax = Vector2.one;
            mapRow.pivot = new Vector2(0.5f, 0.5f);
            mapRow.offsetMin = new Vector2(Padding, Padding);
            mapRow.offsetMax = new Vector2(
                -Padding,
                -(Padding + CategoryRowHeight + ContentSpacing));
            var mapRowImage = mapRow.gameObject.AddComponent<Image>();
            mapRowImage.sprite = HomeUiFonts.Rounded(MapRowRadius);
            mapRowImage.type = Image.Type.Sliced;
            mapRowImage.color = MapRowFill;
            mapRowImage.raycastTarget = false;

            var preview = CreateRect("MapPreview", mapRow);
            preview.anchorMin = preview.anchorMax = new Vector2(0f, 0.5f);
            preview.pivot = new Vector2(0f, 0.5f);
            preview.sizeDelta = MapPreviewSize;
            preview.anchoredPosition = new Vector2(MapRowPadding, 0f);
            var previewImage = preview.gameObject.AddComponent<Image>();
            previewImage.sprite = HomeUiFonts.Rounded(MapPreviewRadius);
            previewImage.type = Image.Type.Sliced;
            previewImage.color = MapPreviewFill;
            previewImage.raycastTarget = false;
            mapPreviewPhoto = MapPreviewSprites.FindPhoto(previewImage) ?? MapPreviewSprites.AttachPhoto(previewImage);

            mapName = CreateLabel(
                mapRow,
                "MapName",
                PlaySettingsMapCatalog.Default.Label,
                HomeUiFonts.ApplyRegular(),
                TextAlignmentOptions.MidlineLeft);
            var mapNameRect = mapName.rectTransform;
            mapNameRect.anchorMin = Vector2.zero;
            mapNameRect.anchorMax = Vector2.one;
            mapNameRect.offsetMin = new Vector2(
                MapRowPadding + MapPreviewSize.x + MapNameSpacing,
                0f);
            mapNameRect.offsetMax = new Vector2(-MapRowPadding, 0f);
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

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }
    }
}
