using System;
using Game.Client.Common;
using Game.Client.Home;
using Game.Core.Flow;
using Game.Core.Lobby;
using Game.Core.Rooms;
using Game.Network.Session;
using R3;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>Project-owned so a disconnect also leaves matches, highlights and results.</summary>
    public sealed class NetworkRoomDisconnectController : IStartable, ITickable, IDisposable
    {
        private readonly NetworkRunnerService network;
        private readonly RoomBrowserSystem room;
        private readonly AppFlowSystem flow;
        private readonly IHomeApplicationHost application;
        private readonly ILoadingOverlay loading;
        private IDisposable subscription;
        private bool pending;

        public NetworkRoomDisconnectController(NetworkRunnerService network, RoomBrowserSystem room,
            AppFlowSystem flow, IHomeApplicationHost application, ILoadingOverlay loading = null)
        {
            this.network = network ?? throw new ArgumentNullException(nameof(network));
            this.room = room ?? throw new ArgumentNullException(nameof(room));
            this.flow = flow ?? throw new ArgumentNullException(nameof(flow));
            this.application = application ?? throw new ArgumentNullException(nameof(application));
            this.loading = loading;
        }

        public void Start() => subscription = room.LastExit.Subscribe(reason =>
        {
            if (reason.HasValue) pending = true;
        });

        public void Tick()
        {
            // Never load a Unity scene on Fusion's disconnect/shutdown callback stack.
            // Its old scene manager must finish teardown before the browser reconnects.
            if (!pending || network.HasRoomSession || network.IsRoomExitPending) return;
            pending = false;
            if (room.IsInRoom.CurrentValue) return;
            var destination = room.LastExit.CurrentValue == RoomExitReason.Kicked
                ? AppFlowState.RoomBrowser : AppFlowState.Home;
            if (!flow.TryExitSession(destination)) return;
            if (destination == AppFlowState.Home)
            {
                loading?.Show();
                application.OpenHome();
            }
            else application.OpenRoomBrowser();
        }

        public void Dispose()
        {
            subscription?.Dispose();
            pending = false;
        }
    }
}
