using System;
using Game.Client.Home;
using Game.Core.Lobby;
using Game.Core.Match;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Match
{
    public interface IDestroyedItemsHudView
    {
        void Show(
            int playerCount,
            System.Collections.Generic.IReadOnlyList<PlayerItemStatusSnapshot> statuses,
            string localItemId,
            System.Collections.Generic.IReadOnlyList<string> destroyedItemIdsInOrder);
        void Hide();
    }

    /// <summary>
    /// Top-left circles for assignment items. The local item stays leftmost
    /// with an orange ring. Remaining player circles show "?" until they
    /// fill in destruction order.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DestroyedItemsHudView : MonoBehaviour, IDestroyedItemsHudView
    {
        public const float SlotSize = 100f;
        public const int PreviewTextureSize = 256;
        public const float SlotGap = 12f;
        public const float QuestionFontSize = 30f;
        public const float LeftPadding = 36f;
        public const float TopPadding = 36f;
        public const float OwnBorderThickness = 6f;
        public const string QuestionMark = "?";
        public const string OwnBorderName = "OwnBorder";
        public const string FillName = "Fill";

        public static readonly Color SlotColor = new Color(0f, 0f, 0f, 0.6f);
        public static readonly Color QuestionColor = new Color(200f / 255f, 200f / 255f, 200f / 255f, 1f);
        public static readonly Color OwnBorderColor = new Color(1f, 140f / 255f, 0f, 1f);

        [SerializeField]
        private GameObject panel;

        [SerializeField]
        private RectTransform slotRoot;

        private Slot[] slots = System.Array.Empty<Slot>();
        private DestroyedItemHudSlot[] laidOutSlots = System.Array.Empty<DestroyedItemHudSlot>();
        private bool retryPreviews;
        private static Sprite ownBorderSprite;

        public static DestroyedItemsHudView Create(Transform parent)
        {
            var rootObject = new GameObject("DestroyedItems", typeof(RectTransform));
            rootObject.transform.SetParent(parent, false);
            Stretch((RectTransform)rootObject.transform);
            return rootObject.AddComponent<DestroyedItemsHudView>();
        }

        private void Awake()
        {
            EnsureLayout();
            if (panel != null)
            {
                panel.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            DisposeSlots();
        }

        private void LateUpdate()
        {
            if (!retryPreviews)
            {
                return;
            }

            var missingPreview = false;
            for (var index = 0; index < slots.Length; index++)
            {
                if (index < laidOutSlots.Length &&
                    !slots[index].Set(laidOutSlots[index]))
                {
                    missingPreview = true;
                }
            }

            retryPreviews = missingPreview;
        }

        public void Show(
            int playerCount,
            System.Collections.Generic.IReadOnlyList<PlayerItemStatusSnapshot> statuses)
        {
            Show(playerCount, statuses, null, null);
        }

        public void Show(
            int playerCount,
            System.Collections.Generic.IReadOnlyList<PlayerItemStatusSnapshot> statuses,
            string localItemId,
            System.Collections.Generic.IReadOnlyList<string> destroyedItemIdsInOrder)
        {
            EnsureLayout();
            laidOutSlots = DestroyedItemsHudLayout.Build(
                playerCount,
                statuses,
                localItemId,
                destroyedItemIdsInOrder);
            if (laidOutSlots.Length == 0)
            {
                Hide();
                return;
            }

            EnsureSlots(laidOutSlots.Length);
            retryPreviews = false;
            for (var index = 0; index < slots.Length; index++)
            {
                var slot = index < laidOutSlots.Length
                    ? laidOutSlots[index]
                    : default;
                if (!slots[index].Set(slot))
                {
                    retryPreviews = true;
                }
            }

            if (panel != null)
            {
                panel.SetActive(true);
            }

            transform.SetAsLastSibling();
        }

        public void Hide()
        {
            if (panel != null)
            {
                panel.SetActive(false);
            }
        }

        private void EnsureLayout()
        {
            if (panel == null || slotRoot == null)
            {
                var panelRect = CreateRect(transform, "Panel");
                panelRect.anchorMin = new Vector2(0f, 1f);
                panelRect.anchorMax = new Vector2(0f, 1f);
                panelRect.pivot = new Vector2(0f, 1f);
                var layout = panelRect.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = SlotGap;
                layout.childAlignment = TextAnchor.MiddleLeft;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
                layout.reverseArrangement = false;
                panel = panelRect.gameObject;
                slotRoot = panelRect;
            }

            slotRoot.anchoredPosition = new Vector2(LeftPadding, -TopPadding);
            slotRoot.sizeDelta = new Vector2(
                (SlotSize * RoomSettings.MaxPlayerCount) + (SlotGap * (RoomSettings.MaxPlayerCount - 1)),
                SlotSize);
        }

        private void EnsureSlots(int count)
        {
            if (count < 0)
            {
                count = 0;
            }

            if (!SlotsMatchSize())
            {
                DisposeSlots();
            }

            if (slots.Length == count)
            {
                ApplySlotOrder();
                return;
            }

            if (slots.Length > count)
            {
                for (var index = count; index < slots.Length; index++)
                {
                    slots[index]?.Dispose();
                }

                var kept = new Slot[count];
                for (var index = 0; index < count; index++)
                {
                    kept[index] = slots[index];
                }

                slots = kept;
                ApplySlotOrder();
                return;
            }

            var grown = new Slot[count];
            for (var index = 0; index < slots.Length; index++)
            {
                grown[index] = slots[index];
            }

            for (var index = slots.Length; index < count; index++)
            {
                grown[index] = Slot.Create(slotRoot, index);
            }

            slots = grown;
            ApplySlotOrder();
        }

        private void ApplySlotOrder()
        {
            for (var index = 0; index < slots.Length; index++)
            {
                slots[index]?.SetSiblingIndex(index);
            }
        }

        private void DisposeSlots()
        {
            for (var index = 0; index < slots.Length; index++)
            {
                slots[index]?.Dispose();
            }

            slots = Array.Empty<Slot>();
            if (slotRoot == null)
            {
                return;
            }

            for (var index = slotRoot.childCount - 1; index >= 0; index--)
            {
                var child = slotRoot.GetChild(index);
                if (child != null && child.name.StartsWith("Slot", StringComparison.Ordinal))
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
            }
        }

        private bool SlotsMatchSize()
        {
            if (slotRoot == null)
            {
                return false;
            }

            var slot = slotRoot.Find("Slot0");
            var layout = slot?.GetComponent<LayoutElement>();
            return layout != null &&
                   slot.Find(FillName) != null &&
                   slot.Find(OwnBorderName) != null &&
                   Mathf.Approximately(layout.preferredWidth, SlotSize) &&
                   Mathf.Approximately(layout.preferredHeight, SlotSize);
        }

        private static RectTransform CreateRect(Transform parent, string name)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject.GetComponent<RectTransform>();
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Sprite OwnBorderSprite
        {
            get
            {
                if (ownBorderSprite != null)
                {
                    return ownBorderSprite;
                }

                const int size = 128;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear
                };
                var center = (size - 1) * 0.5f;
                var outer = center - 1f;
                var inner = outer - (OwnBorderThickness / SlotSize * size);
                var outerSq = outer * outer;
                var innerSq = inner * inner;
                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        var dx = x - center;
                        var dy = y - center;
                        var distanceSq = (dx * dx) + (dy * dy);
                        texture.SetPixel(x, y, distanceSq <= outerSq && distanceSq >= innerSq
                            ? Color.white
                            : Color.clear);
                    }
                }

                texture.Apply(false, false);
                ownBorderSprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, size, size),
                    new Vector2(0.5f, 0.5f),
                    100f);
                ownBorderSprite.hideFlags = HideFlags.HideAndDontSave;
                return ownBorderSprite;
            }
        }

        private sealed class Slot
        {
            private readonly GameObject root;
            private readonly Image ownBorder;
            private readonly RectTransform fillRect;
            private readonly TMP_Text question;
            private readonly RawImage previewImage;
            private readonly HidingIntroItemPreview preview;

            private Slot(
                GameObject root,
                Image ownBorder,
                RectTransform fillRect,
                TMP_Text question,
                RawImage previewImage,
                HidingIntroItemPreview preview)
            {
                this.root = root;
                this.ownBorder = ownBorder;
                this.fillRect = fillRect;
                this.question = question;
                this.previewImage = previewImage;
                this.preview = preview;
            }

            public static Slot Create(Transform parent, int index)
            {
                var root = new GameObject($"Slot{index}", typeof(RectTransform), typeof(LayoutElement));
                root.transform.SetParent(parent, false);
                var layout = root.GetComponent<LayoutElement>();
                layout.preferredWidth = SlotSize;
                layout.preferredHeight = SlotSize;
                layout.minWidth = SlotSize;
                layout.minHeight = SlotSize;

                var fill = new GameObject(
                    FillName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Mask));
                fill.transform.SetParent(root.transform, false);
                Stretch((RectTransform)fill.transform);
                var fillImage = fill.GetComponent<Image>();
                fillImage.sprite = HomeUiFonts.CircleSprite;
                fillImage.color = SlotColor;
                fillImage.raycastTarget = false;
                fill.GetComponent<Mask>().showMaskGraphic = true;

                var borderObject = new GameObject(
                    OwnBorderName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                borderObject.transform.SetParent(root.transform, false);
                Stretch((RectTransform)borderObject.transform);
                var ownBorder = borderObject.GetComponent<Image>();
                ownBorder.sprite = OwnBorderSprite;
                ownBorder.color = OwnBorderColor;
                ownBorder.raycastTarget = false;
                ownBorder.enabled = false;
                borderObject.SetActive(false);

                var questionObject = new GameObject(
                    "Question",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(TextMeshProUGUI));
                questionObject.transform.SetParent(fill.transform, false);
                Stretch((RectTransform)questionObject.transform);
                var question = questionObject.GetComponent<TextMeshProUGUI>();
                question.text = QuestionMark;
                question.font = HomeUiFonts.Apply();
                question.fontSize = QuestionFontSize;
                question.fontStyle = FontStyles.Normal;
                question.alignment = TextAlignmentOptions.Center;
                question.color = QuestionColor;
                question.raycastTarget = false;
                question.textWrappingMode = TextWrappingModes.NoWrap;

                var previewObject = new GameObject(
                    "Preview",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(RawImage));
                previewObject.transform.SetParent(fill.transform, false);
                Stretch((RectTransform)previewObject.transform);
                var previewImage = previewObject.GetComponent<RawImage>();
                previewImage.color = Color.white;
                previewImage.raycastTarget = false;
                previewImage.enabled = false;
                var preview = new HidingIntroItemPreview(
                    previewImage,
                    PreviewTextureSize,
                    new Color(0f, 0f, 0f, 0f),
                    Vector3.right * (20f * (index + 1)),
                    rotates: false);
                return new Slot(
                    root,
                    ownBorder,
                    (RectTransform)fill.transform,
                    question,
                    previewImage,
                    preview);
            }

            private string shownItemId;

            public void SetSiblingIndex(int index)
            {
                if (root != null)
                {
                    root.transform.SetSiblingIndex(index);
                }
            }

            public bool Set(DestroyedItemHudSlot slot)
            {
                SetOwnBorder(slot.IsOwn);
                if (slot.ShowPreview && !string.IsNullOrWhiteSpace(slot.ItemId))
                {
                    if (!string.Equals(shownItemId, slot.ItemId, System.StringComparison.Ordinal))
                    {
                        preview.Show(slot.ItemId);
                        shownItemId = preview.HasPreview ? slot.ItemId : null;
                    }

                    var shown = preview.HasPreview;
                    previewImage.enabled = shown;
                    previewImage.material = null;
                    preview.SetGrayscale(slot.Grayscale);
                    question.gameObject.SetActive(!shown);
                    return shown;
                }

                shownItemId = null;
                preview.Clear();
                previewImage.enabled = false;
                previewImage.material = null;
                question.gameObject.SetActive(true);
                return true;
            }

            public void Dispose()
            {
                preview.Dispose();
                if (root != null)
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }

            private void SetOwnBorder(bool isOwn)
            {
                ownBorder.enabled = isOwn;
                ownBorder.gameObject.SetActive(isOwn);
                fillRect.offsetMin = Vector2.zero;
                fillRect.offsetMax = Vector2.zero;
            }

        }
    }
}
