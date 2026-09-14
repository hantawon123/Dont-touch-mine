using System;
using Game.Client.Home;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Lobby
{
    /// <summary>
    /// 2 hosts the two-column player modal. 1 opens the character closet
    /// overlay and Esc opens environment settings, both outside this view.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LobbyShortcutOverlayView : MonoBehaviour, ILobbyShortcutOverlay
    {
        public const string RootName = "ShortcutOverlay";
        public const int SortingOrder = PlaySettingsStyle.Overlay.SortingOrder + 5;

        public static readonly string[] Titles =
        {
            string.Empty,
            "캐릭터 설정",
            "플레이어",
            "환경설정"
        };

        private GameObject overlayRoot;
        private TextMeshProUGUI title;
        private RectTransform playerListRoot;

        public event Action CloseRequested;

        public LobbyShortcutKind OpenKind { get; private set; }

        public bool IsOpen =>
            overlayRoot != null && overlayRoot.activeSelf && OpenKind != LobbyShortcutKind.None;

        public static LobbyShortcutOverlayView Ensure(Transform hud)
        {
            if (hud == null)
            {
                return null;
            }

            var view = hud.GetComponent<LobbyShortcutOverlayView>();
            if (view == null)
            {
                view = hud.gameObject.AddComponent<LobbyShortcutOverlayView>();
            }

            view.EnsureOverlay();
            return view;
        }

        public void BindPlayerList(LobbyPlayerListView list)
        {
            playerListRoot = list != null ? list.transform as RectTransform : null;
            if (playerListRoot != null)
            {
                playerListRoot.gameObject.SetActive(false);
            }
        }

        public void Show(LobbyShortcutKind kind)
        {
            if (kind == LobbyShortcutKind.None)
            {
                Hide();
                return;
            }

            EnsureOverlay();
            OpenKind = kind;
            var showPlayers = kind == LobbyShortcutKind.Players && playerListRoot != null;
            if (title != null)
            {
                var index = (int)kind;
                title.text = index >= 0 && index < Titles.Length ? Titles[index] : string.Empty;
                title.gameObject.SetActive(!showPlayers);
            }

            ShowPlayerList(showPlayers);
            if (overlayRoot != null)
            {
                overlayRoot.SetActive(true);
            }
        }

        public void Hide()
        {
            OpenKind = LobbyShortcutKind.None;
            ShowPlayerList(false);
            if (title != null)
            {
                title.gameObject.SetActive(true);
            }

            if (overlayRoot != null)
            {
                overlayRoot.SetActive(false);
            }
        }

        private void ShowPlayerList(bool visible)
        {
            if (playerListRoot == null)
            {
                return;
            }

            if (visible)
            {
                playerListRoot.SetParent(overlayRoot != null ? overlayRoot.transform : transform, false);
                playerListRoot.anchorMin = playerListRoot.anchorMax = new Vector2(0.5f, 0.5f);
                playerListRoot.pivot = new Vector2(0.5f, 0.5f);
                playerListRoot.anchoredPosition = Vector2.zero;
                playerListRoot.sizeDelta = LobbyPlayerListView.ModalSize;
                playerListRoot.SetAsLastSibling();
            }

            playerListRoot.gameObject.SetActive(visible);
        }

        public void RequestClose()
        {
            if (!IsOpen)
            {
                return;
            }

            Hide();
            CloseRequested?.Invoke();
        }

        private void Awake()
        {
            EnsureOverlay();
            Hide();
        }

        private void EnsureOverlay()
        {
            if (overlayRoot != null && title != null)
            {
                return;
            }

            var existing = transform.Find(RootName);
            if (existing == null)
            {
                var root = new GameObject(
                    RootName,
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(GraphicRaycaster));
                root.transform.SetParent(transform, false);
                existing = root.transform;
            }

            overlayRoot = existing.gameObject;
            var rect = (RectTransform)existing;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var canvas = overlayRoot.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = overlayRoot.AddComponent<Canvas>();
            }

            canvas.overrideSorting = true;
            canvas.sortingOrder = SortingOrder;
            Game.Client.Common.HudScreenScale.Ensure(overlayRoot);

            if (overlayRoot.GetComponent<GraphicRaycaster>() == null)
            {
                overlayRoot.AddComponent<GraphicRaycaster>();
            }

            var dim = existing.Find("Dim") as RectTransform;
            if (dim == null)
            {
                var dimGo = new GameObject(
                    "Dim",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Button));
                dimGo.transform.SetParent(existing, false);
                dim = dimGo.GetComponent<RectTransform>();
            }

            dim.anchorMin = Vector2.zero;
            dim.anchorMax = Vector2.one;
            dim.offsetMin = Vector2.zero;
            dim.offsetMax = Vector2.zero;
            var dimImage = dim.GetComponent<Image>();
            dimImage.color = PlaySettingsStyle.Overlay.Scrim;
            dimImage.raycastTarget = true;
            var dimButton = dim.GetComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            dimButton.onClick.RemoveListener(RequestClose);
            dimButton.onClick.AddListener(RequestClose);

            var titleTransform = existing.Find("Title");
            if (titleTransform == null)
            {
                var titleGo = new GameObject(
                    "Title",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(TextMeshProUGUI));
                titleGo.transform.SetParent(existing, false);
                titleTransform = titleGo.transform;
            }

            var titleRect = (RectTransform)titleTransform;
            titleRect.anchorMin = titleRect.anchorMax = new Vector2(0.5f, 0.5f);
            titleRect.pivot = new Vector2(0.5f, 0.5f);
            titleRect.sizeDelta = new Vector2(800f, 80f);
            titleRect.anchoredPosition = Vector2.zero;

            title = titleTransform.GetComponent<TextMeshProUGUI>();
            title.font = HomeUiFonts.ApplyMedium();
            title.fontSize = 36f;
            title.fontStyle = FontStyles.Normal;
            title.color = Color.white;
            title.alignment = TextAlignmentOptions.Center;
            title.raycastTarget = false;
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.overflowMode = TextOverflowModes.Overflow;
        }
    }
}
