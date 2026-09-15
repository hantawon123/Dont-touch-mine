using System;
using System.Threading;
using Cysharp.Threading.Tasks;
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
        public DedicatedServerStartup(NetworkRunnerService network, PlayerProfile profile, AppFlowSystem appFlow)
        {
            this.network = network;
            this.profile = profile;
            this.appFlow = appFlow;
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
                var startedAt = emptySince;
                while (network.IsRunning)
                {
#if UNITY_EDITOR
                    if (editorServer)
                        EditorDevelopmentSession.Report(network.IsRoomExitPending ? "연결 종료 대기 중" :
                            network.IsAwaitingRoomClaim ? $"새 방 배정 대기: {code} (접속 {network.PlayerCount}명)" :
                            $"방 사용 중: {code} (접속 {network.PlayerCount}명)");
#endif
                    // Owner departure is handled by the network callback, not a transient room-list count.
                    if (!editorServer && network.IsAwaitingRoomClaim && Time.realtimeSinceStartupAsDouble - startedAt > 120d) break;
                    if (network.PlayerCount > 0) emptySince = Time.realtimeSinceStartupAsDouble;
                    if (!editorServer && Time.realtimeSinceStartupAsDouble - emptySince > 120d) break;
                    await UniTask.Delay(250, DelayType.Realtime, cancellationToken: cancellation);
                }
#if UNITY_EDITOR
                if (editorServer) EditorDevelopmentSession.Report("이전 연결 종료 및 다음 방 준비 중");
#endif
                network.Shutdown();
                await UniTask.WaitUntil(() => !network.IsRoomExitPending, cancellationToken: cancellation);
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
    }
}
