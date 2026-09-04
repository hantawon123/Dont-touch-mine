using System;
using System.Collections.Generic;
using Game.Client.Match;
using Game.Core.Items;
using Game.Core.Lobby;
using Game.Core.Match;
using Game.Network.Match;
using Game.SOAP.Config;
using Game.Server.Match;
using UnityEngine;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>Adapts authority-confirmed match events to the scene HUD.</summary>
    public sealed class NetworkMatchHudPresenter : IStartable, ITickable, IDisposable
    {
        private const double NoticeDurationSeconds = 3d;
        private const float MarkerScreenMargin = 32f;

        private readonly INetworkMatchEvents events;
        private readonly INetworkMatchRuntimeSource clock;
        private readonly RoomBrowserSystem room;
        private readonly MatchRulesSO rules;
        private readonly INetworkMatchHudView view;
        private readonly NetworkHighlightPlaybackController playback;
        private readonly List<PlayerItemDestroyedEvent> destructions = new();

        private MatchStateSnapshot snapshot;
        private bool hasSnapshot;
        private double noticeEndsAt;
        private double gameEndNoticeEndsAt = -1d;
        private string gameEndNotice = "게임이 종료되었습니다!";
        private Transform shredder;
        private Camera worldCamera;

        /// <summary>
        /// What the phase line currently says, so a turn that has not moved is
        /// not written to the view on every tick.
        /// </summary>
        private string reportedHidingName = string.Empty;
        private MatchPhase reportedPhase;
        private bool hasReportedPhase;
        private string assignedItemId;
        private string assignedItemDisplayName;
        private bool hidingIntroVisible;
        private bool hidingIntroOpenedThisPhase;
        private double hidingIntroEndsAt;
        private bool hidingTurnStartVisible;
        private bool hidingActiveHudVisible;
        private bool hidingWaitHudVisible;

        public NetworkMatchHudPresenter(
            INetworkMatchEvents events,
            INetworkMatchRuntimeSource clock,
            RoomBrowserSystem room,
            MatchRulesSO rules,
            INetworkMatchHudView view,
            NetworkHighlightPlaybackController playback = null)
        {
            this.events = events ?? throw new ArgumentNullException(nameof(events));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.room = room ?? throw new ArgumentNullException(nameof(room));
            this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.playback = playback;
        }

        public void Start()
        {
            events.MatchStateReceived += OnMatchStateReceived;
            events.MatchResultReceived += OnMatchResultReceived;
            events.ItemAssignmentReceived += OnItemAssignmentReceived;
            events.ItemDestroyedReceived += OnItemDestroyedReceived;
            events.PlayerItemStatusesReceived += OnPlayerItemStatusesReceived;
            events.PlayerInteractionStatesReceived += OnPlayerInteractionStatesReceived;
            view.HideDestructionNotice();
            view.SetRemainingDestructionUses(-1);
            view.SetPlayerItemStatuses(events.LatestPlayerItemStatuses);
            view.SetShredderMarker(default, false);
            view.HideHidingIntro();
            view.HideHidingTurnStart();
            view.HideHidingActiveHud();
            view.HideHidingWaitHud();
            view.SetMatchChatVisible(false);
            view.SetPlayerStatusVisible(false);
            FindSceneReferences();
        }

        public void Dispose()
        {
            events.MatchStateReceived -= OnMatchStateReceived;
            events.MatchResultReceived -= OnMatchResultReceived;
            events.ItemAssignmentReceived -= OnItemAssignmentReceived;
            events.ItemDestroyedReceived -= OnItemDestroyedReceived;
            events.PlayerItemStatusesReceived -= OnPlayerItemStatusesReceived;
            events.PlayerInteractionStatesReceived -= OnPlayerInteractionStatesReceived;
            view.HideDestructionNotice();
            view.SetPlayerItemStatuses(Array.Empty<PlayerItemStatusSnapshot>());
            view.SetShredderMarker(default, false);
            HideHidingIntro();
            HideHidingTurnStart();
            HideHidingActiveHud();
            HideHidingWaitHud();
        }

        public void Tick()
        {
            if (!hasSnapshot || !clock.IsRuntimeReady)
            {
                return;
            }

            var now = clock.ServerTime;
            view.SetRemainingSeconds(snapshot.Phase == MatchPhase.Hiding
                ? HidingTurns.RemainingSecondsAt(
                    snapshot.Phase,
                    snapshot.PhaseEndsAt,
                    now,
                    room.MatchParticipants.CurrentValue.Count,
                    HidingTurnDurationSeconds)
                : Math.Max(0d, snapshot.PhaseEndsAt - now));

            // Whose turn it is moves with time, not with any event: the phase
            // stays Hiding while the turn travels down the line-up.
            ReportPhase();

            if (UpdateGameEndNotice())
            {
                // The end announcement takes priority over destruction notices.
            }
            else if (snapshot.Phase == MatchPhase.Highlight)
            {
                UpdateReplayNotice(playback?.PlaybackSourceTime);
            }
            else if (noticeEndsAt > 0d && now >= noticeEndsAt)
            {
                noticeEndsAt = 0d;
                view.HideDestructionNotice();
            }

            UpdateHidingIntro(now);
            UpdateHidingTurnStart(now);
            UpdateShredderMarker();
        }

        private void OnMatchStateReceived(MatchStateSnapshot received)
        {
            if ((!hasSnapshot || snapshot.Phase != received.Phase) &&
                (received.Phase == MatchPhase.Hiding || received.Phase == MatchPhase.Waiting))
                destructions.Clear();
            if (received.Phase == MatchPhase.Hiding || received.Phase == MatchPhase.Waiting)
            {
                gameEndNoticeEndsAt = -1d;
                gameEndNotice = "게임이 종료되었습니다!";
            }
            if (received.Phase == MatchPhase.Highlight || received.Phase == MatchPhase.Result)
            {
                noticeEndsAt = 0d;
                view.HideDestructionNotice();
            }
            if (received.Phase != MatchPhase.Hiding)
            {
                hidingIntroOpenedThisPhase = false;
                HideHidingIntro();
                HideHidingTurnStart();
                HideHidingActiveHud();
                HideHidingWaitHud();
            }

            snapshot = received;
            hasSnapshot = true;
            var extrasVisible = received.Phase != MatchPhase.Hiding;
            view.SetMatchChatVisible(extrasVisible);
            view.SetPlayerStatusVisible(extrasVisible);
            ReportPhase();
            UpdateGameEndNotice();
            TryShowHidingIntro();
            UpdateHidingTurnStart(clock.IsRuntimeReady ? clock.ServerTime : 0d);
        }

        private void OnMatchResultReceived(MatchResult result)
        {
            if (result.EndReason == MatchEndReason.LastPlayerStanding) return;
            gameEndNotice = "게임이 종료되었습니다!";
            gameEndNoticeEndsAt = result.EndedAt + HighlightPresentationTiming.PostRollSeconds;
            UpdateGameEndNotice();
        }

        private bool UpdateGameEndNotice()
        {
            if (!clock.IsRuntimeReady) return false;
            var remaining = gameEndNoticeEndsAt - clock.ServerTime;
            var active = remaining > 0d && (!hasSnapshot || snapshot.Phase != MatchPhase.Result);
            view.SetEndCountdown(active ? remaining : 0d);
            if (!active) return false;
            view.ShowDestructionNotice(gameEndNotice);
            noticeEndsAt = gameEndNoticeEndsAt;
            return true;
        }

        /// <summary>
        /// Writes the phase, and during hiding who it is waiting on.
        /// </summary>
        /// <remarks>
        /// The turn is worked out here rather than replicated. It is a function
        /// of the phase, when the phase ends and how many are playing, and this
        /// peer already has all three, so <see cref="HidingTurns"/> answers the
        /// same question the authority asks of it.
        /// </remarks>
        private void ReportPhase()
        {
            if (!clock.IsRuntimeReady) return;
            var name = HidingPlayerName();

            if (hasReportedPhase &&
                snapshot.Phase == reportedPhase &&
                string.Equals(name, reportedHidingName, StringComparison.Ordinal))
            {
                return;
            }

            reportedPhase = snapshot.Phase;
            reportedHidingName = name;
            hasReportedPhase = true;

            view.SetPhase(snapshot.Phase, name);
        }

        /// <summary>
        /// Empty is a normal answer, not a failure: the line-up arrives over the
        /// network, so a turn can be known before the names that go with it are.
        /// The view falls back to the bare phase then.
        /// </summary>
        private string HidingPlayerName()
        {
            var playing = room.MatchParticipants.CurrentValue;

            var turnIndex = HidingTurns.IndexAt(
                snapshot.Phase,
                snapshot.PhaseEndsAt,
                clock.ServerTime,
                playing.Count,
                HidingTurnDurationSeconds);

            return turnIndex == HidingTurns.NoTurn
                ? string.Empty
                : DisplayNameOf(playing[turnIndex].PlayerIndex);
        }

        private double HidingTurnDurationSeconds =>
            clock.MatchRules.HidingDurationSeconds > 0
                ? clock.MatchRules.HidingDurationSeconds
                : rules.HidingTurnDurationSeconds;

        private void OnItemDestroyedReceived(PlayerItemDestroyedEvent confirmed)
        {
            destructions.Add(confirmed);
            if (UpdateGameEndNotice()) return;
            if (hasSnapshot && (snapshot.Phase == MatchPhase.Highlight || snapshot.Phase == MatchPhase.Result))
                return;
            view.ShowDestructionNotice(
                $"{DisplayNameOf(confirmed.DestroyerPlayerIndex)}님이 물건을 파괴했습니다!");
            noticeEndsAt = Math.Max(clock.IsRuntimeReady ? clock.ServerTime : confirmed.DestroyedAt, confirmed.DestroyedAt) +
                           NoticeDurationSeconds;
        }

        internal void UpdateReplayNotice(double? sourceTime)
        {
            PlayerItemDestroyedEvent? latest = null;
            foreach (var destruction in destructions)
            {
                if (sourceTime.HasValue && destruction.DestroyedAt <= sourceTime.Value &&
                    sourceTime.Value < destruction.DestroyedAt + NoticeDurationSeconds &&
                    (!latest.HasValue || latest.Value.DestroyedAt <= destruction.DestroyedAt))
                    latest = destruction;
            }
            if (latest.HasValue)
                view.ShowDestructionNotice(
                    $"{DisplayNameOf(latest.Value.DestroyerPlayerIndex)}님이 물건을 파괴했습니다!");
            else
                view.HideDestructionNotice();
        }

        private void OnItemAssignmentReceived(string itemId)
        {
            assignedItemId = itemId?.Trim();
            assignedItemDisplayName = ItemCatalog.DisplayNameOf(itemId);
            view.SetAssignedItem(assignedItemDisplayName);
            if (hidingIntroVisible)
            {
                view.ShowHidingIntro(assignedItemDisplayName, assignedItemId);
                return;
            }

            TryShowHidingIntro();
        }

        private void UpdateHidingIntro(double now)
        {
            TryShowHidingIntro();
            if (hidingIntroVisible && now >= hidingIntroEndsAt)
            {
                HideHidingIntro();
            }
        }

        private void TryShowHidingIntro()
        {
            if (hidingIntroOpenedThisPhase ||
                !hasSnapshot ||
                snapshot.Phase != MatchPhase.Hiding ||
                string.IsNullOrEmpty(assignedItemDisplayName) ||
                !clock.IsRuntimeReady)
            {
                return;
            }

            var playerCount = room.MatchParticipants.CurrentValue.Count;
            if (playerCount <= 0)
            {
                return;
            }

            var startedAt = snapshot.PhaseEndsAt - (HidingTurnDurationSeconds * playerCount);
            var endsAt = startedAt + HidingIntroView.VisibleSeconds;
            if (clock.ServerTime >= endsAt)
            {
                return;
            }

            hidingIntroEndsAt = endsAt;
            hidingIntroOpenedThisPhase = true;
            hidingIntroVisible = true;
            view.ShowHidingIntro(assignedItemDisplayName, assignedItemId);
        }

        private void HideHidingIntro()
        {
            if (!hidingIntroVisible)
            {
                return;
            }

            hidingIntroVisible = false;
            view.HideHidingIntro();
        }

        private void UpdateHidingTurnStart(double now)
        {
            if (hidingIntroVisible ||
                !hasSnapshot ||
                snapshot.Phase != MatchPhase.Hiding ||
                !clock.IsRuntimeReady)
            {
                HideHidingTurnStart();
                HideHidingActiveHud();
                HideHidingWaitHud();
                if (hasSnapshot && snapshot.Phase != MatchPhase.Hiding)
                {
                    view.SetTopHudVisible(true);
                    view.SetMatchChatVisible(true);
                }

                return;
            }

            var playing = room.MatchParticipants.CurrentValue;
            var turnIndex = HidingTurns.IndexAt(
                snapshot.Phase,
                snapshot.PhaseEndsAt,
                now,
                playing.Count,
                HidingTurnDurationSeconds);
            var remaining = HidingTurns.RemainingSecondsAt(
                snapshot.Phase,
                snapshot.PhaseEndsAt,
                now,
                playing.Count,
                HidingTurnDurationSeconds);
            var isLocalTurn = turnIndex != HidingTurns.NoTurn &&
                              turnIndex == room.LocalPlayerIndex;
            var phaseStartedAt = snapshot.PhaseEndsAt - (HidingTurnDurationSeconds * playing.Count);
            var turnStartedAt = isLocalTurn
                ? phaseStartedAt + (turnIndex * HidingTurnDurationSeconds)
                : 0d;
            var overlayStartsAt = Math.Max(
                turnStartedAt,
                phaseStartedAt + HidingIntroView.VisibleSeconds);
            var showStartOverlay = isLocalTurn &&
                                   now >= overlayStartsAt &&
                                   now < overlayStartsAt + HidingTurnStartView.VisibleSeconds;

            if (showStartOverlay)
            {
                if (!hidingTurnStartVisible)
                {
                    hidingTurnStartVisible = true;
                    view.ShowHidingTurnStart(remaining);
                }
                else
                {
                    view.SetHidingTurnStartSeconds(remaining);
                }
            }
            else
            {
                HideHidingTurnStart();
            }

            ShowHidingActiveHud(remaining, isLocalTurn && !showStartOverlay, isLocalTurn);
            view.SetTopHudVisible(false);
            if (isLocalTurn)
            {
                HideHidingWaitHud();
                view.SetMatchChatVisible(false);
                return;
            }

            ShowHidingWaitHud(turnIndex, playing);
            view.SetMatchChatVisible(true);
        }

        private void HideHidingTurnStart()
        {
            if (!hidingTurnStartVisible)
            {
                return;
            }

            hidingTurnStartVisible = false;
            view.HideHidingTurnStart();
        }

        private void ShowHidingActiveHud(double remainingSeconds, bool showTopPrompt, bool showCompleteGuide)
        {
            if (!hidingActiveHudVisible)
            {
                hidingActiveHudVisible = true;
                view.ShowHidingActiveHud(remainingSeconds, showTopPrompt, showCompleteGuide);
                return;
            }

            view.SetHidingActiveHudSeconds(remainingSeconds);
            view.ShowHidingActiveHud(remainingSeconds, showTopPrompt, showCompleteGuide);
        }

        private void HideHidingActiveHud()
        {
            if (!hidingActiveHudVisible)
            {
                return;
            }

            hidingActiveHudVisible = false;
            view.HideHidingActiveHud();
        }

        private void ShowHidingWaitHud(int turnIndex, IReadOnlyList<MatchParticipant> playing)
        {
            var players = new HidingWaitPlayer[playing.Count];
            var hidingName = string.Empty;
            for (var index = 0; index < playing.Count; index++)
            {
                var name = DisplayNameOf(playing[index].PlayerIndex);
                var current = turnIndex != HidingTurns.NoTurn && index == turnIndex;
                players[index] = new HidingWaitPlayer(
                    name,
                    turnIndex != HidingTurns.NoTurn && index < turnIndex,
                    current);
                if (current)
                {
                    hidingName = name;
                }
            }

            var completed = turnIndex == HidingTurns.NoTurn ? 0 : turnIndex;
            if (!hidingWaitHudVisible)
            {
                hidingWaitHudVisible = true;
                view.ShowHidingWaitHud(completed, playing.Count, hidingName, players);
                return;
            }

            view.ShowHidingWaitHud(completed, playing.Count, hidingName, players);
        }

        private void HideHidingWaitHud()
        {
            if (!hidingWaitHudVisible)
            {
                return;
            }

            hidingWaitHudVisible = false;
            view.HideHidingWaitHud();
        }

        private void OnPlayerItemStatusesReceived(
            IReadOnlyList<PlayerItemStatusSnapshot> statuses)
        {
            view.SetPlayerItemStatuses(statuses);
        }

        private void OnPlayerInteractionStatesReceived(
            System.Collections.Generic.IReadOnlyList<PlayerInteractionStateSnapshot> states)
        {
            var localPlayerIndex = room.LocalPlayerIndex;
            if (states == null || localPlayerIndex < 0)
            {
                return;
            }

            for (var index = 0; index < states.Count; index++)
            {
                if (states[index].PlayerIndex == localPlayerIndex)
                {
                    view.SetRemainingDestructionUses(
                        states[index].RemainingDestructionUses);
                    return;
                }
            }
        }

        private string DisplayNameOf(int playerIndex)
        {
            var playing = room.MatchParticipants.CurrentValue;
            string playerId = null;
            for (var index = 0; index < playing.Count; index++)
            {
                if (playing[index].PlayerIndex == playerIndex)
                {
                    playerId = playing[index].PlayerId;
                    break;
                }
            }

            if (!string.IsNullOrEmpty(playerId))
            {
                var participants = room.Participants.CurrentValue;
                for (var index = 0; index < participants.Count; index++)
                {
                    var participant = participants[index];
                    if (!string.Equals(
                            participant.PlayerId,
                            playerId,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    return string.IsNullOrEmpty(participant.Nickname)
                        ? playerId
                        : participant.Nickname;
                }

                return playerId;
            }

            return $"플레이어 {playerIndex + 1}";
        }

        private void FindSceneReferences()
        {
            var interactable = UnityEngine.Object.FindFirstObjectByType<ShredderInteractable>(
                FindObjectsInactive.Exclude);
            shredder = interactable == null ? null : interactable.transform;
            worldCamera = Camera.main;
        }

        private void UpdateShredderMarker()
        {
            var activePhase = snapshot.Phase == MatchPhase.Hiding ||
                              snapshot.Phase == MatchPhase.Searching;
            if (!activePhase)
            {
                view.SetShredderMarker(default, false);
                return;
            }

            if (shredder == null || worldCamera == null)
            {
                FindSceneReferences();
            }

            if (shredder == null || worldCamera == null)
            {
                view.SetShredderMarker(default, false);
                return;
            }

            var screen = worldCamera.WorldToScreenPoint(
                shredder.position + (Vector3.up * 1.5f));
            if (screen.z <= 0f)
            {
                view.SetShredderMarker(default, false);
                return;
            }

            screen.x = Mathf.Clamp(screen.x, MarkerScreenMargin, Screen.width - MarkerScreenMargin);
            screen.y = Mathf.Clamp(screen.y, MarkerScreenMargin, Screen.height - MarkerScreenMargin);
            view.SetShredderMarker(screen, true);
        }
    }
}
