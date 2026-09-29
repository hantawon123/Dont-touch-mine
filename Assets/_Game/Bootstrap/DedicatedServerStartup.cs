using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Backend;
using Game.Core.Home;
using Game.Core.Flow;
using Game.Core.Maps;
using Game.Network.Session;
using UnityEngine;
using VContainer.Unity;

namespace Game.Bootstrap
{
    // Explicit local/server process entry. A browser can never select this role.
    public sealed class DedicatedServerStartup : IAsyncStartable
    {
        private const double AnalyticsDrainSeconds = 60d;

        public static bool IsRequested
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return false;
#else
#if UNITY_EDITOR
                if (EditorDevelopmentSession.IsServer) return true;
#endif
                return Array.IndexOf(Environment.GetCommandLineArgs(), "-gameServer") >= 0;
#endif
            }
        }

        internal static string Argument(string name, string fallback = null)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return fallback;
#else
#if UNITY_EDITOR
            if (EditorDevelopmentSession.IsServer)
            {
                if (name == "-roomCode") return EditorDevelopmentSession.Code;
                if (name == "-region") return "kr";
            }
#endif
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
#endif
        }

        /// <summary>
        /// A secret the host handed this process, or null.
        /// </summary>
        /// <remarks>
        /// Through the environment rather than the command line, because arguments are readable
        /// by anyone who can run <c>ps</c> on the box. Addresses and room codes go on the command
        /// line; keys do not.
        /// </remarks>
        internal static string Secret(string name)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return null;
#else
            return Environment.GetEnvironmentVariable(name);
