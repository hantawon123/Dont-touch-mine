using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// 씬에서 선택한 오브젝트들을 한 부모 아래로 모아 프리팹으로 저장하고, 씬의 묶음은 그 프리팹의
    /// 인스턴스로 연결한다. 조립 씬에서 매장 한 구역, 진열대 한 줄 같은 덩어리를 재사용 가능한 에셋으로 만들 때 쓴다.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>피벗은 선택 묶음의 바닥 중앙(바운드 min.y, 중심 x/z). 다른 곳에 놓을 때 바닥에 맞추기 쉽다.</item>
    /// <item>선택에 다른 프리팹 인스턴스의 자식(루트가 아닌 것)이 섬여 있으면 그 인스턴스 루트로 올려 잡는다.
    /// 프리팹 인스턴스 내부에서 오브젝트를 빼내는 것은 Unity가 허용하지 않기 때문이다.</item>
    /// <item>Synty 조각들은 중첩 프리팹으로 그대로 남는다(팩 업데이트에 안전).</item>
    /// <item>"종류별 하위 그룹"을 켜면 이름 접두사로 Modules(SM_Bld)·Env(SM_Env)·Props(SM_Prop)·Other로 나눠 담는다.</item>
    /// </list>
    /// </remarks>
    public sealed class MakePrefabFromSelectionMenu : EditorWindow
    {
        private const string OutputFolder = "Assets/_Game/Content/Prefabs/Mart";

        private string prefabName = "MartPiece";
        private bool groupByKind;
        private GameObject[] targets;

        [MenuItem("Game/Match Map/Make Prefab From Selection... %#&P")]
        public static void Open()
        {
            var roots = CollectRoots(Selection.gameObjects);
            if (roots.Length == 0)
            {
                Debug.LogWarning("[MakePrefab] 씬 오브젝트를 선택하세요.");
                return;
            }

            var window = GetWindow<MakePrefabFromSelectionMenu>(true, "선택을 프리팹으로", true);
            window.targets = roots;
            window.prefabName = SuggestName(roots);
            window.minSize = new Vector2(360, 150);
            window.ShowUtility();
        }

        private void OnGUI()
        {
            if (targets == null || targets.Length == 0)
            {
                EditorGUILayout.HelpBox("선택이 비어 있습니다. 창을 닫고 다시 선택하세요.", MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField($"대상: {targets.Length}개 오브젝트 (렌더러 {targets.Sum(t => t.GetComponentsInChildren<MeshRenderer>(true).Length)}개)");
            prefabName = EditorGUILayout.TextField("프리팹 이름", prefabName);
            groupByKind = EditorGUILayout.ToggleLeft("종류별 하위 그룹 (Modules / Env / Props / Other)", groupByKind);
            EditorGUILayout.LabelField("저장 위치", $"{OutputFolder}/{Sanitize(prefabName)}.prefab");

            GUILayout.FlexibleSpace();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("취소"))
                {
                    Close();
                }

                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(prefabName)))
                {
                    if (GUILayout.Button("프리팹 만들기"))
                    {
                        var created = Create(targets, prefabName.Trim(), groupByKind);
                        if (created != null)
                        {
                            Selection.activeGameObject = created;
                        }

                        Close();
                    }
                }
            }
        }

        /// <summary>
        /// 선택 루트들을 새 부모 아래로 모아 프리팹으로 저장하고 씬 인스턴스를 돌려준다.
        /// </summary>
        public static GameObject Create(GameObject[] roots, string name, bool groupByKind)
        {
            roots = CollectRoots(roots);
            if (roots.Length == 0)
            {
                return null;
            }

            EnsureFolder(OutputFolder);
            var scene = roots[0].scene;
            var bounds = WorldBounds(roots);
            var pivot = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Make Prefab From Selection");

            var parent = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(parent, "Make Prefab From Selection");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(parent, scene);
            parent.transform.position = pivot;

            var commonParent = CommonParent(roots);
            if (commonParent != null)
            {
                parent.transform.SetParent(commonParent, true);
            }

            var kindGroups = new Dictionary<string, Transform>();
            foreach (var root in roots.OrderBy(r => r.transform.GetSiblingIndex()))
            {
                var target = parent.transform;
                if (groupByKind)
                {
                    var kind = KindOf(root.name);
                    if (!kindGroups.TryGetValue(kind, out target))
                    {
                        var group = new GameObject(kind);
                        Undo.RegisterCreatedObjectUndo(group, "Make Prefab From Selection");
                        group.transform.SetParent(parent.transform, false);
                        target = kindGroups[kind] = group.transform;
                    }
                }

                Undo.SetTransformParent(root.transform, target, "Make Prefab From Selection");
            }

            var path = AssetDatabase.GenerateUniqueAssetPath($"{OutputFolder}/{Sanitize(name)}.prefab");
            // 반환값은 프리팹 에셋 루트이고, 씬의 parent가 그 인스턴스로 연결된다.
            PrefabUtility.SaveAsPrefabAssetAndConnect(parent, path, InteractionMode.AutomatedAction, out var success);
            if (!success)
            {
                Debug.LogError($"[MakePrefab] 프리팹 저장 실패: {path}");
                return parent;
            }

            Debug.Log($"[MakePrefab] {roots.Length}개 오브젝트 → {path} (피벗 {pivot})", parent);
            return parent;
        }

        // ---- 유틸 ---------------------------------------------------------------

        /// <summary>선택을 "옮길 수 있는 루트"로 정리: 프리팹 인스턴스 내부 자식은 그 인스턴스 루트로, 중복·서로 포함 관계는 제거.</summary>
        private static GameObject[] CollectRoots(GameObject[] selection)
        {
            var set = new HashSet<GameObject>();
            foreach (var go in selection)
            {
                if (go == null || !go.scene.IsValid())
                {
                    continue;
                }

                var candidate = go;
                if (PrefabUtility.IsPartOfPrefabInstance(go) && !PrefabUtility.IsAnyPrefabInstanceRoot(go))
                {
                    candidate = PrefabUtility.GetOutermostPrefabInstanceRoot(go) ?? go;
                }

                set.Add(candidate);
            }

            // 조상이 이미 선택에 있으면 자식은 뺀다
            return set.Where(go => !set.Any(other => other != go && go.transform.IsChildOf(other.transform))).ToArray();
        }

        private static Transform CommonParent(GameObject[] roots)
        {
            var parent = roots[0].transform.parent;
            return roots.All(r => r.transform.parent == parent) ? parent : null;
        }

        private static Bounds WorldBounds(GameObject[] roots)
        {
            var bounds = new Bounds();
            var first = true;
            foreach (var root in roots)
            {
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (first)
                    {
                        bounds = renderer.bounds;
                        first = false;
                    }
                    else
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }
                }
            }

            if (first)
            {
                bounds = new Bounds(roots[0].transform.position, Vector3.zero);
                foreach (var root in roots) bounds.Encapsulate(root.transform.position);
            }

            return bounds;
        }

        private static string KindOf(string objectName)
        {
            if (objectName.StartsWith("SM_Bld")) return "Modules";
            if (objectName.StartsWith("SM_Env")) return "Env";
            if (objectName.StartsWith("SM_Prop") || objectName.StartsWith("Gen_")) return "Props";
            return "Other";
        }

        private static string SuggestName(GameObject[] roots)
        {
            var kinds = roots.Select(r => KindOf(r.name)).Distinct().ToArray();
            var kind = kinds.Length == 1 ? kinds[0] : "Piece";
            var existing = AssetDatabase.IsValidFolder(OutputFolder)
                ? AssetDatabase.FindAssets("t:Prefab", new[] { OutputFolder }).Length
                : 0;
            return $"Mart_{kind}_{existing + 1:00}";
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static string Sanitize(string name)
        {
            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(invalid, '_');
            }

            return name.Trim().Replace(' ', '_');
        }
    }
}
