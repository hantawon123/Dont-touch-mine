using System;
using Game.Client.Home;
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
    /// Paints the mute and speaker buttons the lobby and the match HUD share.
    /// </summary>
    public sealed class VoiceView : MonoBehaviour, IVoiceView
    {
        public const string BarName = "VoiceBar";
        public const string ButtonName = "VoiceButton";
        public const string SpeakerButtonName = "SpeakerButton";
        public const string IconName = "Icon";
        public const string MicOnResource = "UI/Icon_Mic_White";
        public const string MicTalkResource = "UI/Icon_Mic_Green";
        public const string MicOffResource = "UI/Icon_Mic_Off_Gray";
        public const string SpeakerOnResource = "UI/Icon_Headset_White";
        public const string SpeakerOffResource = "UI/Icon_Headset_Off_Gray";
        public const float ButtonSize = 50f;
        public const int ButtonRadius = 10;
        public const float IconSize = 28f;
        public const float ButtonSpacing = 12f;
        public const float CornerMarginRight = 48f;
        public const float CornerMarginBottom = 34f;
        public static readonly Color PlateColor = new Color(0f, 0f, 0f, 0.6f);

        private static Sprite micOn;
        private static Sprite micTalk;
        private static Sprite micOff;
        private static Sprite speakerOn;
        private static Sprite speakerOff;

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
            var bar = parent.Find(BarName) as RectTransform;
            if (bar == null)
            {
                var root = new GameObject(BarName, typeof(RectTransform));
                root.transform.SetParent(parent, false);
                bar = root.GetComponent<RectTransform>();
            }

            if (leftover != null && leftover.parent != bar)
            {
                leftover.SetParent(bar, false);
            }

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

            var mic = EnsureSlot(bar);
            mic.SetSiblingIndex(0);
            SizeSlot(mic);
            var speaker = EnsureSpeakerSlot(bar);
            speaker.SetAsLastSibling();
            SizeSlot(speaker);
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
            var listening = isActiveAndEnabled;
            if (listening)
            {
                UnbindClick();
            }

            muteButton = button;
            background = backgroundImage;
            icon = iconImage;
            HideCaptions();

            if (listening)
            {
                BindClick();
            }
        }

        public void BindSpeakerControl(Button button, Image backgroundImage, Image iconImage)
        {
            var listening = isActiveAndEnabled;
            if (listening)
            {
                UnbindSpeakerClick();
            }

            speakerButton = button;
            speakerBackground = backgroundImage;
            speakerIcon = iconImage;

            if (listening)
            {
                BindSpeakerClick();
            }
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

            BindSlot(bar.Find(ButtonName) as RectTransform);
            BindSpeakerSlot(bar.Find(SpeakerButtonName) as RectTransform);
        }

        private void OnEnable()
        {
            BindClick();
            BindSpeakerClick();
            HideCaptions();
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
            if (icon != null)
            {
                icon.sprite = muted
                    ? MicOffSprite
                    : transmitting ? MicTalkSprite : MicOnSprite;
            }

            if (speakerIcon != null)
            {
                speakerIcon.sprite = listening ? SpeakerOnSprite : SpeakerOffSprite;
            }

            HideCaptions();

            if (muteButton != null)
            {
                muteButton.interactable = true;
            }

            if (speakerButton != null)
            {
                speakerButton.interactable = true;
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

        private void BindClick()
        {
            if (muteButton == null)
            {
                return;
            }

            muteButton.onClick.RemoveListener(HandleMuteClicked);
            muteButton.onClick.AddListener(HandleMuteClicked);
        }

        private void UnbindClick()
        {
            if (muteButton != null)
            {
                muteButton.onClick.RemoveListener(HandleMuteClicked);
            }
        }

        private void BindSpeakerClick()
        {
            if (speakerButton == null)
            {
                return;
            }

            speakerButton.onClick.RemoveListener(HandleSpeakerClicked);
            speakerButton.onClick.AddListener(HandleSpeakerClicked);
        }

        private void UnbindSpeakerClick()
        {
            if (speakerButton != null)
            {
                speakerButton.onClick.RemoveListener(HandleSpeakerClicked);
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
