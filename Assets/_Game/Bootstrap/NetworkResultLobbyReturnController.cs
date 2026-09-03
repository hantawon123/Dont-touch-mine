using System;
using System.Collections.Generic;
using Game.Core.Lobby;
using Game.Core.Match;
using Game.Network.Match;
using Game.Server.Match;
using R3;
using UnityEngine;
using VContainer.Unity;

namespace Game.Bootstrap
{
    // Project-scoped: the authority owns the single post-highlight lobby return.
    public sealed class NetworkResultLobbyReturnController : IStartable, ITickable, IDisposable
    {
        internal const double ResultDisplaySeconds = 5d;
        private readonly INetworkMatchEvents events;
        private readonly INetworkResultNavigation navigation;
        private readonly RoomBrowserSystem room;
        private readonly ReactiveProperty<string> resultText = new("표시할 경기 결과가 없습니다.");
        private MatchPhase phase;
        private bool hasResult;
        private bool returned;
        private double resultDataFallbackAt = -1d;
        private bool resultDataFallbackActive;
        private bool directLobbyResult;
        private bool resultLoadRequested;
        private double resultLoadAt = -1d;
        private double resultEndsAt = -1d;
        private bool highlightLobbyRequested;

        public ReadOnlyReactiveProperty<string> ResultText => resultText;

        public NetworkResultLobbyReturnController(
            INetworkMatchEvents events, INetworkResultNavigation navigation, RoomBrowserSystem room)
        {
            this.events = events ?? throw new ArgumentNullException(nameof(events));
            this.navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            this.room = room ?? throw new ArgumentNullException(nameof(room));
        }

        public void Start()
        {
            events.MatchStateReceived += OnMatchStateReceived;
            events.MatchResultReceived += OnMatchResultReceived;
        }

        public void Dispose()
        {
            events.MatchStateReceived -= OnMatchStateReceived;
            events.MatchResultReceived -= OnMatchResultReceived;
            resultText.Dispose();
        }

        public void Tick()
        {
            var now = navigation.IsRuntimeReady &&
                      navigation is INetworkMatchRuntimeSource clock
                ? clock.ServerTime
                : Time.unscaledTimeAsDouble;
            Tick(now);
        }

        internal void Tick(double now)
        {
            if (!navigation.IsRuntimeReady)
            {
                return;
            }
            if (navigation.IsServer && phase == MatchPhase.Highlight && hasResult)
            {
                if (!resultLoadRequested && now >= resultLoadAt)
                    resultLoadRequested = navigation.EnterResultScene();
                if (navigation.IsResultSceneLoaded && resultEndsAt < 0d)
                    resultEndsAt = now + ResultDisplaySeconds;
                if (resultEndsAt >= 0d && now >= resultEndsAt &&
                    !highlightLobbyRequested)
                    highlightLobbyRequested = navigation.PrepareLobbyForHighlights();
            }
            if (phase == MatchPhase.Result && !hasResult && !directLobbyResult)
            {
                if (resultDataFallbackAt < 0d)
                    resultDataFallbackAt = now + NetworkMatchFlowSynchronizer.ResultDataGraceSeconds;
                if (now >= resultDataFallbackAt)
                {
                    resultDataFallbackAt = -1d;
                    resultDataFallbackActive = true;
                    resultText.Value = "경기 결과 데이터를 받지 못했습니다.\n\n로비로 돌아갑니다.";
                }
            }
            if (!navigation.IsServer || phase != MatchPhase.Result ||
                (!hasResult && !resultDataFallbackActive) || returned) return;
            if (navigation.RequestReturnToLobby())
                returned = true;
        }

        private void OnMatchStateReceived(MatchStateSnapshot snapshot)
        {
            var rolledBackToSearching = snapshot.Phase == MatchPhase.Searching &&
                phase is MatchPhase.Highlight or MatchPhase.Result;
            phase = snapshot.Phase;
            if (phase != MatchPhase.Result)
            {
                resultDataFallbackAt = -1d;
                resultDataFallbackActive = false;
            }
            if (phase != MatchPhase.Waiting && phase != MatchPhase.Hiding && !rolledBackToSearching) return;
            hasResult = false;
            directLobbyResult = false;
            returned = false;
            resultLoadRequested = false;
            resultLoadAt = -1d;
            resultEndsAt = -1d;
            highlightLobbyRequested = false;
            resultDataFallbackAt = -1d;
            resultDataFallbackActive = false;
            // Waiting arrives before Result finishes unloading. Keep its text
            // until the next match starts, independently of navigation state.
            if (phase == MatchPhase.Hiding)
                resultText.Value = "표시할 경기 결과가 없습니다.";
        }

        private void OnMatchResultReceived(MatchResult result)
        {
            // Early departure retains the existing direct-to-lobby path.
            hasResult = result.EndReason != MatchEndReason.LastPlayerStanding;
            directLobbyResult = !hasResult;
            resultDataFallbackAt = -1d;
            resultDataFallbackActive = false;
            resultLoadAt = result.EndedAt + HighlightPresentationTiming.PostRollSeconds;
            resultText.Value =
                $"{FormatResult(result, room, false)}\n\n잠시 후 하이라이트가 재생됩니다.";
        }

        internal static string FormatResult(
            MatchResult result,
            RoomBrowserSystem room,
            bool includeLobbyNotice = true)
        {
            var winners = new List<string>();
            var localWon = false;
            foreach (var winner in result.WinnerPlayerIndices)
            {
                if (winner == room.LocalPlayerIndex) localWon = true;
                var name = $"플레이어 {winner + 1}";
                foreach (var player in room.MatchParticipants.CurrentValue)
                {
                    if (player.PlayerIndex != winner) continue;
                    name = player.PlayerId;
                    foreach (var participant in room.Participants.CurrentValue)
                        if (participant.PlayerId == player.PlayerId && !string.IsNullOrWhiteSpace(participant.Nickname))
                            name = participant.Nickname;
                }
                winners.Add(name);
            }
            var outcome = winners.Count == 0 ? "승자 없음" :
                room.LocalPlayerIndex < 0 ? "경기 종료" : localWon ? "승리" : "패배";
            var reason = result.EndReason switch
            {
                MatchEndReason.TimeExpired => "제한 시간 종료",
                MatchEndReason.AllPlayerItemsDestroyed => "모든 플레이어 물건 파괴",
                MatchEndReason.LastPlayerStanding => "마지막 플레이어 생존",
                _ => "경기 종료"
            };
            var summary = $"게임 결과\n\n{outcome}\n승자: {(winners.Count == 0 ? "없음" : string.Join(", ", winners))}\n종료 사유: {reason}";
            return includeLobbyNotice ? $"{summary}\n\n로비로 돌아갑니다." : summary;
        }
    }
}
