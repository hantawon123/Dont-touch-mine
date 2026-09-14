using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.Networking;

public sealed class TopologyProbe : MonoBehaviour, INetworkRunnerCallbacks
{
    [Serializable] private sealed class Account { public string userId, photonToken; }
    [Serializable] private sealed class Device { public string deviceId; }
    private NetworkRunner runner;
    private string room, peer, status = "starting";
    private int nonce, echoes, wrongEchoes, firstTick = -1, maxPlayers;
    private double connectedAt, nextReport;
    private bool stopping, requested, sawTwoThenOne;
    private bool server;
    private CancellationToken token;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void ProbeStatus(string value);
#endif

    private static string Arg(string name, string fallback)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        var query = new Uri(Application.absoluteURL).Query.TrimStart('?').Split('&');
        foreach (var part in query)
        {
            var pair = part.Split(new[] { '=' }, 2);
            if (pair.Length == 2 && pair[0] == name) return Uri.UnescapeDataString(pair[1]);
        }
#else
        var args = Environment.GetCommandLineArgs();
        var index = Array.IndexOf(args, "--" + name);
        if (index >= 0 && index + 1 < args.Length) return args[index + 1];
#endif
        return fallback;
    }

    private void Start()
    {
        token = this.GetCancellationTokenOnDestroy();
        RunAsync(token).Forget(e => { status = "FAIL " + e.GetType().Name + ": " + e.Message; Report(); });
    }

    private async UniTask RunAsync(CancellationToken cancellation)
    {
        Application.runInBackground = true;
        Application.targetFrameRate = 60;
        room = Arg("room", ""); peer = Arg("peer", "1");
        if (string.IsNullOrWhiteSpace(room)) throw new ArgumentException("room is required");
        server = Arg("mode", "Client") == "Server";
#if UNITY_WEBGL && !UNITY_EDITOR
        if (server) throw new InvalidOperationException("Browser cannot be the probe server");
#endif
        // Uses the existing account endpoint, never disables Photon authentication.
        // No account identifiers or tokens are written to logs or telemetry.
        var deviceKey = "topology-probe-device-" + peer;
        var device = PlayerPrefs.GetString(deviceKey, "");
        if (device.Length == 0) { device = Guid.NewGuid().ToString(); PlayerPrefs.SetString(deviceKey, device); PlayerPrefs.Save(); }
        var accountUrl = "https://j15d205.p.ssafy.io/api/v1/accounts";
#if UNITY_WEBGL && !UNITY_EDITOR
        accountUrl = new Uri(new Uri(Application.absoluteURL), "/api/v1/accounts").AbsoluteUri;
#endif
        using var request = UnityWebRequest.Post(accountUrl,
            JsonUtility.ToJson(new Device { deviceId = device }), "application/json");
        request.timeout = 15;
        await request.SendWebRequest().ToUniTask(cancellationToken: cancellation);
        var account = JsonUtility.FromJson<Account>(request.downloadHandler.text);
        if (string.IsNullOrEmpty(account?.userId) || string.IsNullOrEmpty(account.photonToken))
            throw new InvalidOperationException("Account did not issue Photon credentials");
        var auth = new AuthenticationValues { AuthType = CustomAuthenticationType.Custom, UserId = account.userId };
        auth.AddAuthParameter("userId", account.userId); auth.AddAuthParameter("token", account.photonToken);
        var settings = Fusion.Photon.Realtime.PhotonAppSettings.Global.AppSettings.GetCopy();
        settings.FixedRegion = "kr";
        // Identical on native server and browser, isolated from every game release.
        settings.AppVersion = Arg("version", "topology-987-v1");
        var config = NetworkProjectConfig.Global;
        config.AllowClientServerModesInWebGL = true;
        config.HostMigration.EnableAutoUpdate = false;
        runner = new GameObject("ProbeRunner").AddComponent<NetworkRunner>();
        DontDestroyOnLoad(gameObject); DontDestroyOnLoad(runner.gameObject);
        runner.AddCallbacks(this); runner.ProvideInput = !server;
        var scenes = new NetworkSceneInfo(); scenes.AddSceneRef(SceneRef.FromIndex(0), UnityEngine.SceneManagement.LoadSceneMode.Additive);
        var result = await runner.StartGame(new StartGameArgs {
            GameMode = server ? GameMode.Server : GameMode.Client, SessionName = room,
            PlayerCount = 2, IsVisible = false, EnableClientSessionCreation = false,
            CustomPhotonAppSettings = settings, AuthValues = auth, Config = config,
            Scene = scenes, SceneManager = runner.gameObject.AddComponent<NetworkSceneManagerDefault>(),
            StartGameCancellationToken = cancellation
        });
        if (!result.Ok) { status = "CONNECT_FAILED " + result.ShutdownReason; Report(); return; }
        if (runner.IsServer != server || server && runner.LocalPlayer.IsRealPlayer)
            throw new InvalidOperationException("Wrong authority role");
        connectedAt = Time.realtimeSinceStartupAsDouble;
        status = "CONNECTED"; Report();
    }

    private void Update()
    {
        if (runner == null || !runner.IsRunning || stopping) return;
        var state = FindFirstObjectByType<ProbeState>();
        if (state != null && state.Object != null && state.Object.IsValid)
        {
            if (firstTick < 0) firstTick = state.AuthorityTicks;
            maxPlayers = Math.Max(maxPlayers, state.Players);
            sawTwoThenOne |= maxPlayers == 2 && state.Players == 1;
            if (!server && !requested)
            {
                requested = true; nonce = UnityEngine.Random.Range(1, int.MaxValue);
                runner.SendReliableDataToServer(ReliableKey.FromInts(987, 1, 0, 0), BitConverter.GetBytes(nonce));
            }
            if (Time.realtimeSinceStartupAsDouble >= nextReport)
            {
                nextReport = Time.realtimeSinceStartupAsDouble + 2;
                var passed = !server && state.AuthorityTicks - firstTick > 128 && state.Requests > 0 &&
                    echoes == 1 && wrongEchoes == 0 && state.Height > 0.3f && state.Height < 0.7f;
                status = $"{(passed ? "PASS" : "RUNNING")} role={(server ? "Server" : "Client")} " +
                    $"tick={state.AuthorityTicks} requests={state.Requests} players={state.Players} " +
                    $"height={state.Height:F3} echo={echoes} wrongEcho={wrongEchoes} survivedPeerExit={sawTwoThenOne}";
                Report();
            }
        }
        if (double.TryParse(Arg("seconds", "180"), out var seconds) &&
            connectedAt > 0 && Time.realtimeSinceStartupAsDouble - connectedAt >= seconds)
            StopProbe();
    }

    public void StopProbe()
    {
        if (stopping) return;
        stopping = true;
        StopAsync().Forget(e => { status = "STOP_FAILED " + e.GetType().Name; Report(); });
    }

    private async UniTask StopAsync()
    {
        if (runner != null && runner.IsRunning) await runner.Shutdown();
        status = "STOPPED cleanly; last=" + status; Report();
    }

    private void Report()
    {
        var message = $"peer={peer} room={room} {status}";
        Debug.Log("[Probe] " + message);
#if UNITY_WEBGL && !UNITY_EDITOR
        ProbeStatus(message);
#endif
    }

    public void OnReliableDataReceived(NetworkRunner r, PlayerRef source, ReliableKey key, ReadOnlySpan<byte> data)
    {
        key.GetInts(out var kind, out var version, out _, out _);
        if (kind != 987 || version != 1 || data.Length != 4) return;
        if (r.IsServer) r.SendReliableDataToPlayer(source, key, data.ToArray());
        else if (BitConverter.ToInt32(data) == nonce) echoes++;
        else wrongEchoes++;
    }
    public void OnPlayerJoined(NetworkRunner r, PlayerRef p) { Debug.Log("[Probe] joined " + p); }
    public void OnPlayerLeft(NetworkRunner r, PlayerRef p) { Debug.Log("[Probe] left " + p); }
    public void OnShutdown(NetworkRunner r, ShutdownReason reason) { Debug.Log("[Probe] shutdown " + reason); }
    public void OnConnectedToServer(NetworkRunner r) { }
    public void OnDisconnectedFromServer(NetworkRunner r, NetDisconnectReason reason) { status = "DISCONNECTED " + reason; Report(); }
    public void OnConnectRequest(NetworkRunner r, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] t) { request.Accept(); }
    public void OnConnectFailed(NetworkRunner r, NetAddress address, NetConnectFailedReason reason) { }
    public void OnInput(NetworkRunner r, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner r, PlayerRef p, NetworkInput input) { }
    public void OnSessionListUpdated(NetworkRunner r, List<SessionInfo> list) { }
    public void OnCustomAuthenticationResponse(NetworkRunner r, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner r, HostMigrationToken migration) { }
    public void OnSceneLoadDone(NetworkRunner r) { }
    public void OnSceneLoadStart(NetworkRunner r) { }
    public void OnObjectEnterAOI(NetworkRunner r, NetworkObject o, PlayerRef p) { }
    public void OnObjectExitAOI(NetworkRunner r, NetworkObject o, PlayerRef p) { }
    public void OnReliableDataProgress(NetworkRunner r, PlayerRef p, ReliableKey key, float progress) { }
#pragma warning disable CS0618
    public void OnUserSimulationMessage(NetworkRunner r, SimulationMessagePtr message) { }
#pragma warning restore CS0618
}
