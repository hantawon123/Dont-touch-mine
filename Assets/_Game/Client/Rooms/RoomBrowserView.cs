using Game.Client.Common;
using System;
using System.Collections.Generic;
using Game.Core.Rooms;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Rooms
{
    /// <summary>
    /// The room browser screen. Its layout lives in the other half of this
    /// class, <see cref="BuildLayout"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class RoomBrowserView : MonoBehaviour, IRoomBrowserView
    {
        // Built by BuildLayout rather than assigned in the scene, so the screen
        // has one description of itself and the scene holds no hierarchy to
        // conflict over.
        private TMP_InputField searchInputField;
        private Button refreshButton;
        private Button backButton;
        private Transform listContent;

        private readonly List<RoomListItemView> spawnedItems = new List<RoomListItemView>();

        /// <summary>
        /// The session publishes an empty room list as it shuts down, and a
        /// shutdown can land while this scene is being destroyed. Rebuilding the
        /// list then would spawn items into a dying scene.
        /// </summary>
        private bool isDestroyed;

        /// <summary>
        /// Which way into a room was last used from this screen, so a refusal
        /// can be answered in the terms of the thing the player just did.
        /// </summary>
        /// <remarks>
        /// Held here because this is where both ways in are: a row was clicked
        /// or a code was submitted, and nothing between here and the session
        /// carries that apart.
        /// </remarks>
        private RoomEntrySource lastEntrySource = RoomEntrySource.RoomList;

        public event Action<string> SearchTextChanged;
        public event Action RefreshRequested;
        public event Action<string> RoomCodeEntered;
        public event Action BackRequested;
        public event Action<string> RoomSelected;
        public event Action DisconnectionAcknowledged;

        private void Awake()
        {
            BuildLayout();

            searchInputField.onValueChanged.AddListener(OnSearchTextChanged);
            refreshButton.onClick.AddListener(OnRefreshButtonClicked);
            backButton.onClick.AddListener(OnBackButtonClicked);
        }

        private void OnDestroy()
        {
            isDestroyed = true;

            searchInputField.onValueChanged.RemoveListener(OnSearchTextChanged);
            refreshButton.onClick.RemoveListener(OnRefreshButtonClicked);
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

        public void SetBusy(bool busy)
        {
            if (isDestroyed)
            {
                return;
            }

            SetRefreshBusy(busy);
            SetListBusy(busy);
        }

        public void SetEmptyMessage(string message)
        {
            if (isDestroyed)
            {
                return;
            }

            ShowEmptyState(message);
        }

        public void ShowEntryFailure(RoomEntryFailure failure)
        {
            if (isDestroyed || failure == RoomEntryFailure.None)
            {
                return;
            }

            ShowToast(RoomEntryMessages.Describe(failure, lastEntrySource));
        }

        /// <summary>
        /// Says why the player is back on this screen, on the same toast the
        /// entry failures use.
        /// </summary>
        /// <remarks>
        /// It used to be a dialog of its own — a grey slab with a stock blue
        /// 확인 button, built for the lobby's canvas and reading as a crash
        /// rather than as this screen speaking. Home already says the same
        /// things on its toast, and a player kicked to the browser should hear
        /// it the way a player dropped to Home does.
        /// <para>
        /// Acknowledged at once. There is no button left to press, and the
        /// notice is a fact about the room they just left rather than a
        /// question: leaving the exit unacknowledged would say it again the
        /// next time this screen opened.
        /// </para>
        /// </remarks>
        public void ShowDisconnection(string message)
        {
            if (isDestroyed) return;
            ShowToast(message);

            // The game locked the cursor away. This is a screen to click on.
            Game.Client.Common.WebPointerInput.Release();

            DisconnectionAcknowledged?.Invoke();
        }

        private void EnsurePoolSize(int requiredCount)
        {
            while (spawnedItems.Count < requiredCount)
            {
                var item = RoomListItemView.Create(
                    listContent, ResolveFont(semiBoldFont), ResolveFont(mediumFont));

                item.Selected += OnRoomItemSelected;
                spawnedItems.Add(item);
            }
        }

        private void OnSearchTextChanged(string text) => SearchTextChanged?.Invoke(text);

        private void OnRefreshButtonClicked()
        {
            BeginRefreshCooldown();
            RefreshRequested?.Invoke();
        }

        private void OnBackButtonClicked() => BackRequested?.Invoke();

        private void OnRoomItemSelected(string selectedRoomId)
        {
            lastEntrySource = RoomEntrySource.RoomList;
            RoomSelected?.Invoke(selectedRoomId);
        }
    }
}
