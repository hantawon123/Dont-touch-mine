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
        private Game.Core.Settings.InterfacePresentation presentation;
        [VContainer.Inject]
        public void BindPresentation(Game.Core.Settings.InterfacePresentation value) => presentation = value;
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
        private bool introReadySent;
        private double noticeEndsAt;
        private double gameEndNoticeEndsAt = -1d;
        // 맵에 파쇄기가 여러 대일 수 있으므로 전부 모아 두고, 카메라(=로컬 플레이어)에 가장 가까운 것을 표시한다.
        private readonly List<Transform> shredders = new();
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
        public bool BlocksGameplayInput => hidingIntroVisible || searchingIntroVisible;

        private bool hidingIntroVisible;
        private bool hidingIntroOpenedThisPhase;
        private double hidingIntroEndsAt;
        private bool searchingIntroVisible;
        private bool searchingIntroOpenedThisPhase;
        private double searchingIntroEndsAt;
        private bool hidingTurnStartVisible;
        private bool hidingActiveHudVisible;
        private bool hidingWaitHudVisible;
        private int localHitCount;

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
            if (presentation != null) presentation.Changed += OnPresentationChanged;
            events.MatchStateReceived += OnMatchStateReceived;
            events.MatchResultReceived += OnMatchResultReceived;
            events.ItemAssignmentReceived += OnItemAssignmentReceived;
            events.ItemDestroyedReceived += OnItemDestroyedReceived;
            events.PlayerItemStatusesReceived += OnPlayerItemStatusesReceived;
            events.PlayerInteractionStatesReceived += OnPlayerInteractionStatesReceived;
            view.HideDestructionNotice();
            view.SetRemainingDestructionUses(-1);
            RefreshDestroyedItems();
            view.SetShredderMarker(default, false);
            view.HideHidingIntro();
            view.HideSearchingIntro();
            view.HideHidingTurnStart();
            view.HideHidingActiveHud();
            view.HideHidingWaitHud();
            view.HideVitals();
            view.SetMatchChatMode(MatchChatHudMode.Hidden);
            view.SetPlayerStatusVisible(false);
            FindSceneReferences();
        }

        public void Dispose()
        {
            if (presentation != null) presentation.Changed -= OnPresentationChanged;
            events.MatchStateReceived -= OnMatchStateReceived;
            events.MatchResultReceived -= OnMatchResultReceived;
            events.ItemAssignmentReceived -= OnItemAssignmentReceived;
            events.ItemDestroyedReceived -= OnItemDestroyedReceived;
            events.PlayerItemStatusesReceived -= OnPlayerItemStatusesReceived;
            events.PlayerInteractionStatesReceived -= OnPlayerInteractionStatesReceived;
            view.HideDestructionNotice();
            view.SetDestroyedItems(0, Array.Empty<PlayerItemStatusSnapshot>());
            view.SetShredderMarker(default, false);
            HideHidingIntro();
            HideSearchingIntro();
            HideHidingTurnStart();
            HideHidingActiveHud();
            HideHidingWaitHud();
            HideVitals();
        }

        private void OnPresentationChanged()
        {
            view.HideDestructionNotice();
            noticeEndsAt = 0;
            hasReportedPhase = false;
            if (hasSnapshot && clock.IsRuntimeReady) ReportPhase();
        }

        public void Tick()
        {
            if (!hasSnapshot || !clock.IsRuntimeReady)
            {
                return;
            }

            var now = clock.ServerTime;
            if (snapshot.Phase == MatchPhase.Hiding &&
                Cursor.lockState == CursorLockMode.Locked &&
                UnityEngine.InputSystem.Keyboard.current?.yKey.wasPressedThisFrame == true &&
                UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject == null &&
                HidingTurns.IndexAt(snapshot.Phase, snapshot.PhaseEndsAt, now,
                    room.MatchParticipants.CurrentValue.Count, HidingTurnDurationSeconds) == room.LocalPlayerIndex &&
                clock is Game.Network.Session.NetworkRunnerService network)
                network.RequestCompleteHidingTurn();
            view.SetRemainingSeconds(snapshot.Phase == MatchPhase.Hiding
                ? HidingTurns.RemainingSecondsAt(
                    snapshot.Phase,
                    snapshot.PhaseEndsAt,
                    now,
                    room.MatchParticipants.CurrentValue.Count,
                    HidingTurnDurationSeconds)
                : snapshot.Phase == MatchPhase.Searching
                    ? snapshot.PhaseEndsAt == 0d ? SearchingDurationSeconds : Math.Min(SearchingDurationSeconds, Math.Max(0d, snapshot.PhaseEndsAt - now))
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
            UpdateSearchingIntro(now);
            if (!introReadySent && snapshot.PhaseEndsAt == 0d &&
                view.IsPhaseIntroPresented(snapshot.Phase) && events is INetworkPhaseIntroReady ready)
                introReadySent = ready.TryConfirmPhaseIntroReady(snapshot.Phase);
            UpdateHidingTurnStart(now);
            UpdateShredderMarker();
            UpdateVitals();
        }

        private void OnMatchStateReceived(MatchStateSnapshot received)
        {
            if ((!hasSnapshot || snapshot.Phase != received.Phase) &&
                (received.Phase == MatchPhase.Hiding || received.Phase == MatchPhase.Waiting))
            {
                destructions.Clear();
                localHitCount = 0;
            }
            if (received.Phase == MatchPhase.Hiding || received.Phase == MatchPhase.Waiting)
            {
                gameEndNoticeEndsAt = -1d;
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

            if (received.Phase != MatchPhase.Searching)
            {
                searchingIntroOpenedThisPhase = false;
                HideSearchingIntro();
            }

            if (!hasSnapshot || snapshot.Phase != received.Phase) introReadySent = false;
            snapshot = received;
            hasSnapshot = true;
            var extrasVisible = received.Phase != MatchPhase.Hiding;
            ApplyMatchChat();
            view.SetPlayerStatusVisible(extrasVisible);
            RefreshDestroyedItems();
            ReportPhase();
            UpdateGameEndNotice();
            TryShowHidingIntro();
            TryShowSearchingIntro();
            UpdateHidingTurnStart(clock.IsRuntimeReady ? clock.ServerTime : 0d);
            UpdateVitals();
        }

        private void OnMatchResultReceived(MatchResult result)
        {
            if (result.EndReason == MatchEndReason.LastPlayerStanding) return;
            gameEndNoticeEndsAt = result.EndedAt + HighlightPresentationTiming.FadeSeconds;
            UpdateGameEndNotice();
        }

        private bool UpdateGameEndNotice()
        {
            if (!clock.IsRuntimeReady) return false;
            var remaining = gameEndNoticeEndsAt - clock.ServerTime;
            var active = remaining > 0d && (!hasSnapshot || snapshot.Phase != MatchPhase.Result);
            view.SetEndCountdown(0d);
            if (!active) return false;
            view.HideDestructionNotice();
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

        private double SearchingDurationSeconds =>
            clock.MatchRules.SearchingDurationSeconds > 0
                ? clock.MatchRules.SearchingDurationSeconds
                : rules.SearchingDurationSeconds;

        private double FinalWarningSeconds =>
            rules.FinalWarningSeconds > 0f
                ? rules.FinalWarningSeconds
                : MatchTimerView.WarningSeconds;

        private int HitsRequiredToStun =>
            clock.MatchRules.StunHitCount > 0
                ? clock.MatchRules.StunHitCount
                : rules.HitsRequiredToStun;

        private void OnItemDestroyedReceived(PlayerItemDestroyedEvent confirmed)
        {
            destructions.Add(confirmed);
            RefreshDestroyedItems();
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
            RefreshDestroyedItems();
            if (hidingIntroVisible)
            {
                view.ShowHidingIntro(assignedItemDisplayName, assignedItemId);
                return;
            }

            if (searchingIntroVisible)
            {
                view.ShowSearchingIntro(assignedItemDisplayName);
                return;
            }

            TryShowHidingIntro();
            TryShowSearchingIntro();
        }

        private void UpdateHidingIntro(double now)
        {
            TryShowHidingIntro();
            if (hidingIntroVisible) hidingIntroEndsAt = snapshot.PhaseEndsAt == 0d ? double.PositiveInfinity :
                snapshot.PhaseEndsAt - HidingTurnDurationSeconds * room.MatchParticipants.CurrentValue.Count;
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
            var endsAt = snapshot.PhaseEndsAt == 0d ? double.PositiveInfinity : startedAt;
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

        private void UpdateSearchingIntro(double now)
        {
            TryShowSearchingIntro();
            if (searchingIntroVisible) searchingIntroEndsAt = snapshot.PhaseEndsAt == 0d ? double.PositiveInfinity :
                snapshot.PhaseEndsAt - SearchingDurationSeconds;
            if (searchingIntroVisible && now >= searchingIntroEndsAt)
            {
                HideSearchingIntro();
            }
        }

        private void TryShowSearchingIntro()
        {
            if (searchingIntroOpenedThisPhase ||
                !hasSnapshot ||
                snapshot.Phase != MatchPhase.Searching ||
                string.IsNullOrEmpty(assignedItemDisplayName) ||
                !clock.IsRuntimeReady)
            {
                return;
            }

            var startedAt = snapshot.PhaseEndsAt - SearchingDurationSeconds;
            var endsAt = snapshot.PhaseEndsAt == 0d ? double.PositiveInfinity : startedAt;
            if (clock.ServerTime >= endsAt)
            {
                return;
            }

            searchingIntroEndsAt = endsAt;
            searchingIntroOpenedThisPhase = true;
            searchingIntroVisible = true;
            view.ShowSearchingIntro(assignedItemDisplayName);
        }

        private void HideSearchingIntro()
        {
            if (!searchingIntroVisible)
            {
                return;
            }

            searchingIntroVisible = false;
            view.HideSearchingIntro();
        }

        private void UpdateVitals()
        {
            if (!hasSnapshot || snapshot.Phase != MatchPhase.Searching)
            {
                HideVitals();
                return;
            }

            if (!clock.TryGetLocalStamina(out var stamina, out var maxStamina, out var exhausted) ||
                maxStamina <= 0f)
            {
                stamina = MatchVitalsHudView.DefaultStamina;
                maxStamina = MatchVitalsHudView.DefaultStamina;
                exhausted = false;
            }

            var maxHits = HitsRequiredToStun;
            view.ShowVitals(
                stamina,
                maxStamina,
                MatchVitalsHudView.RemainingHits(localHitCount, maxHits),
                maxHits,
                exhausted);
        }

        private void HideVitals()
        {
            view.HideVitals();
        }

        private void ApplyMatchChat(bool showHidingWaitChat = false)
        {
            if (!hasSnapshot)
            {
                view.SetMatchChatMode(MatchChatHudMode.Hidden);
                return;
            }

            if (snapshot.Phase == MatchPhase.Hiding)
            {
                view.SetMatchChatMode(
                    showHidingWaitChat ? MatchChatHudMode.Full : MatchChatHudMode.Hidden);
                return;
            }

            if (snapshot.Phase == MatchPhase.Searching)
            {
                view.SetMatchChatMode(MatchChatHudMode.Searching);
                return;
            }

            view.SetMatchChatMode(MatchChatHudMode.Full);
        }

        private void UpdateHidingTurnStart(double now)
        {
            if (hasSnapshot && snapshot.Phase == MatchPhase.Searching)
            {
                UpdateFinalWarningOverlay(now);
                return;
            }

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
                    ApplyMatchChat();
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
            var overlayStartsAt = turnStartedAt;
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
                ApplyMatchChat();
                return;
            }

            ShowHidingWaitHud(turnIndex, playing, remaining);
            ApplyMatchChat(showHidingWaitChat: true);
        }

        private void UpdateFinalWarningOverlay(double now)
        {
            if (searchingIntroVisible || !clock.IsRuntimeReady)
            {
                HideHidingTurnStart();
                view.SetTopHudVisible(true);
                return;
            }

            var warningSeconds = FinalWarningSeconds;
            var warningStartedAt = snapshot.PhaseEndsAt - warningSeconds;
            var remaining = Math.Max(0d, snapshot.PhaseEndsAt - now);
            var showOverlay = remaining > 0d &&
                              now >= warningStartedAt &&
                              now < warningStartedAt + HidingTurnStartView.VisibleSeconds;

            if (showOverlay)
            {
                if (!hidingTurnStartVisible)
                {
                    hidingTurnStartVisible = true;
                    view.ShowHidingTurnStart(
                        remaining,
                        HidingTurnStartView.FinalWarningBannerText);
                }
                else
                {
                    view.SetHidingTurnStartSeconds(remaining);
                }

                return;
            }

            HideHidingTurnStart();
            view.SetTopHudVisible(true);
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

        private void ShowHidingWaitHud(
            int turnIndex,
            IReadOnlyList<MatchParticipant> playing,
            double remainingSeconds)
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

            var completed = turnIndex == HidingTurns.NoTurn ? 0 : turnIndex + 1;
            var showNextTurn = turnIndex != HidingTurns.NoTurn &&
                               room.LocalPlayerIndex == turnIndex + 1;
            if (!hidingWaitHudVisible)
            {
                hidingWaitHudVisible = true;
                view.ShowHidingWaitHud(
                    completed,
                    playing.Count,
                    hidingName,
                    players,
                    showNextTurn,
                    remainingSeconds,
                    HidingTurnDurationSeconds);
                return;
            }

            view.ShowHidingWaitHud(
                completed,
                playing.Count,
                hidingName,
                players,
                showNextTurn,
                remainingSeconds,
                HidingTurnDurationSeconds);
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
            RefreshDestroyedItems();
        }

        private void RefreshDestroyedItems()
        {
            var order = new string[destructions.Count];
            for (var index = 0; index < destructions.Count; index++)
            {
                order[index] = destructions[index].ItemId;
            }

            view.SetDestroyedItems(
                room.MatchParticipants.CurrentValue.Count,
                events.LatestPlayerItemStatuses,
                assignedItemId,
                order);
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
                    localHitCount = states[index].HitCount;
                    view.SetRemainingDestructionUses(
                        states[index].RemainingDestructionUses);
                    UpdateVitals();
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

                    var name = string.IsNullOrEmpty(participant.Nickname) ? playerId : participant.Nickname;
                    return presentation == null ? name : presentation.Name(playerId, name);
                }

                return presentation == null ? playerId : presentation.Name(playerId, playerId);
            }

            return $"플레이어 {playerIndex + 1}";
        }

        private void FindSceneReferences()
        {
            shredders.Clear();
            var interactables = UnityEngine.Object.FindObjectsByType<ShredderInteractable>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            foreach (var interactable in interactables)
            {
                if (interactable != null)
                {
                    shredders.Add(interactable.transform);
                }
            }

            worldCamera = Camera.main;
        }

        private bool TryGetNearestShredder(Vector3 from, out Transform nearest)
        {
            nearest = null;
            var bestDistance = float.MaxValue;
            for (var index = shredders.Count - 1; index >= 0; index--)
            {
                var candidate = shredders[index];
                if (candidate == null)
                {
                    shredders.RemoveAt(index);
                    continue;
                }

                var distance = (candidate.position - from).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    nearest = candidate;
                }
            }

            return nearest != null;
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

            if (shredders.Count == 0 || worldCamera == null)
            {
                FindSceneReferences();
            }

            if (worldCamera == null ||
                !TryGetNearestShredder(worldCamera.transform.position, out var shredder))
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
