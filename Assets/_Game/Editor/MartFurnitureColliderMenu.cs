using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.Client.Interactions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Game.Editor
{
    /// <summary>
    /// 마트 가구 콜라이더를 실제 모양으로 바꿔 선반 칸 안의 상품에 조준 광선이 닿게 한다.
    /// </summary>
    /// <remarks>
    /// 문제: Synty 가구(진열대·냉장고·진열 스탠드·카트)는 convex 메시 콜라이더라 볼록 껍질이 선반 칸을 통째로 덮고,
    /// 분해 도구가 만든 구조물 조각(계산대 선반·벽 냉장고 본체)은 바운드 크기 BoxCollider였다. 둘 다 안쪽 상품을 가린다.
    /// <list type="bullet">
    /// <item>우리 생성 구조물 프리팹(<c>Gen_*</c>, CarryableItem 없음): BoxCollider → non-convex MeshCollider(자기 메시). 프리팹 에셋을 고친다.</item>
    /// <item>Synty 가구 인스턴스(경계 안, Carryable 아님): MeshCollider의 convex를 끈다(인스턴스 오버라이드, 팩 에셋은 그대로).</item>
    /// <item>움직이지 않는 가구라 non-convex 메시 콜라이더가 허용된다. 벽·바닥 BoxCollider는 그대로 둔다.</item>
    /// <item><b>Reachability Report</b>: 상품마다 네 방향에서 눈높이 광선을 쏘아 몇 개가 닿는지, 무엇이 가리는지 집계한다.</item>
    /// </list>
    /// </remarks>
    public static class MartFurnitureColliderMenu
    {
        private const string MenuRoot = "Game/Match Map/Carryable/";
        private const string GeneratedFolder = "Assets/_Game/Content/MatchMap/GeneratedProps";
        private const string BoundaryName = "Boundary";

        [MenuItem(MenuRoot + "4. Fix Furniture Colliders (Non-Convex)")]
        public static void FixFurnitureColliders()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[MartCollider] Play 모드를 끝낸 뒤 실행하세요.");
                return;
            }

            var scene = SceneManager.GetActiveScene();
            var prefabsFixed = FixGeneratedStructurePrefabs();
            var instancesFixed = FixSceneFurnitureInstances(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[MartCollider] 생성 구조물 프리팹 {prefabsFixed}개 → 메시 콜라이더, 가구 인스턴스 {instancesFixed}개 convex 해제. 씬 저장됨.");
        }

        /// <summary>CarryableItem이 없는 생성 프리팹(구조물)의 BoxCollider를 자기 메시의 non-convex MeshCollider로 바꾼다.</summary>
        public static int FixGeneratedStructurePrefabs()
        {
            var fixedCount = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { GeneratedFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null || asset.GetComponent<CarryableItem>() != null)
                {
                    continue; // 상품은 그대로(들 수 있는 물건은 convex/box여야 한다)
                }

                var filter = asset.GetComponent<MeshFilter>();
                var box = asset.GetComponent<BoxCollider>();
                var existing = asset.GetComponent<MeshCollider>();
                if (filter == null || filter.sharedMesh == null || (box == null && (existing == null || !existing.convex)))
                {
                    continue;
                }

                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (var collider in root.GetComponents<BoxCollider>())
                    {
                        Object.DestroyImmediate(collider);
                    }

                    var mesh = root.GetComponent<MeshFilter>().sharedMesh;
                    var meshCollider = root.GetComponent<MeshCollider>();
                    if (meshCollider == null)
                    {
                        meshCollider = root.AddComponent<MeshCollider>();
                    }

                    meshCollider.sharedMesh = mesh;
                    meshCollider.convex = false;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    fixedCount++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            return fixedCount;
        }

        /// <summary>
        /// 경계 안의 Carryable이 아닌 가구 인스턴스에서 메시 콜라이더를 <b>시각 메시 + non-convex</b>로 바꾼다(오버라이드).
        /// </summary>
        /// <remarks>
        /// Synty 가구 프리팹의 MeshCollider는 시각 메시가 아니라 <c>Models/Collision/Convex/*_Convex.asset</c>(닫힌 껍질)을 쓴다.
        /// convex만 꺼도 앞면이 막힌 채라 선반 칸·냉동고 안의 상품에 광선이 닿지 않는다. 같은 오브젝트의 MeshFilter 메시
        /// (Read/Write 켜 둠)를 콜라이더에 쓰면 실제 열린 모양대로 막힌다.
        /// </remarks>
        public static int FixSceneFurnitureInstances(Scene scene)
        {
            var hasBoundary = TryGetBoundary(scene, out var boundary);
            var changed = 0;
            foreach (var collider in Object.FindObjectsByType<MeshCollider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (collider.gameObject.scene != scene)
                {
                    continue;
                }

                if (collider.GetComponentInParent<CarryableItem>() != null || collider.GetComponentInParent<Rigidbody>() != null)
                {
                    continue; // 들 수 있는 물건·움직이는 것은 convex 유지
                }

                if (hasBoundary && !Inside(boundary, collider.bounds.center))
                {
                    continue;
                }

                var filter = collider.GetComponent<MeshFilter>();
                var visualMesh = filter != null ? filter.sharedMesh : null;
                var needsMesh = visualMesh != null && collider.sharedMesh != visualMesh;
                if (!collider.convex && !needsMesh)
                {
                    continue;
                }

                Undo.RecordObject(collider, "Fix Furniture Collider");
                if (needsMesh)
                {
                    collider.sharedMesh = visualMesh;
                }

                collider.convex = false;
                changed++;
            }

            changed += ReplaceEnclosingBoxColliders(scene, hasBoundary, boundary);
            return changed;
        }

        /// <summary>
        /// 들 수 있는 물건을 상자 안에 품고 있는 고정 가구의 BoxCollider(예: 저택 비밀 책장문 <c>SM_Bld_Bookshelf_Door_01</c> —
        /// 책장 전체를 덮는 상자 하나)를 시각 메시 non-convex MeshCollider로 바꾼다. 상자가 그대로면 안의 책은 조준 광선에
        /// 절대 닿지 않는다(2026-09-17). 물건을 품지 않은 상자(벽·문짝)는 그대로 둔다.
        /// </summary>
        private static int ReplaceEnclosingBoxColliders(Scene scene, bool hasBoundary, Bounds boundary)
        {
            var items = Object.FindObjectsByType<CarryableItem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(item => item.gameObject.scene == scene)
                .Select(item => item.GetComponentInChildren<Renderer>())
                .Where(renderer => renderer != null)
                .Select(renderer => renderer.bounds.center)
                .ToArray();
            var changed = 0;
            foreach (var box in Object.FindObjectsByType<BoxCollider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (box.gameObject.scene != scene || box.isTrigger)
                {
                    continue;
                }

                if (box.GetComponentInParent<CarryableItem>() != null || box.GetComponentInParent<Rigidbody>() != null)
                {
                    continue;
                }

                if (hasBoundary && !Inside(boundary, box.bounds.center))
                {
                    continue;
                }

                var filter = box.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                {
                    continue;
                }

                var bounds = box.bounds;
                var enclosed = items.Count(center => bounds.Contains(center));
                if (enclosed == 0)
                {
                    continue;
                }

                Undo.RecordObject(box, "Fix Furniture Collider");
                box.enabled = false;
                var mesh = Undo.AddComponent<MeshCollider>(box.gameObject);
                mesh.sharedMesh = filter.sharedMesh;
                mesh.convex = false;
                Debug.Log($"[MartCollider] '{box.gameObject.name}' BoxCollider가 물건 {enclosed}개를 감싸고 있어 시각 메시 콜라이더로 바꿈.");
                changed++;
            }

            return changed;
        }

        // ------------------------------------------------------------------ 도달 보고

        [MenuItem(MenuRoot + "5. Reachability Report")]
        public static void ReachabilityReport()
        {
            var scene = SceneManager.GetActiveScene();
            Physics.SyncTransforms();
            var report = BuildReachability(scene, out var reachable, out var blocked);
            Debug.Log($"[MartCollider] 조준 도달 검사: 닿음 {reachable}개 / 막힘 {blocked}개\n{report}");
        }

        /// <summary>
        /// 상품마다 네 방향 × 자세(서서 1.6 m·앉아서 1.0 m) × 거리(1.2 m·0.7 m)에서 광선을 쏘아, 첫 충돌이 그 상품인지 본다.
        /// 낮은 책장 칸의 책은 서서 멀리서는 위 선반 판에 가리지만 앉아서 가까이 가면 닿는다(저택 2026-09-17).
        /// </summary>
        public static string BuildReachability(Scene scene, out int reachable, out int blocked)
        {
            reachable = 0;
            blocked = 0;
            var blockers = new Dictionary<string, int>(StringComparer.Ordinal);
            var noHit = new Dictionary<string, int>(StringComparer.Ordinal);
            var noHitSample = new Dictionary<string, string>(StringComparer.Ordinal);
            var hits = new RaycastHit[16];
            var directions = new[] { Vector3.left, Vector3.right, Vector3.back, Vector3.forward };
            var eyeHeights = new[] { 1.6f, 1.0f };
            var distances = new[] { 1.2f, 0.7f };
            foreach (var item in Object.FindObjectsByType<CarryableItem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (item.gameObject.scene != scene)
                {
                    continue;
                }

                var renderer = item.GetComponentInChildren<Renderer>();
                if (renderer == null)
                {
                    continue;
                }

                var target = renderer.bounds.center;
                // 층별 바닥 기준으로 눈높이를 잡는다. 절대 y로 잡으면 2층·다락 물건(저택 y 4~9 m)은 광선이 닿지 않는다.
                var floorY = FindFloorY(target, item, hits);
                var seen = false;
                string firstBlocker = null;
                foreach (var direction in directions)
                {
                    foreach (var eyeHeight in eyeHeights)
                    {
                        foreach (var distance in distances)
                        {
                            var eye = target + (direction * distance);
                            eye.y = floorY + eyeHeight;
                            var count = Physics.RaycastNonAlloc(new Ray(eye, (target - eye).normalized), hits, 2.5f,
                                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                            if (count == 0)
                            {
                                continue;
                            }

                            var first = hits.Take(count).OrderBy(h => h.distance).First();
                            if (first.collider.GetComponentInParent<CarryableItem>() == item)
                            {
                                seen = true;
                                break;
                            }

                            firstBlocker ??= System.Text.RegularExpressions.Regex.Replace(first.collider.gameObject.name, @"\s*\(\d+\)|\s\d+$", string.Empty);
                        }

                        if (seen)
                        {
                            break;
                        }
                    }

                    if (seen)
                    {
                        break;
                    }
                }

                if (seen)
                {
                    reachable++;
                }
                else
                {
                    blocked++;
                    if (firstBlocker != null)
                    {
                        blockers[firstBlocker] = blockers.GetValueOrDefault(firstBlocker) + 1;
                    }
                    else
                    {
                        // 어느 방향에서도 아무것도 맞지 않은 것: 물건 콜라이더가 없거나 비활성, 또는 바닥 기준이 어긋난 경우.
                        var key = System.Text.RegularExpressions.Regex.Replace(item.name, @"\s*\(\d+\)|\s\d+$", string.Empty);
                        noHit[key] = noHit.GetValueOrDefault(key) + 1;
                        noHitSample.TryAdd(key, $"{item.transform.position} floorY={floorY:F2} colliders={item.GetComponentsInChildren<Collider>(true).Length}");
                    }
                }
            }

            var sb = new StringBuilder();
            foreach (var pair in blockers.OrderByDescending(p => p.Value).Take(15))
            {
                sb.AppendLine($"  막는 콜라이더 {pair.Key} x{pair.Value}");
            }

            foreach (var pair in noHit.OrderByDescending(p => p.Value).Take(15))
            {
                sb.AppendLine($"  광선 무반응 {pair.Key} x{pair.Value}  예: {noHitSample[pair.Key]}");
            }

            return sb.ToString();
        }

        /// <summary>물건 바로 아래의 바닥 높이(자기 콜라이더 제외, 최대 6 m). 못 찾으면 물건 아래 0.5 m를 바닥으로 본다.</summary>
        private static float FindFloorY(Vector3 target, CarryableItem item, RaycastHit[] hits)
        {
            var count = Physics.RaycastNonAlloc(new Ray(target, Vector3.down), hits, 6f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            var floorY = float.NaN;
            for (var i = 0; i < count; i++)
            {
                if (hits[i].collider.GetComponentInParent<CarryableItem>() == item)
                {
                    continue;
                }

                if (float.IsNaN(floorY) || hits[i].point.y > floorY)
                {
                    floorY = hits[i].point.y;
                }
            }

            return float.IsNaN(floorY) ? target.y - 0.5f : floorY;
        }

        // ------------------------------------------------------------------ 공용

        private static bool TryGetBoundary(Scene scene, out Bounds boundary)
        {
            boundary = default;
            var found = false;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var box in root.GetComponentsInChildren<BoxCollider>(true))
                {
                    if (box.transform.parent == null || box.transform.parent.name != BoundaryName)
                    {
                        continue;
                    }

                    if (!found)
                    {
                        boundary = box.bounds;
                        found = true;
                    }
                    else
                    {
                        boundary.Encapsulate(box.bounds);
                    }
                }
            }

            return found;
        }

        private static bool Inside(Bounds boundary, Vector3 point)
        {
            return point.x > boundary.min.x && point.x < boundary.max.x &&
                   point.z > boundary.min.z && point.z < boundary.max.z &&
                   point.y < boundary.max.y;
        }
    }
}
