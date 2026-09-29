using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// 로비 환경 루트 주변에 보이지 않는 두꺼운 경계 콜라이더 6면을 생성한다.
    /// 벽 모듈 콜라이더가 있어도 물리 버그(고속 충돌, 끼임 튕김)로 밖으로 나가는 것을 막는 안전망.
    /// 측정 기준은 벽·문·천장 모듈 렌더러만 사용한다 — 바닥 판(Ground)이나 천장 위로 솟은
    /// 램프·장선 메시를 포함하면 경계가 벽에서 수 m 떨어져 버리기 때문.
    /// </summary>
    public static class LobbyBoundsBuilderMenu
    {
        private const string MenuPath = "Game/Lobby/Build Boundary Colliders";
        private const string DefaultRootName = "LobbyBasementEnvironment";
        private const string ModulesName = "BasementModules";
        private const string BoundaryName = "Boundary";

        // 벽 두께(m): 두꺼울수록 고속 관통에 안전
        private const float Thickness = 2.0f;
        // 실측 바운드 바깥으로 띄우는 여유(m)
        private const float Padding = 0.05f;

        // 동서남북 측정 대상(수직 벽·문 모듈) 이름 접두사
        private static readonly string[] WallPrefixes = { "Basement_Wall", "Basement_Door" };
        // 천장 측정 대상 이름 접두사
        private static readonly string[] CeilingPrefixes = { "Basement_Ceiling" };

        [MenuItem(MenuPath)]
        public static void BuildBoundaryColliders()
        {
            var root = Selection.activeGameObject;
            if (root == null)
            {
                root = GameObject.Find(DefaultRootName);
            }
            if (root == null)
            {
                EditorUtility.DisplayDialog(
                    "Lobby Bounds",
                    $"환경 루트를 선택하거나 씬에 '{DefaultRootName}' 오브젝트를 두세요.",
                    "확인");
                return;
            }

            // 기존 경계 제거 (데칼/경계 자신은 측정에서 제외)
            var old = root.transform.Find(BoundaryName);
            if (old != null)
            {
                Undo.DestroyObjectImmediate(old.gameObject);
            }

            if (!TryMeasure(root, out var bounds, out var source))
            {
                Debug.LogWarning("[Lobby] 측정할 렌더러가 없습니다.");
                return;
            }

            var boundary = new GameObject(BoundaryName);
            Undo.RegisterCreatedObjectUndo(boundary, "Build Lobby Boundary");
            boundary.transform.SetParent(root.transform, false);
            boundary.transform.position = bounds.center;
            boundary.isStatic = true;

            var min = bounds.min - Vector3.one * Padding;
            var max = bounds.max + Vector3.one * Padding;
            var size = max - min;
            var center = (min + max) * 0.5f;
            var t = Thickness;

            // 각 면: 바운드 바깥에 두께 t짜리 판. 모서리가 겹치도록 다른 축은 두께만큼만 확장.
            AddWall(boundary, "West",  new Vector3(min.x - t * 0.5f, center.y, center.z), new Vector3(t, size.y + 2 * t, size.z + 2 * t));
            AddWall(boundary, "East",  new Vector3(max.x + t * 0.5f, center.y, center.z), new Vector3(t, size.y + 2 * t, size.z + 2 * t));
            AddWall(boundary, "South", new Vector3(center.x, center.y, min.z - t * 0.5f), new Vector3(size.x + 2 * t, size.y + 2 * t, t));
            AddWall(boundary, "North", new Vector3(center.x, center.y, max.z + t * 0.5f), new Vector3(size.x + 2 * t, size.y + 2 * t, t));
            AddWall(boundary, "Floor", new Vector3(center.x, min.y - t * 0.5f, center.z), new Vector3(size.x + 2 * t, t, size.z + 2 * t));
            AddWall(boundary, "Ceiling", new Vector3(center.x, max.y + t * 0.5f, center.z), new Vector3(size.x + 2 * t, t, size.z + 2 * t));

            Selection.activeGameObject = boundary;
            Debug.Log(
                $"[Lobby] Boundary built around {root.name} ({source}): " +
                $"x[{min.x:F2}, {max.x:F2}] y[{min.y:F2}, {max.y:F2}] z[{min.z:F2}, {max.z:F2}], " +
                $"interior {size.x:F1} x {size.y:F1} x {size.z:F1} m, thickness {t} m.");
        }

        /// <summary>
        /// 1순위: BasementModules 아래 벽·문 모듈로 XZ, 벽·천장 모듈로 Y 상한을 잰다.
        /// 벽 모듈이 없으면 Ground/Decal을 뺀 전체 렌더러로 폴백.
        /// </summary>
        private static bool TryMeasure(GameObject root, out Bounds bounds, out string source)
        {
            bounds = default;
            source = string.Empty;

            var modules = root.transform.Find(ModulesName);
            var scope = modules != null ? modules : root.transform;
            var renderers = scope.GetComponentsInChildren<Renderer>(true)
                .Where(r => r.enabled)
                .ToArray();

            var walls = renderers.Where(r => StartsWithAny(r.name, WallPrefixes)).ToList();
            var ceilings = renderers.Where(r => StartsWithAny(r.name, CeilingPrefixes)).ToList();

            if (walls.Count > 0)
            {
                bounds = Encapsulate(walls);
                if (ceilings.Count > 0)
                {
                    var c = Encapsulate(ceilings);
                    // 천장은 높이(Y 상한)에만 반영. XZ는 벽보다 넓게 깔린 천장판이 있어 제외.
                    var max = bounds.max;
                    max.y = Mathf.Max(max.y, c.max.y);
                    bounds.SetMinMax(bounds.min, max);
                }
                // 바닥 기준은 y=0 아래로 내려가지 않게 보정(벽 메시가 살짝 떠 있는 경우 대비)
                var min = bounds.min;
                min.y = Mathf.Min(min.y, 0f);
                bounds.SetMinMax(min, bounds.max);
                source = $"walls {walls.Count}, ceilings {ceilings.Count}";
                return true;
            }

            var fallback = root.GetComponentsInChildren<Renderer>(true)
                .Where(r => r.enabled && !r.name.Contains("Decal") && !r.name.Contains("Ground"))
                .ToList();
            if (fallback.Count == 0)
            {
                return false;
            }
            bounds = Encapsulate(fallback);
            source = $"fallback renderers {fallback.Count}";
            return true;
        }

        private static bool StartsWithAny(string name, IEnumerable<string> prefixes)
        {
            return prefixes.Any(p => name.StartsWith(p, System.StringComparison.Ordinal));
        }

        private static Bounds Encapsulate(List<Renderer> renderers)
        {
            var b = renderers[0].bounds;
            foreach (var r in renderers)
            {
                b.Encapsulate(r.bounds);
            }
            return b;
        }

        private static void AddWall(GameObject parent, string name, Vector3 worldCenter, Vector3 worldSize)
        {
            var go = new GameObject($"Bound_{name}");
            go.transform.SetParent(parent.transform, false);
            go.transform.position = worldCenter;
            go.isStatic = true;
            var box = go.AddComponent<BoxCollider>();
            box.size = worldSize;
        }
    }
}
