using System;
using System.Collections.Generic;
using Game.Client.Home;
using Game.Core.Flow;
using Game.Core.Lobby;
using Game.Core.Rooms;
using R3;
using VContainer.Unity;

namespace Game.Client.Rooms
{
    public sealed class RoomBrowserPresenter : IStartable, IDisposable
    {
        private readonly IRoomBrowserView view;
        private readonly RoomBrowserSystem rooms;
        private readonly IHomeApplicationHost applicationHost;
        private readonly AppFlowSystem appFlow;
        private IDisposable roomsSubscription;

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
            roomsSubscription = rooms.Rooms.Subscribe(OnRoomsChanged);
            view.SetRooms(rooms.Rooms.CurrentValue);
        }

        public void Dispose()
        {
            view.BackRequested -= OnBackRequested;
            roomsSubscription?.Dispose();
        }

        private void OnBackRequested()
        {
            if (appFlow.CurrentState != AppFlowState.Home &&
                !appFlow.TryTransitionTo(AppFlowState.Home))
            {
                return;
            }

            applicationHost.OpenHome();
        }

        private void OnRoomsChanged(IReadOnlyList<RoomSummary> summaries)
        {
            view.SetRooms(summaries);
        }
    }
}
