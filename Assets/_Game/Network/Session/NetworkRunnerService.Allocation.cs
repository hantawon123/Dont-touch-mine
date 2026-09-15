using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Fusion;
using Game.Core.Lobby;
using Game.Core.Maps;
using Game.Core.Rooms;
using Game.Network.Players;
using UnityEngine;

namespace Game.Network.Session
{
    public sealed partial class NetworkRunnerService
    {
        private bool _receivedLobbySnapshot;
        private bool _awaitingRoomClaim;
        private bool _claimAdmissionPending;
        private UniTaskCompletionSource<bool> _claimAnswer;
        public bool IsAwaitingRoomClaim => _awaitingRoomClaim;

        public async UniTask<string> FindAvailableServerAsync(CancellationToken cancellation)
        {
            if (!IsBrowsingLobby && !(await JoinLobbyAsync(cancellation)).Ok) return null;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            using var timer = timeout.CancelAfterSlim(TimeSpan.FromSeconds(30), DelayType.Realtime);
            await UniTask.WaitUntil(() => _receivedLobbySnapshot, cancellationToken: timeout.Token);
            // Availability is published by the authority. Lobby player counts lag joins/leaves;
            // admission and room-claim checks on the server prevent double allocation.
            // A small test pool replaces a finished room asynchronously. Keep
            // waiting for lobby updates instead of failing between processes.
            string availableRoom = null;
            try
            {
                await UniTask.WaitUntil(() =>
            {
                foreach (var info in _realtimeRooms.Values)
                    if (info.IsOpen &&
                        info.CustomProperties[SessionPropertyKeys.AvailableServer] is bool available && available)
                    {
                        availableRoom = info.Name;
                        return true;
                    }
                return false;
            }, cancellationToken: timeout.Token);
            }
            catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
            {
                var open = 0;
                var available = 0;
                foreach (var info in _realtimeRooms.Values)
                {
                    if (info.IsOpen) open++;
                    if (info.CustomProperties[SessionPropertyKeys.AvailableServer] is bool ready && ready) available++;
                }
                Debug.LogWarning($"[Rooms] Server allocation timed out: listed={_realtimeRooms.Count}, open={open}, available={available}.");
                throw;
            }
            return availableRoom;
        }

        public async UniTask<bool> ClaimRoomAsync(RoomCreateRequest request, CancellationToken cancellation)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            using var timer = timeout.CancelAfterSlim(TimeSpan.FromSeconds(30), DelayType.Realtime);
            try
            {
                await UniTask.WaitUntil(() => !IsRunning ||
                    (IsSceneLoadComplete && IsLocalRoomOwner && _matchStarter?.CanSendLobbyCommand == true),
                    cancellationToken: timeout.Token);
                if (!IsRunning) return false;
                _claimAnswer = new UniTaskCompletionSource<bool>();
                _matchStarter.RequestRoomClaim(request, PublicHostNickname);
                var accepted = await _claimAnswer.Task.AttachExternalCancellation(timeout.Token);
                if (accepted) _expectedPassword = request.IsLocked ? request.Password : null;
                return accepted;
            }
            finally { _claimAnswer = null; }
        }

        private void OnRoomClaimAnswered(bool accepted) => _claimAnswer?.TrySetResult(accepted);

        private void OnRoomClaimRequested(PlayerRef source, RoomCreateRequest request, string nickname)
        {
            var properties = new Dictionary<string, SessionProperty>
            {
                [SessionPropertyKeys.AvailableServer] = false,
                [SessionPropertyKeys.Locked] = request.IsLocked,
                [SessionPropertyKeys.HostNickname] = nickname,
                [SessionPropertyKeys.OpenedAt] = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
            // Publish claim and settings together: a second update can reuse stale session properties.
            var accepted = IsDedicatedServer && _awaitingRoomClaim && PlayerSpawner.IsRoomOwner(_runner, source) &&
                request.TryCreateSettings(RoomSettings.MaxPlayerCount, out var settings, out _) &&
                MapCatalog.IsLobbyChoice(settings.MapId) &&
                ApplyLobbySettings(settings.MaxPlayers, PlaySettingsDraft.DefaultDestructionLimit,
                    settings.MapId, MatchRuleSettings.Default, settings.Title, properties);
            if (accepted)
            {
                _expectedPassword = request.IsLocked ? request.Password : null;
                _runner.SessionInfo.IsVisible = !request.IsPrivate;
                _awaitingRoomClaim = false;
            }
            if (source.IsRealPlayer) _matchStarter.AnswerRoomClaim(source, accepted);
        }
    }
}
