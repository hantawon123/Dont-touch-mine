using Game.Client.Common;
using Game.Network.Session;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>
    /// Shows the loading cover when the lobby countdown has run out and the
    /// match scene has not yet taken over.
    /// </summary>
    public sealed class LoadingOverlayCoordinator : ITickable
    {
        public const double MatchStartWindowSeconds = 0.25d;

        private readonly ILoadingOverlay overlay;
        private readonly NetworkRunnerService network;

        public LoadingOverlayCoordinator(ILoadingOverlay overlay, NetworkRunnerService network)
        {
            this.overlay = overlay;
            this.network = network;
        }

        public static bool ShouldShowForMatchStart(double remainingSeconds) =>
            remainingSeconds > 0d && remainingSeconds <= MatchStartWindowSeconds;

        public void Tick()
        {
            if (ShouldShowForMatchStart(network.StartCountdownRemaining))
            {
                overlay.Show();
            }
        }
    }
}
