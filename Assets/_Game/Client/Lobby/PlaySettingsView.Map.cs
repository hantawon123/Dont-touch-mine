using System.Collections.Generic;
using Game.Core.Lobby;
using Game.Core.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Lobby
{
    public sealed partial class PlaySettingsView
    {
        private ScrollRect mapScroll;
        private RectTransform mapContent;
        private readonly List<Image> mapSlotImages = new();
        private readonly List<Button> mapSlotButtons = new();

        private void CacheMapScrollRefs()
        {
            if (panel == null)
            {
                return;
            }

            var scrollTransform = panel.transform.Find(
                "Body/SettingsScroll/Viewport/Content/MapArea/MapSelect/MapScroll");
            if (scrollTransform == null)
            {
                return;
            }

            mapScroll = scrollTransform.GetComponent<ScrollRect>();
            mapContent = scrollTransform.Find("Viewport/MapContent") as RectTransform;
        }

        private void EnsureMapUiReady()
        {
            if (mapContent == null)
            {
                CacheMapScrollRefs();
            }

            EnsureMapSlotsBuilt();
        }

        private void StepMapSelection(int direction)
        {
            if (!editable || mapOptions.Count <= 1)
            {
                return;
            }

            SelectMap((selectedMapIndex + direction + mapOptions.Count) % mapOptions.Count);
        }

        private void SelectMap(int index)
        {
            if (!editable || index < 0 || index >= mapOptions.Count)
            {
                return;
            }

            selectedMapIndex = index;
            RefreshMapSelection(scrollIntoView: true);
            RefreshApplyChrome();
        }

        private void RefreshMapSelection(bool scrollIntoView)
        {
            if (mapOptions.Count == 0)
            {
                return;
            }

            selectedMapIndex = Mathf.Clamp(selectedMapIndex, 0, mapOptions.Count - 1);
            var selected = mapOptions[selectedMapIndex];

            if (mapNameText == null && settingsContent != null)
            {
                CacheMapAreaRefs(settingsContent);
            }

            if (mapNameText != null)
            {
                mapNameText.text = PlaySettingsMapCatalog.LabelOf(selected.Id, Language);
            }

            if (mapPreviewImage != null)
            {
                mapPreviewImage.color = PlaySettingsStyle.Palette.MapPreview;
                if (mapPreviewPhoto == null)
                {
                    mapPreviewPhoto = MapPreviewSprites.FindPhoto(mapPreviewImage);
                }

                MapPreviewSprites.Apply(
                    mapPreviewPhoto,
                    selected.IsRandom ? null : MapPreviewSprites.For(selected.Id));
                MapPreviewSprites.ApplyRandomMark(mapPreviewImage, selected.IsRandom);
            }

            for (var i = 0; i < mapSlotImages.Count; i++)
            {
                var image = mapSlotImages[i];
                if (image == null)
                {
                    continue;
                }

                var selectedSlot = i == selectedMapIndex;
                image.color = selectedSlot
                    ? PlaySettingsStyle.MapSlotPalette.Selected
                    : PlaySettingsStyle.MapSlotPalette.Normal;

                var randomSlot = mapOptions[i].IsRandom;
                // 슬롯에도 같은 사진을 작게 넣는다. 선택 안 된 슬롯은 살짝 어둡게 해 선택 슬롯과 구분한다.
                MapPreviewSprites.Apply(
                    MapPreviewSprites.FindPhoto(image),
                    randomSlot ? null : MapPreviewSprites.For(mapOptions[i].Id),
                    selectedSlot ? Color.white : MapPreviewSprites.DimmedTint);
                MapPreviewSprites.ApplyRandomMark(
                    image,
                    randomSlot,
                    MapPreviewSprites.RandomMarkSlotFontSize);

                var outline = image.transform.Find("Selection");
                if (outline != null)
                {
                    outline.gameObject.SetActive(selectedSlot);
                }
            }

            RefreshMapArrows();

            if (scrollIntoView)
            {
                ScrollSelectedIntoView();
            }
        }

        private void RefreshMapArrows()
        {
            var showArrows = mapOptions.Count > 1;
            if (mapPrevButton != null)
            {
                mapPrevButton.gameObject.SetActive(showArrows);
                mapPrevButton.interactable = editable && showArrows;
            }

            if (mapNextButton != null)
            {
                mapNextButton.gameObject.SetActive(showArrows);
                mapNextButton.interactable = editable && showArrows;
            }
        }

        private void ScrollSelectedIntoView()
        {
            if (mapScroll == null || mapContent == null || mapOptions.Count <= 1)
            {
                return;
            }

            var viewport = mapScroll.viewport != null
                ? mapScroll.viewport.rect.width
                : mapScroll.GetComponent<RectTransform>().rect.width;
            var contentWidth = mapContent.rect.width;
            if (contentWidth <= viewport)
            {
                mapScroll.horizontalNormalizedPosition = 0f;
                return;
            }

            var slotCenter = selectedMapIndex *
                             (PlaySettingsStyle.Layout.MapSlotSize + PlaySettingsStyle.Layout.MapSlotSpacing) +
                             (PlaySettingsStyle.Layout.MapSlotSize * 0.5f);
            var target = (slotCenter - (viewport * 0.5f)) / (contentWidth - viewport);
            mapScroll.horizontalNormalizedPosition = Mathf.Clamp01(target);
        }

        private void EnsureMapSlotsBuilt()
        {
            if (mapContent == null)
            {
                return;
            }

            if (mapSlotButtons.Count == mapOptions.Count && mapSlotImages.Count == mapOptions.Count)
            {
                return;
            }

            UnbindMapSlots();
            for (var i = mapContent.childCount - 1; i >= 0; i--)
            {
                var child = mapContent.GetChild(i);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }

            mapSlotImages.Clear();
            mapSlotButtons.Clear();

            var slotSize = PlaySettingsStyle.Layout.MapSlotSize;
            var slotSpacing = PlaySettingsStyle.Layout.MapSlotSpacing;
            var width = (mapOptions.Count * slotSize) + (Mathf.Max(0, mapOptions.Count - 1) * slotSpacing);
            mapContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            mapContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, slotSize);

            for (var i = 0; i < mapOptions.Count; i++)
            {
                var slot = CreateMapSlot(mapContent, i);
                mapSlotImages.Add(slot.GetComponent<Image>());
                mapSlotButtons.Add(slot.GetComponent<Button>());
            }

            BindMapSlots();
        }

        private static RectTransform CreateMapSlot(RectTransform parent, int index)
        {
            var slotSize = PlaySettingsStyle.Layout.MapSlotSize;
            var slotSpacing = PlaySettingsStyle.Layout.MapSlotSpacing;
            var go = new GameObject(
                $"MapSlot{index}",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(slotSize, slotSize);
            rect.anchoredPosition = new Vector2(index * (slotSize + slotSpacing), 0f);
            var slotImage = go.GetComponent<Image>();
            slotImage.color = PlaySettingsStyle.MapSlotPalette.Normal;
            // 정사각 슬롯 안에 4:3 사진을 늘리지 않고 가운데 맞춘다. 위아래 남는 띠는 슬롯 색이 채운다.
            MapPreviewSprites.AttachPhoto(slotImage, new Vector2(slotSize, slotSize / MapPreviewSprites.Aspect));

            var selectionGo = new GameObject("Selection", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            selectionGo.transform.SetParent(go.transform, false);
            var selectionRect = selectionGo.GetComponent<RectTransform>();
            selectionRect.anchorMin = Vector2.zero;
            selectionRect.anchorMax = Vector2.one;
            selectionRect.offsetMin = new Vector2(-4f, -4f);
            selectionRect.offsetMax = new Vector2(4f, 4f);
            selectionRect.SetAsFirstSibling();
            var selectionImage = selectionGo.GetComponent<Image>();
            selectionImage.color = PlaySettingsStyle.MapSlotPalette.SelectionOutline;
            selectionImage.raycastTarget = false;
            selectionGo.SetActive(false);
            return rect;
        }

        private void BindMapSlots()
        {
            for (var i = 0; i < mapSlotButtons.Count; i++)
            {
                var index = i;
                Bind(mapSlotButtons[i], () => SelectMap(index));
            }
        }

        private void UnbindMapSlots()
        {
            for (var i = 0; i < mapSlotButtons.Count; i++)
            {
                Unbind(mapSlotButtons[i]);
            }
        }
    }
}
