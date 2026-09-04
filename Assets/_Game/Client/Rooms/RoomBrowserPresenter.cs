using System;
using System.Collections.Generic;
using Game.Client.Home;
using Game.Core.Flow;
using Game.Core.Lobby;
using Game.Core.Rooms;
using R3;
using UnityEngine;
using VContainer.Unity;

namespace Game.Client.Rooms
{
    public sealed class RoomBrowserPresenter : IStartable, IDisposable
    {
        public const string NoRooms = "열려 있는 방이 없어요";
        public const string NoSearchResults = "검색 결과가 없어요";


        private readonly IRoomBrowserView view;
        private readonly RoomBrowserSystem rooms;
        private readonly IHomeApplicationHost applicationHost;
        private readonly AppFlowSystem appFlow;
        private IDisposable roomsSubscription;
        private IDisposable exitSubscription;
        private IDisposable busySubscription;
        private IDisposable failureSubscription;

        /// <summary>
        /// What the player typed, kept here rather than read back from the
        /// field, so a refreshed list is filtered the same way the visible one
        /// was without the search box having to be asked.
        /// </summary>
        private string searchText = string.Empty;

        public RoomBrowserPresenter(
            IRoomBrowserView view,
            RoomBrowserSystem rooms,
            IHomeApplicationHost applicationHost,
            AppFlowSystem appFlow)
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.rooms = rooms ?? throw new ArgumentNullException(nameof(rooms));
            this.applicationHost = applicationHost
                ?? throw new ArgumentNullException(nameof(applicationHost));
            this.appFlow = appFlow ?? throw new ArgumentNullException(nameof(appFlow));
        }

        public void Start()
        {
            view.BackRequested += OnBackRequested;
            view.SearchTextChanged += OnSearchTextChanged;
            view.DisconnectionAcknowledged += OnDisconnectionAcknowledged;
            roomsSubscription = rooms.Rooms.Subscribe(OnRoomsChanged);
            exitSubscription = rooms.LastExit.Subscribe(OnRoomExit);
            busySubscription = rooms.IsBusy.Subscribe(view.SetBusy);
            failureSubscription = rooms.LastFailure.Subscribe(view.ShowEntryFailure);
            Render();
        }

        public void Dispose()
        {
            view.BackRequested -= OnBackRequested;
            view.SearchTextChanged -= OnSearchTextChanged;
            view.DisconnectionAcknowledged -= OnDisconnectionAcknowledged;
            roomsSubscription?.Dispose();
            exitSubscription?.Dispose();
            busySubscription?.Dispose();
            failureSubscription?.Dispose();
        }

        private void OnRoomExit(RoomExitReason? reason)
        {
            if (!reason.HasValue || reason == RoomExitReason.Left) return;
            view.ShowDisconnection(reason == RoomExitReason.HostClosed
                ? "호스트의 연결이 끊어졌습니다"
                : "서버와의 연결이 끊어졌습니다");
        }

        private void OnDisconnectionAcknowledged() => rooms.AcknowledgeExit();

        /// <summary>
        /// A refusal is reported rather than swallowed. Returning in silence
        /// makes a dead back button and a hung screen look identical, which is
        /// exactly the confusion this cost once already.
        /// </summary>
        private void OnBackRequested()
        {
            if (appFlow.CurrentState != AppFlowState.Home &&
                !appFlow.TryTransitionTo(AppFlowState.Home))
            {
                Debug.LogError(
                    $"[Rooms] Cannot leave the room browser for the home screen " +
                    $"from {appFlow.CurrentState}.");
                return;
            }

            applicationHost.OpenHome();
        }

        private void OnSearchTextChanged(string text)
        {
            searchText = text ?? string.Empty;
            Render();
        }

        private void OnRoomsChanged(IReadOnlyList<RoomSummary> summaries)
        {
            Render();
        }

        /// <summary>
        /// Puts the rooms matching the search on screen, and says why there are
        /// none when there are none.
        /// </summary>
        /// <remarks>
        /// Filtering happens here rather than in the view because the view is
        /// told what to draw, and rather than in the room system because the
        /// search belongs to this screen: a room does not stop existing because
        /// somebody typed.
        /// </remarks>
        private void Render()
        {
            var all = rooms.Rooms.CurrentValue;
            var matching = new List<RoomSummary>(all.Count);

            for (var index = 0; index < all.Count; index++)
            {
                if (all[index].MatchesTitle(searchText))
                {
                    matching.Add(all[index]);
                }
            }

            view.SetRooms(matching);
            view.SetEmptyMessage(DescribeEmptyList(all.Count, matching.Count));
        }

        /// <summary>
        /// Null while there is something to show. An empty list has two causes
        /// and the player can only act on one of them.
        /// </summary>
        private string DescribeEmptyList(int roomCount, int matchCount)
        {
            if (matchCount > 0)
            {
                return null;
            }

            return roomCount > 0 ? NoSearchResults : NoRooms;
        }
    }
}
