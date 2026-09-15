using Game.Client.Common;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static class ClientPerformanceCapture
    {
        [MenuItem("Game/Performance/Capture 30 Seconds")]
        private static void Capture()
        {
            var capture = Object.FindFirstObjectByType<WebFrameCapture>();
            if (capture == null) { Debug.LogWarning("[Performance] Start Play from a game scene first."); return; }
            capture.BeginCapture();
            Debug.Log("[Performance] Capturing 30 seconds. Keep the Game view focused; results are saved under persistentDataPath/Performance.");
        }

        [MenuItem("Game/Performance/Capture 30 Seconds", true)]
        private static bool CanCapture() => EditorApplication.isPlaying;
    }
}
