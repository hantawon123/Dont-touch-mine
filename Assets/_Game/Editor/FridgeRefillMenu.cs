using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// 냉장고·냉동고 안의 "그림 상품"(상품이 그려진 납작한 판·상자)을 치우고, 팩의 개별 상품 프리팹으로 선반을 다시 채운다.
    /// Synty 냉동고(Freezer_03)는 문 하나마다 선반 5칸 상품이 8 cm 두께 판 한 장에 그려져 있고,
    /// 벽 냉장고(Wall_Fridge) 프리셋은 3 cm 두께 납작 상자가 많아 플레이어가 집을 만한 물건이 아니다.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>단위(냉장고 한 대)는 본체 메시로 잡는다: 메시 이름에 Fridge/Freezer, 높이 0.85 m 이상, 가로·세로 0.3 m 이상.</item>
    /// <item>정면은 본체 메시에서 정점이 적은 쪽(뚫린 면). 선반은 본체·구조 조각의 위를 보는 면을 높이별로 묶어 찾는다.</item>
    /// <item>치운 것은 삭제하지 않고 비활성화 + 이름 뒤에 <c>[RefillHidden]</c>을 붙인다. Revert 메뉴가 되돌린다.</item>
    /// <item>새 상품은 <c>…_Exploded/Refill</c>(없으면 <c>&lt;본체&gt;_Refill</c> 루트) 아래 프리팹 인스턴스로 놓는다.</item>
    /// <item>남아 있는 3D 상품·칸막이·문은 장애물로 보고 겹치는 자리는 비운다.</item>
    /// </list>
    /// </remarks>
    public sealed class FridgeRefillMenu : EditorWindow
    {
        private const string HiddenSuffix = " [RefillHidden]";
        private const string RefillGroupName = "Refill";
        private const string PrefabRoot = "Assets/Synty/PolygonShops/Prefabs/";

        public enum PaletteKind
        {
            Auto,
            Freezer,
            Fridge,
            Drinks,
        }

        [Serializable]
        public sealed class Options
        {
            /// <summary>true면 안에 있던 생성 상품(3D 포함)을 전부 치우고 새로 채운다. false면 납작한 그림 상품만.</summary>
            public bool replaceAllProducts;

            /// <summary>선반 한 칸에 앞에서부터 몇 줄까지 채울지. 줄이 늘수록 오브젝트 수가 늘어난다.</summary>
            public int maxRows = 2;

            public PaletteKind palette = PaletteKind.Auto;

            /// <summary>같은 냉장고를 다시 채울 때 배치를 바꾸고 싶으면 값을 바꾼다.</summary>
            public int seedOffset;

            /// <summary>1이면 빈틈 없이, 0.8이면 자리 20%를 비워 자연스럽게.</summary>
            [Range(0.5f, 1f)] public float density = 1f;
        }

        private static readonly string[] FreezerPalette =
        {
            // 냉동식품 상자(22×25×7 cm, 세워서)
            "Products/SM_Prop_Product_09", "Products/SM_Prop_Product_10", "Products/SM_Prop_Product_11",
            "Products/SM_Prop_Product_12", "Products/SM_Prop_Product_13", "Products/SM_Prop_Product_14",
            "Products/SM_Prop_Product_31", "Products/SM_Prop_Product_32", "Products/SM_Prop_Product_33",
            "Products/SM_Prop_Product_34", "Products/SM_Prop_Product_35", "Products/SM_Prop_Product_36",
            // 아이스크림 통 느낌의 작은 통(15×17×13 cm)
            "Products/SM_Prop_Product_06", "Products/SM_Prop_Product_17", "Products/SM_Prop_Product_18",
            "Products/SM_Prop_Product_19", "Products/SM_Prop_Product_39", "Products/SM_Prop_Product_40",
            // 도시락 트레이(Food_Takeaway_Container_01)는 빈 쟁반처럼 보여 제외
        };

        private static readonly string[] FridgePalette =
        {
            // 우유·주스(카톤/저그)
            "Food/SM_Prop_Product_Milk_01", "Products/SM_Prop_Product_01", "Products/SM_Prop_Product_23",
            "Products/SM_Prop_Product_02", "Products/SM_Prop_Product_03", "Products/SM_Prop_Product_24", "Products/SM_Prop_Product_25",
            // 소스·드레싱 병
            "Products/SM_Prop_Product_04", "Products/SM_Prop_Product_05", "Products/SM_Prop_Product_26", "Products/SM_Prop_Product_27",
            // 요거트·버터 통
            "Products/SM_Prop_Product_06", "Products/SM_Prop_Product_17", "Products/SM_Prop_Product_18", "Products/SM_Prop_Product_19",
            "Products/SM_Prop_Product_28", "Products/SM_Prop_Product_39", "Products/SM_Prop_Product_40", "Products/SM_Prop_Product_41",
            "Products/SM_Prop_Product_47", "Products/SM_Prop_Product_48",
            // 버터 상자·큰 병·샌드위치(치즈 슬라이스 더미는 사용자 요청으로 제외)
            "Products/SM_Prop_Product_09", "Products/SM_Prop_Product_12", "Products/SM_Prop_Product_13", "Products/SM_Prop_Product_33",
            "Products/SM_Prop_Product_45", "Products/SM_Prop_Product_46", "Food/SM_Prop_Food_Sandwich_01",
            // 캔
            "Products/SM_Prop_Product_21", "Products/SM_Prop_Product_22", "Products/SM_Prop_Product_43", "Products/SM_Prop_Product_44",
        };

        private static readonly string[] DrinksPalette =
        {
            "Products/SM_Prop_Product_02", "Products/SM_Prop_Product_03", "Products/SM_Prop_Product_24", "Products/SM_Prop_Product_25",
            "Products/SM_Prop_Product_04", "Products/SM_Prop_Product_05", "Products/SM_Prop_Product_26", "Products/SM_Prop_Product_27",
            "Products/SM_Prop_Product_21", "Products/SM_Prop_Product_22", "Products/SM_Prop_Product_43", "Products/SM_Prop_Product_44",
            "Food/SM_Prop_Product_Milk_01", "Products/SM_Prop_Product_01", "Products/SM_Prop_Product_23",
        };

        private Options options = new();
        private Renderer[] targets = Array.Empty<Renderer>();

        [MenuItem("Game/Match Map/Refill Fridge (Selected)...")]
        public static void Open()
        {
            var units = ResolveUnits(Selection.gameObjects);
            if (units.Length == 0)
            {
                Debug.LogWarning("[FridgeRefill] 냉장고·냉동고(또는 그 안의 오브젝트)를 선택하세요.");
                return;
            }

            var window = GetWindow<FridgeRefillMenu>(true, "냉장고 채우기", true);
            window.targets = units;
            window.minSize = new Vector2(360f, 240f);
            window.Show();
        }

        [MenuItem("Game/Match Map/Revert Fridge Refill (Selected)")]
        public static void RevertSelected()
        {
            var units = ResolveUnits(Selection.gameObjects);
            var reverted = 0;
            foreach (var unit in units)
            {
                reverted += Revert(unit);
            }

            Debug.Log($"[FridgeRefill] 되돌림: 냉장고 {units.Length}대, 복구 {reverted}개");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField($"대상 냉장고: {targets.Length}대", EditorStyles.boldLabel);
            foreach (var t in targets.Take(6))
            {
                EditorGUILayout.LabelField("  " + t.name);
            }

            if (targets.Length > 6)
            {
                EditorGUILayout.LabelField($"  … 외 {targets.Length - 6}대");
            }

            EditorGUILayout.Space();
            options.replaceAllProducts = EditorGUILayout.ToggleLeft("안의 생성 상품을 전부 교체(끄면 납작한 그림 상품만)", options.replaceAllProducts);
            options.maxRows = EditorGUILayout.IntSlider("선반당 줄 수", options.maxRows, 1, 4);
            options.palette = (PaletteKind)EditorGUILayout.EnumPopup("상품 팔레트", options.palette);
            options.density = EditorGUILayout.Slider("채움 밀도", options.density, 0.5f, 1f);
            options.seedOffset = EditorGUILayout.IntField("배치 시드", options.seedOffset);

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("채우기"))
                {
                    var placed = 0;
                    var removed = 0;
                    foreach (var unit in targets)
                    {
                        var result = Refill(unit, options);
                        placed += result.placed;
                        removed += result.removed;
                    }

                    Debug.Log($"[FridgeRefill] 완료: 냉장고 {targets.Length}대, 치움 {removed}개, 배치 {placed}개");
                }

                if (GUILayout.Button("되돌리기"))
                {
                    var reverted = 0;
                    foreach (var unit in targets)
                    {
                        reverted += Revert(unit);
                    }

                    Debug.Log($"[FridgeRefill] 되돌림: 복구 {reverted}개");
                }
            }
        }

        // ------------------------------------------------------------------ 단위 찾기

        /// <summary>선택(또는 그 자손)에서 냉장고 본체 렌더러를 찾는다. 안의 작은 조각만 선택했다면 그것을 감싸는 본체를 찾는다.</summary>
        public static Renderer[] ResolveUnits(IEnumerable<GameObject> selection)
        {
            var found = new List<Renderer>();
            Renderer[] allBodies = null;
            foreach (var go in selection)
            {
                if (go == null)
                {
                    continue;
                }

                var bodies = go.GetComponentsInChildren<Renderer>(true).Where(IsUnitBody).ToArray();
                if (bodies.Length > 0)
                {
                    found.AddRange(bodies);
                    continue;
                }

                allBodies ??= UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .Where(IsUnitBody).ToArray();
                var position = go.transform.position;
                var container = allBodies.FirstOrDefault(b => Expanded(b.bounds, 0.05f).Contains(position));
                if (container != null)
                {
                    found.Add(container);
                }
            }

            return found.Distinct().ToArray();
        }

        public static bool IsUnitBody(Renderer renderer)
        {
            if (renderer == null || !renderer.gameObject.activeInHierarchy)
            {
                return false;
            }

            var filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
            {
                return false;
            }

            var name = filter.sharedMesh.name;
            if (!(name.Contains("Fridge") || name.Contains("Freezer")) || name.Contains("Door") || name.Contains("Glass"))
            {
                return false;
            }

            var size = renderer.bounds.size;
            var horizontalMin = Mathf.Min(size.x, size.z);
            var horizontalMax = Mathf.Max(size.x, size.z);
            return size.y > 0.85f && horizontalMin > 0.3f && horizontalMax > 0.5f;
        }

        private static Bounds Expanded(Bounds bounds, float amount)
        {
            bounds.Expand(amount);
            return bounds;
        }

        // ------------------------------------------------------------------ 좌표계·선반

        /// <summary>냉장고 한 대의 좌표계: U=가로(폭) 방향, W=정면(손님) 방향, 높이는 월드 Y.</summary>
        private sealed class Frame
        {
            public Vector3 Origin;
            public Vector3 U;
            public Vector3 W;
            public float UMin, UMax, WMin, WMax, YMin, YMax;

            public float ProjectU(Vector3 world) => Vector3.Dot(world - Origin, U);
            public float ProjectW(Vector3 world) => Vector3.Dot(world - Origin, W);
            public Vector3 ToWorld(float u, float y, float w) => Origin + (U * u) + (W * w) + (Vector3.up * (y - Origin.y));

            /// <summary>월드 AABB를 이 좌표계의 U/W/Y 범위로 옮긴다(회전이 90° 단위가 아니면 넉넉하게 감싼다).</summary>
            public void Project(Bounds bounds, out float uMin, out float uMax, out float wMin, out float wMax)
            {
                uMin = wMin = float.MaxValue;
                uMax = wMax = float.MinValue;
                for (var i = 0; i < 8; i++)
                {
                    var corner = new Vector3(
                        (i & 1) == 0 ? bounds.min.x : bounds.max.x,
                        (i & 2) == 0 ? bounds.min.y : bounds.max.y,
                        (i & 4) == 0 ? bounds.min.z : bounds.max.z);
                    var u = ProjectU(corner);
                    var w = ProjectW(corner);
                    uMin = Mathf.Min(uMin, u);
                    uMax = Mathf.Max(uMax, u);
                    wMin = Mathf.Min(wMin, w);
                    wMax = Mathf.Max(wMax, w);
                }
            }
        }

        private sealed class Shelf
        {
            public float Y;
            public float UMin, UMax, WMin, WMax;
            public float Clearance;
            public float Area;
        }

        private sealed class Face
        {
            public float Y;
            public float UMin, UMax, WMin, WMax;
            public float Area;
        }

        private static Frame BuildFrame(Renderer body)
        {
            var mesh = body.GetComponent<MeshFilter>().sharedMesh;
            var local = mesh.bounds;
            var transform = body.transform;

            // 폭 축 = 로컬 x/z 중 더 긴 쪽, 깊이 축 = 나머지. 정면 = 깊이 축 양 끝 면 중 정점이 적은(뚫린) 쪽.
            var widthIsX = local.size.x >= local.size.z;
            var depthAxisLocal = widthIsX ? Vector3.forward : Vector3.right;
            var vertices = mesh.vertices;
            var depthMin = widthIsX ? local.min.z : local.min.x;
            var depthMax = widthIsX ? local.max.z : local.max.x;
            var nearMin = 0;
            var nearMax = 0;
            foreach (var v in vertices)
            {
                var d = widthIsX ? v.z : v.x;
                if (Mathf.Abs(d - depthMin) < 0.05f) nearMin++;
                if (Mathf.Abs(d - depthMax) < 0.05f) nearMax++;
            }

            var frontLocal = nearMax <= nearMin ? depthAxisLocal : -depthAxisLocal;
            var frame = new Frame
            {
                Origin = new Vector3(body.bounds.center.x, body.bounds.min.y, body.bounds.center.z),
                W = Flatten(transform.TransformDirection(frontLocal)),
            };
            frame.U = Vector3.Cross(Vector3.up, frame.W).normalized;
            if (frame.U.sqrMagnitude < 0.5f)
            {
                frame.U = Vector3.right;
            }

            frame.Project(body.bounds, out frame.UMin, out frame.UMax, out frame.WMin, out frame.WMax);
            frame.YMin = body.bounds.min.y;
            frame.YMax = body.bounds.max.y;
            return frame;
        }

        private static Vector3 Flatten(Vector3 direction)
        {
            direction.y = 0f;
            return direction.sqrMagnitude < 1e-6f ? Vector3.forward : direction.normalized;
        }

        /// <summary>본체와 구조 조각(선반·칸막이)의 위를 보는 면을 높이별·가로 구간별로 묶어 선반으로 만든다.</summary>
        private static List<Shelf> DetectShelves(Renderer body, Frame frame, IReadOnlyList<Renderer> structure)
        {
            var upFaces = new List<Face>();
            var downFaces = new List<Face>();
            CollectFaces(body, frame, upFaces, downFaces);
            foreach (var piece in structure)
            {
                CollectFaces(piece, frame, upFaces, downFaces);
            }

            var shelves = new List<Shelf>();
            foreach (var level in upFaces.GroupBy(f => Mathf.RoundToInt(f.Y * 100f)).OrderBy(g => g.Key))
            {
                var y = level.Key / 100f;
                if (y > frame.YMax - 0.05f)
                {
                    continue; // 천장 윗면
                }

                // 가로 방향으로 이어진 면끼리 묶는다(칸막이로 나뉜 칸은 따로).
                var sorted = level.OrderBy(f => f.UMin).ToList();
                var cluster = new List<Face>();
                foreach (var face in sorted)
                {
                    if (cluster.Count > 0 && face.UMin > cluster.Max(c => c.UMax) + 0.08f)
                    {
                        AddShelf(shelves, cluster, y);
                        cluster = new List<Face>();
                    }

                    cluster.Add(face);
                }

                if (cluster.Count > 0)
                {
                    AddShelf(shelves, cluster, y);
                }
            }

            // 위쪽 여유: 그 선반 위에 있는 첫 아래보기 면(선반 밑면·천장)까지.
            foreach (var shelf in shelves)
            {
                var ceiling = frame.YMax - 0.06f;
                foreach (var face in downFaces)
                {
                    if (face.Y <= shelf.Y + 0.05f || face.Y >= ceiling)
                    {
                        continue;
                    }

                    var overlapsU = face.UMax > shelf.UMin + 0.02f && face.UMin < shelf.UMax - 0.02f;
                    var overlapsW = face.WMax > shelf.WMin + 0.02f && face.WMin < shelf.WMax - 0.02f;
                    if (overlapsU && overlapsW)
                    {
                        ceiling = face.Y;
                    }
                }

                shelf.Clearance = ceiling - shelf.Y;
            }

            return shelves.Where(s => s.Clearance > 0.08f).OrderBy(s => s.Y).ThenBy(s => s.UMin).ToList();
        }

        private static void AddShelf(List<Shelf> shelves, List<Face> cluster, float y)
        {
            var shelf = new Shelf
            {
                Y = y,
                UMin = cluster.Min(c => c.UMin),
                UMax = cluster.Max(c => c.UMax),
                WMin = cluster.Min(c => c.WMin),
                WMax = cluster.Max(c => c.WMax),
                Area = cluster.Sum(c => c.Area),
            };
            // 너무 작거나(라벨 홈), 앞뒤 깊이가 얕은 것(테두리 립)은 선반이 아니다.
            if (shelf.Area >= 0.03f && shelf.UMax - shelf.UMin >= 0.15f && shelf.WMax - shelf.WMin >= 0.1f)
            {
                shelves.Add(shelf);
            }
        }

        private static void CollectFaces(Renderer renderer, Frame frame, List<Face> upFaces, List<Face> downFaces)
        {
            var filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null || !filter.sharedMesh.isReadable)
            {
                return;
            }

            var mesh = filter.sharedMesh;
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            var matrix = renderer.transform.localToWorldMatrix;
            for (var i = 0; i + 2 < triangles.Length; i += 3)
            {
                var a = matrix.MultiplyPoint3x4(vertices[triangles[i]]);
                var b = matrix.MultiplyPoint3x4(vertices[triangles[i + 1]]);
                var c = matrix.MultiplyPoint3x4(vertices[triangles[i + 2]]);
                var normal = Vector3.Cross(b - a, c - a);
                var area = normal.magnitude * 0.5f;
                if (area < 1e-4f)
                {
                    continue;
                }

                normal /= area * 2f;
                if (Mathf.Abs(normal.y) < 0.95f)
                {
                    continue;
                }

                var face = new Face
                {
                    Y = (a.y + b.y + c.y) / 3f,
                    Area = area,
                    UMin = Mathf.Min(frame.ProjectU(a), frame.ProjectU(b), frame.ProjectU(c)),
                    UMax = Mathf.Max(frame.ProjectU(a), frame.ProjectU(b), frame.ProjectU(c)),
                    WMin = Mathf.Min(frame.ProjectW(a), frame.ProjectW(b), frame.ProjectW(c)),
                    WMax = Mathf.Max(frame.ProjectW(a), frame.ProjectW(b), frame.ProjectW(c)),
                };
                (normal.y > 0f ? upFaces : downFaces).Add(face);
            }
        }

        // ------------------------------------------------------------------ 채우기

        public struct Result
        {
            public int removed;
            public int placed;
            public int shelves;
        }

        private sealed class PaletteItem
        {
            public GameObject Prefab;
            public Vector3 LocalSize;
            public Vector3 LocalCenter;
            public float LocalMinY;
        }

        public static Result Refill(Renderer body, Options options)
        {
            var result = new Result();
            if (!IsUnitBody(body))
            {
                Debug.LogWarning($"[FridgeRefill] 본체가 아닙니다: {body.name}");
                return result;
            }

            var frame = BuildFrame(body);
            var unitBounds = Expanded(body.bounds, 0.06f);
            var scene = body.gameObject.scene;
            var explodedRoot = FindExplodedRoot(body.transform);
            // 평대형(위가 열린 낮은 냉동고): 그림이 수평 판으로 떠 있고, 바닥 전체를 채우며, 얇은 상자는 눕힌다.
            var chest = IsChest(body);

            var existingGroup = FindRefillGroup(body, explodedRoot);
            if (existingGroup != null && existingGroup.childCount > 0)
            {
                Debug.LogWarning($"[FridgeRefill] 이미 채워진 냉장고입니다(먼저 되돌리기): {body.name}");
                return result;
            }

            // 안에 있는 것들 분류
            var inside = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(r => r != body && r.GetComponent<MeshFilter>() != null && unitBounds.Contains(r.bounds.center))
                .ToList();
            var removal = new List<Renderer>();
            var structure = new List<Renderer>();
            foreach (var renderer in inside)
            {
                if (IsPictureProduct(renderer, frame, options.replaceAllProducts, chest))
                {
                    removal.Add(renderer);
                }
                else if (IsStructurePiece(renderer))
                {
                    structure.Add(renderer);
                }
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Refill Fridge");
            foreach (var renderer in removal)
            {
                var go = renderer.gameObject;
                Undo.RecordObject(go, "Refill Fridge");
                if (!go.name.EndsWith(HiddenSuffix))
                {
                    go.name += HiddenSuffix;
                }

                go.SetActive(false);
                result.removed++;
            }

            // 가느다란 막대(칸막이 틀·손잡이)는 장애물이지만 선반·천장은 아니다.
            var surfacePieces = structure.Where(r => Mathf.Min(r.bounds.size.x, r.bounds.size.z) >= 0.12f).ToList();
            var shelves = DetectShelves(body, frame, surfacePieces);
            result.shelves = shelves.Count;
            if (shelves.Count == 0)
            {
                Debug.LogWarning($"[FridgeRefill] 선반을 찾지 못했습니다: {body.name}");
                return result;
            }

            var palette = LoadPalette(ResolvePalette(body, options.palette));
            if (palette.Count == 0)
            {
                Debug.LogWarning("[FridgeRefill] 팔레트 프리팹을 하나도 찾지 못했습니다.");
                return result;
            }

            // 장애물 = 안에 남아 있는 모든 렌더러(칸막이·문·남긴 3D 상품). 본체 자체는 선반 사각형이 이미 안쪽이라 제외.
            var obstacles = inside.Where(r => r.gameObject.activeInHierarchy).Select(r => r.bounds).ToList();
            var group = GetOrCreateRefillGroup(body, explodedRoot);
            var seed = Mathf.RoundToInt(body.bounds.center.x * 73.1f + body.bounds.center.z * 31.7f) + options.seedOffset;
            var random = new System.Random(seed);
            var yaw = Quaternion.LookRotation(frame.W, Vector3.up);
            // 평대형에서는 얇은 상자(깊이가 높이의 절반 미만)를 눕혀 라벨이 위를 보게 한다.
            var oriented = palette
                .Select(p => Orient(p, yaw, frame, chest && p.LocalSize.z < p.LocalSize.y * 0.5f))
                .ToList();
            var maxRows = chest ? 99 : Mathf.Max(1, options.maxRows);

            const float sideMargin = 0.02f;
            const float frontMargin = 0.02f;
            const float gap = 0.015f;
            const float lift = 0.005f;

            foreach (var shelf in shelves)
            {
                var fits = oriented.Where(p => p.Height <= shelf.Clearance - 0.02f && p.WExtent <= shelf.WMax - shelf.WMin - 0.04f).ToList();
                if (fits.Count == 0)
                {
                    continue;
                }

                var rowPitch = fits.Max(p => p.WExtent) + 0.02f;
                for (var row = 0; row < maxRows; row++)
                {
                    var frontLine = shelf.WMax - frontMargin - (row * rowPitch);
                    if (frontLine - fits.Min(p => p.WExtent) < shelf.WMin + 0.02f)
                    {
                        break;
                    }

                    var rowFits = fits.Where(p => frontLine - p.WExtent >= shelf.WMin + 0.02f).ToList();
                    if (rowFits.Count == 0)
                    {
                        break;
                    }

                    var cursor = shelf.UMin + sideMargin;
                    OrientedItem current = null;
                    var runLeft = 0;
                    var guard = 0;
                    while (cursor < shelf.UMax - sideMargin && guard++ < 400)
                    {
                        if (runLeft <= 0 || current == null)
                        {
                            current = rowFits[random.Next(rowFits.Count)];
                            runLeft = random.Next(2, 6);
                        }

                        var remaining = shelf.UMax - sideMargin - cursor;
                        if (current.UExtent > remaining)
                        {
                            var alternative = rowFits.Where(p => p.UExtent <= remaining).OrderByDescending(p => p.UExtent).FirstOrDefault();
                            if (alternative == null)
                            {
                                break;
                            }

                            current = alternative;
                            runLeft = 1;
                        }

                        var u = cursor + (current.UExtent * 0.5f);
                        var w = frontLine - (current.WExtent * 0.5f);
                        var floorPoint = frame.ToWorld(u, shelf.Y + lift, w);
                        var center = floorPoint + (Vector3.up * (current.Height * 0.5f));
                        var candidate = new Bounds(center, current.WorldSize - (Vector3.one * 0.01f));
                        if (obstacles.Any(o => o.Intersects(candidate)))
                        {
                            cursor += 0.05f;
                            runLeft--;
                            continue;
                        }

                        if (random.NextDouble() <= options.density)
                        {
                            var instance = (GameObject)PrefabUtility.InstantiatePrefab(current.Item.Prefab, scene);
                            Undo.RegisterCreatedObjectUndo(instance, "Refill Fridge");
                            instance.transform.SetParent(group, true);
                            instance.transform.rotation = current.Rotation;
                            // 회전된 바운드의 바닥 중앙이 선반 위 목표점에 오도록 피벗을 놓는다.
                            instance.transform.position = floorPoint - new Vector3(current.AabbCenter.x, current.AabbMin.y, current.AabbCenter.z);
                            obstacles.Add(new Bounds(center, current.WorldSize));
                            result.placed++;
                        }

                        cursor += current.UExtent + gap;
                        runLeft--;
                    }
                }
            }

            EditorUtility.SetDirty(group.gameObject);
            Debug.Log($"[FridgeRefill] {body.name}: 선반 {result.shelves}칸, 치움 {result.removed}개, 배치 {result.placed}개 (정면 {frame.W})");
            return result;
        }

        /// <summary>위가 열린 낮은 평대형(체스트) 냉동고인지. 높이 1.3 m 미만.</summary>
        public static bool IsChest(Renderer body) => body.bounds.size.y < 1.3f;

        /// <summary>팔레트 상품을 실제 놓을 회전으로 돌렸을 때의 월드 AABB(피벗 기준)와 냉장고 좌표계 폭·깊이.</summary>
        private sealed class OrientedItem
        {
            public PaletteItem Item;
            public Quaternion Rotation;
            public Vector3 WorldSize;
            public Vector3 AabbCenter;
            public Vector3 AabbMin;
            public float UExtent;
            public float WExtent;
            public float Height;
        }

        private static OrientedItem Orient(PaletteItem item, Quaternion yaw, Frame frame, bool layFlat)
        {
            // 눕히기: 로컬 +Z(라벨 면)가 위를 보게 X축으로 -90° 돌린 뒤 정면 방향으로 yaw.
            var rotation = layFlat ? yaw * Quaternion.Euler(-90f, 0f, 0f) : yaw;
            var half = item.LocalSize * 0.5f;
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            for (var i = 0; i < 8; i++)
            {
                var corner = item.LocalCenter + new Vector3(
                    (i & 1) == 0 ? -half.x : half.x,
                    (i & 2) == 0 ? -half.y : half.y,
                    (i & 4) == 0 ? -half.z : half.z);
                var rotated = rotation * corner;
                min = Vector3.Min(min, rotated);
                max = Vector3.Max(max, rotated);
            }

            var size = max - min;
            return new OrientedItem
            {
                Item = item,
                Rotation = rotation,
                WorldSize = size,
                AabbCenter = (min + max) * 0.5f,
                AabbMin = min,
                UExtent = Mathf.Abs(size.x * frame.U.x) + Mathf.Abs(size.z * frame.U.z),
                WExtent = Mathf.Abs(size.x * frame.W.x) + Mathf.Abs(size.z * frame.W.z),
                Height = size.y,
            };
        }

        /// <summary>상품이 그려진 납작한 판·상자인지. replaceAll이면 Products 그룹의 생성 상품 전부.</summary>
        private static bool IsPictureProduct(Renderer renderer, Frame frame, bool replaceAll, bool chest)
        {
            var go = renderer.gameObject;
            var name = go.name;
            if (name.Contains("Door") || name.Contains("Glass"))
            {
                return false;
            }

            var underExploded = FindExplodedRoot(renderer.transform) != null;
            var generated = name.StartsWith("Gen_");
            if (!underExploded && !generated)
            {
                return false; // 팩 프리팹 그대로인 진짜 물건·구조는 건드리지 않는다.
            }

            var inProducts = renderer.transform.parent != null && renderer.transform.parent.name == "Products";
            if (replaceAll && inProducts)
            {
                return true;
            }

            frame.Project(renderer.bounds, out var uMin, out var uMax, out var wMin, out var wMax);
            var width = uMax - uMin;
            var depth = wMax - wMin;
            var height = renderer.bounds.size.y;

            // 냉동고식: 선반 한 칸 상품이 통째로 그려진 얇고 넓은 판(8~10 cm 두께, 폭 0.4 m 이상)
            if (depth <= 0.12f && width >= 0.4f && height >= 0.15f)
            {
                return true;
            }

            // 평대형: 바닥 위에 떠 있는 수평 그림 판(6 cm 두께, 0.5×1.3 m). 본체 바닥·천장은 본체 메시라 여기 안 들어온다.
            if (chest && height <= 0.1f && width >= 0.4f && depth >= 0.4f &&
                renderer.bounds.min.y > frame.YMin + 0.2f && renderer.bounds.max.y < frame.YMax - 0.1f)
            {
                return true;
            }

            // 벽 냉장고식: 3 cm 이하 두께로 세워진 납작 상자(칸막이는 얇은 축이 폭 방향이라 제외됨)
            return generated && depth <= 0.04f && width >= 0.08f && height >= 0.1f;
        }

        private static bool IsStructurePiece(Renderer renderer)
        {
            var parent = renderer.transform.parent;
            if (parent != null && parent.name == "Structure")
            {
                return true;
            }

            // 분해되지 않은 선반 프리팹(Freezer_Shelf 등)
            var filter = renderer.GetComponent<MeshFilter>();
            return filter != null && filter.sharedMesh != null && filter.sharedMesh.name.Contains("Shelf");
        }

        private static Transform FindExplodedRoot(Transform transform)
        {
            for (var current = transform; current != null; current = current.parent)
            {
                if (current.name.EndsWith("_Exploded"))
                {
                    return current;
                }
            }

            return null;
        }

        private static Transform FindRefillGroup(Renderer body, Transform explodedRoot)
        {
            if (explodedRoot != null)
            {
                return explodedRoot.Find(RefillGroupName);
            }

            var rootName = body.name + "_" + RefillGroupName;
            return body.gameObject.scene.GetRootGameObjects().FirstOrDefault(g => g.name == rootName)?.transform;
        }

        private static Transform GetOrCreateRefillGroup(Renderer body, Transform explodedRoot)
        {
            var existing = FindRefillGroup(body, explodedRoot);
            if (existing != null)
            {
                return existing;
            }

            if (explodedRoot != null)
            {
                var group = new GameObject(RefillGroupName);
                Undo.RegisterCreatedObjectUndo(group, "Refill Fridge");
                group.transform.SetParent(explodedRoot, false);
                return group.transform;
            }

            var root = new GameObject(body.name + "_" + RefillGroupName);
            Undo.RegisterCreatedObjectUndo(root, "Refill Fridge");
            root.transform.SetPositionAndRotation(body.transform.position, body.transform.rotation);
            return root.transform;
        }

        private static PaletteKind ResolvePalette(Renderer body, PaletteKind requested)
        {
            if (requested != PaletteKind.Auto)
            {
                return requested;
            }

            var name = body.GetComponent<MeshFilter>().sharedMesh.name;
            if (name.Contains("Freezer"))
            {
                return PaletteKind.Freezer;
            }

            return name.Contains("Drinks") ? PaletteKind.Drinks : PaletteKind.Fridge;
        }

        private static List<PaletteItem> LoadPalette(PaletteKind kind)
        {
            var paths = kind switch
            {
                PaletteKind.Freezer => FreezerPalette,
                PaletteKind.Drinks => DrinksPalette,
                _ => FridgePalette,
            };

            var items = new List<PaletteItem>();
            foreach (var relative in paths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabRoot + relative + ".prefab");
                if (prefab == null)
                {
                    Debug.LogWarning($"[FridgeRefill] 프리팹 없음: {relative}");
                    continue;
                }

                var renderers = prefab.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0)
                {
                    continue;
                }

                // 프리팹 루트 기준 로컬 바운드(프리팹 에셋은 원점·무회전이라 월드 바운드가 곧 로컬 바운드)
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers)
                {
                    bounds.Encapsulate(renderer.bounds);
                }

                items.Add(new PaletteItem
                {
                    Prefab = prefab,
                    LocalSize = bounds.size,
                    LocalCenter = bounds.center,
                    LocalMinY = bounds.min.y,
                });
            }

            return items;
        }

        // ------------------------------------------------------------------ 되돌리기

        public static int Revert(Renderer body)
        {
            var unitBounds = Expanded(body.bounds, 0.06f);
            var reverted = 0;
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Revert Fridge Refill");

            // 채운 그룹 제거
            var group = FindRefillGroup(body, FindExplodedRoot(body.transform));
            if (group != null)
            {
                reverted += group.childCount;
                Undo.DestroyObjectImmediate(group.gameObject);
            }

            // 숨긴 것 복구(비활성 오브젝트는 FindObjectsByType로 안 잡히니 씬 루트에서 훑는다)
            foreach (var root in body.gameObject.scene.GetRootGameObjects())
            {
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    if (!transform.name.EndsWith(HiddenSuffix) || !unitBounds.Contains(transform.position))
                    {
                        continue;
                    }

                    var go = transform.gameObject;
                    Undo.RecordObject(go, "Revert Fridge Refill");
                    go.name = go.name.Substring(0, go.name.Length - HiddenSuffix.Length);
                    go.SetActive(true);
                    reverted++;
                }
            }

            return reverted;
        }
    }
}
