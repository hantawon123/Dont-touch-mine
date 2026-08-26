using System;
using System.Collections.Generic;
using Game.Core.Lobby;
using Game.Core.Rooms;
using Game.Network.Session;
using R3;
using UnityEngine;

namespace Game.Bootstrap
{
    /// <summary>
    /// Backs the lobby's host controls with the real session, replacing the
    /// sample host the screen was built against.
    /// </summary>
    /// <remarks>
    /// Who the host is comes from the network rather than from a setter: the
    /// answer differs per screen and changes when the host leaves, so the room
    /// is the only thing entitled to say it.
    /// <para>
    /// Start is the one request that gets through today. Kick, host transfer and
    /// settings all need a client-to-host message, which the RPC blocker
    /// prevents; they keep their guards and say so in the log rather than
    /// failing silently.
    /// </para>
    /// </remarks>
    public sealed class NetworkLobbyHostSession : ILobbyHostSession, IDisposable
    {
        private readonly RoomBrowserSystem room;
        private readonly NetworkRunnerService network;
        private readonly ReactiveProperty<bool> isLocalHost = new(false);
        private readonly ReactiveProperty<PlaySettingsDraft> settings;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();

        public NetworkLobbyHostSession(
            RoomBrowserSystem room,
            NetworkRunnerService network,
            PlaySettingsDraft unsyncedDefaults)
        {
            this.room = room ?? throw new ArgumentNullException(nameof(room));
            this.network = network ?? throw new ArgumentNullException(nameof(network));
            settings = new ReactiveProperty<PlaySettingsDraft>(unsyncedDefaults);

            // Host-ness needs both halves, so either one arriving recomputes it.
            subscriptions.Add(this.room.Participants.Subscribe(_ => RecomputeHost()));
            subscriptions.Add(this.room.LocalPlayerId.Subscribe(_ => RecomputeHost()));

            // Only these two are known to the session; the rest of the draft
            // waits on 205.
            subscriptions.Add(this.room.RoomCode.Subscribe(_ => RepublishSettings()));
            subscriptions.Add(this.room.MaxPlayers.Subscribe(_ => RepublishSettings()));
        }

        /// <summary>Null until the session reports who this peer is.</summary>
        public string LocalPlayerId => room.LocalPlayerId.CurrentValue;

        public ReadOnlyReactiveProperty<bool> IsLocalHost => isLocalHost;
        public ReadOnlyReactiveProperty<PlaySettingsDraft> Settings => settings;

        public event Action StartRequested;
        public event Action<string> KickRequested;
        public event Action<string> HostTransferRequested;
        public event Action<PlaySettingsDraft> SettingsApplyRequested;

        /// <summary>
        /// Ignored. The room decides who hosts, and accepting this would let a
        /// screen disagree with the session it is showing.
        /// </summary>
        public void SetLocalHost(bool value)
        {
            Debug.LogWarning(
                "[Lobby] Host state comes from the room and cannot be set from a screen.");
        }

        public void ReplaceSettings(PlaySettingsDraft next)
        {
            settings.Value = next;
        }

        public void RequestStart()
        {
            if (!isLocalHost.CurrentValue)
            {
                return;
            }

            // The authority decides and answers only the peer that asked; the
            // refusal, if any, arrives on RoomBrowserSystem.LastStartRefusal.
            network.RequestMatchStart();
            StartRequested?.Invoke();
        }

        public void RequestKick(string playerId)
        {
            if (!CanActOnOther(playerId, out var target))
            {
                return;
            }

            ReportUnreachable("강퇴", "199");
            KickRequested?.Invoke(target);
        }

        public void RequestHostTransfer(string playerId)
        {
            if (!CanActOnOther(playerId, out var target))
            {
                return;
            }

            ReportUnreachable("방장 위임", "202");
            HostTransferRequested?.Invoke(target);
        }

        public void RequestApplySettings(PlaySettingsDraft draft)
        {
            if (!isLocalHost.CurrentValue)
            {
                return;
            }

            ReportUnreachable("방 설정 적용", "205");

            // Kept local so the form does not snap back on the host's own
            // screen. Nobody else sees it until 205 lands.
            SettingsApplyRequested?.Invoke(draft);
            settings.Value = draft;
        }

        public void Dispose()
        {
            foreach (var subscription in subscriptions)
            {
                subscription.Dispose();
            }

            subscriptions.Clear();
            isLocalHost.Dispose();
            settings.Dispose();
        }

        private bool CanActOnOther(string playerId, out string target)
        {
            target = playerId?.Trim();

            if (!isLocalHost.CurrentValue || string.IsNullOrWhiteSpace(target))
            {
                return false;
            }

            return !string.Equals(target, LocalPlayerId, StringComparison.Ordinal);
        }

        private void RecomputeHost()
        {
            var localId = LocalPlayerId;

            if (string.IsNullOrWhiteSpace(localId))
            {
                isLocalHost.Value = false;
                return;
            }

            var seated = room.Participants.CurrentValue;

            foreach (var one in seated)
            {
                if (string.Equals(one.PlayerId, localId, StringComparison.Ordinal))
                {
                    isLocalHost.Value = one.IsHost;
                    return;
                }
            }

            // Not seated yet, so not hosting yet.
            isLocalHost.Value = false;
        }

        private void RepublishSettings()
        {
            var current = settings.CurrentValue;

            settings.Value = new PlaySettingsDraft(
                current.Title,
                room.RoomCode.CurrentValue ?? string.Empty,
                current.PasswordEnabled,
                current.Password,
                room.MaxPlayers.CurrentValue > 0 ? room.MaxPlayers.CurrentValue : current.MaxPlayers,
                current.DestructionLimit,
                current.MapId);
        }

        private static void ReportUnreachable(string what, string ticket)
        {
            Debug.LogWarning(
                $"[Lobby] '{what}' 요청이 호스트에 도달하지 못합니다. " +
                $"클라->호스트 RPC 가 막혀 있습니다 (S15P21D205-{ticket}).");
        }
    }
}
