using System;
using Game.Client.Common;
using Game.Client.Home;
using Game.Client.Interactions;
using Game.Client.Settings;
using Game.Core.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Match
{
    /// <summary>Camera-owned item card immediately left of the bottom-center vitals.</summary>
    public sealed class HeldItemHudView : IDisposable
    {
        public static readonly Vector2 CardSize = new(188f, 184f);
        public const float VitalsGap = 16f;
        private const float Padding = 12f;
        private const float CaptionHeight = 28f;

        private GameObject root;
        private CanvasScaler scaler;
        private HidingIntroItemPreview preview;
        private CarryableItem shownItem;
        private TMP_Text caption;
        private string captionLanguage;

        public void Apply(Transform owner, bool firstPerson, CarryableItem item,
            InterfaceSettingsSystem settings = null)
        {
            if (!firstPerson || item == null ||
                (settings != null && !settings.Current.IsOn(InterfaceOption.InGameUi)))
            {
                Hide();
                return;
            }

            EnsureLayout(owner);
            scaler.referenceResolution = HudScreenScale.ScaledReference /
                InterfaceHudView.HudScale(settings?.Current.Get(InterfaceOption.UiScale));
            root.SetActive(true);
            RefreshCaption(settings);
            if (shownItem != item)
            {
                preview.ShowVisual(item.transform);
                shownItem = item;
            }
        }

        public void Hide()
        {
            if (root == null || !root.activeSelf) return;
            root.SetActive(false);
            shownItem = null;
            preview.Clear();
        }

        public void Dispose()
        {
            preview?.Dispose();
            preview = null;
            shownItem = null;
            if (root != null) UnityEngine.Object.Destroy(root);
            root = null;
            caption = null;
            captionLanguage = null;
        }

        private void RefreshCaption(InterfaceSettingsSystem settings)
        {
            var language = UiLocale.AppliedLanguage;
            if (captionLanguage != language)
            {
                caption.text = UiLocale.Applied(UiText.Match.HeldItem);
                caption.font = HomeUiFonts.Apply();
                captionLanguage = language;
            }
            var fontSize = 22f * InterfaceHudView.Scale(settings?.Current.Get(InterfaceOption.FontScale));
            if (!Mathf.Approximately(caption.fontSizeMax, fontSize))
            {
                caption.fontSizeMax = fontSize;
                caption.fontSize = fontSize;
            }
        }

        private void EnsureLayout(Transform owner)
        {
            if (root != null) return;
            root = new GameObject("HeldItemHud", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(owner, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            scaler = HudScreenScale.Ensure(root);

            var card = new GameObject("Card", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(root.transform, false);
            var rect = (RectTransform)card.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(
                -(MatchVitalsHudView.PanelWidth * 0.5f + VitalsGap), MatchVitalsHudView.BottomPadding);
            rect.sizeDelta = CardSize;
            var background = card.GetComponent<Image>();
            background.sprite = HomeUiFonts.Rounded(16);
            background.type = Image.Type.Sliced;
            background.color = MatchVitalsHudView.PanelColor;
            background.raycastTarget = false;

            var borderObject = new GameObject("Border", typeof(RectTransform), typeof(Image));
            borderObject.transform.SetParent(card.transform, false);
            var border = borderObject.GetComponent<Image>();
            border.sprite = HomeUiFonts.Outline(16);
            border.type = Image.Type.Sliced;
            border.color = new Color(1f, 0.94f, 0.86f, 0.65f);
            border.raycastTarget = false;
            border.rectTransform.anchorMin = Vector2.zero;
            border.rectTransform.anchorMax = Vector2.one;
            border.rectTransform.offsetMin = border.rectTransform.offsetMax = Vector2.zero;

            var labelObject = new GameObject("Caption", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(card.transform, false);
            caption = labelObject.GetComponent<TextMeshProUGUI>();
            caption.color = Color.white;
            caption.raycastTarget = false;
            caption.alignment = TextAlignmentOptions.MidlineLeft;
            caption.textWrappingMode = TextWrappingModes.NoWrap;
            caption.enableAutoSizing = true;
            caption.fontSizeMin = 16f;
            caption.rectTransform.anchorMin = caption.rectTransform.anchorMax = new Vector2(0f, 1f);
            caption.rectTransform.pivot = new Vector2(0f, 1f);
            caption.rectTransform.anchoredPosition = new Vector2(Padding, -8f);
            caption.rectTransform.sizeDelta = new Vector2(CardSize.x - Padding * 2f, CaptionHeight);

            var imageObject = new GameObject("Preview", typeof(RectTransform), typeof(RawImage));
            imageObject.transform.SetParent(card.transform, false);
            var image = imageObject.GetComponent<RawImage>();
            image.raycastTarget = false;
            image.enabled = false;
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            image.rectTransform.pivot = new Vector2(0.5f, 0f);
            image.rectTransform.anchoredPosition = new Vector2(0f, 8f);
            image.rectTransform.sizeDelta = new Vector2(CardSize.x - Padding * 2f, 140f);
            preview = new HidingIntroItemPreview(image, DestroyedItemsHudView.PreviewTextureSize,
                Color.clear, Vector3.left * 20f, rotates: false);
        }
    }
}
