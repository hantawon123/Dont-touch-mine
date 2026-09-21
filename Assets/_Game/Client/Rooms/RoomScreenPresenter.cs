using System;
using Cysharp.Threading.Tasks;
using Game.Client.Home;
using Game.Client.Common;
using Game.Core.Flow;
using Game.Core.Lobby;
using Game.Core.Maps;
using Game.Core.Rooms;
using R3;
using UnityEngine;
using VContainer;

namespace Game.Client.Rooms
{
    /// <summary>
    /// Drives room entry on the browser screen.
    /// </summary>
    /// <remarks>
    /// The list is read, never written: the session is the only thing that
    /// publishes rooms, and <see cref="RoomBrowserPresenter"/> is the only thing
    /// that renders them. A picked room leaves through
    /// <see cref="RoomJoinRequested"/>, for the layer that talks to the session.
    /// <para>
    /// Rooms are made on the home screen, not here. This screen used to carry a
    /// create form of its own; two forms for one thing meant two places to keep
    /// the room rules in step.
    /// </para>
    /// <para>
    /// Entering is answered rather than assumed. The screen asks, then waits for
    /// the session to report a room code or a failure, because the authority is
    /// what judges a password and how full a room is. Moving to the lobby before
    /// that answer arrives lands the player in an empty lobby whenever the join
    /// was refused.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class RoomScreenPresenter : MonoBehaviour
    {
        /// <summary>
        /// Which request is waiting on the session, and therefore where its
        /// answer has to be shown. A verdict written into a closed modal is a
        /// verdict nobody reads.
        /// </summary>
        private enum PendingEntry
        {
            None,
            RoomList,
            Password,
            RoomCode,
        }

        [SerializeField]
        private RoomPasswordModalView passwordModalPrefab;

        [SerializeField]
        private Transform modalParent;

        private IRoomBrowserView browserView;
        private RoomBrowserSystem roomBrowser;
        private IHomeApplicationHost applicationHost;
        private AppFlowSystem appFlow;
        private ILoadingOverlay loading;
        private RoomPasswordModalView passwordModal;
        private IDisposable enteredSubscription;
        private IDisposable failureSubscription;

        /// <summary>
        /// The locked room the password modal is currently asking about, or
        /// null while it is closed.
        /// </summary>
        private string pendingRoomId;

        /// <summary>
        /// Also guards against the room code and failure properties replaying a
        /// value from before this screen existed: while this is
        /// <see cref="PendingEntry.None"/>, neither is an answer to anything
        /// asked here.
        /// </summary>
        private PendingEntry pending = PendingEntry.None;

        /// <summary>
        /// A room the player asked to enter, with the password they gave for a
        /// locked one and null for an open one. The authority judges it.
        /// </summary>
        public event Action<RoomId, string> RoomJoinRequested;

        /// <summary>
        /// A room code the player typed. Entered by code rather than looked up
        /// in the list first: a code names a room whether or not the list
        /// happens to be holding it.
        /// </summary>
        public event Action<string> RoomCodeEntryRequested;

        [Inject]
        public void Construct(
            IRoomBrowserView view,
            RoomBrowserSystem browserSystem,
            IHomeApplicationHost host,
            AppFlowSystem flow,
            ILoadingOverlay loadingOverlay)
        {
            browserView = view ?? throw new ArgumentNullException(nameof(view));
            roomBrowser = browserSystem
                ?? throw new ArgumentNullException(nameof(browserSystem));
            applicationHost = host ?? throw new ArgumentNullException(nameof(host));
            appFlow = flow ?? throw new ArgumentNullException(nameof(flow));
            loading = loadingOverlay;

            browserView.RoomSelected += OnRoomSelected;
            browserView.RoomCodeEntered += OnRoomCodeEntered;

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

            enteredSubscription = roomBrowser.IsInRoom.Subscribe(OnRoomEnteredChanged);
            failureSubscription = roomBrowser.LastFailure.Subscribe(OnFailureChanged);
        }

        private void Start()
        {
            if (roomBrowser == null)
            {
                Debug.LogError(
                    "RoomScreenPresenter was never injected. Assign it on " +
                    "RoomBrowserLifetimeScope so rooms can be entered.",
                    this);
            }
        }

        private void OnDestroy()
        {
            if (browserView != null)
            {
                browserView.RoomSelected -= OnRoomSelected;
                browserView.RoomCodeEntered -= OnRoomCodeEntered;
            }

            if (passwordModal != null)
            {
                passwordModal.CloseRequested -= OnPasswordModalCloseRequested;
                passwordModal.SubmitRequested -= OnPasswordSubmitted;
            }

            enteredSubscription?.Dispose();
            failureSubscription?.Dispose();
        }

        private void OnRoomSelected(string selectedRoomId)
        {
            if (!roomBrowser.TryFindById(selectedRoomId, out var room) || !room.CanJoin)
            {
                return;
            }

            if (!room.IsLocked)
            {
                pending = PendingEntry.RoomList;
                RoomJoinRequested?.Invoke(room.Id, null);
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
            pending = PendingEntry.None;
            loading?.HideImmediate();
            passwordModal.SetBusy(false);
            passwordModal.Close();
        }

        /// <summary>
        /// Hands the password to the authority instead of judging it here. The
        /// room is looked up again rather than remembered, because the list can
        /// be republished while the modal is open.
        /// </summary>
        private void OnPasswordSubmitted(string password)
        {
            if (!roomBrowser.TryFindById(pendingRoomId, out var room))
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

            pending = PendingEntry.Password;
            passwordModal.SetBusy(true);
            RoomJoinRequested?.Invoke(room.Id, password);
        }

        private void OnRoomCodeEntered(string code)
        {
            var normalized = RoomCodeFormat.Normalize(code);
            if (!RoomCodeFormat.IsWellFormed(normalized))
            {
                return;
            }

            pending = PendingEntry.RoomCode;
            RoomCodeEntryRequested?.Invoke(normalized);
        }

        /// <summary>
        /// Moves the screen into the room lobby. The flow state is asked first,
        /// so a scene only loads for a move the app actually allows.
        /// </summary>
        private async UniTask OpenLobby()
        {
            if (appFlow.CurrentState != AppFlowState.Lobby &&
                !appFlow.TryTransitionTo(AppFlowState.Lobby))
            {
                Debug.LogError(
                    $"Cannot enter a lobby from {appFlow.CurrentState}.",
                    this);
                return;
            }

            if (loading != null)
            {
                await loading.ShowPainted();
            }

            applicationHost.OpenLobby();
        }

        /// <summary>
        /// A confirmed session entry moves every peer to the room lobby. Room
        /// code visibility is separate: list entrants intentionally do not
        /// receive a shareable code.
        /// </summary>
        private void OnRoomEnteredChanged(bool entered)
        {
            if (pending == PendingEntry.None || !entered)
            {
                return;
            }

            pending = PendingEntry.None;
            pendingRoomId = null;

            passwordModal?.SetBusy(false);
            passwordModal?.Close();

            OpenLobby().Forget();
        }

        /// <summary>
        /// Shows the verdict where the request was made, so a wrong password is
        /// reported in the field it was typed into.
        /// </summary>
        private void OnFailureChanged(RoomEntryFailure failure)
        {
            if (pending == PendingEntry.None || failure == RoomEntryFailure.None)
            {
                return;
            }

            var source = pending;
            pending = PendingEntry.None;
            loading?.HideImmediate();

            switch (source)
            {
                case PendingEntry.Password:
                    passwordModal.SetBusy(false);
                    passwordModal.ShowFailure(failure);
                    break;

                default:
                    // Picked straight from the list, so there is no modal to
                    // answer in. The list stays as it was.
                    Debug.LogWarning($"[Rooms] Could not enter the room: {failure}.");
                    break;
            }
        }

        /// <summary>
        /// Asked of the session rather than of a local table: the code a room is
        /// reached by is its session name, so the listing already answers this.
        /// </summary>
        private bool TryFindRoomByCode(string typedCode, out RoomSummary room)
        {
            var normalized = RoomCodeFormat.Normalize(typedCode);

            if (!RoomCodeFormat.IsWellFormed(normalized))
            {
                room = default;
                return false;
            }

            return roomBrowser.TryFindByCode(normalized, out room);
        }
    }
}
