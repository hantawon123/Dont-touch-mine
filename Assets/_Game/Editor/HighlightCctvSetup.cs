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
                    // The north-east mount must cover the shredder aisle, not the nearby spawn behind it.
                    if (position.x > 5f && position.z > 0f)
                    {
                        var shredder = all.Where(t => t.name == "ShredderSpot")
                            .OrderBy(t => (t.position - position).sqrMagnitude).First();
                        marker.transform.LookAt(shredder.position + Vector3.up * 0.5f);
                    }
                    var area = position.x < -20f ? "서측 통로" : position.x > 5f ? "동측 통로" : position.z < -15f ? "안쪽 매대" : "중앙 통로";
                    marker.AddComponent<HighlightCctvCamera>().Configure($"CAM {i + 1:00} · {area}");
                }
                // Additional fixed ceiling viewpoints, validated with the supermarket geometry visible.
                var additional = new (float x, float z, float focusX, float focusZ, string area)[]
                {
                    (-19f, -10f, -15.45f, -11.42f, "파쇄기 1 서쪽"),
                    (-12f, -14f, -15.45f, -11.42f, "파쇄기 1 남동"),
                    (7f, -3f, 9.88f, -6.19f, "파쇄기 2 북서"),
                    (7f, -9f, 9.88f, -6.19f, "파쇄기 2 남서"),
                    (-49f, -12f, -46f, -15f, "헬스장"),
                    (-42f, -11f, -46f, -12f, "서쪽 연결부"),
                    (-33f, -7f, -31f, -12f, "오락실"),
                    (-34f, 5f, -30f, 5f, "카페 북쪽"),
                    (-27f, 1f, -29f, -3f, "카페 출입구"),
                    (-19f, 2f, -15f, -2f, "중앙 북쪽"),
                    (-10f, 1f, -14f, -3f, "중앙 동쪽"),
                    (-2f, 8f, 3f, 6f, "계산대 북쪽"),
                    (4f, 5f, 3f, 0f, "계산대 사이"),
                    (-3f, -7f, -2f, -11f, "매대 서쪽"),
                    (4f, -7f, 7f, -10f, "매대 동쪽"),
                    (-3f, -14f, 1f, -16f, "매대 남쪽"),
                    (4f, -17f, 1f, -17f, "동쪽 하단 매대"),
                    (18f, -7f, 14f, -10f, "동쪽 별실"),
                    (-20f, -17f, -17f, -21f, "중앙 남서쪽"),
                    (-13f, -24f, -17f, -20f, "중앙 남쪽"),
                    (-49f, -31f, -47f, -34f, "전자제품점"),
                    (-40f, -34f, -43f, -31f, "남서쪽 안쪽"),
                    (-26f, -34f, -29f, -36f, "의류 매장"),
                    (-8f, -36f, -5f, -32f, "남동쪽 창고"),
                };
                for (var i = 0; i < additional.Length; i++)
                {
                    var point = additional[i];
                    var number = cameras.Length + i + 1;
                    var marker = new GameObject($"CCTV {number:00}");
                    marker.transform.SetParent(root.transform, false);
                    marker.transform.position = new Vector3(point.x, 3.2f, point.z);
                    marker.transform.LookAt(new Vector3(point.focusX, 0.9f, point.focusZ));
                    marker.AddComponent<HighlightCctvCamera>().Configure($"CAM {number:00} · {point.area}", 65f);
                }
                Directory.CreateDirectory("Assets/_Game/Content/Resources/CCTV");
                PrefabUtility.SaveAsPrefabAsset(root, "Assets/_Game/Content/Resources/CCTV/Supermarket.prefab");
                Debug.Log($"[Highlight] Saved {root.transform.childCount} fixed supermarket CCTV viewpoints.");
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
