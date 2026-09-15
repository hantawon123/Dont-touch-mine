using Game.Client.Home;
using Game.Client.Voice;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Lobby
{
    /// <summary>
    /// Always-on 1 / 2 / Esc shortcut row in the lobby's bottom-right corner,
    /// with the local microphone toggle to the right of 환경설정.
    /// Right edge lines up with <see cref="KeySettingGuideView"/>; the keys
    /// themselves are handled by <see cref="LobbyPauseMenuPresenter"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LobbyShortcutGuideView : MonoBehaviour
    {
        public const string RootName = "ShortcutGuide";
        public const float MarginRight = KeySettingGuideView.MarginRight;
        public const float MarginBottom = 34f;
        public const float KeyBoxSize = 50f;
        public const int KeyBoxRadius = 10;
        public const float KeyLabelGap = 12f;
        public const float ItemSpacing = 24f;
        public const float KeyFontSize = 18f;
        public const float ActionFontSize = 20f;
        public static readonly Color KeyBoxColor = new Color(0f, 0f, 0f, 0.6f);

        public static readonly string[] KeyLabels = { "1", "2", "ESC" };

        public static readonly string[] Actions =
        {
            "캐릭터 설정",
            "플레이어",
            "환경설정"
        };

        public const string VoiceButtonName = VoiceView.ButtonName;
        public const string SpeakerButtonName = VoiceView.SpeakerButtonName;
        public const string VoiceIconName = VoiceView.IconName;
        public const float VoiceIconSize = VoiceView.IconSize;

        public Button VoiceMuteButton { get; private set; }
        public Image VoiceBackground { get; private set; }
        public Image VoiceIcon { get; private set; }
        public Button VoiceSpeakerButton { get; private set; }
        public Image VoiceSpeakerBackground { get; private set; }
        public Image VoiceSpeakerIcon { get; private set; }

        public static LobbyShortcutGuideView Create(Transform parent)
        {
            var root = new GameObject(RootName, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var view = root.AddComponent<LobbyShortcutGuideView>();
            view.EnsureLayout();
            return view;
        }

        public static LobbyShortcutGuideView Ensure(Transform parent)
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

            var view = existing.GetComponent<LobbyShortcutGuideView>();
            if (view == null)
            {
                view = existing.gameObject.AddComponent<LobbyShortcutGuideView>();
            }

            view.EnsureLayout();
            return view;
        }

        private void Awake()
        {
            EnsureLayout();
        }

        public void EnsureLayout()
        {
            if (transform.Find("Item0/Key/Label") != null)
            {
                PlacePanel();
                ApplyStyle();
                EnsureVoiceButton();
                EnsureSpeakerButton();
                return;
            }

            BuildLayout();
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

            var layout = gameObject.GetComponent<HorizontalLayoutGroup>();
            if (layout == null)
            {
                layout = gameObject.AddComponent<HorizontalLayoutGroup>();
            }

            layout.childAlignment = TextAnchor.MiddleRight;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.spacing = ItemSpacing;
            layout.padding = new RectOffset(0, 0, 0, 0);

            var fitter = gameObject.GetComponent<ContentSizeFitter>();
            if (fitter == null)
            {
                fitter = gameObject.AddComponent<ContentSizeFitter>();
            }

            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            for (var index = 0; index < KeyLabels.Length; index++)
            {
                CreateItem(index);
            }

            PlacePanel();
            ApplyStyle();
            EnsureVoiceButton();
            EnsureSpeakerButton();
        }

        private void CreateItem(int index)
        {
            var item = CreateRect($"Item{index}", transform);
            var itemLayout = item.gameObject.AddComponent<HorizontalLayoutGroup>();
            itemLayout.childAlignment = TextAnchor.MiddleLeft;
            itemLayout.childControlWidth = true;
            itemLayout.childControlHeight = true;
            itemLayout.childForceExpandWidth = false;
            itemLayout.childForceExpandHeight = false;
            itemLayout.spacing = KeyLabelGap;

            var key = CreateRect("Key", item);
            var keyImage = key.gameObject.AddComponent<Image>();
            keyImage.raycastTarget = false;
            var keySize = key.gameObject.AddComponent<LayoutElement>();
            keySize.minWidth = keySize.preferredWidth = KeyBoxSize;
            keySize.minHeight = keySize.preferredHeight = KeyBoxSize;
            keySize.flexibleWidth = 0f;

            CreateLabel(key, "Label", KeyLabels[index], KeyFontSize, HomeUiFonts.ApplyRegular());

            var action = CreateLabel(
                item,
                "Action",
                Actions[index],
                ActionFontSize,
                HomeUiFonts.ApplyMedium(),
                stretch: false);
            var actionFitter = action.gameObject.AddComponent<ContentSizeFitter>();
            actionFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            actionFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private void PlacePanel()
        {
            var rect = (RectTransform)transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-MarginRight, MarginBottom);
        }

        private void ApplyStyle()
        {
            var regular = HomeUiFonts.ApplyRegular();
            var medium = HomeUiFonts.ApplyMedium();
            var keySprite = HomeUiFonts.Rounded(KeyBoxRadius);

            for (var index = 0; index < KeyLabels.Length; index++)
            {
                var item = transform.Find($"Item{index}");
                if (item == null)
                {
                    continue;
                }

                var key = item.Find("Key")?.GetComponent<Image>();
                if (key != null)
                {
                    key.sprite = keySprite;
                    key.type = Image.Type.Sliced;
                    key.pixelsPerUnitMultiplier = 1f;
                    key.color = KeyBoxColor;
                }

                var keyLabel = item.Find("Key/Label")?.GetComponent<TMP_Text>();
                if (keyLabel != null)
                {
                    keyLabel.text = KeyLabels[index];
                    keyLabel.font = regular;
                    keyLabel.fontSize = KeyFontSize;
                    keyLabel.fontStyle = FontStyles.Normal;
                    keyLabel.color = Color.white;
                    keyLabel.alignment = TextAlignmentOptions.Center;
                }

                var action = item.Find("Action")?.GetComponent<TMP_Text>();
                if (action != null)
                {
                    action.text = Actions[index];
                    action.font = medium;
                    action.fontSize = ActionFontSize;
                    action.fontStyle = FontStyles.Normal;
                    action.color = Color.white;
                    action.alignment = TextAlignmentOptions.MidlineLeft;
                }
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform);
        }

        /// <summary>
        /// The mute toggle sits to the right of 환경설정: a white mic or a
        /// grey slash, nothing else.
        /// </summary>
        private void EnsureVoiceButton()
        {
            var slot = VoiceView.EnsureSlot(transform);
            slot.SetAsLastSibling();

            var size = slot.GetComponent<LayoutElement>();
            if (size == null)
            {
                size = slot.gameObject.AddComponent<LayoutElement>();
            }

            size.minWidth = size.preferredWidth = VoiceView.ButtonSize;
            size.minHeight = size.preferredHeight = VoiceView.ButtonSize;
            size.flexibleWidth = 0f;

            VoiceBackground = slot.GetComponent<Image>();
            VoiceMuteButton = slot.GetComponent<Button>();
            VoiceIcon = slot.Find(VoiceView.IconName)?.GetComponent<Image>();
            VoiceView.SizeSlot(slot);

            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform);
        }

        /// <summary>
        /// The listen toggle sits to the right of the microphone.
        /// </summary>
        private void EnsureSpeakerButton()
        {
            var slot = VoiceView.EnsureSpeakerSlot(transform);
            slot.SetAsLastSibling();
            VoiceView.SizeSlot(slot);

            VoiceSpeakerBackground = slot.GetComponent<Image>();
            VoiceSpeakerButton = slot.GetComponent<Button>();
            VoiceSpeakerIcon = slot.Find(VoiceView.IconName)?.GetComponent<Image>();

            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform);
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            return root.GetComponent<RectTransform>();
        }

        private static TextMeshProUGUI CreateLabel(
            Transform parent,
            string name,
            string content,
            float fontSize,
            TMP_FontAsset font,
            bool stretch = true)
        {
            var root = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            root.transform.SetParent(parent, false);
            var label = root.GetComponent<TextMeshProUGUI>();
            label.text = content;
            label.font = font != null ? font : HomeUiFonts.Apply();
            label.fontSize = fontSize;
            label.fontStyle = FontStyles.Normal;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.richText = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            if (stretch)
            {
                Stretch(label.rectTransform);
            }

            return label;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
