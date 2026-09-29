using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Client.Interactions;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// 로비 지하실 환경 프리팹 안의 소품 중 "들 수 있는" 것을 Carryable Variant로 교체한다.
    /// 대상은 상자류·콘·가스통·타이어류만이며(2026-09-07 결정), 사다리·사물함·공구판과 그 위 공구·
    /// 전등·전깃줄·데칼은 고정으로 둔다.
    /// </summary>
    /// <remarks>
    /// 씬이 아니라 프리팹 자산(<see cref="EnvironmentPrefabPath"/>)을 직접 편집한다. 로비 씬과
    /// 조립 씬(LobbyBuild) 둘 다 이 프리팹 인스턴스이므로 한 번에 반영된다.
    /// 교체된 소품은 Static이 풀려 라이트맵에서 빠지므로 실행 뒤 조명을 다시 베이크해야 한다.
    /// </remarks>
    public static class LobbyCarryablePropSetupMenu
    {
        private const string MenuRoot = "Game/Lobby/Carryable/";
        private const string EnvironmentPrefabPath = "Assets/_Game/Content/Prefabs/LobbyBasementEnvironment.prefab";
        private const string SourceGroupName = "StaticProps";
        private const string ItemsGroupName = "Items";
        private const string TargetGroupName = "Carryables";

        // StaticProps 그룹: 들 수 있는 소품의 원본 프리팹 이름 접두사
        private static readonly string[] CarryablePrefixes =
        {
            "Basement_CardboardBox",
            "Basement_PlasticBox",
            "Basement_RoadCone",
            "Basement_GasCylinder",
            "Basement_Wheel",
            "Basement_Clock", // 서벽 벽시계 1개 (2026-09-07 추가)
            "Basement_RoadSign", // 도로 표지판 2개 (2026-09-07 추가)
        };

        // Items(공구) 그룹: 기본은 전부 고정이고, 여기 적은 원본 프리팹 이름만 예외로 들 수 있다.
        // 2026-09-07: 서북 구석 상자 위의 무선 드릴 1개, 작업대 왼쪽 아래 노란 수평계 1개.
        private static readonly string[] CarryableItemPrefabNames =
        {
            "Basement_Tools_CordlessDrill",
            "Basement_Tools_SpiritLevel",
            "Basement_Tools_Flashlight", // 동벽 상자 위 손전등 (2026-09-07 추가)
        };

        // Items 그룹에서 같은 종류가 여러 개일 때 특정 인스턴스만 허용한다(오브젝트 이름 기준).
        // 십자 렌치: 바닥에 놓인 "(1)"만 들 수 있고, 공구판에 걸린 것은 고정.
        private static readonly string[] CarryableItemInstanceNames =
        {
            "Basement_Tools_WheelWrench (1)",
        };

        [MenuItem(MenuRoot + "1. Preview Lobby Carryable Props")]
        public static void Preview()
        {
            var root = PrefabUtility.LoadPrefabContents(EnvironmentPrefabPath);
            try
            {
                var candidates = FindCandidates(root);
                var bySource = candidates
                    .GroupBy(c => Path.GetFileNameWithoutExtension(GetSourcePrefabPath(c)))
                    .OrderBy(g => g.Key);
                var lines = bySource.Select(g => $"  {g.Key} x{g.Count()}");
                var existing = root.GetComponentsInChildren<CarryableItem>(true).Length;
                Debug.Log($"[Lobby Carryable] candidates={candidates.Count}, already carryable={existing}\n" +
                          string.Join("\n", lines));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem(MenuRoot + "2. Generate Variants And Replace In Environment Prefab")]
        public static void GenerateAndReplace()
        {
            var root = PrefabUtility.LoadPrefabContents(EnvironmentPrefabPath);
            try
            {
                var candidates = FindCandidates(root);
                if (candidates.Count == 0)
                {
                    EditorUtility.DisplayDialog("Lobby Carryable",
                        "교체할 소품이 없습니다. 이미 전부 Carryable이거나 접두사 목록에 없는 이름입니다.", "확인");
                    return;
                }

                var sourcePaths = new HashSet<string>(
                    candidates.Select(GetSourcePrefabPath).Where(p => !string.IsNullOrEmpty(p)),
                    StringComparer.Ordinal);

                if (!EditorUtility.DisplayDialog("Lobby Carryable",
                        $"Carryable Variant {sourcePaths.Count}개를 만들고\n" +
                        $"환경 프리팹의 소품 {candidates.Count}개를 교체합니다.\n\n" +
                        "교체 후 Game/Lobby/Lighting/2. Bake 로 조명을 다시 베이크하세요.",
                        "적용", "취소"))
                {
                    return;
                }

                CarryablePropSetupMenu.EnsureVariantFolder();
                var variants = new Dictionary<string, GameObject>(StringComparer.Ordinal);
                foreach (var sourcePath in sourcePaths)
                {
                    var variant = CarryablePropSetupMenu.GetOrCreateVariant(sourcePath);
                    if (variant != null)
                    {
                        variants[sourcePath] = variant;
                    }
                }

                var group = root.transform.Find(TargetGroupName);
                if (group == null)
                {
                    group = new GameObject(TargetGroupName).transform;
                    group.SetParent(root.transform, false);
                }

                var replaced = 0;
                var skipped = 0;
                foreach (var candidate in candidates)
                {
                    var sourcePath = GetSourcePrefabPath(candidate);
                    if (!variants.TryGetValue(sourcePath, out var variant))
                    {
                        skipped++;
                        continue;
                    }

                    if (ReplaceInPrefab(candidate, variant, group))
                    {
                        replaced++;
                    }
                    else
                    {
                        skipped++;
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(root, EnvironmentPrefabPath, out var saved);
                AssetDatabase.SaveAssets();

                var total = root.GetComponentsInChildren<CarryableItem>(true).Length;
                Debug.Log($"[Lobby Carryable] variants={variants.Count}, replaced={replaced}, skipped={skipped}, " +
                          $"carryable total={total}, prefab saved={saved}. " +
                          "다음: Game/Lobby/Lighting/2. Bake 로 라이트맵 재베이크.");
                EditorUtility.DisplayDialog("Lobby Carryable",
                    $"Variant {variants.Count}개, 교체 {replaced}개, 실패 또는 제외 {skipped}개.\n\n" +
                    "라이트맵 재베이크가 필요합니다: Game/Lobby/Lighting/2. Bake", "확인");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// 메시 콜라이더만 가진 Carryable Variant(콘·타이어·플라스틱통)에 렌더러 크기의 BoxCollider를 더하고
        /// 메시 콜라이더는 끈다. 2026-09-07 테스트에서 Read/Write가 꺼진 메시의 convex 메시 콜라이더는
        /// 조준 광선에 잡히지 않았고, 박스·캡슐 콜라이더 소품만 프롬프트가 떴다.
        /// </summary>
        [MenuItem(MenuRoot + "3. Replace Mesh Colliders With Box (Variants)")]
        public static void ReplaceMeshCollidersWithBox()
        {
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game/Content/Prefabs/Carryable" });
            var changed = new List<string>();
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!Path.GetFileName(path).StartsWith("Basement_", StringComparison.Ordinal))
                {
                    continue;
                }

                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var meshColliders = root.GetComponentsInChildren<MeshCollider>(true)
                        .Where(c => c.enabled).ToArray();
                    if (meshColliders.Length == 0)
                    {
                        continue;
                    }

                    var renderers = root.GetComponentsInChildren<Renderer>(true)
                        .Where(r => r.enabled && r.name.IndexOf("LOD1", StringComparison.Ordinal) < 0 &&
                                    r.name.IndexOf("LOD2", StringComparison.Ordinal) < 0 &&
                                    r.name.IndexOf("LOD3", StringComparison.Ordinal) < 0)
                        .ToArray();
                    if (renderers.Length == 0)
                    {
                        Debug.LogWarning($"[Lobby Carryable] {path}: 렌더러가 없어 박스 콜라이더를 만들 수 없습니다.");
                        continue;
                    }

                    // 프리팹 루트 로컬 공간 기준 바운드 (프리뷰 씬에서 루트는 원점·단위 스케일)
                    var worldBounds = renderers[0].bounds;
                    foreach (var r in renderers)
                    {
                        worldBounds.Encapsulate(r.bounds);
                    }
                    var localCenter = root.transform.InverseTransformPoint(worldBounds.center);
                    var localSize = root.transform.InverseTransformVector(worldBounds.size);
                    localSize = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));

                    var box = root.GetComponents<BoxCollider>().FirstOrDefault(b => b.name == root.name && !b.isTrigger);
                    if (box == null)
                    {
                        box = root.AddComponent<BoxCollider>();
                    }
                    box.center = localCenter;
                    box.size = localSize;

                    foreach (var mc in meshColliders)
                    {
                        mc.enabled = false;
                    }

                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    changed.Add($"{Path.GetFileNameWithoutExtension(path)} box=({localSize.x:F2},{localSize.y:F2},{localSize.z:F2}) meshColliders off={meshColliders.Length}");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Lobby Carryable] mesh→box replaced in {changed.Count} variant(s)\n" + string.Join("\n", changed.Select(c => "  " + c)));
            EditorUtility.DisplayDialog("Lobby Carryable",
                changed.Count == 0 ? "메시 콜라이더를 쓰는 Variant가 없습니다." : $"{changed.Count}개 Variant에 박스 콜라이더를 적용했습니다. 콘솔 참고.",
                "확인");
        }

        [MenuItem(MenuRoot + "Report")]
        public static void Report()
        {
            var root = PrefabUtility.LoadPrefabContents(EnvironmentPrefabPath);
            try
            {
                var items = root.GetComponentsInChildren<CarryableItem>(true);
                var nonStatic = items.Count(i => !i.gameObject.isStatic);
                var withBody = items.Count(i => i.GetComponent<Rigidbody>() != null);
                var byName = items.GroupBy(i => i.name.Split(' ')[0]).OrderBy(g => g.Key)
                    .Select(g => $"  {g.Key} x{g.Count()}");
                Debug.Log($"[Lobby Carryable] carryable={items.Length} nonStatic={nonStatic} rigidbody={withBody}\n" +
                          string.Join("\n", byName));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// StaticProps 아래에서 접두사에 맞고 아직 Carryable이 아닌 최상위 프리팹 인스턴스를 고른다.
        /// </summary>
        private static List<GameObject> FindCandidates(GameObject root)
        {
            var result = new List<GameObject>();
            Collect(root.transform.Find(SourceGroupName), result,
                (go, prefabName) => CarryablePrefixes.Any(p => prefabName.StartsWith(p, StringComparison.Ordinal)));
            Collect(root.transform.Find(ItemsGroupName), result,
                (go, prefabName) => CarryableItemPrefabNames.Contains(prefabName, StringComparer.Ordinal) ||
                                    CarryableItemInstanceNames.Contains(go.name.Trim(), StringComparer.Ordinal));
            return result;
        }

        private static void Collect(Transform group, List<GameObject> result, Func<GameObject, string, bool> accepts)
        {
            if (group == null)
            {
                return;
            }

            foreach (Transform child in group)
            {
                var go = child.gameObject;
                if (go.GetComponent<CarryableItem>() != null)
                {
                    continue;
                }

                var sourcePath = GetSourcePrefabPath(go);
                if (string.IsNullOrEmpty(sourcePath))
                {
                    continue;
                }

                if (accepts(go, Path.GetFileNameWithoutExtension(sourcePath)))
                {
                    result.Add(go);
                }
            }
        }

        private static bool ReplaceInPrefab(GameObject current, GameObject variant, Transform group)
        {
            var replacement = PrefabUtility.InstantiatePrefab(variant, group) as GameObject;
            if (replacement == null)
            {
                return false;
            }

            var t = current.transform;
            replacement.transform.SetPositionAndRotation(t.position, t.rotation);
            replacement.transform.localScale = t.lossyScale;
            replacement.name = current.name;
            replacement.SetActive(current.activeSelf);

            // 들 수 있는 물건은 움직이므로 라이트맵·정적 배칭 대상에서 뺀다. 조명은 라이트 프로브가 준다.
            foreach (var child in replacement.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.SetStaticEditorFlags(child.gameObject, 0);
            }

            UnityEngine.Object.DestroyImmediate(current);
            return true;
        }

        private static string GetSourcePrefabPath(GameObject instanceRoot)
        {
            var source = PrefabUtility.GetCorrespondingObjectFromSource(instanceRoot);
            return source == null
                ? string.Empty
                : AssetDatabase.GetAssetPath(source).Replace('\\', '/');
        }
    }
}
