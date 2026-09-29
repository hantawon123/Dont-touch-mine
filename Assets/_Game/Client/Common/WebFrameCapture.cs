using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;

namespace Game.Client.Common
{
    /// <summary>Opt-in local capture shared by Editor, desktop (-perf, F8) and WebGL (?perf=1).</summary>
    public sealed class WebFrameCapture : MonoBehaviour
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void GamePerfInstall(string target);
        [DllImport("__Internal")] private static extern void GamePerfReport(string json);
#endif
        [Serializable] internal sealed class Report
        {
            public string revision, scene, endScene, quality, graphicsDevice, processor, platform, unityVersion, utc;
            public int width, height, endWidth, endHeight, frameCap, vSync, frames, slow50ms, overBudget120, gcCollections;
            public double seconds, averageFps, p50ms, p95ms, p99ms, maxMs, overBudget120Percent, refreshHz;
            public long managedHeapBytes, initialManagedHeapBytes;
            public bool interrupted, editor, developmentBuild;
        }

        private float[] samples;
        private int count, initialGc, initialQuality;
        private double startedAt, previousFrame;
        private bool capturing, hotkey;
        private Report report;

        private void Start()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            GamePerfInstall(gameObject.name);
#else
            hotkey = Array.IndexOf(Environment.GetCommandLineArgs(), "-perf") >= 0;
#endif
            enabled = capturing || hotkey;
        }

        [Preserve, ContextMenu("Capture 30 Seconds")]
        public void BeginCapture()
        {
            if (capturing) return;
            samples ??= new float[32768];
            count = 0;
            initialQuality = QualitySettings.GetQualityLevel();
            report = new Report
            {
                revision = Application.version, scene = SceneManager.GetActiveScene().name,
                quality = QualitySettings.names[initialQuality],
                graphicsDevice = SystemInfo.graphicsDeviceName, processor = SystemInfo.processorType,
                platform = Application.platform.ToString(), unityVersion = Application.unityVersion,
                editor = Application.isEditor, developmentBuild = Debug.isDebugBuild,
                utc = DateTime.UtcNow.ToString("O"), refreshHz = Screen.currentResolution.refreshRateRatio.value,
                width = Screen.width, height = Screen.height, frameCap = Application.targetFrameRate,
                vSync = QualitySettings.vSyncCount, initialManagedHeapBytes = GC.GetTotalMemory(false),
                interrupted = !Application.isFocused
            };
            SceneManager.activeSceneChanged += OnSceneChanged;
            initialGc = GC.CollectionCount(0);
            startedAt = previousFrame = Time.realtimeSinceStartupAsDouble;
            capturing = enabled = true;
        }

        private void Update()
        {
            if (hotkey && Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame)
            {
                BeginCapture();
                return;
            }
            if (!capturing) return;
            var now = Time.realtimeSinceStartupAsDouble;
            samples[count++] = (float)((now - previousFrame) * 1000d);
            previousFrame = now;
            report.interrupted |= !Application.isFocused ||
                Screen.width != report.width || Screen.height != report.height ||
                Application.targetFrameRate != report.frameCap || QualitySettings.vSyncCount != report.vSync ||
                QualitySettings.GetQualityLevel() != initialQuality;
            if (now - startedAt >= 30d || count == samples.Length) FinishCapture(now);
        }

        private void FinishCapture(double now)
        {
            SceneManager.activeSceneChanged -= OnSceneChanged;
            capturing = false;
            enabled = hotkey;
            Summarize(samples, count, now - startedAt, report);
            report.endScene = SceneManager.GetActiveScene().name;
            report.endWidth = Screen.width;
            report.endHeight = Screen.height;
            report.gcCollections = GC.CollectionCount(0) - initialGc;
            report.managedHeapBytes = GC.GetTotalMemory(false);
            var json = JsonUtility.ToJson(report, true);
#if UNITY_WEBGL && !UNITY_EDITOR
            GamePerfReport(json);
#else
            try
            {
                var directory = Path.Combine(Application.persistentDataPath, "Performance");
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, "frames-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".json");
                File.WriteAllText(path, json);
                Debug.Log("[Performance] " + path + "\n" + json);
            }
            catch (Exception error) { Debug.LogWarning("[Performance] Save failed: " + error.Message + "\n" + json); }
#endif
        }

        internal static void Summarize(float[] values, int length, double seconds, Report result)
        {
            if (values == null || length <= 0 || length > values.Length || seconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(length));
            result.frames = length;
            result.seconds = seconds;
            result.averageFps = length / seconds;
            result.slow50ms = result.overBudget120 = 0;
            for (var i = 0; i < length; i++)
            {
                if (values[i] > 50f) result.slow50ms++;
                if (values[i] > 1000d / 120d) result.overBudget120++;
            }
            result.overBudget120Percent = 100d * result.overBudget120 / length;
            Array.Sort(values, 0, length);
            result.p50ms = values[Math.Max(0, (int)Math.Ceiling(length * .50) - 1)];
            result.p95ms = values[Math.Max(0, (int)Math.Ceiling(length * .95) - 1)];
            result.p99ms = values[Math.Max(0, (int)Math.Ceiling(length * .99) - 1)];
            result.maxMs = values[length - 1];
        }

        private void OnSceneChanged(Scene before, Scene after) => report.interrupted = true;

        private void OnDestroy() => SceneManager.activeSceneChanged -= OnSceneChanged;

        private void OnApplicationFocus(bool focused)
        {
            if (capturing && !focused) report.interrupted = true;
        }
    }
}
