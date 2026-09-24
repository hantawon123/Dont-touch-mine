using System;
using System.Collections.Generic;
using Game.Client.Match;
using Game.Client.Common;
using Game.Core.Lobby;
using Game.Core.Match;
using Game.Core.Settings;
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
        // 유치장 무대(3D 연출)가 들어오면서 5초는 짧아 8초로 늘림 (2026-09-08).
        internal const double ResultDisplaySeconds = 8d;
        private readonly INetworkMatchEvents events;
        private readonly INetworkResultNavigation navigation;
        private readonly RoomBrowserSystem room;
        private readonly ILoadingOverlay loading;
        private readonly ReactiveProperty<string> resultText = new(
            UiTextCatalog.Shipped.Get(UiText.Match.NoResult, "ko"));
        private readonly UiLocale locale;
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
        private bool highlightPresented;

        public ReadOnlyReactiveProperty<string> ResultText => resultText;
        public string ResultHeadline { get; private set; } = string.Empty;
        public string ResultSubtitle { get; private set; } = string.Empty;

        /// <summary>결과가 도착해 승자 명단을 알 수 있는 상태인지. 다음 경기 시작 시 해제된다.</summary>
        public bool HasMatchResult { get; private set; }

        /// <summary>마지막 경기의 승자(탈출) 플레이어 번호. 결과가 없으면 빈 목록.</summary>
        public IReadOnlyList<int> LastWinnerPlayerIndices { get; private set; } = Array.Empty<int>();

        public NetworkResultLobbyReturnController(
            INetworkMatchEvents events,
            INetworkResultNavigation navigation,
            RoomBrowserSystem room,
            ILoadingOverlay loading = null,
            UiLocale locale = null)
        {
            this.events = events ?? throw new ArgumentNullException(nameof(events));
            this.navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            this.room = room ?? throw new ArgumentNullException(nameof(room));
            this.loading = loading;
            this.locale = locale;
        }

        private string Copy(string key) =>
            locale != null
                ? locale.Get(key)
                : UiLocale.Applied(key);

        internal static bool ShouldShowForLobbyReturn(
            MatchPhase currentPhase,
            bool resultReady,
            bool sawHighlight) =>
            currentPhase == MatchPhase.Result && resultReady && !sawHighlight;

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
            else if (phase == MatchPhase.Highlight && hasResult &&
                     navigation.IsResultSceneLoaded && resultEndsAt < 0d)
            {
                resultEndsAt = now + ResultDisplaySeconds;
            }
            if (phase == MatchPhase.Result && !hasResult && !directLobbyResult)
            {
                if (resultDataFallbackAt < 0d)
                    resultDataFallbackAt = now + NetworkMatchFlowSynchronizer.ResultDataGraceSeconds;
                if (now >= resultDataFallbackAt)
                {
                    resultDataFallbackAt = -1d;
                    resultDataFallbackActive = true;
                    ResultHeadline = string.Empty;
                    ResultSubtitle = Copy(UiText.Match.ResultMissing);
                    resultText.Value = ResultSubtitle;
                }
            }
            if (ShouldShowForLobbyReturn(
                    phase,
                    hasResult || resultDataFallbackActive,
                    highlightPresented))
            {
                loading?.Show();
            }
            if (!navigation.IsServer || phase != MatchPhase.Result ||
                (!hasResult && !resultDataFallbackActive) || returned) return;
            if (!highlightPresented)
            {
                loading?.Show();
            }
            if (navigation.RequestReturnToLobby())
                returned = true;
        }

        private void OnMatchStateReceived(MatchStateSnapshot snapshot)
        {
            var rolledBackToSearching = snapshot.Phase == MatchPhase.Searching &&
                phase is MatchPhase.Highlight or MatchPhase.Result;
            phase = snapshot.Phase;
            if (phase == MatchPhase.Highlight)
            {
                highlightPresented = true;
                loading?.HideImmediate();
            }
            if (phase == MatchPhase.Waiting || phase == MatchPhase.Hiding)
            {
                highlightPresented = false;
            }
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
            {
                ResultHeadline = string.Empty;
                ResultSubtitle = Copy(UiText.Match.NoResult);
                resultText.Value = ResultSubtitle;
                HasMatchResult = false;
                LastWinnerPlayerIndices = Array.Empty<int>();
            }
        }

        private void OnMatchResultReceived(MatchResult result)
        {
            // Early departure retains the existing direct-to-lobby path.
            hasResult = result.EndReason != MatchEndReason.LastPlayerStanding;
            directLobbyResult = !hasResult;
            resultDataFallbackAt = -1d;
            resultDataFallbackActive = false;
            resultLoadAt = result.EndedAt + HighlightPresentationTiming.FadeSeconds;
            LastWinnerPlayerIndices = result.WinnerPlayerIndices != null
                ? new List<int>(result.WinnerPlayerIndices)
                : Array.Empty<int>();
            HasMatchResult = true;
            ApplyEndOutcome(result);
        }

        private void ApplyEndOutcome(MatchResult result)
        {
            var won = IsLocalWinner(result, room);
            ResultHeadline = won ? Copy(UiText.Match.WinHeadline) : Copy(UiText.Match.LoseHeadline);
            ResultSubtitle = won ? Copy(UiText.Match.WinSubtitle) : Copy(UiText.Match.LoseSubtitle);
            resultText.Value = $"{ResultHeadline}\n{ResultSubtitle}";
        }

        internal static bool IsLocalWinner(MatchResult result, RoomBrowserSystem room)
        {
            var localPlayerIndex = room.LocalPlayerIndex;
            if (localPlayerIndex < 0 || result.WinnerPlayerIndices == null)
            {
                return false;
            }

            for (var index = 0; index < result.WinnerPlayerIndices.Count; index++)
            {
                if (result.WinnerPlayerIndices[index] == localPlayerIndex)
                {
                    return true;
                }
            }

            return false;
        }

        internal static string FormatResult(
            MatchResult result,
            RoomBrowserSystem room,
            bool includeLobbyNotice = true) =>
            FormatResult(result, room, includeLobbyNotice, "ko");

        internal static string FormatResult(
            MatchResult result,
            RoomBrowserSystem room,
            bool includeLobbyNotice,
            string language)
        {
            var catalog = UiTextCatalog.Shipped;
            var winners = new List<string>();
            var localWon = false;
            foreach (var winner in result.WinnerPlayerIndices)
            {
                if (winner == room.LocalPlayerIndex) localWon = true;
                var name = string.Format(catalog.Get(UiText.Match.PlayerN, language), winner + 1);
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
            var outcome = winners.Count == 0 ? catalog.Get(UiText.Match.NoWinner, language) :
                room.LocalPlayerIndex < 0 ? catalog.Get(UiText.Match.MatchOver, language) :
                localWon ? catalog.Get(UiText.Match.Victory, language) : catalog.Get(UiText.Match.Defeat, language);
            var reason = result.EndReason switch
            {
                MatchEndReason.TimeExpired => catalog.Get(UiText.Match.TimeExpired, language),
                MatchEndReason.AllPlayerItemsDestroyed => catalog.Get(UiText.Match.AllItemsDestroyed, language),
                MatchEndReason.LastPlayerStanding => catalog.Get(UiText.Match.LastStanding, language),
                _ => catalog.Get(UiText.Match.MatchOver, language)
            };
            var winnerLine = string.Format(
                catalog.Get(UiText.Match.Winners, language),
                winners.Count == 0 ? catalog.Get(UiText.Match.None, language) : string.Join(", ", winners));
            var summary =
                $"{catalog.Get(UiText.Match.ResultHeading, language)}\n\n{outcome}\n{winnerLine}\n{string.Format(catalog.Get(UiText.Match.EndReason, language), reason)}";
            return includeLobbyNotice
                ? $"{summary}\n\n{catalog.Get(UiText.Match.ReturnLobby, language)}"
                : summary;
        }
    }
}
