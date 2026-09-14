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

        /// <summary>
        /// Paints the microphone button: on is a white mic, muted is a grey
        /// slash. Latch and transmit do not get a third picture.
        /// </summary>
        void SetState(bool available, bool muted, bool latched, bool transmitting);
    }

    /// <summary>
    /// Paints the mute button the lobby and the match HUD share: a white
    /// microphone when open, a grey slashed microphone when silenced.
    /// </summary>
    public sealed class VoiceView : MonoBehaviour, IVoiceView
    {
        public const string ButtonName = "VoiceButton";
        public const string IconName = "Icon";
        public const string MicOnResource = "UI/Icon_Mic_White";
        public const string MicOffResource = "UI/Icon_Mic_Off_Gray";
        public const float ButtonSize = 50f;
        public const int ButtonRadius = 10;
        public const float IconSize = 28f;
        public const float CornerMarginRight = 48f;
        public const float CornerMarginBottom = 34f;
        public static readonly Color PlateColor = new Color(0f, 0f, 0f, 0.6f);

        private static Sprite micOn;
        private static Sprite micOff;

        [SerializeField]
        private Button muteButton;

        [SerializeField]
        private Image background;

        [SerializeField]
        private Image icon;

        [SerializeField]
        private Text label;

        /// <summary>
        /// The same label where the screen was built with TextMeshPro.
        /// </summary>
        [SerializeField]
        private TMP_Text tmpLabel;

        public event Action MuteToggleRequested;

        /// <summary>
        /// Builds or restyles the icon-only mute control under
        /// <paramref name="parent"/>.
        /// </summary>
        public static RectTransform EnsureSlot(Transform parent)
        {
            if (parent == null)
            {
                return null;
            }

            var slot = parent.Find(ButtonName) as RectTransform;
            if (slot == null)
            {
                var root = new GameObject(
                    ButtonName,
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

            StyleSlot(slot);
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

        public static void StyleSlot(RectTransform slot)
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
                iconImage.sprite = MicOnSprite;
            }

            var iconRect = iconImage.rectTransform;
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = new Vector2(IconSize, IconSize);
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

        private void OnEnable()
        {
            BindClick();
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
        }

        public void SetState(
            bool available,
            bool muted,
            bool latched,
            bool transmitting)
        {
            if (icon != null)
            {
                icon.sprite = muted ? MicOffSprite : MicOnSprite;
            }

            HideCaptions();

            if (muteButton != null)
            {
                muteButton.interactable = true;
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

        private void HandleMuteClicked() => MuteToggleRequested?.Invoke();

        public static Sprite MicOnSprite =>
            micOn ??= Resources.Load<Sprite>(MicOnResource);

        public static Sprite MicOffSprite =>
            micOff ??= Resources.Load<Sprite>(MicOffResource);
    }
}
