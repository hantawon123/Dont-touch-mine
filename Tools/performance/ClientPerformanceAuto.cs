using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Bootstrap;
using Game.Core.Flow;
using Game.Core.Lobby;
using Game.Core.Match;
using Game.Core.Players;
using Game.Core.Rooms;
using Game.Editor;
using Game.Network.Players;
using Game.Network.Session;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using VContainer;

// Validation harness: install only in Assets/Editor of an isolated copy.
[InitializeOnLoad]
public static class ClientPerformanceAuto
{
    const string Key = "Performance994.";
    static double stableAt;
    static CancellationTokenSource activeRun;
    static string Output => Environment.GetEnvironmentVariable("PERF_OUTPUT") ?? "../performance-994-auto";
    static ClientPerformanceAuto()
    {
        stableAt = EditorApplication.timeSinceStartup + 30;
        EditorApplication.update += () =>
        {
            if (!SessionState.GetBool(Key + "Pending", false) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { stableAt = EditorApplication.timeSinceStartup + 30; return; }
            if (EditorApplication.timeSinceStartup < stableAt) return;
            DevelopmentServerWindow.Start(SessionState.GetBool(Key + "Server", false)
                ? EditorDevelopmentSession.PeerRole.Server : EditorDevelopmentSession.PeerRole.Client, Environment.GetEnvironmentVariable("PERF_TEST_CODE") ?? "PER994");
        };
        EditorApplication.playModeStateChanged += s =>
        {
            if (s == PlayModeStateChange.ExitingPlayMode && SessionState.GetBool(Key + "Armed", false) && !File.Exists(Path.Combine(Output, "client-done.txt")))
            {
                SessionState.SetBool(Key + "Pending", true);
                activeRun?.Cancel();
                stableAt = EditorApplication.timeSinceStartup + 30;
            }
            if (s != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key + "Pending", false)) return;
            SessionState.SetBool(Key + "Pending", false);
            Run().Forget(e => { if (e is OperationCanceledException && SessionState.GetBool(Key + "Pending", false)) return; SessionState.SetBool(Key + "Armed", false); Debug.LogException(e); EditorApplication.Exit(1); });
        };
    }
    public static void Server() => Start(true);
    public static void Client() => Start(false);
    static void Start(bool server)
    {
        if (!File.Exists(Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".performance-lab"))))
            throw new InvalidOperationException("Use an isolated project copy marked with .performance-lab.");
        if (File.Exists(Path.Combine(Output, "client-done.txt")))
            throw new InvalidOperationException("Use a fresh PERF_OUTPUT directory for each run.");
        Directory.CreateDirectory(Output);
        SessionState.SetBool(Key + "Armed", true);
        var revision = Environment.GetEnvironmentVariable("PERF_REVISION");
        if (string.IsNullOrWhiteSpace(revision)) throw new InvalidOperationException("Set PERF_REVISION to the tested revision.");
        if (PlayerSettings.bundleVersion != revision) PlayerSettings.bundleVersion = revision;
        if (PlayerSettings.companyName != "KeepItPerformance") PlayerSettings.companyName = "KeepItPerformance";
        var product = server ? "Server994" : "Client994"; if (PlayerSettings.productName != product) PlayerSettings.productName = product;
        SessionState.SetBool(Key + "Server", server);
        SessionState.SetBool(Key + "Pending", true);
        EditorSceneManager.OpenScene("Assets/_Game/Content/Scenes/Home.unity");
        if (!server)
        {
            var game = EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
            game.Show(); game.Focus(); game.maximized = true;
        }
        stableAt = EditorApplication.timeSinceStartup + 30;
    }
    static async UniTask Run()
    {
        Application.runInBackground = true;
        Application.targetFrameRate = SessionState.GetBool(Key + "Server", false) ? 60 : 120;
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(20));
        activeRun = timeout;
        var token = timeout.Token;
        await UniTask.Delay(1500, DelayType.Realtime, cancellationToken: token);
        var scope = Resources.FindObjectsOfTypeAll<ProjectLifetimeScope>().First(x => x.Container != null);
        var network = scope.Container.Resolve<NetworkRunnerService>();
        var phase = MatchPhase.Waiting;
        network.MatchStateReceived += state => phase = state.Phase;
        if (SessionState.GetBool(Key + "Server", false))
        {
            await UniTask.WaitUntil(() => network.IsRuntimeReady, cancellationToken: token);
            File.WriteAllText(Path.Combine(Output, "server-ready.txt"), "ready");
            bool positioned = false;
            while (!File.Exists(Path.Combine(Output, "client-done.txt")))
            {
                if (!positioned && phase == MatchPhase.Searching && network.TryTeleportPlayer(0, new Pose(new Vector3(-20f, 0.2f, -13f), Quaternion.identity)))
                { positioned = true; File.WriteAllText(Path.Combine(Output, "position-ready.txt"), "-20,0.2,-13"); }
                await UniTask.Delay(500, DelayType.Realtime, cancellationToken: token);
            }
            EditorApplication.Exit(0); return;
        }

        var commands = scope.Container.Resolve<RoomUiCommands>();
        var entry = await commands.CreateAsync(new RoomCreateRequest("Performance 994", false, null, 2, "supermarket"), token);
        for (int i = 0; !entry.Ok && i < 50; i++)
        {
            await UniTask.Delay(5000, DelayType.Realtime, cancellationToken: token);
            entry = await commands.CreateAsync(new RoomCreateRequest("Performance 994", false, null, 2, "supermarket"), token);
        }
        if (!entry.Ok) throw new Exception("Room allocation failed: " + entry.Failure);
        scope.Container.Resolve<AppFlowSystem>().TryTransitionTo(AppFlowState.Lobby);
        network.EnterLobbyScene();
        await UniTask.WaitUntil(() => network.IsLocalRoomOwner && network.IsSceneLoadComplete, cancellationToken: token);
        MatchRuleSettings.TryCreate(10, 10, 1f, 3, "", out var rules, out _);
        if (!network.TryApplyLobbySettings(2, 5, "supermarket", rules, "Performance 994")) throw new Exception("Lobby settings failed");
        await UniTask.Delay(1500, DelayType.Realtime, cancellationToken: token);
        network.RequestMatchStart();
        await UniTask.WaitUntil(() => phase == MatchPhase.Searching && network.IsSceneLoadComplete, cancellationToken: token);
        await UniTask.WaitUntil(() => File.Exists(Path.Combine(Output, "position-ready.txt")), cancellationToken: token);
        await UniTask.Delay(1500, DelayType.Realtime, cancellationToken: token);
        var motor = UnityEngine.Object.FindObjectsByType<NetworkPlayerMotor>(FindObjectsSortMode.None).First(x => x.Object.HasInputAuthority);
        var field = typeof(NetworkPlayerMotor).GetField("inputSource", BindingFlags.NonPublic | BindingFlags.Instance);
        var original = (IPlayerInputIntentSource)field.GetValue(motor);
        var scripted = new ScriptedInput(original.MovementSettings, motor.transform);
        field.SetValue(motor, scripted);
        try
        {
            await UniTask.Delay(10000, DelayType.Realtime, cancellationToken: token);
            File.WriteAllText(Path.Combine(Output, "environment.json"), JsonUtility.ToJson(new EnvironmentReport {
                gpu = SystemInfo.graphicsDeviceName, api = SystemInfo.graphicsDeviceType.ToString(),
                width = Screen.width, height = Screen.height, refresh = Screen.currentResolution.refreshRateRatio.value,
                version = Application.version, batch = Application.isBatchMode, quality = QualitySettings.names[QualitySettings.GetQualityLevel()], targetFps = Application.targetFrameRate, vSync = QualitySettings.vSyncCount, renderScale = ((UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset)UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline).renderScale
            }, true));
            for (int trial = 0; trial < 4; trial++)
            {
                // Profile only the final trial; keep its overhead out of plain frame timing trials.
                if (trial == 3) { Profiler.logFile = Path.GetFullPath(Path.Combine(Output, "cpu.raw")); Profiler.enableBinaryLog = true; Profiler.enabled = true; }
                var ms = new double[20000]; var draws = new int[20000]; var tris = new int[20000];
                int n = 0, gc = GC.CollectionCount(0); double start = Time.realtimeSinceStartupAsDouble, last = start;
                long heap = GC.GetTotalMemory(false);
                while (Time.realtimeSinceStartupAsDouble - start < 30 && n < ms.Length)
                {
                    await UniTask.NextFrame(token);
                    double now = Time.realtimeSinceStartupAsDouble;
                    ms[n] = (now-last)*1000; draws[n] = UnityStats.batches; tris[n] = UnityStats.triangles;
                    last = now; n++;
                }
                if (trial == 3) { Profiler.enabled = false; Profiler.enableBinaryLog = false; }
                var text = new StringBuilder("frame,ms,batches,triangles\n");
                for (int i=0;i<n;i++) text.Append(i).Append(',').Append(ms[i].ToString("F6", System.Globalization.CultureInfo.InvariantCulture)).Append(',').Append(draws[i]).Append(',').Append(tris[i]).Append('\n');
                File.WriteAllText(Path.Combine(Output, "trial-" + trial + ".csv"), text.ToString());
                Debug.Log($"[PerfAuto] trial={trial} frames={n} seconds={last-start:F3} gc={GC.CollectionCount(0)-gc} heapStart={heap} heapEnd={GC.GetTotalMemory(false)} position={motor.transform.position} profiled={trial==3}");
            }
            await UniTask.WaitForEndOfFrame(token);
            var shot = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(Output, "game.png"), shot.EncodeToPNG());
            UnityEngine.Object.Destroy(shot);
        }
        finally { if(motor!=null) field.SetValue(motor, original); Profiler.enabled=false; Profiler.enableBinaryLog=false; }
        File.WriteAllText(Path.Combine(Output, "client-done.txt"), "complete");
        await commands.LeaveAsync(token);
        EditorApplication.Exit(0);
    }
    [Serializable] class EnvironmentReport { public string gpu,api,version,quality; public int width,height,targetFps,vSync; public float renderScale; public double refresh; public bool batch; }
    sealed class ScriptedInput : IPlayerInputIntentSource
    {
        public PlayerMovementSettings MovementSettings { get; }
        readonly Transform target; readonly Vector3 origin; readonly double start;
        public ScriptedInput(PlayerMovementSettings settings, Transform target) { MovementSettings=settings; this.target=target; origin=target.position; start=Time.realtimeSinceStartupAsDouble; }
        public PlayerInputIntent CaptureInputIntent()
        {
            var elapsed=Time.realtimeSinceStartupAsDouble-start;
            // Repeat a small route through normal Fusion/KCC input, without writing transforms.
            var segment=(int)(elapsed/4)%4;
            var goal=origin+new Vector3(segment<2?2:-2,0,segment==0||segment==3?2:-2);
            var d=goal-target.position; d.y=0; d=Vector3.ClampMagnitude(d,1);
            return new PlayerInputIntent(d.x,d.z,0,PlayerInputButtons.None);
        }
    }
}
