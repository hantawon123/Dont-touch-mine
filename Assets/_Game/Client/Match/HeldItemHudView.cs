using System;
using Game.Client.Common;
using Game.Client.Home;
using Game.Client.Interactions;
using Game.Client.Settings;
using Game.Core.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Match
{
    /// <summary>Camera-owned, bottom-left portrait of the actual carried prop.</summary>
    public sealed class HeldItemHudView : IDisposable
    {
        private GameObject root;
        private CanvasScaler scaler;
        private HidingIntroItemPreview preview;
        private CarryableItem shownItem;
        private RectTransform circleRect;
        private MatchChatView chat;
        private float nextChatScan;
        private readonly Vector3[] chatCorners = new Vector3[4];

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
            PlaceAboveChat();
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
        }

        private void PlaceAboveChat()
        {
            if (chat == null && Time.unscaledTime >= nextChatScan)
            {
                chat = UnityEngine.Object.FindFirstObjectByType<MatchChatView>();
                nextChatScan = Time.unscaledTime + 0.5f;
            }

            var bottom = 36f;
            if (chat != null && chat.isActiveAndEnabled && chat.transform is RectTransform chatRect &&
                chatRect.rect.height > 0f)
            {
                chatRect.GetWorldCorners(chatCorners);
                var canvasRect = (RectTransform)root.transform;
                // Chat uses its own overlay canvas and can have a different UI scale.
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, chatCorners[1], null, out var top))
                    bottom = Mathf.Max(bottom, top.y - canvasRect.rect.yMin + 16f);
            }
            circleRect.anchoredPosition = new Vector2(DestroyedItemsHudView.LeftPadding, bottom);
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

            var circle = new GameObject("Circle", typeof(RectTransform), typeof(Image), typeof(Mask));
            circle.transform.SetParent(root.transform, false);
            var rect = (RectTransform)circle.transform;
            circleRect = rect;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(DestroyedItemsHudView.LeftPadding, 36f);
            rect.sizeDelta = Vector2.one * DestroyedItemsHudView.SlotSize;
            var background = circle.GetComponent<Image>();
            background.sprite = HomeUiFonts.CircleSprite;
            background.color = DestroyedItemsHudView.SlotColor;
            background.raycastTarget = false;
            circle.GetComponent<Mask>().showMaskGraphic = true;

            var imageObject = new GameObject("Preview", typeof(RectTransform), typeof(RawImage));
            imageObject.transform.SetParent(circle.transform, false);
            var image = imageObject.GetComponent<RawImage>();
            image.raycastTarget = false;
            image.enabled = false;
            image.rectTransform.anchorMin = Vector2.zero;
            image.rectTransform.anchorMax = Vector2.one;
            image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
            preview = new HidingIntroItemPreview(image, DestroyedItemsHudView.PreviewTextureSize,
                Color.clear, Vector3.left * 20f, rotates: false);
        }
    }
}
