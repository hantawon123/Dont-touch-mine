using System;
using System.Collections.Generic;
using Game.Client.Home;
using Game.Core.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Voice
{
    public interface IVoiceView
    {
        event Action MuteToggleRequested;

        event Action SpeakerToggleRequested;

        /// <summary>
        /// Paints the two plates: a white mic, a green mic while voice is
        /// leaving, or a grey slash when muted; and a white headset or grey
        /// slash.
        /// </summary>
        void SetState(bool available, bool muted, bool latched, bool transmitting, bool listening);
    }

    /// <summary>
    /// Paints the mute and speaker buttons the lobby and the match HUD share,
    /// with the 컨트롤 tab's toggle keys to the right of each plate.
    /// </summary>
    public sealed class VoiceView : MonoBehaviour, IVoiceView
    {
        public const string BarName = "VoiceBar";
        public const string ButtonName = "VoiceButton";
        public const string SpeakerButtonName = "SpeakerButton";
        public const string MuteItemName = "VoiceMuteItem";
        public const string SpeakerItemName = "VoiceSpeakerItem";
        public const string IconName = "Icon";
        public const string KeyHintName = "KeyHint";
        public const string MicOnResource = "UI/Icon_Mic_White";
        public const string MicTalkResource = "UI/Icon_Mic_Green";
        public const string MicOffResource = "UI/Icon_Mic_Off_Gray";
        public const string SpeakerOnResource = "UI/Icon_Headset_White";
        public const string SpeakerOffResource = "UI/Icon_Headset_Off_Gray";
        public const float ButtonSize = 50f;
        public const int ButtonRadius = 10;
        public const float IconSize = 28f;
        public const float ButtonSpacing = 12f;
        public const float KeyHintGap = 12f;
        public const float KeyHintFontSize = 20f;
        public const float CornerMarginRight = 48f;
        public const float CornerMarginBottom = 34f;
        public static readonly Color PlateColor = new Color(0f, 0f, 0f, 0.6f);

        private static Sprite micOn;
        private static Sprite micTalk;
        private static Sprite micOff;
        private static Sprite speakerOn;
        private static Sprite speakerOff;
        private static ControlSettingsSystem sharedSettings;
        private static readonly List<TMP_Text> muteHints = new List<TMP_Text>();
        private static readonly List<TMP_Text> speakerHints = new List<TMP_Text>();

        [SerializeField]
        private Button muteButton;

        [SerializeField]
        private Image background;

        [SerializeField]
        private Image icon;

        [SerializeField]
        private Button speakerButton;

        [SerializeField]
        private Image speakerBackground;

        [SerializeField]
        private Image speakerIcon;

        [SerializeField]
        private Text label;

        /// <summary>
        /// The same label where the screen was built with TextMeshPro.
        /// </summary>
        [SerializeField]
        private TMP_Text tmpLabel;

        [NonSerialized]
        private Button wiredMuteButton;

        [NonSerialized]
        private Image wiredIcon;

        [NonSerialized]
        private Button wiredSpeakerButton;

        [NonSerialized]
        private Image wiredSpeakerIcon;

        public event Action MuteToggleRequested;

        public event Action SpeakerToggleRequested;

        /// <summary>
        /// Mic then speaker, as one row the match HUD pins to the corner.
        /// </summary>
        public static RectTransform EnsureBar(Transform parent)
        {
            if (parent == null)
            {
                return null;
            }

            var leftover = parent.Find(ButtonName) as RectTransform;
            var leftoverSpeaker = parent.Find(SpeakerButtonName) as RectTransform;
            var bar = parent.Find(BarName) as RectTransform;
            if (bar == null)
            {
                var root = new GameObject(BarName, typeof(RectTransform));
                root.transform.SetParent(parent, false);
                bar = root.GetComponent<RectTransform>();
            }

            AdoptLeftoverSlot(leftover, bar);
            AdoptLeftoverSlot(leftoverSpeaker, bar);

            var layout = bar.GetComponent<HorizontalLayoutGroup>();
            if (layout == null)
            {
                layout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            }

            layout.childAlignment = TextAnchor.MiddleRight;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.spacing = ButtonSpacing;
            layout.padding = new RectOffset(0, 0, 0, 0);

            var fitter = bar.GetComponent<ContentSizeFitter>();
            if (fitter == null)
            {
                fitter = bar.gameObject.AddComponent<ContentSizeFitter>();
            }

            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var mic = EnsureKeyedMuteItem(bar);
            mic.parent.SetSiblingIndex(0);
            var speaker = EnsureKeyedSpeakerItem(bar);
            speaker.parent.SetAsLastSibling();
            return bar;
        }

        /// <summary>
        /// Builds or restyles the icon-only mute control under
        /// <paramref name="parent"/>.
        /// </summary>
        public static RectTransform EnsureSlot(Transform parent)
        {
            return EnsureIconSlot(parent, ButtonName, MicOnSprite);
        }

        public static RectTransform EnsureSpeakerSlot(Transform parent)
        {
            return EnsureIconSlot(parent, SpeakerButtonName, SpeakerOnSprite);
        }

        /// <summary>
        /// Icon plate plus the 마이크 고정 key, as one row item.
        /// </summary>
        public static RectTransform EnsureKeyedMuteItem(Transform parent)
        {
            return EnsureKeyedItem(
                parent, MuteItemName, ButtonName, MicOnSprite, ControlAction.VoiceToggle);
        }

        /// <summary>
        /// Icon plate plus the 음성 듣기 key, as one row item.
        /// </summary>
        public static RectTransform EnsureKeyedSpeakerItem(Transform parent)
        {
            return EnsureKeyedItem(
                parent, SpeakerItemName, SpeakerButtonName, SpeakerOnSprite, ControlAction.ToggleSpeaker);
        }

        public static RectTransform FindMuteSlot(Transform parent)
        {
            return FindSlot(parent, MuteItemName, ButtonName);
        }

        public static RectTransform FindSpeakerSlot(Transform parent)
        {
            return FindSlot(parent, SpeakerItemName, SpeakerButtonName);
        }

        /// <summary>
        /// Hands the 컨트롤 tab's applied keys to every mic/speaker hint.
        /// Pass null to fall back to the shipped defaults, as tests do.
        /// </summary>
        public static void UseSettings(ControlSettingsSystem settings)
        {
            if (sharedSettings != null)
            {
                sharedSettings.Changed -= OnSharedSettingsChanged;
            }

            sharedSettings = settings;
            if (sharedSettings != null)
            {
                sharedSettings.Changed += OnSharedSettingsChanged;
            }

            RefreshBoundHints();
        }

        public static string MuteKeyLabel() => KeyLabelFor(ControlAction.VoiceToggle);

        public static string SpeakerKeyLabel() => KeyLabelFor(ControlAction.ToggleSpeaker);

        private static RectTransform EnsureKeyedItem(
            Transform parent,
            string itemName,
            string slotName,
            Sprite fallbackIcon,
            ControlAction action)
        {
            if (parent == null)
            {
                return null;
            }

            var item = parent.Find(itemName) as RectTransform;
            if (item == null)
            {
                var root = new GameObject(itemName, typeof(RectTransform));
                root.transform.SetParent(parent, false);
                item = root.GetComponent<RectTransform>();
            }

            var layout = item.GetComponent<HorizontalLayoutGroup>();
            if (layout == null)
            {
                layout = item.gameObject.AddComponent<HorizontalLayoutGroup>();
            }

            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.spacing = KeyHintGap;
            layout.padding = new RectOffset(0, 0, 0, 0);

            AdoptLeftoverSlot(parent.Find(slotName) as RectTransform, item);

            var slot = EnsureIconSlot(item, slotName, fallbackIcon);
            SizeSlot(slot);
            slot.SetSiblingIndex(0);
            EnsureKeyHint(item, action);
            return slot;
        }

        private static void AdoptLeftoverSlot(RectTransform leftover, Transform parent)
        {
            if (leftover == null || parent == null || leftover.parent == parent)
            {
                return;
            }

            leftover.SetParent(parent, false);
        }

        private static RectTransform FindSlot(Transform parent, string itemName, string slotName)
        {
            if (parent == null)
            {
                return null;
            }

            return parent.Find($"{itemName}/{slotName}") as RectTransform
                ?? parent.Find(slotName) as RectTransform;
        }

        private static void EnsureKeyHint(RectTransform item, ControlAction action)
        {
            var hint = item.Find(KeyHintName) as RectTransform;
            if (hint == null)
            {
                var root = new GameObject(
                    KeyHintName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(TextMeshProUGUI));
                root.transform.SetParent(item, false);
                hint = root.GetComponent<RectTransform>();
            }

            hint.SetAsLastSibling();
            var label = hint.GetComponent<TextMeshProUGUI>();
            label.text = KeyLabelFor(action);
            label.font = HomeUiFonts.ApplyMedium();
            label.fontSize = KeyHintFontSize;
            label.fontStyle = FontStyles.Normal;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.raycastTarget = false;
            label.richText = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;

            var fitter = hint.GetComponent<ContentSizeFitter>();
            if (fitter == null)
            {
                fitter = hint.gameObject.AddComponent<ContentSizeFitter>();
            }

            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            RememberHint(label, action);
        }

        private static void RememberHint(TMP_Text label, ControlAction action)
        {
            var hints = action == ControlAction.ToggleSpeaker ? speakerHints : muteHints;
            if (!hints.Contains(label))
            {
                hints.Add(label);
            }
        }

        private static string KeyLabelFor(ControlAction action)
        {
            var code = sharedSettings != null
                ? sharedSettings.Current.Get(action)
                : ControlCatalog.Defaults.Get(action);
            return ControlCatalog.KeyLabel(code);
        }

        private static void OnSharedSettingsChanged(ControlSettings _) => RefreshBoundHints();

        private static void RefreshBoundHints()
        {
            ApplyHints(muteHints, MuteKeyLabel());
            ApplyHints(speakerHints, SpeakerKeyLabel());
        }

        private static void ApplyHints(List<TMP_Text> hints, string text)
        {
            for (var index = hints.Count - 1; index >= 0; index--)
            {
                if (hints[index] == null)
                {
                    hints.RemoveAt(index);
                    continue;
                }

                hints[index].text = text;
            }
        }

        private static RectTransform EnsureIconSlot(Transform parent, string name, Sprite fallback)
        {
            if (parent == null)
            {
                return null;
            }

            var slot = parent.Find(name) as RectTransform;
            if (slot == null)
            {
                var root = new GameObject(
                    name,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Button));
                root.transform.SetParent(parent, false);
                slot = root.GetComponent<RectTransform>();
            }

            if (slot.Find(IconName) == null)
            {
                var iconGo = new GameObject(
                    IconName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                iconGo.transform.SetParent(slot, false);
            }

            StyleSlot(slot, fallback);
            return slot;
        }

        /// <summary>
        /// Pins the standalone match-HUD copy to the same bottom-right corner
        /// the lobby uses, to the right of 환경설정.
        /// </summary>
        public static void PlaceInCorner(RectTransform slot)
        {
            if (slot == null)
            {
                return;
            }

            slot.anchorMin = slot.anchorMax = new Vector2(1f, 0f);
            slot.pivot = new Vector2(1f, 0f);
            slot.anchoredPosition = new Vector2(-CornerMarginRight, CornerMarginBottom);
            slot.sizeDelta = new Vector2(ButtonSize, ButtonSize);
        }

        public static void PlaceBarInCorner(RectTransform bar)
        {
            if (bar == null)
            {
                return;
            }

            bar.anchorMin = bar.anchorMax = new Vector2(1f, 0f);
            bar.pivot = new Vector2(1f, 0f);
            bar.anchoredPosition = new Vector2(-CornerMarginRight, CornerMarginBottom);
        }

        public static void StyleSlot(RectTransform slot) => StyleSlot(slot, MicOnSprite);

        public static void StyleSlot(RectTransform slot, Sprite fallbackIcon)
        {
            if (slot == null)
            {
                return;
            }

            var backgroundImage = slot.GetComponent<Image>();
            if (backgroundImage != null)
            {
                backgroundImage.sprite = HomeUiFonts.Rounded(ButtonRadius);
                backgroundImage.type = Image.Type.Sliced;
                backgroundImage.pixelsPerUnitMultiplier = 1f;
                backgroundImage.color = PlateColor;
                backgroundImage.raycastTarget = true;
            }

            var button = slot.GetComponent<Button>();
            if (button != null)
            {
                button.transition = Selectable.Transition.None;
                button.targetGraphic = backgroundImage;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
            }

            var caption = slot.Find("Label");
            if (caption != null)
            {
                caption.gameObject.SetActive(false);
            }

            var iconImage = slot.Find(IconName)?.GetComponent<Image>();
            if (iconImage == null)
            {
                return;
            }

            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            iconImage.color = Color.white;
            if (iconImage.sprite == null)
            {
                iconImage.sprite = fallbackIcon;
            }

            var iconRect = iconImage.rectTransform;
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = new Vector2(IconSize, IconSize);
        }

        public static void SizeSlot(RectTransform slot)
        {
            if (slot == null)
            {
                return;
            }

            var size = slot.GetComponent<LayoutElement>();
            if (size == null)
            {
                size = slot.gameObject.AddComponent<LayoutElement>();
            }

            size.minWidth = size.preferredWidth = ButtonSize;
            size.minHeight = size.preferredHeight = ButtonSize;
            size.flexibleWidth = 0f;
        }

        /// <summary>
        /// Points this view at a mute control built elsewhere, such as the
        /// lobby shortcut row. Safe to call more than once.
        /// </summary>
        public void BindMuteControl(Button button, Image backgroundImage, Image iconImage)
        {
            UnbindClick();
            muteButton = button;
            background = backgroundImage;
            icon = iconImage;
            wiredMuteButton = button;
            wiredIcon = iconImage;
            HideCaptions();
            BindClick();
            RefreshBoundHints();
        }

        public void BindSpeakerControl(Button button, Image backgroundImage, Image iconImage)
        {
            UnbindSpeakerClick();
            speakerButton = button;
            speakerBackground = backgroundImage;
            speakerIcon = iconImage;
            wiredSpeakerButton = button;
            wiredSpeakerIcon = iconImage;
            BindSpeakerClick();
            RefreshBoundHints();
        }

        public void BindSlot(RectTransform slot)
        {
            if (slot == null)
            {
                return;
            }

            BindMuteControl(
                slot.GetComponent<Button>(),
                slot.GetComponent<Image>(),
                slot.Find(IconName)?.GetComponent<Image>());
        }

        public void BindSpeakerSlot(RectTransform slot)
        {
            if (slot == null)
            {
                return;
            }

            BindSpeakerControl(
                slot.GetComponent<Button>(),
                slot.GetComponent<Image>(),
                slot.Find(IconName)?.GetComponent<Image>());
        }

        public void BindBar(RectTransform bar)
        {
            if (bar == null)
            {
                return;
            }

            BindSlot(FindMuteSlot(bar));
            BindSpeakerSlot(FindSpeakerSlot(bar));
        }

        private void OnEnable()
        {
            HydrateWiredControls();
            BindClick();
            BindSpeakerClick();
            HideCaptions();
            RefreshBoundHints();
            if (label != null && HomeUiFonts.Legacy() != null)
            {
                label.font = HomeUiFonts.Legacy();
            }

            if (tmpLabel != null)
            {
                tmpLabel.font = HomeUiFonts.Apply();
            }
        }

        private void OnDisable()
        {
            UnbindClick();
            UnbindSpeakerClick();
        }

        public void SetState(
            bool available,
            bool muted,
            bool latched,
            bool transmitting,
            bool listening)
        {
            if (wiredIcon != null)
            {
                wiredIcon.sprite = muted
                    ? MicOffSprite
                    : transmitting ? MicTalkSprite : MicOnSprite;
            }

            if (wiredSpeakerIcon != null)
            {
                wiredSpeakerIcon.sprite = listening ? SpeakerOnSprite : SpeakerOffSprite;
            }

            HideCaptions();

            if (wiredMuteButton != null)
            {
                wiredMuteButton.interactable = true;
            }

            if (wiredSpeakerButton != null)
            {
                wiredSpeakerButton.interactable = true;
            }
        }

        private void HideCaptions()
        {
            if (label != null)
            {
                label.gameObject.SetActive(false);
            }

            if (tmpLabel != null)
            {
                tmpLabel.gameObject.SetActive(false);
            }
        }

        private void HydrateWiredControls()
        {
            wiredMuteButton ??= muteButton;
            wiredIcon ??= icon;
            wiredSpeakerButton ??= speakerButton;
            wiredSpeakerIcon ??= speakerIcon;
        }

        private void BindClick()
        {
            if (wiredMuteButton == null)
            {
                return;
            }

            wiredMuteButton.onClick.RemoveListener(HandleMuteClicked);
            wiredMuteButton.onClick.AddListener(HandleMuteClicked);
        }

        private void UnbindClick()
        {
            if (wiredMuteButton != null)
            {
                wiredMuteButton.onClick.RemoveListener(HandleMuteClicked);
            }
        }

        private void BindSpeakerClick()
        {
            if (wiredSpeakerButton == null)
            {
                return;
            }

            wiredSpeakerButton.onClick.RemoveListener(HandleSpeakerClicked);
            wiredSpeakerButton.onClick.AddListener(HandleSpeakerClicked);
        }

        private void UnbindSpeakerClick()
        {
            if (wiredSpeakerButton != null)
            {
                wiredSpeakerButton.onClick.RemoveListener(HandleSpeakerClicked);
            }
        }

        private void HandleMuteClicked() => MuteToggleRequested?.Invoke();

        private void HandleSpeakerClicked() => SpeakerToggleRequested?.Invoke();

        public static Sprite MicOnSprite =>
            micOn ??= Resources.Load<Sprite>(MicOnResource);

        public static Sprite MicTalkSprite =>
            micTalk ??= Resources.Load<Sprite>(MicTalkResource);

        public static Sprite MicOffSprite =>
            micOff ??= Resources.Load<Sprite>(MicOffResource);

        public static Sprite SpeakerOnSprite =>
            speakerOn ??= Resources.Load<Sprite>(SpeakerOnResource);

        public static Sprite SpeakerOffSprite =>
            speakerOff ??= Resources.Load<Sprite>(SpeakerOffResource);
    }
}
