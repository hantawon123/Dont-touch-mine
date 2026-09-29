using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core.Lobby;
using Game.Core.Flow;
using Game.Core.Match;
using Game.Core.Rooms;
using Game.Network.Match;
using Game.Network.Session;
using Game.Server.Match;
using UnityEngine;
using VContainer.Unity;

namespace Game.Bootstrap
{
    // Copied into the isolated validation build only. Uses production commands,
    // authentication, scenes and rule timers; never writes authority state.
    public sealed class ServerFlowValidation : IAsyncStartable, IDisposable
    {
        private readonly RoomUiCommands commands;
        private readonly NetworkRunnerService network;
        private readonly RoomBrowserSystem room;
        private readonly AppFlowSystem appFlow;
        private MatchPhase phase;
        private int rounds, assignments;
        private bool sawSearching, sawResult;
        private int peer;

        public ServerFlowValidation(RoomUiCommands commands, NetworkRunnerService network, RoomBrowserSystem room, AppFlowSystem appFlow)
        {
            this.commands = commands;
            this.network = network;
            this.room = room;
            this.appFlow = appFlow;
        }

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            if (DedicatedServerStartup.IsRequested ||
                !int.TryParse(DedicatedServerStartup.Argument("-validationPeer"), out peer)) return;
            network.MatchStateReceived += OnPhase;
            network.ItemAssignmentReceived += OnAssignment;
            network.MatchResultReceived += OnResult;
            try
            {
                Application.runInBackground = true;
                Application.targetFrameRate = 30;
                QualitySettings.vSyncCount = 0;
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                using var timer = timeout.CancelAfterSlim(TimeSpan.FromMinutes(10), DelayType.Realtime);
                var token = timeout.Token;
                var entry = peer == 1 ?
                    await commands.CreateAsync(new RoomCreateRequest("988 local test", false, null, 6, "supermarket"), token) :
                    await commands.EnterByCodeAsync(DedicatedServerStartup.Argument("-roomCode"), null, token);
                if (!entry.Ok || network.IsServer) throw new InvalidOperationException("Client entry/role failed: " + entry.Failure);
                // Match the normal frontend's successful room-entry navigation.
                if (!appFlow.TryTransitionTo(AppFlowState.Lobby)) throw new InvalidOperationException("Room navigation failed");
                if (!network.EnterLobbyScene()) throw new InvalidOperationException("Lobby entry failed");
                Debug.Log($"[Flow988] peer={peer} CONNECTED role=Client");
                Screen.SetResolution(640, 360, FullScreenMode.Windowed);
                if (peer == 1)
                {
                    await UniTask.WaitUntil(() => network.IsLocalRoomOwner && network.IsSceneLoadComplete, cancellationToken: token);
                    MatchRuleSettings.TryCreate(10, 1, 1f, 3, string.Empty, out var rules, out _);
                    if (!network.TryApplyLobbySettings(6, 5, "supermarket", rules, "988 local test"))
                        throw new InvalidOperationException("Owner settings request failed");
                    await UniTask.WaitUntil(() => network.TryReadLobbySettings(out var settings) &&
                        settings.MatchRules.HidingDurationSeconds == 10 && network.PlayerCount == 6, cancellationToken: token);
                    network.RequestMatchStart();
                }
                else
                {
                    // Production entry rejects management requests from every guest.
                    if (network.IsLocalRoomOwner || network.TryKickPlayer("P1") ||
                        network.TryApplyLobbySettings(2, 5, "supermarket", MatchRuleSettings.Default))
                        throw new InvalidOperationException("Guest received management authority");
                    // Bypass the UI check too: the authority must reject this RPC.
                    // StartGame can finish before the room-state object arrives.
                    // Keep asking until the replicated object can deliver a refusal.
                    while (room.LastStartRefusal.CurrentValue != RoomStartResult.NotHost)
                    {
                        network.RequestMatchStart();
                        await UniTask.Delay(500, DelayType.Realtime, cancellationToken: token);
                    }
                    Debug.Log($"[Flow988] peer={peer} GUEST_START_REFUSED");
                }
                var nextReport = 0d;
                var searchingSince = -1d;
                var rematchRequested = false;
                var resultSceneSeen = false;
                while (network.IsRunning)
                {
                    var now = Time.realtimeSinceStartupAsDouble;
                    if (phase == MatchPhase.Searching && searchingSince < 0) searchingSince = now;
                    if (peer == 6 && searchingSince >= 0 && now - searchingSince > 5d)
                    {
                        await commands.LeaveAsync(token);
                        Debug.Log("[Flow988] peer=6 LEFT_DURING_MATCH");
                        break;
                    }
                    if (network.IsResultSceneLoaded && !resultSceneSeen)
                    {
                        resultSceneSeen = true;
                        Debug.Log($"[Flow988] peer={peer} RESULT_SCENE_LOADED");
                    }
                    if (peer == 1 && rounds == 1 && sawResult && network.IsWaitingForMatch && network.IsSceneLoadComplete && !rematchRequested)
                    {
                        rematchRequested = true;
                        Debug.Log("[Flow988] peer=1 REMATCH_REQUEST");
                        network.RequestMatchStart();
                    }
                    if (peer == 1 && rounds >= 2 && phase == MatchPhase.Searching)
                    {
                        await UniTask.Delay(3000, DelayType.Realtime, cancellationToken: token);
                        await commands.LeaveAsync(token);
                        Debug.Log("[Flow988] peer=1 OWNER_CLOSED_AFTER_REMATCH");
                        break;
                    }
                    if (now >= nextReport)
                    {
                        nextReport = now + 5d;
                        Debug.Log($"[Flow988] peer={peer} phase={phase} players={network.PlayerCount} rounds={rounds} assignments={assignments} serverTime={network.ServerTime:F2}");
                    }
                    await UniTask.Delay(250, DelayType.Realtime, cancellationToken: token);
                }
                await UniTask.WaitUntil(() => !network.IsRoomExitPending, cancellationToken: token);
                if (!sawSearching || assignments == 0 || (peer != 6 && (rounds < 2 || !sawResult)))
                    throw new InvalidOperationException("Missing complete game flow evidence");
                Debug.Log($"[Flow988] peer={peer} PASS rounds={rounds} result={sawResult}");
                if (!Application.isEditor) Application.Quit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Flow988] peer={peer} FAIL {exception.GetType().Name}: {exception.Message}");
                network.Shutdown();
                if (!Application.isEditor) Application.Quit(1);
            }
        }

        private void OnPhase(MatchStateSnapshot snapshot)
        {
            if (snapshot.Phase == MatchPhase.Hiding && phase != MatchPhase.Hiding) rounds++;
            phase = snapshot.Phase;
            sawSearching |= phase == MatchPhase.Searching;
            sawResult |= phase == MatchPhase.Result;
        }
        private void OnAssignment(string item) { if (!string.IsNullOrEmpty(item)) assignments++; }
        private void OnResult(MatchResult result)
        {
            sawResult = true;
            Debug.Log($"[Flow988] peer={peer} RESULT_RECEIVED");
        }
        public void Dispose()
        {
            network.MatchStateReceived -= OnPhase;
            network.ItemAssignmentReceived -= OnAssignment;
            network.MatchResultReceived -= OnResult;
        }
    }
}