#endif
        }

        internal static string DeviceId()
        {
            const string key = "game.server.deviceId";
            var value = PlayerPrefs.GetString(key, string.Empty);
            if (value.Length != 0) return value;
            value = Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(key, value);
            PlayerPrefs.Save();
            return value;
        }

        private readonly NetworkRunnerService network;
        private readonly PlayerProfile profile;
        private readonly AppFlowSystem appFlow;
        private readonly MatchAnalyticsUpload analytics;
        private readonly ChatModerationService chat;
        public DedicatedServerStartup(NetworkRunnerService network, PlayerProfile profile, AppFlowSystem appFlow,
            MatchAnalyticsUpload analytics, ChatModerationService chat)
        {
            this.network = network;
            this.profile = profile;
            this.appFlow = appFlow;
            this.analytics = analytics;
            this.chat = chat;
        }

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            try
            {
                var editorServer = false;
#if UNITY_EDITOR
                editorServer = EditorDevelopmentSession.IsServer;
                if (editorServer) EditorDevelopmentSession.Report("인증 및 방 준비 중");
#endif
                var code = Argument("-roomCode");
                Application.runInBackground = true;
                if ((!Application.isBatchMode && !editorServer) || !Game.Network.Lobby.RoomCodeGenerator.IsWellFormed(code))
                    throw new InvalidOperationException("Use -batchmode -gameServer -roomCode <6-character code>.");
                // Before the room opens, so the first message is already filtered. A failure
                // here logs and carries on: a room that will not open over a word list is worse
                // than a room without one.
                await chat.LoadAsync(cancellation);

                var request = SessionRequest.AvailableServer(code, MapCatalog.DefaultMapId);
                var result = await network.StartAsync(request, cancellation);
                if (!result.Ok) throw new InvalidOperationException("Server session could not start: " + result.Failure);
                if (!network.IsDedicatedServer || string.IsNullOrEmpty(profile.PhotonToken))
                    throw new InvalidOperationException("Authenticated server role was not established.");
                // Clients enter the session through the frontend's room action.
                // Headless startup must make the same application-flow transition.
                if (!appFlow.TryTransitionTo(AppFlowState.Lobby))
                    throw new InvalidOperationException("Server application could not enter the room flow.");
                if (!network.EnterLobbyScene()) throw new InvalidOperationException("Server could not enter lobby scene.");
                Debug.Log("[Server] Ready; waiting for first room owner.");
#if UNITY_EDITOR
                if (editorServer) EditorDevelopmentSession.Report("준비됨: " + code);
#endif
                var emptySince = Time.realtimeSinceStartupAsDouble;
                double? claimStartedAt = null;
                while (network.IsRunning)
                {
#if UNITY_EDITOR
                    if (editorServer)
                        EditorDevelopmentSession.Report(network.IsRoomExitPending ? "연결 종료 대기 중" :
                            network.IsAwaitingRoomClaim ? $"새 방 배정 대기: {code} (접속 {network.PlayerCount}명)" :
                            $"방 사용 중: {code} (접속 {network.PlayerCount}명)");
#endif
                    // Owner departure is handled by the network callback, not a transient room-list count.
                    if (!editorServer && ShouldCloseIdleRoom(network.IsAwaitingRoomClaim, network.PlayerCount,
                            Time.realtimeSinceStartupAsDouble, ref emptySince, ref claimStartedAt)) break;
                    await UniTask.Delay(250, DelayType.Realtime, cancellationToken: cancellation);
                }
#if UNITY_EDITOR
                if (editorServer) EditorDevelopmentSession.Report("이전 연결 종료 및 다음 방 준비 중");
#endif
                network.Shutdown();
                await UniTask.WaitUntil(() => !network.IsRoomExitPending, cancellationToken: cancellation);
                await DrainUploadsAsync(cancellation);
                Debug.Log("[Server] Session closed.");
                if (!Application.isEditor) Application.Quit(0);
#if UNITY_EDITOR
                if (editorServer && UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    EditorDevelopmentSession.RestartRequested = true;
                    UnityEditor.EditorApplication.isPlaying = false;
                }
#endif
            }
            // Scope disposal owns network cleanup. Its canceled startup continuation must not shut down twice.
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            catch (Exception exception)
            {
                network.Shutdown();
                Debug.LogException(exception);
#if UNITY_EDITOR
                if (EditorDevelopmentSession.IsServer)
                {
                    EditorDevelopmentSession.Report("시작 실패: Console 확인");
                    UnityEditor.EditorApplication.isPlaying = false;
                }
#endif
                if (!Application.isEditor) Application.Quit(1);
            }
        }

        internal static bool ShouldCloseIdleRoom(bool awaitingClaim, int playerCount, double now,
            ref double emptySince, ref double? claimStartedAt)
        {
            // A healthy, unused warm room belongs to the pool. Do not restart it on a timer.
            if (awaitingClaim && playerCount == 0 && !claimStartedAt.HasValue)
            {
                emptySince = now;
                return false;
            }

            if (awaitingClaim)
            {
                // Once a client arrives, keep the existing deadline for an unfinished claim.
                // Reconnects must not reset it and monopolize a pool slot indefinitely.
                claimStartedAt ??= now;
                if (now - claimStartedAt.Value > 120d) return true;
            }

            if (playerCount > 0) emptySince = now;
            return now - emptySince > 120d;
        }

        /// <summary>
        /// Sends the finished match before the process ends. The recorder only
        /// closes the match on its first tick after the shutdown above, so this
        /// waits a frame before asking. A server's outbox lives in the container
        /// and does not survive it, so a match left here is a match lost.
        /// </summary>
        private async UniTask DrainUploadsAsync(CancellationToken cancellation)
        {
            await UniTask.NextFrame(cancellation);

            // Chat first: it is a handful of small requests, and the last seconds of a match are
            // what a report will be about.
            await chat.DrainAsync(cancellation);
            if (chat.HasPending)
                Debug.LogWarning("[Chat] Shutting down with chat still queued; those lines are lost.");

            analytics.Retry();
            var deadline = Time.realtimeSinceStartupAsDouble + AnalyticsDrainSeconds;
            while (analytics.IsSending && Time.realtimeSinceStartupAsDouble < deadline)
                await UniTask.Delay(250, DelayType.Realtime, cancellationToken: cancellation);
            if (analytics.IsSending)
                Debug.LogWarning("[Analytics] Shutting down with the match still uploading; it will be incomplete.");
        }
    }
}
