using Game.Core.Ports;
using R3;

namespace Game.Backend
{
    /// <summary>
    /// A notification stream that is never connected and never says anything.
    /// </summary>
    /// <remarks>
    /// Stands in when the socket package is not available to this build, so that
    /// everything which asks for <see cref="INotificationStream"/> still resolves
    /// and the game runs as it did before there was a channel: lists refresh
    /// when the player opens them. A frame handed to it goes nowhere, which the
    /// caller reads as "use REST".
    /// </remarks>
    public sealed class SilentNotificationStream : INotificationStream, INotificationFrameSender
    {
        private readonly ReactiveProperty<NotificationLinkState> state =
            new ReactiveProperty<NotificationLinkState>(NotificationLinkState.Disconnected);

        public Observable<ServerNotification> Notifications => Observable.Empty<ServerNotification>();

        public ReadOnlyReactiveProperty<NotificationLinkState> State => state;

        public bool TrySend(string json) => false;
    }
}
