using System;
using Game.Client.Match;
using Game.Core.Lobby;
using Game.Core.Match;
using Game.Network.Match;
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
        private readonly INetworkMatchHudView view;

        private MatchStateSnapshot snapshot;
        private bool hasSnapshot;
        private double noticeEndsAt;
        private Transform shredder;
        private Camera worldCamera;

        public NetworkMatchHudPresenter(
            INetworkMatchEvents events,
            INetworkMatchRuntimeSource clock,
            RoomBrowserSystem room,
            INetworkMatchHudView view)
        {
            this.events = events ?? throw new ArgumentNullException(nameof(events));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.room = room ?? throw new ArgumentNullException(nameof(room));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
        }

        public void Start()
        {
            events.MatchStateReceived += OnMatchStateReceived;
            events.ItemDestroyedReceived += OnItemDestroyedReceived;
            view.HideDestructionNotice();
            view.SetShredderMarker(default, false);
            FindSceneReferences();
        }

        public void Dispose()
        {
            events.MatchStateReceived -= OnMatchStateReceived;
            events.ItemDestroyedReceived -= OnItemDestroyedReceived;
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

            if (noticeEndsAt > 0d && now >= noticeEndsAt)
            {
                noticeEndsAt = 0d;
                view.HideDestructionNotice();
            }

            UpdateShredderMarker();
        }

        private void OnMatchStateReceived(MatchStateSnapshot received)
        {
            snapshot = received;
            hasSnapshot = true;
            view.SetPhase(received.Phase);
        }

        private void OnItemDestroyedReceived(PlayerItemDestroyedEvent confirmed)
        {
            view.ShowDestructionNotice(
                $"{DisplayNameOf(confirmed.DestroyerPlayerIndex)}님이 물건을 파괴했습니다!");
            noticeEndsAt = Math.Max(clock.ServerTime, confirmed.DestroyedAt) +
                           NoticeDurationSeconds;
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
