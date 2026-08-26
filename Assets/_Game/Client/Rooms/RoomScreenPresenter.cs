using System;
using System.Collections.Generic;
using Game.Core.Lobby;
using Game.Core.Rooms;
using UnityEngine;
using VContainer;

namespace Game.Client.Rooms
{
    /// <summary>
    /// Drives room creation on the browser screen, and stands in for the room
    /// source until the network supplies one.
    /// </summary>
    /// <remarks>
    /// Publishes through <see cref="RoomBrowserSystem"/> rather than straight to
    /// the view, so <see cref="RoomBrowserPresenter"/> stays the only thing that
    /// renders the list. The filled-in request also leaves through
    /// <see cref="RoomCreateRequested"/> for the layer that will open the room
    /// for real and move the host into its lobby.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class RoomScreenPresenter : MonoBehaviour
    {
        /// <summary>
        /// Stands in for a listed room until the browser is fed by the network.
        /// </summary>
        [Serializable]
        private sealed class PlaceholderRoom
        {
            public string title = "방이름";
            public string hostNickname = "닉네임";
            public string mapId = "맵1";
            public int playerCount = 4;
            public int maxPlayers = RoomSettings.MaxPlayerCount;
            public bool isLocked = false;
            public bool isPlaying = false;
        }

        [SerializeField]
        private RoomCreateModalView modalPrefab;

        [SerializeField]
        private Transform modalParent;

        [SerializeField]
        private string[] mapIds = Array.Empty<string>();

        /// <summary>
        /// Credited as the host of rooms opened here, until the signed-in
        /// profile is available on this screen.
        /// </summary>
        [SerializeField]
        private string hostNickname = "나";

        [SerializeField]
        private PlaceholderRoom[] placeholderRooms = Array.Empty<PlaceholderRoom>();

        private readonly List<RoomSummary> rooms = new List<RoomSummary>();

        private IRoomBrowserView browserView;
        private RoomBrowserSystem roomBrowser;
        private RoomCreateModalView modal;
        private int issuedRoomCount;

        public event Action<RoomCreateRequest> RoomCreateRequested;

        [Inject]
        public void Construct(IRoomBrowserView view, RoomBrowserSystem browserSystem)
        {
            browserView = view ?? throw new ArgumentNullException(nameof(view));
            roomBrowser = browserSystem
                ?? throw new ArgumentNullException(nameof(browserSystem));

            modal = Instantiate(modalPrefab, modalParent);
            modal.Close();
            modal.SetMapOptions(mapIds);

            browserView.CreateRoomRequested += OnCreateRoomRequested;
            modal.CloseRequested += OnModalCloseRequested;
            modal.CreateRequested += OnModalCreateRequested;

            foreach (var placeholder in placeholderRooms)
            {
                rooms.Add(ToSummary(placeholder));
            }

            PublishRooms();
        }

        private void Start()
        {
            if (roomBrowser == null)
            {
                Debug.LogError(
                    "RoomScreenPresenter was never injected. Assign it on " +
                    "RoomBrowserLifetimeScope so the create-room modal works.",
                    this);
            }
        }

        private void OnDestroy()
        {
            if (browserView != null)
            {
                browserView.CreateRoomRequested -= OnCreateRoomRequested;
            }

            if (modal != null)
            {
                modal.CloseRequested -= OnModalCloseRequested;
                modal.CreateRequested -= OnModalCreateRequested;
            }
        }

        private void OnCreateRoomRequested() => modal.Open();

        private void OnModalCloseRequested() => modal.Close();

        private void OnModalCreateRequested(RoomCreateRequest request)
        {
            modal.Close();

            if (TryAddRoom(request))
            {
                PublishRooms();
            }

            RoomCreateRequested?.Invoke(request);
        }

        /// <summary>
        /// Hands the list to the one property the browser renders from. The
        /// property replays its current value on subscribe, so it does not
        /// matter whether this runs before or after the browser subscribes.
        /// </summary>
        private void PublishRooms()
        {
            roomBrowser.SetRooms(rooms);
        }

        private bool TryAddRoom(RoomCreateRequest request)
        {
            if (!request.TryCreateSettings(
                    RoomSettings.MaxPlayerCount,
                    out var settings,
                    out _))
            {
                return false;
            }

            rooms.Insert(0, new RoomSummary(
                NextRoomId(),
                settings,
                currentPlayerCount: 1,
                isOpen: true,
                RoomStatus.Waiting,
                hostNickname));

            return true;
        }

        private RoomSummary ToSummary(PlaceholderRoom placeholder)
        {
            return new RoomSummary(
                new RoomId(NextRoomId()),
                placeholder.title,
                placeholder.mapId,
                placeholder.playerCount,
                placeholder.maxPlayers,
                placeholder.isLocked,
                isOpen: true,
                placeholder.isPlaying ? RoomStatus.Playing : RoomStatus.Waiting,
                placeholder.hostNickname);
        }

        private string NextRoomId() => $"LOCAL-{++issuedRoomCount}";
    }
}
