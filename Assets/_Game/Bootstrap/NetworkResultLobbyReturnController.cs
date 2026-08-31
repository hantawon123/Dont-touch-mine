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
    // Project-scoped: the match scene is unloaded before the result view is built.
    public sealed class NetworkResultLobbyReturnController : IStartable, ITickable, IDisposable
    {
        private const double ResultDisplaySeconds = 5d;
        private readonly INetworkMatchEvents events;
        private readonly INetworkResultNavigation navigation;
        private readonly RoomBrowserSystem room;
        private readonly ReactiveProperty<string> resultText = new("표시할 경기 결과가 없습니다.");
        private MatchPhase phase;
        private bool hasResult;
        private bool loadRequested;
        private bool returned;
        private double returnAt = -1d;

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

        public void Tick() => Tick(Time.unscaledTimeAsDouble);

        internal void Tick(double now)
        {
            if (!navigation.IsServer || phase != MatchPhase.Result || !hasResult || returned) return;
            if (!loadRequested)
            {
                loadRequested = true;
                if (!navigation.EnterResultScene())
                {
                    Debug.LogError("[Result] Cannot load Result scene. Returning to lobby after the result delay.");
                    returnAt = now + ResultDisplaySeconds;
                }
            }
            if (returnAt < 0d && navigation.IsResultSceneLoaded)
                returnAt = now + ResultDisplaySeconds;
            if (returnAt >= 0d && now >= returnAt && navigation.RequestReturnToLobby())
                returned = true;
        }

        private void OnMatchStateReceived(MatchStateSnapshot snapshot)
        {
            phase = snapshot.Phase;
            if (phase != MatchPhase.Waiting && phase != MatchPhase.Hiding) return;
            hasResult = false;
            loadRequested = false;
            returned = false;
            returnAt = -1d;
            resultText.Value = "표시할 경기 결과가 없습니다.";
        }

        private void OnMatchResultReceived(MatchResult result)
        {
            // Early departure retains the existing direct-to-lobby path.
            hasResult = result.EndReason != MatchEndReason.LastPlayerStanding;
            resultText.Value = FormatResult(result, room);
        }

        internal static string FormatResult(MatchResult result, RoomBrowserSystem room)
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
            return $"게임 결과\n\n{outcome}\n승자: {(winners.Count == 0 ? "없음" : string.Join(", ", winners))}\n종료 사유: {reason}\n\n잠시 후 로비로 돌아갑니다.";
        }
    }
}
