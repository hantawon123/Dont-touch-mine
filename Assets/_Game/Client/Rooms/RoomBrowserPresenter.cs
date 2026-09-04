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
        private readonly IRoomBrowserView view;
        private readonly RoomBrowserSystem rooms;
        private readonly IHomeApplicationHost applicationHost;
        private readonly AppFlowSystem appFlow;
        private IDisposable roomsSubscription;
        private IDisposable exitSubscription;
        private IDisposable busySubscription;

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
            view.DisconnectionAcknowledged += OnDisconnectionAcknowledged;
            roomsSubscription = rooms.Rooms.Subscribe(OnRoomsChanged);
            exitSubscription = rooms.LastExit.Subscribe(OnRoomExit);
            busySubscription = rooms.IsBusy.Subscribe(view.SetBusy);
            view.SetRooms(rooms.Rooms.CurrentValue);
        }

        public void Dispose()
        {
            view.BackRequested -= OnBackRequested;
            view.DisconnectionAcknowledged -= OnDisconnectionAcknowledged;
            roomsSubscription?.Dispose();
            exitSubscription?.Dispose();
            busySubscription?.Dispose();
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

        private void OnRoomsChanged(IReadOnlyList<RoomSummary> summaries)
        {
            view.SetRooms(summaries);
        }
    }
}
