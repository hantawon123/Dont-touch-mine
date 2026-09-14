using Game.Client.Common;
using Game.Client.Home;
using TMPro;
using UnityEngine;

namespace Game.Client.Lobby
{
    /// <summary>
    /// The lobby's always-on screen furniture.
    /// </summary>
    /// <remarks>
    /// Nothing here is pressed. The cursor is captured for looking around for
    /// most of the visit, and a captured cursor reports from the centre of the
    /// screen, so a button pinned to a corner cannot be reached at all. Every
    /// button the lobby had up here — start, leave, settings, play settings,
    /// the key guide — is an entry in the Esc menu now. See
    /// <see cref="LobbyPauseMenuView"/>.
    /// <para>
    /// What is left is the things a player reads rather than clicks: the
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
            HideVoiceButton();
            return LobbyShortcutGuideView.Ensure(transform);
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
        /// The talk keys still run through <c>VoicePresenter</c>. The corner
        /// button is gone because a captured cursor cannot reach it.
        /// </summary>
        private void HideVoiceButton()
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
