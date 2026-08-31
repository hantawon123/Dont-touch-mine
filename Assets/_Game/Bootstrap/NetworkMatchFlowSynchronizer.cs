using System;
using Game.Core.Flow;
using Game.Core.Match;
using Game.Network.Match;
using Game.Server.Match;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>
    /// Keeps each peer's application flow aligned with the authority-confirmed
    /// match phase and result.
    /// </summary>
    public sealed class NetworkMatchFlowSynchronizer : IStartable, IDisposable
    {
        private readonly INetworkMatchEvents network;
        private readonly AppFlowSystem appFlow;
        private bool started;
        private bool hasNormalResult;
        private bool hasResultPhase;

        public NetworkMatchFlowSynchronizer(
            INetworkMatchEvents network,
            AppFlowSystem appFlow)
        {
            this.network = network ?? throw new ArgumentNullException(nameof(network));
            this.appFlow = appFlow ?? throw new ArgumentNullException(nameof(appFlow));
        }

        public void Start()
        {
            if (started)
            {
                return;
            }

            started = true;
            network.MatchStateReceived += OnMatchStateReceived;
            network.MatchResultReceived += OnMatchResultReceived;
        }

        public void Dispose()
        {
            if (!started)
            {
                return;
            }

            started = false;
            network.MatchStateReceived -= OnMatchStateReceived;
            network.MatchResultReceived -= OnMatchResultReceived;
        }

        private void OnMatchStateReceived(MatchStateSnapshot snapshot)
        {
            switch (snapshot.Phase)
            {
                case MatchPhase.Waiting:
                    hasNormalResult = false;
                    hasResultPhase = false;
                    TransitionToLobby();
                    break;

                case MatchPhase.Hiding:
                case MatchPhase.Searching:
                    hasNormalResult = false;
                    hasResultPhase = false;
                    TransitionToInGame();
                    break;

                case MatchPhase.Highlight:
                    TransitionToHighlight();
                    break;

                case MatchPhase.Result:
                    hasResultPhase = true;
                    TryTransitionToResult();
                    break;
            }
        }

        private void OnMatchResultReceived(MatchResult result)
        {
            if (result.EndReason == MatchEndReason.LastPlayerStanding)
            {
                hasNormalResult = false;
                TransitionToLobby();
                return;
            }

            hasNormalResult = true;
            TryTransitionToResult();
        }

        private void TryTransitionToResult()
        {
            if (hasNormalResult && hasResultPhase)
            {
                TransitionToResult();
            }
        }

        // These callbacks carry confirmed room state, not a user's request to advance.
        // A saved checkpoint can move backward or skip phases already completed by the host.
        private void TransitionToInGame() => appFlow.TryRestoreSessionState(AppFlowState.InGame);

        private void TransitionToHighlight() => appFlow.TryRestoreSessionState(AppFlowState.Highlight);

        private void TransitionToResult() => appFlow.TryRestoreSessionState(AppFlowState.Result);

        private void TransitionToLobby() => appFlow.TryRestoreSessionState(AppFlowState.Lobby);
    }
}
