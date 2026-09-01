using System;
using System.Collections.Generic;
using Game.Core.Rooms;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Rooms
{
    [DisallowMultipleComponent]
    public sealed class RoomBrowserView : MonoBehaviour, IRoomBrowserView
    {
        [SerializeField]
        private TMP_InputField searchInputField;

        [SerializeField]
        private Button refreshButton;

        [SerializeField]
        private Button roomCodeSearchButton;

        [SerializeField]
        private Button createRoomButton;

        [SerializeField]
        private Button backButton;

        [SerializeField]
        private Transform listContent;

        [SerializeField]
        private RoomListItemView listItemPrefab;

        private readonly List<RoomListItemView> spawnedItems = new List<RoomListItemView>();

        /// <summary>
        /// The session publishes an empty room list as it shuts down, and a
        /// shutdown can land while this scene is being destroyed. Rebuilding the
        /// list then would spawn items into a dying scene.
        /// </summary>
        private bool isDestroyed;
        private GameObject disconnectionPopup;
        private TMP_Text disconnectionMessage;

        public event Action<string> SearchTextChanged;
        public event Action RefreshRequested;
        public event Action RoomCodeSearchRequested;
        public event Action CreateRoomRequested;
        public event Action BackRequested;
        public event Action<string> RoomSelected;
        public event Action DisconnectionAcknowledged;

        private void Awake()
        {
            searchInputField.onValueChanged.AddListener(OnSearchTextChanged);
            refreshButton.onClick.AddListener(OnRefreshButtonClicked);
            roomCodeSearchButton.onClick.AddListener(OnRoomCodeSearchButtonClicked);
            createRoomButton.onClick.AddListener(OnCreateRoomButtonClicked);
            backButton.onClick.AddListener(OnBackButtonClicked);
        }

        private void OnDestroy()
        {
            isDestroyed = true;

            searchInputField.onValueChanged.RemoveListener(OnSearchTextChanged);
            refreshButton.onClick.RemoveListener(OnRefreshButtonClicked);
            roomCodeSearchButton.onClick.RemoveListener(OnRoomCodeSearchButtonClicked);
            createRoomButton.onClick.RemoveListener(OnCreateRoomButtonClicked);
            backButton.onClick.RemoveListener(OnBackButtonClicked);

            foreach (var item in spawnedItems)
            {
                if (item != null)
                {
                    item.Selected -= OnRoomItemSelected;
                }
            }
        }

        public void SetRooms(IReadOnlyList<RoomSummary> rooms)
        {
            if (rooms == null)
            {
                throw new ArgumentNullException(nameof(rooms));
            }

            if (isDestroyed)
            {
                return;
            }

            EnsurePoolSize(rooms.Count);

            for (var index = 0; index < spawnedItems.Count; index++)
            {
                var item = spawnedItems[index];
                var isVisible = index < rooms.Count;

                // Unity destroys a scene's objects in no set order, so a pooled
                // item can already be gone while this view is not.
                if (item == null)
                {
                    continue;
                }

                // Keeps the rendered order equal to the list order even when
                // the content holds children this pool did not create.
                item.transform.SetSiblingIndex(index);
                item.gameObject.SetActive(isVisible);

                if (isVisible)
                {
                    item.Bind(rooms[index]);
                }
            }
        }

        public void ShowDisconnection(string message)
        {
            if (isDestroyed) return;
            if (disconnectionPopup == null) BuildDisconnectionPopup();
            disconnectionMessage.text = message;
            disconnectionPopup.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void BuildDisconnectionPopup()
        {
            disconnectionPopup = new GameObject("Disconnection Popup", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            disconnectionPopup.transform.SetParent(transform, false);
            var popupRect = disconnectionPopup.GetComponent<RectTransform>();
            popupRect.anchorMin = Vector2.zero;
            popupRect.anchorMax = Vector2.one;
            popupRect.offsetMin = popupRect.offsetMax = Vector2.zero;
            var canvas = disconnectionPopup.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1000;
            var scaler = disconnectionPopup.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            var backdrop = CreatePopupRect("Backdrop", disconnectionPopup.transform, Vector2.zero, Vector2.one);
            backdrop.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);
            var panel = CreatePopupRect("Panel", backdrop, new Vector2(0.3f, 0.38f), new Vector2(0.7f, 0.62f));
            panel.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.14f, 0.18f, 1f);
            disconnectionMessage = CreatePopupText("Message", panel, new Vector2(0.04f, 0.4f), new Vector2(0.96f, 0.95f));
            var buttonRect = CreatePopupRect("Confirm", panel, new Vector2(0.35f, 0.08f), new Vector2(0.65f, 0.32f));
            var background = buttonRect.gameObject.AddComponent<Image>();
            background.color = new Color(0.25f, 0.4f, 0.6f, 1f);
            var button = buttonRect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(() =>
            {
                disconnectionPopup.SetActive(false);
                DisconnectionAcknowledged?.Invoke();
            });
            CreatePopupText("Label", buttonRect, Vector2.zero, Vector2.one).text = "확인";
        }

        private TMP_Text CreatePopupText(string label, Transform parent, Vector2 min, Vector2 max)
        {
            var rect = CreatePopupRect(label, parent, min, max);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            // Reuse the room screen's Korean font and its fallback configuration.
            text.font = searchInputField.textComponent.font;
            text.fontSize = 32f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.richText = false;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform CreatePopupRect(string label, Transform parent, Vector2 min, Vector2 max)
        {
            var rect = new GameObject(label, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private void EnsurePoolSize(int requiredCount)
        {
            while (spawnedItems.Count < requiredCount)
            {
                var item = Instantiate(listItemPrefab, listContent);
                item.Selected += OnRoomItemSelected;
                spawnedItems.Add(item);
            }
        }

        private void OnSearchTextChanged(string text) => SearchTextChanged?.Invoke(text);

        private void OnRefreshButtonClicked() => RefreshRequested?.Invoke();

        private void OnRoomCodeSearchButtonClicked() => RoomCodeSearchRequested?.Invoke();

        private void OnCreateRoomButtonClicked() => CreateRoomRequested?.Invoke();

        private void OnBackButtonClicked() => BackRequested?.Invoke();

        private void OnRoomItemSelected(string selectedRoomId) => RoomSelected?.Invoke(selectedRoomId);
    }
}
