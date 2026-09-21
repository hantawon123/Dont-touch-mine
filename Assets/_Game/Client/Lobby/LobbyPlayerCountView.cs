using Game.Client.Home;
using Game.Core.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Lobby
{
    /// <summary>
    /// Compact "참여 플레이어 N/M" line under the lobby map card.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LobbyPlayerCountView : MonoBehaviour, ILobbyPlayerCountView
    {
        public const string RootName = "PlayerCount";
        public static string Caption =>
            UiTextCatalog.Shipped.Get(UiText.Lobby.PlayerCount, "ko");
        public const float FontSize = 20f;
        public const float GapBelowMatchInfo = 24f;
        public const float CaptionCountGap = 8f;

        public static float TopOffset => TopOffsetAt(1f);

        public static float TopOffsetAt(float matchInfoScale) =>
            LobbyMatchInfoView.MarginTop
            + (LobbyMatchInfoView.PanelHeight * matchInfoScale)
            + GapBelowMatchInfo;

        private TextMeshProUGUI caption;
        private TextMeshProUGUI count;

        public string CaptionText => caption != null ? caption.text : string.Empty;

        public string CountText => count != null ? count.text : string.Empty;

        public static LobbyPlayerCountView Create(Transform parent)
        {
            var root = new GameObject(RootName, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var view = root.AddComponent<LobbyPlayerCountView>();
            view.EnsureLayout();
            return view;
        }

        public static LobbyPlayerCountView Ensure(Transform parent)
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

            var view = existing.GetComponent<LobbyPlayerCountView>();
            if (view == null)
            {
                view = existing.gameObject.AddComponent<LobbyPlayerCountView>();
            }

            view.EnsureLayout();
            return view;
        }

        public void SetCount(int current, int max)
        {
            EnsureLayout();
            if (count != null)
            {
                count.text = $"{Mathf.Max(0, current)}/{Mathf.Max(0, max)}";
            }
        }

        private void Awake()
        {
            EnsureLayout();
        }

        public void EnsureLayout()
        {
            if (caption != null && count != null)
            {
                PlacePanel();
                ApplyStyle();
                return;
            }

            caption = transform.Find("Caption")?.GetComponent<TextMeshProUGUI>();
            count = transform.Find("Count")?.GetComponent<TextMeshProUGUI>();
            if (caption != null && count != null)
            {
                PlacePanel();
                ApplyStyle();
                return;
            }

            BuildLayout();
        }

        private void BuildLayout()
        {
            var layout = gameObject.GetComponent<HorizontalLayoutGroup>();
            if (layout == null)
            {
                layout = gameObject.AddComponent<HorizontalLayoutGroup>();
            }

            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.spacing = CaptionCountGap;

            var fitter = gameObject.GetComponent<ContentSizeFitter>();
            if (fitter == null)
            {
                fitter = gameObject.AddComponent<ContentSizeFitter>();
            }

            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            caption = CreateLabel("Caption", Caption, HomeUiFonts.ApplyBold());
            count = CreateLabel("Count", "0/0", HomeUiFonts.ApplyRegular());
            PlacePanel();
            ApplyStyle();
        }

        public void RefreshPlacement(float matchInfoScale = 1f)
        {
            var rect = (RectTransform)transform;
            rect.anchoredPosition = new Vector2(LobbyMatchInfoView.MarginLeft, -TopOffsetAt(matchInfoScale));
        }

        private void PlacePanel()
        {
            var rect = (RectTransform)transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            RefreshPlacement();
        }

        private void ApplyStyle()
        {
            if (caption != null)
            {
                caption.text = Caption;
                caption.font = HomeUiFonts.ApplyBold();
                caption.fontSize = FontSize;
                caption.fontStyle = FontStyles.Normal;
                caption.color = Color.white;
            }

            if (count != null)
            {
                count.font = HomeUiFonts.ApplyRegular();
                count.fontSize = FontSize;
                count.fontStyle = FontStyles.Normal;
                count.color = Color.white;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform);
        }

        private TextMeshProUGUI CreateLabel(string name, string content, TMP_FontAsset font)
        {
            var root = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            root.transform.SetParent(transform, false);
            var label = root.GetComponent<TextMeshProUGUI>();
            label.text = content;
            label.font = font != null ? font : HomeUiFonts.Apply();
            label.fontSize = FontSize;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            var size = root.AddComponent<ContentSizeFitter>();
            size.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            size.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return label;
        }
    }
}
