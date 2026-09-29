using System;
using Game.Core.Flow;
using Game.Core.Match;
using Game.Network.Match;
using Game.Server.Match;
using UnityEngine;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>
    /// Keeps each peer's application flow aligned with the authority-confirmed
    /// match phase and result.
    /// </summary>
    public sealed class NetworkMatchFlowSynchronizer : IStartable, ITickable, IDisposable
    {
        internal const double ResultDataGraceSeconds = 2d;
        private readonly INetworkMatchEvents network;
        private readonly AppFlowSystem appFlow;
        private bool started;
        private bool hasNormalResult;
        private bool hasResultPhase;
        private bool directLobbyResult;
        private double resultDataFallbackAt = -1d;

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

        public void Tick() => Tick(Time.unscaledTimeAsDouble);

        internal void Tick(double now)
        {
            if (!hasResultPhase || hasNormalResult || directLobbyResult)
            {
                return;
            }

            if (resultDataFallbackAt < 0d)
            {
                resultDataFallbackAt = now + ResultDataGraceSeconds;
                return;
            }

            if (now >= resultDataFallbackAt)
            {
                // A missing result must not strand a peer in Highlight forever.
                // The result presenter supplies a user-facing fallback message.
                hasNormalResult = true;
                TransitionToResult();
            }
        }

        private void OnMatchStateReceived(MatchStateSnapshot snapshot)
        {
            switch (snapshot.Phase)
            {
                case MatchPhase.Waiting:
                    hasNormalResult = false;
                    hasResultPhase = false;
                    directLobbyResult = false;
                    resultDataFallbackAt = -1d;
                    TransitionToLobby();
                    break;

                case MatchPhase.Hiding:
                case MatchPhase.Searching:
                    hasNormalResult = false;
                    hasResultPhase = false;
                    directLobbyResult = false;
                    resultDataFallbackAt = -1d;
                    TransitionToInGame();
                    break;

                case MatchPhase.Highlight:
                    hasResultPhase = false;
                    resultDataFallbackAt = -1d;
                    TransitionToHighlight();
                    break;

                case MatchPhase.Result:
                    if (!hasResultPhase)
                    {
                        resultDataFallbackAt = -1d;
                    }
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
                hasResultPhase = false;
                directLobbyResult = true;
                resultDataFallbackAt = -1d;
                TransitionToLobby();
                return;
            }

            hasNormalResult = true;
            directLobbyResult = false;
            resultDataFallbackAt = -1d;
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
