using System;
using System.Collections.Generic;
using Game.Client.Home;
using Game.Core.Flow;
using Game.Core.Lobby;
using Game.Core.Maps;
using Game.Core.Rooms;
using UnityEngine;
using VContainer;

namespace Game.Client.Rooms
{
    /// <summary>
    /// Drives room creation and room entry on the browser screen, and stands in
    /// for the room source until the network supplies one.
    /// </summary>
    /// <remarks>
    /// Publishes through <see cref="RoomBrowserSystem"/> rather than straight to
    /// the view, so <see cref="RoomBrowserPresenter"/> stays the only thing that
    /// renders the list. The filled-in request also leaves through
    /// <see cref="RoomCreateRequested"/> for the layer that will open the room
    /// for real and move the host into its lobby, and a picked room leaves
    /// through <see cref="RoomJoinRequested"/> for the layer that will enter it.
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
            public string mapId = MapCatalog.PlaygroundId;
            public int playerCount = 4;
            public int maxPlayers = RoomSettings.MaxPlayerCount;
            public bool isLocked = false;
            public bool isPlaying = false;

            /// <summary>Read only when <see cref="isLocked"/> is set.</summary>
            public string password = string.Empty;
        }

        [SerializeField]
        private RoomCreateModalView modalPrefab;

        [SerializeField]
        private RoomPasswordModalView passwordModalPrefab;

        [SerializeField]
        private Transform modalParent;

        /// <summary>
        /// Credited as the host of rooms opened here, until the signed-in
        /// profile is available on this screen.
        /// </summary>
        [SerializeField]
        private string hostNickname = "나";

        [SerializeField]
        private PlaceholderRoom[] placeholderRooms = Array.Empty<PlaceholderRoom>();

        private readonly List<RoomSummary> rooms = new List<RoomSummary>();

        /// <summary>
        /// What each locked room accepts, keyed by room id. Stands in for the
        /// authority that will judge passwords once rooms are opened for real;
        /// nothing outside this screen reads it.
        /// </summary>
        private readonly Dictionary<string, string> roomPasswords =
            new Dictionary<string, string>(StringComparer.Ordinal);

        private IRoomBrowserView browserView;
        private RoomBrowserSystem roomBrowser;
        private IHomeApplicationHost applicationHost;
        private AppFlowSystem appFlow;
        private RoomCreateModalView modal;
        private RoomPasswordModalView passwordModal;
        private int issuedRoomCount;

        /// <summary>
        /// The locked room the password modal is currently asking about, or
        /// null while it is closed.
        /// </summary>
        private string pendingRoomId;

        public event Action<RoomCreateRequest> RoomCreateRequested;

        /// <summary>
        /// A room the player asked to enter, with the password they gave for a
        /// locked one and null for an open one.
        /// </summary>
        public event Action<RoomId, string> RoomJoinRequested;

        [Inject]
        public void Construct(
            IRoomBrowserView view,
            RoomBrowserSystem browserSystem,
            IHomeApplicationHost host,
            AppFlowSystem flow)
        {
            browserView = view ?? throw new ArgumentNullException(nameof(view));
            roomBrowser = browserSystem
                ?? throw new ArgumentNullException(nameof(browserSystem));
            applicationHost = host ?? throw new ArgumentNullException(nameof(host));
            appFlow = flow ?? throw new ArgumentNullException(nameof(flow));

            modal = Instantiate(modalPrefab, modalParent);
            modal.Close();
            modal.SetMapOptions(MapCatalog.MapIds);

            browserView.CreateRoomRequested += OnCreateRoomRequested;
            browserView.RoomSelected += OnRoomSelected;
            modal.CloseRequested += OnModalCloseRequested;
            modal.CreateRequested += OnModalCreateRequested;

            // Built last and on its own, so a screen without the prefab still
            // lists and creates rooms; only locked rooms stop working.
            if (passwordModalPrefab != null)
            {
                passwordModal = Instantiate(passwordModalPrefab, modalParent);
                passwordModal.Close();
                passwordModal.CloseRequested += OnPasswordModalCloseRequested;
                passwordModal.SubmitRequested += OnPasswordSubmitted;
            }
            else
            {
                Debug.LogError(
                    "RoomScreenPresenter has no password modal prefab. Assign " +
                    "it so locked rooms can be entered.",
                    this);
            }

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
                browserView.RoomSelected -= OnRoomSelected;
            }

            if (modal != null)
            {
                modal.CloseRequested -= OnModalCloseRequested;
                modal.CreateRequested -= OnModalCreateRequested;
            }

            if (passwordModal != null)
            {
                passwordModal.CloseRequested -= OnPasswordModalCloseRequested;
                passwordModal.SubmitRequested -= OnPasswordSubmitted;
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

        private void OnRoomSelected(string selectedRoomId)
        {
            if (!TryFindRoom(selectedRoomId, out var room) || !room.CanJoin)
            {
                return;
            }

            if (!room.IsLocked)
            {
                RoomJoinRequested?.Invoke(room.Id, null);
                OpenLobby();
                return;
            }

            if (passwordModal == null)
            {
                // Already reported when the modal could not be built. Entering
                // without asking would walk straight into a locked room.
                return;
            }

            pendingRoomId = room.RoomId;
            passwordModal.Open(room.Settings.Title);
        }

        private void OnPasswordModalCloseRequested()
        {
            pendingRoomId = null;
            passwordModal.Close();
        }

        /// <summary>
        /// Answers the modal from the stand-in password table. The room is
        /// looked up again rather than remembered, because the list can be
        /// republished while the modal is open.
        /// </summary>
        private void OnPasswordSubmitted(string password)
        {
            if (!TryFindRoom(pendingRoomId, out var room))
            {
                passwordModal.ShowFailure(RoomEntryFailure.NotFound);
                return;
            }

            if (!room.CanJoin)
            {
                passwordModal.ShowFailure(
                    room.IsFull ? RoomEntryFailure.Full : RoomEntryFailure.Closed);
                return;
            }

            if (!roomPasswords.TryGetValue(room.RoomId, out var expected) ||
                !string.Equals(password, expected, StringComparison.Ordinal))
            {
                passwordModal.ShowFailure(RoomEntryFailure.WrongPassword);
                return;
            }

            pendingRoomId = null;
            passwordModal.Close();
            RoomJoinRequested?.Invoke(room.Id, password);
            OpenLobby();
        }

        /// <summary>
        /// Moves the screen into the room's lobby. The flow state is asked
        /// first, so a scene only loads for a move the app actually allows.
        /// </summary>
        private void OpenLobby()
        {
            if (appFlow.CurrentState != AppFlowState.Lobby &&
                !appFlow.TryTransitionTo(AppFlowState.Lobby))
            {
                Debug.LogError(
                    $"Cannot enter a lobby from {appFlow.CurrentState}.",
                    this);
                return;
            }

            applicationHost.OpenLobby();
        }

        private bool TryFindRoom(string searchedRoomId, out RoomSummary room)
        {
            if (!string.IsNullOrEmpty(searchedRoomId))
            {
                for (var index = 0; index < rooms.Count; index++)
                {
                    if (string.Equals(
                            rooms[index].RoomId,
                            searchedRoomId,
                            StringComparison.Ordinal))
                    {
                        room = rooms[index];
                        return true;
                    }
                }
            }

            room = default;
            return false;
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

            var roomId = NextRoomId();
            RememberPassword(roomId, settings.IsLocked, request.Password);

            rooms.Insert(0, new RoomSummary(
                roomId,
                settings,
                currentPlayerCount: 1,
                isOpen: true,
                RoomStatus.Waiting,
                hostNickname));

            return true;
        }

        private RoomSummary ToSummary(PlaceholderRoom placeholder)
        {
            var mapId = MapCatalog.Contains(placeholder.mapId)
                ? placeholder.mapId.Trim()
                : MapCatalog.DefaultMapId;

            var roomId = NextRoomId();
            RememberPassword(roomId, placeholder.isLocked, placeholder.password);

            return new RoomSummary(
                new RoomId(roomId),
                placeholder.title,
                mapId,
                placeholder.playerCount,
                placeholder.maxPlayers,
                placeholder.isLocked,
                isOpen: true,
                placeholder.isPlaying ? RoomStatus.Playing : RoomStatus.Waiting,
                placeholder.hostNickname);
        }

        private void RememberPassword(string roomId, bool isLocked, string password)
        {
            if (isLocked)
            {
                roomPasswords[roomId] = password ?? string.Empty;
            }
        }

        private string NextRoomId() => $"LOCAL-{++issuedRoomCount}";
    }
}
