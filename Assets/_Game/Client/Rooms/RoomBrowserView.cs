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

        public event Action<string> SearchTextChanged;
        public event Action RefreshRequested;
        public event Action RoomCodeSearchRequested;
        public event Action CreateRoomRequested;
        public event Action BackRequested;
        public event Action<string> RoomSelected;

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

            EnsurePoolSize(rooms.Count);

            for (var index = 0; index < spawnedItems.Count; index++)
            {
                var item = spawnedItems[index];
                var isVisible = index < rooms.Count;
                item.gameObject.SetActive(isVisible);

                if (isVisible)
                {
                    item.Bind(rooms[index]);
                }
            }
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
