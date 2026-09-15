using Game.Client.Common;
using Game.Client.Home;
using Game.Client.Voice;
using TMPro;
using UnityEngine;

namespace Game.Client.Lobby
{
    /// <summary>
    /// The lobby's always-on screen furniture.
    /// </summary>
    /// <remarks>
    /// Most of the visit keeps the cursor captured for looking around, so a
    /// button pinned to a corner cannot be reached. Start, leave, settings,
    /// play settings and the key guide live in the Esc menu. See
    /// <see cref="LobbyPauseMenuView"/>.
    /// <para>
    /// The exception is the microphone beside 환경설정: the player needs to
    /// see whether they are muted without opening a menu, and Esc frees the
    /// pointer long enough to press it. Talk keys still work while the cursor
    /// is captured.
    /// </para>
    /// <para>
    /// What else is left is the things a player reads rather than clicks: the
    /// category/map card, the player count, the shared key guide, the
    /// 1 / 2 / Esc shortcut row, and the chat field, which the keyboard
    /// reaches on its own. The full roster opens from 2.
    /// </para>
    /// </remarks>
    public sealed class LobbyHudView : MonoBehaviour
    {
        [SerializeField]
        private RectTransform playerListRoot;

        [SerializeField]
        private RectTransform chatRoot;
        private TextMeshProUGUI countdown;
        private string lastCountdownText;

        public void SetStartCountdown(double remaining)
        {
            if (remaining <= 0d)
            {
                if (countdown != null && countdown.gameObject.activeSelf)
                {
                    countdown.gameObject.SetActive(false);
                }

                lastCountdownText = null;
                return;
            }

            var text = $"{System.Math.Ceiling(remaining)}초 뒤 게임이 시작됩니다";
            if (countdown == null)
            {
                countdown = CreateCountdown();
            }

            if (!countdown.gameObject.activeSelf)
            {
                countdown.gameObject.SetActive(true);
            }

            if (lastCountdownText == text)
            {
                return;
            }

            lastCountdownText = text;
            countdown.text = text;
        }

        private TextMeshProUGUI CreateCountdown()
        {
            var font = HomeUiFonts.Apply();
            var root = new GameObject("Start countdown");
            root.SetActive(false);
            root.transform.SetParent(transform, false);
            var rect = root.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -60f);
            rect.sizeDelta = new Vector2(1600f, 80f);
            var label = root.AddComponent<TextMeshProUGUI>();
            if (font != null)
            {
                label.font = font;
                if (font.material != null)
                {
                    label.fontSharedMaterial = font.material;
                }
            }

            label.fontSize = 55f;
            label.alignment = TextAlignmentOptions.Top;
            label.color = Color.white;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            return label;
        }

        public void EnsureSharedGuide()
        {
            KeySettingGuideView.Ensure(transform)?.SetVisible(true);
        }

        public LobbyShortcutGuideView EnsureShortcutGuide()
        {
            HideLegacyHudVoiceButton();
            var guide = LobbyShortcutGuideView.Ensure(transform);
            var voice = GetComponent<VoiceView>();
            if (voice != null && guide != null)
            {
                voice.BindMuteControl(
                    guide.VoiceMuteButton,
                    guide.VoiceBackground,
                    guide.VoiceIcon);
                voice.BindSpeakerControl(
                    guide.VoiceSpeakerButton,
                    guide.VoiceSpeakerBackground,
                    guide.VoiceSpeakerIcon);
            }

            return guide;
        }

        public LobbyShortcutOverlayView EnsureShortcutOverlay()
        {
            return LobbyShortcutOverlayView.Ensure(transform);
        }

        public LobbyPlayerCountView EnsurePlayerCount()
        {
            HideHudPlayerList();
            return LobbyPlayerCountView.Ensure(transform);
        }

        public LobbyMatchInfoView EnsureMatchInfo()
        {
            var info = LobbyMatchInfoView.Ensure(transform);
            HideHudPlayerList();
            EnsurePlayerCount();
            return info;
        }

        public void SetMatchInfo(string categoryLabel, string mapLabel)
        {
            EnsureMatchInfo()?.SetInfo(categoryLabel, mapLabel);
        }

        /// <summary>
        /// A leftover corner slot from before the microphone sat beside
        /// 환경설정. Direct children only, so the shortcut-row button stays.
        /// </summary>
        private void HideLegacyHudVoiceButton()
        {
            var slot = transform.Find("VoiceButton");
            if (slot != null)
            {
                slot.gameObject.SetActive(false);
            }
        }

        private void HideHudPlayerList()
        {
            var overlay = GetComponent<LobbyShortcutOverlayView>();
            if (overlay != null &&
                overlay.IsOpen &&
                overlay.OpenKind == LobbyShortcutKind.Players)
            {
                return;
            }

            var slot = playerListRoot != null
                ? playerListRoot
                : transform.Find("PlayerListRoot") as RectTransform;
            if (slot != null)
            {
                slot.gameObject.SetActive(false);
            }
        }

        private void Awake()
        {
            HudScreenScale.EnsureOn(GetComponent<Canvas>() ?? GetComponentInParent<Canvas>());
            EnsureSharedGuide();
            EnsureShortcutGuide();
            EnsureShortcutOverlay();
            EnsureMatchInfo();
        }

        private void OnEnable()
        {
            EnsureSharedGuide();
            EnsureShortcutGuide();
            EnsureShortcutOverlay();
            EnsureMatchInfo();
            var canvas = GetComponent<Canvas>() ?? GetComponentInParent<Canvas>();
            HudScreenScale.EnsureOn(canvas);
            HomeUiFonts.ApplyLegacy(canvas != null ? canvas.transform : transform);
        }
    }
}
