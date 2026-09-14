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
                return Array.IndexOf(Environment.GetCommandLineArgs(), "-gameServer") >= 0;
#endif
            }
        }

        internal static string Argument(string name, string fallback = null)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return fallback;
#else
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
                var code = Argument("-roomCode");
                if (!Application.isBatchMode || !Game.Network.Lobby.RoomCodeGenerator.IsWellFormed(code))
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
                var emptySince = Time.realtimeSinceStartupAsDouble;
                var startedAt = emptySince;
                while (network.IsRunning)
                {
                    if (network.IsAwaitingRoomClaim && Time.realtimeSinceStartupAsDouble - startedAt > 120d) break;
                    if (network.PlayerCount > 0) emptySince = Time.realtimeSinceStartupAsDouble;
                    if (Time.realtimeSinceStartupAsDouble - emptySince > 120d) break;
                    await UniTask.Delay(250, DelayType.Realtime, cancellationToken: cancellation);
                }
                network.Shutdown();
                await UniTask.WaitUntil(() => !network.IsRoomExitPending, cancellationToken: cancellation);
                Debug.Log("[Server] Session closed.");
                if (!Application.isEditor) Application.Quit(0);
            }
            catch (OperationCanceledException) { network.Shutdown(); }
            catch (Exception exception)
            {
                network.Shutdown();
                Debug.LogException(exception);
                if (!Application.isEditor) Application.Quit(1);
            }
        }
    }
}
