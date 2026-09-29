using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Synty 팩의 "Preset"/"Insert"/"Stacked" 프리팹(선반 + 상품, 상자 더미처럼 여러 소품이 한 메시로 합쳐진 것)을
    /// 개별 프리팹 인스턴스로 분해한다. 상품을 하나씩 집거나 숨기려면 개별 오브젝트여야 한다.
    /// </summary>
    /// <remarks>
    /// 원리
    /// <list type="number">
    /// <item>합쳐진 메시의 정점을 위치(0.1 mm)로 용접한 뒤 삼각형 연결로 조각(connected component)을 나눈다.
    /// 인덱스 기준 연결은 하드 에지 때문에 면 단위로 쪼개지므로 용접이 필수다.</item>
    /// <item>팩의 개별 프리팹(Products·Food·Props)도 같은 방식으로 조각을 나눠 카탈로그를 만든다.
    /// 프리팹이 여러 조각(꽃다발 = 화분 + 꽃들, 팔레트 = 판재들)이면 가장 큰 조각으로 위치·Y회전을 잡은 뒤
    /// 나머지 조각들이 예측 위치에 있는지 확인해 통째로 매칭한다. Synty 인서트는 프리팹을 이동+Y 회전으로만 놓은 것이라
    /// 이 모델이 맞는다.</item>
    /// <item>단일 조각 프리팹은 회전 불변 지표(고유 정점 수·중심 거리 분포)로 걸러낸 뒤 Y축 회전을 탐색해 맞춘다.</item>
    /// <item>팩에 없는 모양(인서트 전용 병·캔 등)은 조각 지오메트리로 새 메시·프리팹을 <see cref="GeneratedFolder"/>에 만들어
    /// 세우고, 같은 모양이 다시 나오면 재사용한다. 정점 4개 미만의 부스러기만 "Leftover" 메시로 남긴다.</item>
    /// </list>
    /// 결과는 "&lt;원본&gt;_Exploded/{Structure, Products}" 아래에 놓이고 원본은 비활성화만 한다(비교·되돌리기용).
    /// </remarks>
    public static class MergedPropExplodeMenu
    {
        private static readonly string[] CatalogFolders =
        {
            "Assets/Synty/PolygonShops/Prefabs/Products",
            "Assets/Synty/PolygonShops/Prefabs/Food",
            "Assets/Synty/PolygonShops/Prefabs/Props",
            // 저택(Horror Mansion): 책 더미·접시 세트·종이 뭉치가 낱개 프리팹(Book_01~04, Plate_01~03…)의 합성이다.
            "Assets/Synty/PolygonHorrorMansion/Prefabs/Props",
        };

        // 팩에 개별 프리팹이 없는 조각으로 만든 메시·프리팹이 모이는 곳
        private const string GeneratedFolder = "Assets/_Game/Content/MatchMap/GeneratedProps";
        private const int MinComponentVertices = 4;

        // 위치 용접 정밀도(0.1 mm)와 매칭 허용 오차
        private const float WeldScale = 10000f;
        private const float MatchToleranceMeters = 0.003f;
        private const float MultiPartPositionTolerance = 0.01f;
        private const int CoarseYawStep = 15;
        private const int FineYawRange = 8;

        private static List<CatalogEntry> catalog;

        // ---- 메뉴 --------------------------------------------------------------

        [MenuItem("Game/Match Map/Explode Merged Props (Selected)")]
        public static void ExplodeSelected()
        {
            var targets = Selection.gameObjects
                .Where(go => go.GetComponentInChildren<MeshFilter>() != null)
                .ToArray();
            if (targets.Length == 0)
            {
                Debug.LogWarning("[Explode] MeshFilter가 있는 오브젝트를 선택하세요.");
                return;
            }

            EnsureCatalog();
            var created = new List<GameObject>();
            foreach (var target in targets)
            {
                var result = Explode(target);
                if (result != null)
                {
                    created.Add(result);
                }
            }

            Selection.objects = created.ToArray();
        }

        /// <summary>
        /// 씬에서 합쳐진 소품(프리팹 이름 규칙 기반)을 모두 찾아 분해한다. 진열장 수십 개면 수 분 걸린다.
        /// </summary>
        [MenuItem("Game/Match Map/Explode All Merged Props In Scene")]
        public static void ExplodeAllInScene()
        {
            var targets = FindMergedInScene();
            if (targets.Length == 0)
            {
                Debug.Log("[Explode] 씬에 합쳐진 소품이 없다.");
                return;
            }

            if (!EditorUtility.DisplayDialog("합쳐진 소품 분해",
                    $"씬에서 합쳐진 소품 {targets.Length}개를 찾았습니다.\n모두 개별 소품으로 분해합니다(원본은 비활성화). 진행할까요?",
                    "분해", "취소"))
            {
                return;
            }

            EnsureCatalog();
            var created = new List<GameObject>();
            var done = 0;
            try
            {
                foreach (var target in targets)
                {
                    EditorUtility.DisplayProgressBar("Explode All", $"{target.name} ({done + 1}/{targets.Length})", (float)done / targets.Length);
                    var result = Explode(target);
                    if (result != null) created.Add(result);
                    done++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Selection.objects = created.ToArray();
            Debug.Log($"[Explode] 씬 일괄 분해 완료: {created.Count}/{targets.Length}");
        }

        public static GameObject[] FindMergedInScene()
        {
            return Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Select(f => f.gameObject)
                .Where(go => IsMergedName(SourcePrefabName(go)) && !IsInsideExploded(go.transform))
                .Select(go => PrefabUtility.IsPartOfPrefabInstance(go) ? PrefabUtility.GetOutermostPrefabInstanceRoot(go) ?? go : go)
                .Distinct()
                .ToArray();
        }

        /// <summary>이미 분해된 결과물(…_Exploded 아래)은 다시 대상으로 잡지 않는다.</summary>
        private static bool IsInsideExploded(Transform t)
        {
            for (var current = t; current != null; current = current.parent)
            {
                if (current.name.EndsWith("_Exploded")) return true;
            }

            return false;
        }

        private static bool IsMergedName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            // 생성 프리팹은 원본 이름(_Preset/_Insert)을 물려받지만 이미 개별 부품이다.
            if (name.StartsWith("Gen_")) return false;
            // 팔레트 스택은 팔레트 자체가 판재 여러 조각이라 조각 단위로 부서지기 쉽다 → 다중 조각 매칭이 잡지 못하면 제외 유지
            if (name.Contains("Pallet")) return false;
            // 환경 소품(낙엽 더미·잔디)은 조각이 잎사귀 수백 개라 분해하면 쓰레기 프리팹만 생긴다(저택 2026-09-17).
            if (name.StartsWith("SM_Env_") || name.Contains("Leaves")) return false;
            if (name.Contains("_Preset") || name.Contains("_Insert")) return true;
            // 상자 더미처럼 여러 소품을 한 메시로 쌓아 둔 것
            if (name.Contains("_Stacked") || name.Contains("_Stack_") || name.EndsWith("_Stack") || name.Contains("_Pile")) return true;
            // 저택 팩: 접시 세트(Plate_Set), 책꽂이 한 줄(Book_Line)도 여러 낱개가 한 메시
            if (name.Contains("_Set_") || name.EndsWith("_Set") || name.Contains("Book_Line")) return true;
            // 계산대 진열장: 사탕·잡지 상품이 선반과 한 메시
            if (name.Contains("Checkout_Shelf")) return true;
            // Aisle_02~05는 상품이 구워진 진열대(Aisle_01만 빈 선반)
            return name.StartsWith("SM_Prop_Market_Aisle_0") && !name.StartsWith("SM_Prop_Market_Aisle_01");
        }

        private static string SourcePrefabName(GameObject go)
        {
            var source = PrefabUtility.GetCorrespondingObjectFromSource(go);
            return source != null ? source.name : go.name;
        }

        [MenuItem("Game/Match Map/Rebuild Explode Catalog")]
        public static void RebuildCatalog()
        {
            catalog = null;
            EnsureCatalog();
        }

        // ---- 되돌리기 -------------------------------------------------------------

        /// <summary>씬의 모든 "…_Exploded" 결과물을 지우고 비활성화된 원본을 다시 켠다(에디터 틱마다 하나씩).</summary>
        [MenuItem("Game/Match Map/Revert All Exploded In Scene (Background)")]
        public static void StartBackgroundRevertAll()
        {
            if (revertQueue != null)
            {
                Debug.LogWarning("[Explode] 되돌리기 실행 중: " + RevertStatus);
                return;
            }

            var roots = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(t => t.name.EndsWith("_Exploded") && (t.parent == null || !IsInsideExploded(t.parent)))
                .Select(t => t.gameObject)
                .ToArray();
            if (roots.Length == 0)
            {
                Debug.Log("[Explode] 되돌릴 결과물이 없다.");
                return;
            }

            revertQueue = new Queue<GameObject>(roots);
            revertTotal = roots.Length;
            revertDone = 0;
            EditorApplication.update += RevertStep;
            Debug.Log($"[Explode] 되돌리기 시작: {revertTotal}개");
        }

        /// <summary>
        /// 환경 소품(<c>SM_Env_*</c>: 낙엽 더미 등)의 분해 결과만 되돌린다. 저택에서 Env 제외 규칙이 생기기 전에
        /// 분해된 낙엽 조각을 치울 때 쓴다. 책·접시 같은 소품 분해 결과와 이미 변환된 Carryable은 건드리지 않는다.
        /// </summary>
        [MenuItem("Game/Match Map/Revert Exploded Env Piles In Scene")]
        public static void RevertExplodedEnvPiles()
        {
            var roots = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(t => t.name.EndsWith("_Exploded") && t.name.StartsWith("SM_Env_") &&
                            (t.parent == null || !IsInsideExploded(t.parent)))
                .Select(t => t.gameObject)
                .ToArray();
            if (roots.Length == 0)
            {
                Debug.Log("[Explode] 되돌릴 환경 소품 분해 결과가 없다.");
                return;
            }

            var scene = roots[0].scene;
            foreach (var root in roots)
            {
                RevertExploded(root);
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Explode] 환경 소품 분해 결과 {roots.Length}개를 되돌리고 씬을 저장했다. GeneratedProps의 Gen_SM_Env_* 프리팹은 이제 미사용.");
        }

        private static Queue<GameObject> revertQueue;
        private static int revertTotal;
        private static int revertDone;

        public static string RevertStatus => revertQueue == null ? $"idle (last {revertDone}/{revertTotal})" : $"running {revertDone}/{revertTotal}";
        public static bool IsRevertRunning => revertQueue != null;

        private static void RevertStep()
        {
            if (revertQueue == null || revertQueue.Count == 0)
            {
                EditorApplication.update -= RevertStep;
                revertQueue = null;
                EditorUtility.ClearProgressBar();
                Debug.Log($"[Explode] 되돌리기 완료: {revertDone}/{revertTotal}");
                return;
            }

            var root = revertQueue.Dequeue();
            if (root != null)
            {
                RevertExploded(root);
            }

            revertDone++;
            EditorUtility.DisplayProgressBar("Revert Exploded", $"{revertDone}/{revertTotal}", (float)revertDone / revertTotal);
        }

        /// <summary>결과물 하나를 지우고 같은 부모 아래 비활성화된 같은 이름의 원본을 다시 켠다.</summary>
        public static void RevertExploded(GameObject explodedRoot)
        {
            var originalName = explodedRoot.name.Substring(0, explodedRoot.name.Length - "_Exploded".Length);
            var parent = explodedRoot.transform.parent;
            var siblings = parent != null
                ? Enumerable.Range(0, parent.childCount).Select(i => parent.GetChild(i))
                : explodedRoot.scene.GetRootGameObjects().Select(g => g.transform);
            var original = siblings.FirstOrDefault(t => t.name == originalName && !t.gameObject.activeSelf);
            if (original != null)
            {
                Undo.RecordObject(original.gameObject, "Revert Exploded");
                original.gameObject.SetActive(true);
            }
            else
            {
                Debug.LogWarning($"[Explode] {explodedRoot.name}: 원본을 찾지 못해 결과물만 지운다.");
            }

            Undo.DestroyObjectImmediate(explodedRoot);
        }

        // ---- 백그라운드 일괄 처리(에디터 틱마다 하나씩, 에디터가 멈추지 않음) -----------------

        private static Queue<GameObject> backgroundQueue;
        private static int backgroundTotal;
        private static int backgroundDone;
        private static System.Diagnostics.Stopwatch backgroundClock;

        /// <summary>진행 상황 문자열. 실행 중이 아니면 "idle".</summary>
        public static string BackgroundStatus =>
            backgroundQueue == null
                ? $"idle (last done={backgroundDone}/{backgroundTotal})"
                : $"running {backgroundDone}/{backgroundTotal} elapsed={backgroundClock?.Elapsed.TotalSeconds:F0}s";

        public static bool IsBackgroundRunning => backgroundQueue != null;

        [MenuItem("Game/Match Map/Explode All Merged Props In Scene (Background)")]
        public static void StartBackgroundExplodeAll()
        {
            StartBackgroundExplode(FindMergedInScene());
        }

        public static void StartBackgroundExplode(GameObject[] targets)
        {
            if (backgroundQueue != null)
            {
                Debug.LogWarning("[Explode] 이미 실행 중: " + BackgroundStatus);
                return;
            }

            EnsureCatalog();
            if (targets.Length == 0)
            {
                Debug.Log("[Explode] 분해할 대상이 없다.");
                return;
            }

            backgroundQueue = new Queue<GameObject>(targets);
            backgroundTotal = targets.Length;
            backgroundDone = 0;
            backgroundClock = System.Diagnostics.Stopwatch.StartNew();
            EditorApplication.update += BackgroundStep;
            Debug.Log($"[Explode] 백그라운드 분해 시작: {backgroundTotal}개");
        }

        public static void StopBackgroundExplode()
        {
            EditorApplication.update -= BackgroundStep;
            backgroundQueue = null;
            EditorUtility.ClearProgressBar();
        }

        private static void BackgroundStep()
        {
            if (backgroundQueue == null || backgroundQueue.Count == 0)
            {
                EditorApplication.update -= BackgroundStep;
                backgroundQueue = null;
                EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
                Debug.Log($"[Explode] 백그라운드 분해 완료: {backgroundDone}/{backgroundTotal}, {backgroundClock?.Elapsed.TotalSeconds:F0}s");
                return;
            }

            var target = backgroundQueue.Dequeue();
            if (target != null && target.activeInHierarchy)
            {
                try
                {
                    Explode(target);
                }
                catch (System.Exception e)
                {
                    Debug.LogException(e);
                }
            }

            backgroundDone++;
            EditorUtility.DisplayProgressBar("Explode All (Background)", $"{backgroundDone}/{backgroundTotal}", (float)backgroundDone / backgroundTotal);
        }

        // ---- 분해 --------------------------------------------------------------

        /// <summary>
        /// 한 오브젝트를 분해한다. 결과 루트("&lt;이름&gt;_Exploded")를 돌려주고 원본은 비활성화한다.
        /// </summary>
        public static GameObject Explode(GameObject source)
        {
            EnsureCatalog();
            var filter = source.GetComponentInChildren<MeshFilter>();
            var renderer = filter != null ? filter.GetComponent<MeshRenderer>() : null;
            if (filter == null || filter.sharedMesh == null || renderer == null)
            {
                Debug.LogWarning($"[Explode] {source.name}: 메시가 없다.");
                return null;
            }

            var mesh = filter.sharedMesh;
            var components = SplitComponents(mesh, out var welded);
            var sourcePrefabName = SourcePrefabName(source);
            // 분해 대상이 카탈로그에도 있는 프리팹(예: 물건이 든 상자)이면 자기 자신과 통째로 매칭되지 않게 뺀다.
            excludedPrefab = PrefabUtility.GetCorrespondingObjectFromOriginalSource(source);

            var root = new GameObject(source.name + "_Exploded");
            Undo.RegisterCreatedObjectUndo(root, "Explode Merged Prop");
            root.transform.SetParent(source.transform.parent, false);
            root.transform.SetPositionAndRotation(filter.transform.position, filter.transform.rotation);
            root.transform.localScale = filter.transform.lossyScale;
            root.transform.SetSiblingIndex(source.transform.GetSiblingIndex() + 1);

            // 선반 몸체·다리 같은 구조물과 상품을 나눠 담는다(뒤에 Carryable 전환·Static 지정이 쉬워진다).
            var structureGroup = new GameObject("Structure");
            Undo.RegisterCreatedObjectUndo(structureGroup, "Explode Merged Prop");
            structureGroup.transform.SetParent(root.transform, false);
            var productsGroup = new GameObject("Products");
            Undo.RegisterCreatedObjectUndo(productsGroup, "Explode Merged Prop");
            productsGroup.transform.SetParent(root.transform, false);

            // 조각 정보
            var count = components.Count;
            var points = new Vector3[count][];
            var parts = new Part[count];
            var consumed = new bool[count];
            var leftoverComponents = new List<int[]>();
            var byCount = new Dictionary<int, List<int>>();
            for (var i = 0; i < count; i++)
            {
                points[i] = components[i].Select(v => welded.Positions[v]).ToArray();
                if (points[i].Length < MinComponentVertices)
                {
                    consumed[i] = true;
                    leftoverComponents.Add(components[i]);
                    continue;
                }

                parts[i] = MakePart(points[i]);
                if (!byCount.TryGetValue(points[i].Length, out var list)) byCount[points[i].Length] = list = new List<int>();
                list.Add(i);
            }

            var placements = new List<Placement>();
            var matched = 0;
            var generated = 0;
            var usedNames = new Dictionary<string, int>();
            try
            {
                // 1단계: 여러 조각으로 된 프리팹(꽃다발·팔레트·랙)을 통째로 매칭
                var multiPart = catalog.Where(e => e.Parts.Count >= 2).ToArray();
                for (var i = 0; i < count; i++)
                {
                    if (consumed[i]) continue;
                    EditorUtility.DisplayProgressBar("Explode Merged Prop", $"{source.name} 다중 조각 매칭 {i + 1}/{count}", (float)i / count * 0.5f);
                    if (TryMatchMultiPart(i, parts, byCount, consumed, multiPart, out var placement))
                    {
                        placements.Add(placement);
                        foreach (var idx in placement.ComponentIndices) consumed[idx] = true;
                        matched++;
                    }
                }

                // 2단계: 남은 조각을 단일 조각 프리팹으로, 없으면 생성 프리팹으로
                for (var i = 0; i < count; i++)
                {
                    if (consumed[i]) continue;
                    EditorUtility.DisplayProgressBar("Explode Merged Prop", $"{source.name} 조각 {i + 1}/{count}", 0.5f + (float)i / count * 0.5f);

                    if (TryMatchSingle(parts[i], out var entry, out var rotation, out var localPosition))
                    {
                        matched++;
                    }
                    else
                    {
                        // 팩에 없는 모양: 조각 자체로 프리팹을 만들어 카탈로그에 넣는다(다음 같은 모양은 재사용).
                        entry = CreateGeneratedEntry(mesh, renderer, welded, components[i], points[i], sourcePrefabName);
                        rotation = Quaternion.identity;
                        localPosition = parts[i].Centroid;
                        generated++;
                    }

                    placements.Add(new Placement { Entry = entry, Rotation = rotation, LocalPosition = localPosition, ComponentIndices = new[] { i } });
                    consumed[i] = true;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            foreach (var placement in placements)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(placement.Entry.Prefab, root.scene);
                Undo.RegisterCreatedObjectUndo(instance, "Explode Merged Prop");
                var allPoints = placement.ComponentIndices.SelectMany(idx => points[idx]).ToArray();
                var group = IsStructure(placement.Entry, allPoints) ? structureGroup : productsGroup;
                instance.transform.SetParent(group.transform, false);
                instance.transform.localPosition = placement.LocalPosition;
                instance.transform.localRotation = placement.Rotation;
                instance.transform.localScale = Vector3.one;
                usedNames[placement.Entry.Prefab.name] = usedNames.TryGetValue(placement.Entry.Prefab.name, out var c) ? c + 1 : 1;
            }

            if (leftoverComponents.Count > 0)
            {
                CreateLeftover(structureGroup, mesh, renderer, welded, leftoverComponents);
            }

            if (productsGroup.transform.childCount == 0) Object.DestroyImmediate(productsGroup);
            if (structureGroup.transform.childCount == 0) Object.DestroyImmediate(structureGroup);

            AssetDatabase.SaveAssets();
            Undo.RecordObject(source, "Explode Merged Prop");
            source.SetActive(false);

            var summary = string.Join(", ", usedNames.OrderByDescending(kv => kv.Value).Take(10).Select(kv => $"{kv.Key}×{kv.Value}"));
            Debug.Log($"[Explode] {source.name}: 조각 {count}개 → 팩 프리팹 {matched}개, 생성 프리팹 {generated}개" +
                      $"{(leftoverComponents.Count > 0 ? $", 부스러기 {leftoverComponents.Count}개는 Leftover" : "")}. {summary}", root);
            return root;
        }

        private static GameObject excludedPrefab;

        private static bool IsExcluded(CatalogEntry entry)
        {
            return excludedPrefab != null && entry.Prefab != null && entry.Prefab.name == excludedPrefab.name;
        }

        private sealed class Placement
        {
            public CatalogEntry Entry;
            public Quaternion Rotation;
            public Vector3 LocalPosition;
            public int[] ComponentIndices;
        }

        // ---- 카탈로그 ----------------------------------------------------------

        /// <summary>프리팹 메시의 연결 조각 하나(또는 분해 대상의 조각 하나).</summary>
        private sealed class Part
        {
            public Vector3[] Positions;      // 용접된 고유 정점(프리팹 로컬)
            public Vector3 Centroid;
            public float[] RadialSignature;  // 중심 거리 정렬(회전 불변)
            public float Height;             // Y 범위(회전 불변)
        }

        private sealed class CatalogEntry
        {
            public GameObject Prefab;
            public List<Part> Parts;         // 큰 것부터. 1개면 단일 조각 프리팹
            public Part Anchor => Parts[0];
        }

        private static void EnsureCatalog()
        {
            if (catalog != null)
            {
                return;
            }

            catalog = new List<CatalogEntry>();
            var folders = CatalogFolders.Concat(new[] { GeneratedFolder }).ToArray();
            foreach (var folder in folders)
            {
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    continue;
                }

                foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                    var name = prefab.name;
                    // 합쳐진 것(Preset/Insert/Stacked)은 후보에서 제외: 분해 대상이지 부품이 아니다.
                    // 생성 폴더의 프리팹은 원본 이름을 물려받아 "_Insert"가 들어가도 부품이다.
                    if (folder != GeneratedFolder && IsMergedName(name))
                    {
                        continue;
                    }

                    var filters = prefab.GetComponentsInChildren<MeshFilter>(true);
                    if (filters.Length != 1 || filters[0].sharedMesh == null)
                    {
                        continue;
                    }

                    // 메시가 루트에 로컬 변환 없이 붙은 프리팹만(Synty 소품은 전부 이 형태)
                    var t = filters[0].transform;
                    if (t != prefab.transform && (t.localPosition != Vector3.zero || t.localRotation != Quaternion.identity))
                    {
                        continue;
                    }

                    var entry = BuildEntryFromMesh(prefab, filters[0].sharedMesh);
                    if (entry != null)
                    {
                        catalog.Add(entry);
                    }
                }
            }

            Debug.Log($"[Explode] 카탈로그 {catalog.Count}개 프리팹(다중 조각 {catalog.Count(e => e.Parts.Count >= 2)}개).");
        }

        private static CatalogEntry BuildEntryFromMesh(GameObject prefab, Mesh mesh)
        {
            var components = SplitComponents(mesh, out var welded);
            var parts = components
                .Where(c => c.Length >= MinComponentVertices)
                .Select(c => MakePart(c.Select(i => welded.Positions[i]).ToArray()))
                .ToList();
            return parts.Count == 0 ? null : new CatalogEntry { Prefab = prefab, Parts = parts };
        }

        private static CatalogEntry BuildSingleEntry(GameObject prefab, Vector3[] positions)
        {
            return new CatalogEntry { Prefab = prefab, Parts = new List<Part> { MakePart(positions) } };
        }

        private static Part MakePart(Vector3[] positions)
        {
            var centroid = Centroid(positions);
            return new Part
            {
                Positions = positions,
                Centroid = centroid,
                RadialSignature = RadialSignature(positions, centroid),
                Height = positions.Max(p => p.y) - positions.Min(p => p.y),
            };
        }

        // ---- 매칭 --------------------------------------------------------------

        /// <summary>회전에 무관한 지표(정점 수·중심 거리 분포)만으로 후보를 고른다. 높이는 기울어진 물건에서 달라지므로 쓰지 않는다.</summary>
        private static bool QuickMatch(Part candidate, Part target)
        {
            return candidate.Positions.Length == target.Positions.Length
                   && SignaturesClose(candidate.RadialSignature, target.RadialSignature);
        }

        /// <summary>
        /// 여러 조각 프리팹 매칭: 가장 큰 조각(anchor)으로 위치·회전을 정하고, 나머지 조각이 예측 위치에 있는지 확인.
        /// </summary>
        private static bool TryMatchMultiPart(int anchorIndex, Part[] parts, Dictionary<int, List<int>> byCount, bool[] consumed,
            CatalogEntry[] multiPart, out Placement placement)
        {
            placement = null;
            var anchor = parts[anchorIndex];
            foreach (var entry in multiPart)
            {
                if (IsExcluded(entry) || !QuickMatch(entry.Anchor, anchor))
                {
                    continue;
                }

                if (!FindRotation(anchor, entry.Anchor, out var rotation, out var error) || error > MatchToleranceMeters)
                {
                    continue;
                }

                var localPosition = anchor.Centroid - rotation * entry.Anchor.Centroid;
                var found = new List<int> { anchorIndex };
                var complete = true;
                for (var k = 1; k < entry.Parts.Count; k++)
                {
                    var expected = entry.Parts[k];
                    var predicted = localPosition + rotation * expected.Centroid;
                    var hit = -1;
                    if (byCount.TryGetValue(expected.Positions.Length, out var candidates))
                    {
                        var best = float.MaxValue;
                        foreach (var j in candidates)
                        {
                            if (consumed[j] || found.Contains(j)) continue;
                            var d = (parts[j].Centroid - predicted).magnitude;
                            if (d < MultiPartPositionTolerance && d < best && SignaturesClose(parts[j].RadialSignature, expected.RadialSignature))
                            {
                                best = d;
                                hit = j;
                            }
                        }
                    }

                    if (hit < 0)
                    {
                        complete = false;
                        break;
                    }

                    found.Add(hit);
                }

                if (!complete)
                {
                    continue;
                }

                placement = new Placement { Entry = entry, Rotation = rotation, LocalPosition = localPosition, ComponentIndices = found.ToArray() };
                return true;
            }

            return false;
        }

        private static bool TryMatchSingle(Part target, out CatalogEntry best, out Quaternion bestRotation, out Vector3 localPosition)
        {
            best = null;
            bestRotation = Quaternion.identity;
            localPosition = Vector3.zero;
            var bestError = float.MaxValue;
            foreach (var entry in catalog)
            {
                if (entry.Parts.Count != 1 || IsExcluded(entry) || !QuickMatch(entry.Anchor, target))
                {
                    continue;
                }

                if (!FindRotation(target, entry.Anchor, out var rotation, out var error))
                {
                    continue;
                }

                if (error < bestError)
                {
                    bestError = error;
                    best = entry;
                    bestRotation = rotation;
                }
            }

            if (best == null || bestError > MatchToleranceMeters)
            {
                return false;
            }

            // 조각 중심 = R * 후보 중심 + 위치  →  위치 = 조각 중심 - R * 후보 중심
            localPosition = target.Centroid - bestRotation * best.Anchor.Centroid;
            return true;
        }

        /// <summary>
        /// 후보를 조각에 맞추는 회전을 찾는다. 먼저 Y축 회전만 탐색하고(대부분의 상품), 실패하면
        /// 임의 3D 회전(기울여 놓은 과일 등)을 PCA 초기값 + ICP로 맞춘다.
        /// </summary>
        private static bool FindRotation(Part target, Part candidate, out Quaternion rotation, out float error)
        {
            var yaw = FindYaw(target.Positions, target.Centroid, candidate, out error);
            rotation = Quaternion.Euler(0f, yaw, 0f);
            if (error <= MatchToleranceMeters)
            {
                return true;
            }

            if (FindFreeRotation(target, candidate, out var free, out var freeError) && freeError < error)
            {
                rotation = free;
                error = freeError;
            }

            return error <= MatchToleranceMeters;
        }

        // ---- 임의 회전 정합(PCA + ICP) ------------------------------------------------

        private static bool FindFreeRotation(Part target, Part candidate, out Quaternion best, out float bestError)
        {
            best = Quaternion.identity;
            bestError = float.MaxValue;
            var a = target.Positions.Select(p => p - target.Centroid).ToArray();
            var b = candidate.Positions.Select(p => p - candidate.Centroid).ToArray();
            var axesA = PrincipalAxes(a);
            var axesB = PrincipalAxes(b);

            // 주축 부호 4가지 조합(고유 회전만) → 초기 회전 → ICP 정제
            var signs = new[] { new Vector3(1, 1, 1), new Vector3(1, -1, -1), new Vector3(-1, 1, -1), new Vector3(-1, -1, 1) };
            foreach (var s in signs)
            {
                var flippedB = new[] { axesB[0] * s.x, axesB[1] * s.y, axesB[2] * s.z };
                var initial = RotationFromFrames(flippedB, axesA);
                var refined = RefineIcp(a, b, initial, 8);
                var err = MeanNearestDistance(a, b, refined);
                if (err < bestError)
                {
                    bestError = err;
                    best = refined;
                }
            }

            return bestError < float.MaxValue;
        }

        /// <summary>공분산 고유벡터(큰 고유값부터). 대칭 3×3 야코비 회전.</summary>
        private static Vector3[] PrincipalAxes(Vector3[] centered)
        {
            var c = new double[3, 3];
            foreach (var p in centered)
            {
                c[0, 0] += p.x * p.x; c[0, 1] += p.x * p.y; c[0, 2] += p.x * p.z;
                c[1, 1] += p.y * p.y; c[1, 2] += p.y * p.z; c[2, 2] += p.z * p.z;
            }

            c[1, 0] = c[0, 1]; c[2, 0] = c[0, 2]; c[2, 1] = c[1, 2];
            var (values, vectors) = JacobiEigen(c);
            var order = Enumerable.Range(0, 3).OrderByDescending(i => values[i]).ToArray();
            var axes = order.Select(i => new Vector3((float)vectors[0, i], (float)vectors[1, i], (float)vectors[2, i]).normalized).ToArray();
            // 오른손 좌표계로 맞춘다
            if (Vector3.Dot(Vector3.Cross(axes[0], axes[1]), axes[2]) < 0) axes[2] = -axes[2];
            return axes;
        }

        /// <summary>from 프레임(직교 3축)을 to 프레임으로 보내는 회전.</summary>
        private static Quaternion RotationFromFrames(Vector3[] from, Vector3[] to)
        {
            var f = Matrix4x4.identity;
            var t = Matrix4x4.identity;
            for (var i = 0; i < 3; i++)
            {
                f.SetColumn(i, new Vector4(from[i].x, from[i].y, from[i].z, 0));
                t.SetColumn(i, new Vector4(to[i].x, to[i].y, to[i].z, 0));
            }

            var r = t * f.transpose; // R = T · Fᵀ
            return QuaternionFromMatrix(r);
        }

        private static Quaternion QuaternionFromMatrix(Matrix4x4 m)
        {
            // 열이 직교 정규라고 가정. LookRotation은 비직교에 취약하므로 직접 변환.
            var q = new Quaternion();
            var trace = m.m00 + m.m11 + m.m22;
            if (trace > 0)
            {
                var s = Mathf.Sqrt(trace + 1f) * 2f;
                q.w = 0.25f * s; q.x = (m.m21 - m.m12) / s; q.y = (m.m02 - m.m20) / s; q.z = (m.m10 - m.m01) / s;
            }
            else if (m.m00 > m.m11 && m.m00 > m.m22)
            {
                var s = Mathf.Sqrt(1f + m.m00 - m.m11 - m.m22) * 2f;
                q.w = (m.m21 - m.m12) / s; q.x = 0.25f * s; q.y = (m.m01 + m.m10) / s; q.z = (m.m02 + m.m20) / s;
            }
            else if (m.m11 > m.m22)
            {
                var s = Mathf.Sqrt(1f + m.m11 - m.m00 - m.m22) * 2f;
                q.w = (m.m02 - m.m20) / s; q.x = (m.m01 + m.m10) / s; q.y = 0.25f * s; q.z = (m.m12 + m.m21) / s;
            }
            else
            {
                var s = Mathf.Sqrt(1f + m.m22 - m.m00 - m.m11) * 2f;
                q.w = (m.m10 - m.m01) / s; q.x = (m.m02 + m.m20) / s; q.y = (m.m12 + m.m21) / s; q.z = 0.25f * s;
            }

            return Quaternion.Normalize(q);
        }

        /// <summary>ICP: 최근접 대응 → Horn 사원수 최적 회전, 반복.</summary>
        private static Quaternion RefineIcp(Vector3[] a, Vector3[] b, Quaternion initial, int iterations)
        {
            var rotation = initial;
            for (var iter = 0; iter < iterations; iter++)
            {
                // 대응: a_i ↔ b_j (회전된 b 중 최근접)
                var rotatedB = b.Select(p => rotation * p).ToArray();
                var pairsB = new Vector3[a.Length];
                for (var i = 0; i < a.Length; i++)
                {
                    var min = float.MaxValue;
                    var idx = 0;
                    for (var j = 0; j < rotatedB.Length; j++)
                    {
                        var d = (a[i] - rotatedB[j]).sqrMagnitude;
                        if (d < min) { min = d; idx = j; }
                    }

                    pairsB[i] = b[idx];
                }

                // Horn: 대응 (b_i → a_i)에 대한 최적 회전
                var h = new double[3, 3];
                for (var i = 0; i < a.Length; i++)
                {
                    var p = pairsB[i]; var q = a[i];
                    h[0, 0] += p.x * q.x; h[0, 1] += p.x * q.y; h[0, 2] += p.x * q.z;
                    h[1, 0] += p.y * q.x; h[1, 1] += p.y * q.y; h[1, 2] += p.y * q.z;
                    h[2, 0] += p.z * q.x; h[2, 1] += p.z * q.y; h[2, 2] += p.z * q.z;
                }

                var n = new double[4, 4];
                n[0, 0] = h[0, 0] + h[1, 1] + h[2, 2];
                n[0, 1] = h[1, 2] - h[2, 1]; n[0, 2] = h[2, 0] - h[0, 2]; n[0, 3] = h[0, 1] - h[1, 0];
                n[1, 1] = h[0, 0] - h[1, 1] - h[2, 2]; n[1, 2] = h[0, 1] + h[1, 0]; n[1, 3] = h[2, 0] + h[0, 2];
                n[2, 2] = -h[0, 0] + h[1, 1] - h[2, 2]; n[2, 3] = h[1, 2] + h[2, 1];
                n[3, 3] = -h[0, 0] - h[1, 1] + h[2, 2];
                for (var r = 0; r < 4; r++) for (var c = 0; c < r; c++) n[r, c] = n[c, r];
                var (values, vectors) = JacobiEigen(n);
                var maxIndex = 0;
                for (var i = 1; i < 4; i++) if (values[i] > values[maxIndex]) maxIndex = i;
                var next = new Quaternion((float)vectors[1, maxIndex], (float)vectors[2, maxIndex], (float)vectors[3, maxIndex], (float)vectors[0, maxIndex]);
                next = Quaternion.Normalize(next);
                if (Quaternion.Angle(next, rotation) < 0.01f)
                {
                    rotation = next;
                    break;
                }

                rotation = next;
            }

            return rotation;
        }

        private static float MeanNearestDistance(Vector3[] a, Vector3[] b, Quaternion rotation)
        {
            var rotatedB = b.Select(p => rotation * p).ToArray();
            var total = 0f;
            for (var i = 0; i < a.Length; i++)
            {
                var min = float.MaxValue;
                for (var j = 0; j < rotatedB.Length; j++)
                {
                    var d = (a[i] - rotatedB[j]).sqrMagnitude;
                    if (d < min) min = d;
                }

                total += Mathf.Sqrt(min);
            }

            return total / a.Length;
        }

        /// <summary>대칭 행렬의 고유값·고유벡터(열). 순환 야코비 회전.</summary>
        private static (double[] values, double[,] vectors) JacobiEigen(double[,] input)
        {
            var n = input.GetLength(0);
            var a = (double[,])input.Clone();
            var v = new double[n, n];
            for (var i = 0; i < n; i++) v[i, i] = 1;
            for (var sweep = 0; sweep < 60; sweep++)
            {
                double off = 0;
                for (var p = 0; p < n; p++) for (var q = p + 1; q < n; q++) off += a[p, q] * a[p, q];
                if (off < 1e-20) break;
                for (var p = 0; p < n; p++)
                for (var q = p + 1; q < n; q++)
                {
                    if (System.Math.Abs(a[p, q]) < 1e-18) continue;
                    var theta = (a[q, q] - a[p, p]) / (2 * a[p, q]);
                    var t = System.Math.Sign(theta) / (System.Math.Abs(theta) + System.Math.Sqrt(theta * theta + 1));
                    if (theta == 0) t = 1;
                    var c = 1 / System.Math.Sqrt(t * t + 1);
                    var s = t * c;
                    for (var k = 0; k < n; k++)
                    {
                        var akp = a[k, p]; var akq = a[k, q];
                        a[k, p] = c * akp - s * akq; a[k, q] = s * akp + c * akq;
                    }

                    for (var k = 0; k < n; k++)
                    {
                        var apk = a[p, k]; var aqk = a[q, k];
                        a[p, k] = c * apk - s * aqk; a[q, k] = s * apk + c * aqk;
                    }

                    for (var k = 0; k < n; k++)
                    {
                        var vkp = v[k, p]; var vkq = v[k, q];
                        v[k, p] = c * vkp - s * vkq; v[k, q] = s * vkp + c * vkq;
                    }
                }
            }

            var values = new double[n];
            for (var i = 0; i < n; i++) values[i] = a[i, i];
            return (values, v);
        }

        private static float FindYaw(Vector3[] points, Vector3 centroid, Part candidate, out float bestError)
        {
            var bestYaw = 0f;
            bestError = float.MaxValue;
            for (var yaw = 0; yaw < 360; yaw += CoarseYawStep)
            {
                var error = AlignmentError(points, centroid, candidate, yaw);
                if (error < bestError)
                {
                    bestError = error;
                    bestYaw = yaw;
                }
            }

            var coarse = bestYaw;
            for (var yaw = coarse - FineYawRange; yaw <= coarse + FineYawRange; yaw++)
            {
                var error = AlignmentError(points, centroid, candidate, yaw);
                if (error < bestError)
                {
                    bestError = error;
                    bestYaw = yaw;
                }
            }

            return bestYaw;
        }

        /// <summary>후보를 yaw만큼 돌려 조각 중심에 맞춘 뒤, 조각 정점마다 가장 가까운 후보 정점 거리의 평균.</summary>
        private static float AlignmentError(Vector3[] points, Vector3 centroid, Part candidate, float yaw)
        {
            var rotation = Quaternion.Euler(0f, yaw, 0f);
            var rotated = new Vector3[candidate.Positions.Length];
            for (var i = 0; i < rotated.Length; i++)
            {
                rotated[i] = rotation * (candidate.Positions[i] - candidate.Centroid) + centroid;
            }

            var total = 0f;
            var limit = MatchToleranceMeters * 4f;
            for (var i = 0; i < points.Length; i++)
            {
                var min = float.MaxValue;
                for (var j = 0; j < rotated.Length; j++)
                {
                    var d = (points[i] - rotated[j]).sqrMagnitude;
                    if (d < min) min = d;
                }

                total += Mathf.Sqrt(min);
                if (total / points.Length > limit)
                {
                    return float.MaxValue;
                }
            }

            return total / points.Length;
        }

        private static float[] RadialSignature(Vector3[] points, Vector3 centroid)
        {
            var distances = new float[points.Length];
            for (var i = 0; i < points.Length; i++)
            {
                distances[i] = (points[i] - centroid).magnitude;
            }

            System.Array.Sort(distances);
            return distances;
        }

        private static bool SignaturesClose(float[] a, float[] b)
        {
            if (a.Length != b.Length) return false;
            for (var i = 0; i < a.Length; i++)
            {
                if (Mathf.Abs(a[i] - b[i]) > MatchToleranceMeters)
                {
                    return false;
                }
            }

            return true;
        }

        private static Vector3 Centroid(Vector3[] points)
        {
            var sum = Vector3.zero;
            foreach (var p in points) sum += p;
            return sum / points.Length;
        }

        private static readonly string[] StructureKeywords =
        {
            "Stand", "Shelf", "Rack", "Aisle", "Checkout", "Fridge", "Freezer", "Display", "Pallet",
            "Trolley", "Cart", "Counter", "Table", "Cabinet", "Kiosk", "Crate_Large",
        };

        /// <summary>
        /// 구조물 판정: 팩의 Products/Food 프리팹이면 상품. 그 외는 이름에 진열대·랙·카트 같은 구조물 키워드가 있거나
        /// 한 변이 1.2 m를 넘으면 구조물, 아니면 상품(꽃다발·화분·상자 등 들 수 있는 크기).
        /// </summary>
        private static bool IsStructure(CatalogEntry entry, Vector3[] points)
        {
            var path = AssetDatabase.GetAssetPath(entry.Prefab);
            if (path.Contains("/Prefabs/Products/") || path.Contains("/Prefabs/Food/"))
            {
                return false;
            }

            var bounds = new Bounds(points[0], Vector3.zero);
            foreach (var p in points) bounds.Encapsulate(p);
            var size = bounds.size;

            // 생성 프리팹은 원본 이름(Display/Shelf/Fridge…)을 물려받으므로 키워드가 아니라 크기로만 본다.
            if (path.StartsWith(GeneratedFolder))
            {
                return Mathf.Max(size.x, size.y, size.z) > 0.6f || size.x * size.y * size.z > 0.03f;
            }

            var name = entry.Prefab.name;
            if (StructureKeywords.Any(k => name.Contains(k)))
            {
                return true;
            }

            return Mathf.Max(size.x, size.y, size.z) > 1.2f;
        }

        /// <summary>
        /// 이미 분해된 결과물의 Structure/Products 분류를 현재 규칙으로 다시 매긴다(재분해 없이).
        /// </summary>
        [MenuItem("Game/Match Map/Reclassify Exploded Structure-Products")]
        public static void ReclassifyExploded()
        {
            EnsureCatalog();
            var byPrefab = catalog.ToDictionary(e => e.Prefab, e => e);
            var roots = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(t => t.name.EndsWith("_Exploded")).ToArray();
            var moved = 0;
            foreach (var root in roots)
            {
                var structure = root.Find("Structure");
                var products = root.Find("Products");
                if (structure == null) { structure = new GameObject("Structure").transform; structure.SetParent(root, false); }
                if (products == null) { products = new GameObject("Products").transform; products.SetParent(root, false); }

                var children = new List<Transform>();
                for (var i = 0; i < structure.childCount; i++) children.Add(structure.GetChild(i));
                for (var i = 0; i < products.childCount; i++) children.Add(products.GetChild(i));
                foreach (var child in children)
                {
                    if (child.name == "Leftover") continue;
                    var prefab = PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject);
                    if (prefab == null || !byPrefab.TryGetValue(prefab, out var entry)) continue;
                    var points = entry.Parts.SelectMany(p => p.Positions).ToArray();
                    var target = IsStructure(entry, points) ? structure : products;
                    if (child.parent != target)
                    {
                        Undo.SetTransformParent(child, target, "Reclassify Exploded");
                        moved++;
                    }
                }

                if (structure.childCount == 0) Object.DestroyImmediate(structure.gameObject);
                if (products.childCount == 0) Object.DestroyImmediate(products.gameObject);
            }

            Debug.Log($"[Explode] 재분류: {roots.Length}개 결과물에서 {moved}개 이동");
        }

        // ---- 생성 프리팹(팩에 없는 모양) ---------------------------------------------

        /// <summary>
        /// 조각의 삼각형만 떼어 조각 중심을 원점으로 한 메시를 만들고, 원본 재질과 BoxCollider를 단
        /// 프리팹으로 저장한 뒤 카탈로그 항목으로 돌려준다(중심 0 기준 단일 조각 항목).
        /// </summary>
        private static CatalogEntry CreateGeneratedEntry(Mesh mesh, MeshRenderer sourceRenderer, WeldedMesh welded,
            int[] component, Vector3[] points, string sourceName)
        {
            EnsureFolder(GeneratedFolder);
            var centroid = Centroid(points);
            var keep = new HashSet<int>(component);

            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var uv = mesh.uv;
            var remap = new Dictionary<int, int>();
            var newVertices = new List<Vector3>();
            var newNormals = new List<Vector3>();
            var newUv = new List<Vector2>();
            var submeshTriangles = new List<int[]>();
            var usedMaterials = new List<Material>();
            var sourceMaterials = sourceRenderer.sharedMaterials;

            for (var sub = 0; sub < mesh.subMeshCount; sub++)
            {
                var triangles = mesh.GetTriangles(sub);
                var indices = new List<int>();
                for (var t = 0; t < triangles.Length; t += 3)
                {
                    if (!keep.Contains(welded.VertexToPosition[triangles[t]]))
                    {
                        continue;
                    }

                    for (var k = 0; k < 3; k++)
                    {
                        var original = triangles[t + k];
                        if (!remap.TryGetValue(original, out var mapped))
                        {
                            mapped = newVertices.Count;
                            remap[original] = mapped;
                            newVertices.Add(vertices[original] - centroid);
                            if (normals.Length == vertices.Length) newNormals.Add(normals[original]);
                            if (uv.Length == vertices.Length) newUv.Add(uv[original]);
                        }

                        indices.Add(mapped);
                    }
                }

                if (indices.Count == 0)
                {
                    continue;
                }

                submeshTriangles.Add(indices.ToArray());
                usedMaterials.Add(sub < sourceMaterials.Length ? sourceMaterials[sub] : sourceMaterials[0]);
            }

            var generatedMesh = new Mesh { name = $"Gen_{Sanitize(sourceName)}_{points.Length}v" };
            generatedMesh.indexFormat = newVertices.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            generatedMesh.SetVertices(newVertices);
            if (newNormals.Count == newVertices.Count) generatedMesh.SetNormals(newNormals);
            if (newUv.Count == newVertices.Count) generatedMesh.SetUVs(0, newUv);
            generatedMesh.subMeshCount = submeshTriangles.Count;
            for (var sub = 0; sub < submeshTriangles.Count; sub++)
            {
                generatedMesh.SetTriangles(submeshTriangles[sub], sub, false);
            }

            generatedMesh.RecalculateBounds();
            if (newNormals.Count != newVertices.Count) generatedMesh.RecalculateNormals();

            var meshPath = AssetDatabase.GenerateUniqueAssetPath($"{GeneratedFolder}/{generatedMesh.name}.asset");
            AssetDatabase.CreateAsset(generatedMesh, meshPath);

            var go = new GameObject(System.IO.Path.GetFileNameWithoutExtension(meshPath));
            go.AddComponent<MeshFilter>().sharedMesh = generatedMesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = usedMaterials.ToArray();
            var box = go.AddComponent<BoxCollider>();
            box.center = generatedMesh.bounds.center;
            // 납작한 조각(종잇장·책갈피 5정점)은 두께 0이 되어 BoxCollider 경고와 놓기 판정 불안정을 낳는다 → 최소 2 cm.
            box.size = Vector3.Max(generatedMesh.bounds.size, Vector3.one * 0.02f);
            var prefabPath = AssetDatabase.GenerateUniqueAssetPath($"{GeneratedFolder}/{go.name}.prefab");
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);

            var centered = points.Select(p => p - centroid).ToArray();
            var entry = BuildSingleEntry(prefab, centered);
            catalog.Add(entry);
            return entry;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        private static string Sanitize(string name)
        {
            foreach (var invalid in System.IO.Path.GetInvalidFileNameChars())
            {
                name = name.Replace(invalid, '_');
            }

            return name.Replace(' ', '_');
        }

        // ---- 메시 분해 -----------------------------------------------------------

        private sealed class WeldedMesh
        {
            public Vector3[] Positions;  // 고유 위치
            public int[] VertexToPosition;
        }

        private static WeldedMesh Weld(Vector3[] vertices)
        {
            var map = new Dictionary<Vector3Int, int>();
            var positions = new List<Vector3>();
            var vertexToPosition = new int[vertices.Length];
            for (var i = 0; i < vertices.Length; i++)
            {
                var scaled = vertices[i] * WeldScale;
                var key = new Vector3Int(Mathf.RoundToInt(scaled.x), Mathf.RoundToInt(scaled.y), Mathf.RoundToInt(scaled.z));
                if (!map.TryGetValue(key, out var id))
                {
                    id = positions.Count;
                    map[key] = id;
                    positions.Add(vertices[i]);
                }

                vertexToPosition[i] = id;
            }

            return new WeldedMesh { Positions = positions.ToArray(), VertexToPosition = vertexToPosition };
        }

        /// <summary>삼각형 연결(용접된 위치 기준)로 조각을 나눈다. 각 조각은 고유 위치 인덱스 목록, 큰 것부터.</summary>
        private static List<int[]> SplitComponents(Mesh mesh, out WeldedMesh welded)
        {
            welded = Weld(mesh.vertices);
            var count = welded.Positions.Length;
            var parent = new int[count];
            for (var i = 0; i < count; i++) parent[i] = i;

            int Find(int x)
            {
                while (parent[x] != x)
                {
                    parent[x] = parent[parent[x]];
                    x = parent[x];
                }

                return x;
            }

            void Union(int a, int b)
            {
                a = Find(a);
                b = Find(b);
                if (a != b) parent[b] = a;
            }

            var triangles = mesh.triangles;
            for (var t = 0; t < triangles.Length; t += 3)
            {
                var a = welded.VertexToPosition[triangles[t]];
                var b = welded.VertexToPosition[triangles[t + 1]];
                var c = welded.VertexToPosition[triangles[t + 2]];
                Union(a, b);
                Union(a, c);
            }

            return Enumerable.Range(0, count)
                .GroupBy(Find)
                .Select(g => g.ToArray())
                .OrderByDescending(g => g.Length)
                .ToList();
        }

        /// <summary>못 맞춘 부스러기 조각들의 삼각형만 모아 원본 재질을 단 메시로 남긴다.</summary>
        private static void CreateLeftover(GameObject parentObject, Mesh mesh, MeshRenderer sourceRenderer, WeldedMesh welded,
            List<int[]> unmatched)
        {
            var keep = new HashSet<int>();
            foreach (var component in unmatched)
            foreach (var id in component)
            {
                keep.Add(id);
            }

            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var uv = mesh.uv;
            var remap = new Dictionary<int, int>();
            var newVertices = new List<Vector3>();
            var newNormals = new List<Vector3>();
            var newUv = new List<Vector2>();
            var newSubmeshes = new List<int[]>();

            for (var sub = 0; sub < mesh.subMeshCount; sub++)
            {
                var triangles = mesh.GetTriangles(sub);
                var indices = new List<int>();
                for (var t = 0; t < triangles.Length; t += 3)
                {
                    if (!keep.Contains(welded.VertexToPosition[triangles[t]]))
                    {
                        continue;
                    }

                    for (var k = 0; k < 3; k++)
                    {
                        var original = triangles[t + k];
                        if (!remap.TryGetValue(original, out var mapped))
                        {
                            mapped = newVertices.Count;
                            remap[original] = mapped;
                            newVertices.Add(vertices[original]);
                            if (normals.Length == vertices.Length) newNormals.Add(normals[original]);
                            if (uv.Length == vertices.Length) newUv.Add(uv[original]);
                        }

                        indices.Add(mapped);
                    }
                }

                newSubmeshes.Add(indices.ToArray());
            }

            var leftover = new Mesh { name = mesh.name + "_Leftover" };
            leftover.indexFormat = newVertices.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            leftover.SetVertices(newVertices);
            if (newNormals.Count == newVertices.Count) leftover.SetNormals(newNormals);
            if (newUv.Count == newVertices.Count) leftover.SetUVs(0, newUv);
            leftover.subMeshCount = newSubmeshes.Count;
            for (var sub = 0; sub < newSubmeshes.Count; sub++)
            {
                leftover.SetTriangles(newSubmeshes[sub], sub, false);
            }

            leftover.RecalculateBounds();

            var go = new GameObject("Leftover");
            Undo.RegisterCreatedObjectUndo(go, "Explode Merged Prop");
            go.transform.SetParent(parentObject.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = leftover;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = sourceRenderer.sharedMaterials;
        }
    }
}
