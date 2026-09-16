using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// 씬에 꺼내 둔 프리팹 인스턴스의 <b>Scale만</b> 원본 프리팹 에셋에 기록한다.
    /// 위치·회전은 씬 오버라이드로 남긴다(마트 배치가 프리팹 기본값으로 덮이지 않게).
    /// </summary>
    public static class ApplySelectedPrefabScalesMenu
    {
        private const string MenuPath = "Game/Items/Apply Selected Prefab Scales";

        [MenuItem(MenuPath, false, 50)]
        public static void Apply()
        {
            var roots = Selection.gameObjects
                .Select(go => PrefabUtility.GetOutermostPrefabInstanceRoot(go) ?? go)
                .Distinct()
                .Where(go => PrefabUtility.IsAnyPrefabInstanceRoot(go))
                .ToArray();

            if (roots.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "Apply Prefab Scales",
                    "프리팹 인스턴스를 Hierarchy에서 선택하세요.",
                    "OK");
                return;
            }

            // 같은 프리팹을 여러 개 꺼낸 경우: 스케일이 다르면 건너뛰고 경고한다.
            var byAsset = new Dictionary<string, List<GameObject>>();
            foreach (var root in roots)
            {
                var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                if (!byAsset.TryGetValue(path, out var list))
                {
                    list = new List<GameObject>();
                    byAsset[path] = list;
                }

                list.Add(root);
            }

            var applied = 0;
            var skipped = new List<string>();
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var pair in byAsset)
                {
                    var path = pair.Key;
                    var instances = pair.Value;
                    var scale = instances[0].transform.localScale;
                    if (instances.Any(go => (go.transform.localScale - scale).sqrMagnitude > 1e-8f))
                    {
                        skipped.Add($"{path}: 인스턴스 Scale이 서로 다름");
                        continue;
                    }

                    // 대표 인스턴스의 Scale만 에셋에 쓴다.
                    var target = instances[0];
                    var so = new SerializedObject(target.transform);
                    var scaleProp = so.FindProperty("m_LocalScale");
                    PrefabUtility.ApplyPropertyOverride(
                        scaleProp,
                        path,
                        InteractionMode.UserAction);
                    applied++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
            }

            var message = $"프리팹 Scale 저장: {applied}개";
            if (skipped.Count > 0)
            {
                message += $"\n건너뜀 {skipped.Count}개:\n- " + string.Join("\n- ", skipped.Take(12));
                if (skipped.Count > 12)
                {
                    message += $"\n- … 외 {skipped.Count - 12}개";
                }
            }

            Debug.Log($"[ApplyPrefabScales] {message}");
            EditorUtility.DisplayDialog("Apply Prefab Scales", message, "OK");
        }

        [MenuItem(MenuPath, true)]
        private static bool Validate() => Selection.gameObjects.Length > 0;
    }
}
