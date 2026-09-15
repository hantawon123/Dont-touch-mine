using System;
using System.IO;
using System.Linq;
using Game.Client.Cameras;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor
{
    public static class HighlightCctvSetup
    {
        // Save only the presentation prefab. Opening the gameplay scene can run
        // editor DI callbacks; do not serialize their temporary runtime objects.
        [MenuItem("Game/Highlight/Rebuild Supermarket CCTV Points")]
        public static void Build()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.name != "Supermarket")
                throw new InvalidOperationException("Open Supermarket before rebuilding its CCTV points.");
            var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            var cameras = all.Where(t => t.name.StartsWith("SM_Prop_WallCamera_01") &&
                !t.name.Contains("_Camera_") && !t.name.Contains("_Axle_")).OrderBy(t => t.position.x).ThenBy(t => t.position.z).ToArray();
            var points = all.Where(t => t.name.StartsWith("SpawnPoint_") || t.name == "ShredderSpot").ToArray();
            if (cameras.Length == 0 || points.Length == 0) throw new InvalidOperationException("CCTV mounts and gameplay points are required.");
            var root = new GameObject("Supermarket CCTV");
            try
            {
                for (var i = 0; i < cameras.Length; i++)
                {
                    var camera = cameras[i];
                    var position = camera.GetComponentsInChildren<Renderer>().First().bounds.center;
                    var focus = points.OrderBy(t => (t.position - position).sqrMagnitude).First().position + Vector3.up;
                    var direction = (focus - position).normalized;
                    var marker = new GameObject("CCTV " + (i + 1).ToString("00"));
                    marker.transform.SetParent(root.transform, false);
                    marker.transform.SetPositionAndRotation(position + direction * 0.5f, Quaternion.LookRotation(direction));
                    var area = position.x < -20f ? "서측 통로" : position.x > 5f ? "동측 통로" : position.z < -15f ? "안쪽 매대" : "중앙 통로";
                    marker.AddComponent<HighlightCctvCamera>().Configure($"CAM {i + 1:00} · {area}");
                }
                Directory.CreateDirectory("Assets/_Game/Content/Resources/CCTV");
                PrefabUtility.SaveAsPrefabAsset(root, "Assets/_Game/Content/Resources/CCTV/Supermarket.prefab");
                Debug.Log($"[Highlight] Saved {cameras.Length} CCTV mounting points from the supermarket's wall cameras.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        public static void BuildBatch()
        {
            EditorSceneManager.OpenScene("Assets/_Game/Content/Scenes/Supermarket.unity");
            Build();
        }
    }
}
