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
        private Transform shredder;
        private Camera worldCamera;

        /// <summary>
        /// What the phase line currently says, so a turn that has not moved is
        /// not written to the view on every tick.
        /// </summary>
        private string reportedHidingName = string.Empty;
        private MatchPhase reportedPhase;
        private bool hasReportedPhase;

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
            events.ItemAssignmentReceived += OnItemAssignmentReceived;
            events.ItemDestroyedReceived += OnItemDestroyedReceived;
            events.PlayerInteractionStatesReceived += OnPlayerInteractionStatesReceived;
            view.HideDestructionNotice();
            view.SetRemainingDestructionUses(-1);
            view.SetShredderMarker(default, false);
            FindSceneReferences();
        }

        public void Dispose()
        {
            events.MatchStateReceived -= OnMatchStateReceived;
            events.ItemAssignmentReceived -= OnItemAssignmentReceived;
            events.ItemDestroyedReceived -= OnItemDestroyedReceived;
            events.PlayerInteractionStatesReceived -= OnPlayerInteractionStatesReceived;
            view.HideDestructionNotice();
            view.SetShredderMarker(default, false);
        }

        public void Tick()
        {
            if (!hasSnapshot)
            {
                return;
            }

            var now = clock.ServerTime;
            view.SetRemainingSeconds(Math.Max(0d, snapshot.PhaseEndsAt - now));

            // Whose turn it is moves with time, not with any event: the phase
            // stays Hiding while the turn travels down the line-up.
            ReportPhase();

            if (snapshot.Phase == MatchPhase.Highlight)
            {
                UpdateReplayNotice(playback?.PlaybackSourceTime);
            }
            else if (noticeEndsAt > 0d && now >= noticeEndsAt)
            {
                noticeEndsAt = 0d;
                view.HideDestructionNotice();
            }

            UpdateShredderMarker();
        }

        private void OnMatchStateReceived(MatchStateSnapshot received)
        {
            if ((!hasSnapshot || snapshot.Phase != received.Phase) &&
                (received.Phase == MatchPhase.Hiding || received.Phase == MatchPhase.Waiting))
                destructions.Clear();
            if (received.Phase == MatchPhase.Highlight || received.Phase == MatchPhase.Result)
            {
                noticeEndsAt = 0d;
                view.HideDestructionNotice();
            }
            snapshot = received;
            hasSnapshot = true;
            ReportPhase();
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
                rules.HidingTurnDurationSeconds);

            return turnIndex == HidingTurns.NoTurn
                ? string.Empty
                : DisplayNameOf(playing[turnIndex].PlayerIndex);
        }

        private void OnItemDestroyedReceived(PlayerItemDestroyedEvent confirmed)
        {
            destructions.Add(confirmed);
            if (hasSnapshot && (snapshot.Phase == MatchPhase.Highlight || snapshot.Phase == MatchPhase.Result))
                return;
            view.ShowDestructionNotice(
                $"{DisplayNameOf(confirmed.DestroyerPlayerIndex)}님이 물건을 파괴했습니다!");
            noticeEndsAt = Math.Max(clock.ServerTime, confirmed.DestroyedAt) +
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
            view.SetAssignedItem(ItemCatalog.DisplayNameOf(itemId));
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
