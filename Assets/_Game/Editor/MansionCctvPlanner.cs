using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Game.Client.Cameras;
using Game.Client.Interactions;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// 저택(Mansion) CCTV 설치 지점 계획 도구 (S15P21D205-1082, 2층 1091).
    ///
    /// <para>
    /// 마트는 벽면 카메라 모델 위치에서 지점을 만들었지만 저택 팩에는 카메라 소품이 없다.
    /// 그래서 좌표표(<see cref="Mounts"/>)로 지점을 정하고, 그 표가 그 층 어디를 비추는지
    /// 격자로 검사한다. 설치 높이·초점 높이·시야각은 마트 규칙(바닥 위 3.0 m, 허리 높이,
    /// 65도)을 저택 바닥 높이에 옮긴 것이다.
    /// </para>
    ///
    /// <para>
    /// <b>메뉴는 층마다 있다</b>(<c>1F/…</c>, <c>2F/…</c>). 본관 1·2층은 x·z 로 겹쳐 격자 범위가 같고
    /// 다른 것은 높이·좌표표·씨앗뿐이라, 층 하나(<see cref="FloorPlan"/>)를 골라 같은 코드를 돌린다.
    /// 프리팹은 <c>Mansion.prefab</c> 하나에 두 층이 함께 들어간다 - 그래서 <c>4. Save</c> 는 고른 층만
    /// 다시 만들고 다른 층 카메라는 있던 그대로 옮긴다. 각 지점에는 맡는 높이를 적어 두고
    /// (<c>HighlightCctvCamera.ConfigureFloor</c>), 런타임은 그 밖의 대상에 이 지점을 쓰지 않는다.
    /// 바닥 슬래브는 높이 0.5 m 가 안 되어 가림 상자로 잡히지 않으므로, 적어 두지 않으면 2층 카메라가
    /// 1층 장면을 내려다본다.
    /// </para>
    ///
    /// <para>
    /// <b>가림 판정은 런타임과 같은 규칙을 쓴다.</b> <c>HighlightCameraDirector</c> 는 물리
    /// 광선이 아니라 높이 0.5 m 이상인 정적 렌더러의 경계 상자로 가림을 본다. 문틀 벽 모듈의
    /// 상자는 문 구멍까지 덮으므로 런타임은 문 너머를 못 본다고 판정한다. 여기서도 그렇게
    /// 계산해야 "검사에서는 보였는데 재생에서는 안 잡히는" 지점이 생기지 않는다.
    /// </para>
    ///
    /// <para>
    /// 순서: <c>1. Bake Map</c> 으로 구조를 보고, <c>2. Auto Place</c> 가 벽 가까운 후보 중
    /// 사각을 가장 많이 줄이는 지점을 차례로 고른다(탐욕). 그 표를 <see cref="Mounts"/> 에
    /// 옮겨 이름을 붙이고 <c>3. Check Coverage</c> 로 확인한 뒤 <c>4. Save Prefab</c> 으로 저장한다.
    /// </para>
    /// </summary>
    public static class MansionCctvPlanner
    {
        private const string MenuRoot = "Game/Highlight/Mansion CCTV/";
        private const string SceneName = "Mansion";
        private const string OutputFolder = "docs/design/match-map/mansion";
        private const string PrefabPath = "Assets/_Game/Content/Resources/CCTV/Mansion.prefab";

        // 경계 콜라이더 안쪽 면 (MansionEnvironment/Boundary). 여유를 조금 둔다.
        // 본관 1층과 2층은 x·z 로 겹치므로 범위는 층이 같고, 다른 것은 높이뿐이다(<see cref="FloorPlan"/>).
        private const float X0 = -13.0f, X1 = 13.5f, Z0 = -33.0f, Z1 = -8.5f;
        private const float Cell = 0.25f;
        private const int PixelsPerCell = 8;
        /// <summary>이웃 칸 사이 이 높이 차까지는 걸어서 넘는 것으로 본다.</summary>
        private const float StepHeight = 0.4f;

        // 설치 높이는 천장 기준이다(2026-09-18 결정): 천장에서 0.3 m 아래, 단 바닥 위 4.4 m 를 넘지 않는다.
        // 천장을 못 찾으면 바닥 위 3.0 m(마트 규칙). 초점은 마트처럼 바닥 위 0.7 m(허리), 시야각 65도.
        private const float FallbackMountAboveFloor = 3.0f;
        private const float MaxMountAboveFloor = 4.4f;
        private const float FocusAboveFloor = 0.7f;
        private const float CeilingClearance = 0.3f;
        private const float MinCeilingAboveFloor = 2.3f;
        private const float DefaultFov = 65f;
        private const float MaxViewDistance = 15f;
        private const float Aspect = 16f / 9f;
        /// <summary>런타임 <c>CanCctvSeePoint</c> 와 같은 프레임 여유.</summary>
        private const float FrustumShrink = 0.9f;

        // 자동 배치.
        private const int AutoPlaceMaxCameras = 22;
        private const int AutoPlaceMinGain = 4;
        private const int AutoPlaceHeadings = 24;
        /// <summary>초점 거리 3 m 는 내려보는 각 37도로 카메라 발밑 사각을 줄이고, 7 m 는 긴 복도용이다.</summary>
        private static readonly float[] AutoPlaceFocusDistances = { 3f, 4.5f, 7f };

        public readonly struct Mount
        {
            public Mount(float x, float z, float focusX, float focusZ, string area, float fov = DefaultFov)
            {
                X = x; Z = z; FocusX = focusX; FocusZ = focusZ; Area = area; Fov = fov;
            }

            public float X { get; }
            public float Z { get; }
            public float FocusX { get; }
            public float FocusZ { get; }
            public string Area { get; }
            public float Fov { get; }
        }

        /// <summary>
        /// 층마다 다른 값. 본관 1·2층은 x·z 가 겹쳐 격자 범위는 같고 높이와 좌표표만 다르다.
        /// 메뉴(<c>1F/…</c>, <c>2F/…</c>)가 <see cref="Current"/> 를 바꿔 같은 코드를 두 층에 돌린다.
        /// </summary>
        private sealed class FloorPlan
        {
            /// <summary>출력 파일 이름에 들어가는 층 표시 (<c>cctv-1f-map.png</c>).</summary>
            public string Key;
            /// <summary>보고서·로그에 쓰는 이름.</summary>
            public string Label;
            /// <summary>바닥 높이와 이 층 바닥으로 칠 높이 범위.</summary>
            public float FloorY, FloorMin, FloorMax;
            /// <summary>위에서 내려 쏘는 탐침 시작 높이. 이 층 천장보다 낮아야 천장을 벽으로 오판하지 않는다.</summary>
            public float ProbeY;
            /// <summary>이 층 카메라가 맡는 대상 높이. 프리팹에서 층을 가려낼 때도 이 값을 쓴다.</summary>
            public float CoverY0, CoverY1;
            /// <summary>문이 닫혀 있어 걸어서 못 들어가도 플레이 구역으로 치는 방의 씨앗.</summary>
            public Vector2[] AssumedOpenSeeds;
            /// <summary>좌표표 앞의 이만큼(파쇄기 카메라)은 자동 배치가 건드리지 않는다.</summary>
            public int PinnedMounts;
            /// <summary>설치 지점 좌표표. (x, z, 바라볼 x, 바라볼 z, 위치 이름).</summary>
            public Mount[] Mounts;
            /// <summary>CAM 이름에 붙이는 방 이름.</summary>
            public Func<Vector3, string> Zone;
            /// <summary>자동 배치 결과를 써 넣을 이 층 좌표표의 표식. 좌표표 안에 이 줄이 그대로 있어야 한다.</summary>
            public string MountsBegin => $"            // <auto-mounts-{Key}>";
            public string MountsEnd => $"            // </auto-mounts-{Key}>";
            /// <summary>이 높이의 대상(또는 카메라)이 이 층 것인가.</summary>
            public bool Holds(float y) => y >= CoverY0 && y < CoverY1;
        }

        /// <summary>
        /// 1층 설치 지점 좌표표.
        /// 지점을 고치면 <c>3. Check Coverage</c> 로 사각을 다시 확인하고 <c>4. Save Prefab</c> 으로 저장한다.
        /// </summary>
        private static readonly Mount[] GroundMounts =
        {
            // 앞 PinnedMounts 개: 파쇄기(현관 홀 화로, ShredderSpot (-1.4, -10.7))는 파괴 장면의 중심이라 가까운 두 시점을 둔다.
            new Mount(2.0f, -11.9f, -1.4f, -10.7f, "현관 홀 파쇄기 동쪽"),
            new Mount(-4.6f, -11.9f, -1.4f, -10.7f, "현관 홀 파쇄기 서쪽"),
            // <auto-mounts-1f>
            // 2. Auto Place 2026-09-18 15:27 결과 20개 (고정 2개 뒤). 남쪽에서 북쪽 순. 이름은 방 + 바라보는 방향.
            new Mount(-1.88f, -31.88f, 0.25f, -29.75f, "남쪽 양문 방 북동향"),
            new Mount(4.63f, -31.88f, 2.50f, -29.75f, "남쪽 양문 방 북서향"),
            new Mount(-11.38f, -31.38f, -9.25f, -29.25f, "남서쪽 방 북동향"),
            new Mount(5.63f, -31.38f, 8.22f, -29.88f, "비밀 책장문 방 북동향"),
            new Mount(-4.38f, -30.38f, -1.78f, -28.88f, "남쪽 양문 방 북동향 2"),
            new Mount(12.13f, -29.88f, 9.53f, -31.38f, "비밀 책장문 방 남서향"),
            new Mount(-3.38f, -29.38f, -4.88f, -31.97f, "계단 남서쪽 작은 방 남서향"),
            new Mount(3.63f, -28.88f, 1.37f, -24.98f, "중앙 남동 복도 북서향"),
            new Mount(11.63f, -26.88f, 9.50f, -24.75f, "동남쪽 방 북서향"),
            new Mount(4.13f, -25.88f, 2.63f, -28.47f, "중앙 남동 복도 남서향"),
            new Mount(-11.38f, -22.38f, -9.25f, -24.50f, "남서쪽 방 남동향"),
            new Mount(-5.38f, -21.38f, -8.27f, -20.60f, "남서쪽 방 서향"),
            new Mount(7.63f, -20.88f, 9.75f, -23.00f, "동남쪽 방 남동향"),
            new Mount(-11.88f, -20.38f, -9.28f, -18.88f, "북서쪽 방 북동향"),
            new Mount(12.13f, -19.88f, 10.00f, -22.00f, "동남쪽 방 남서향"),
            new Mount(4.13f, -19.38f, 4.13f, -16.38f, "중앙 홀 북향"),
            new Mount(12.13f, -18.88f, 10.00f, -16.75f, "동북쪽 방 북서향"),
            new Mount(5.63f, -16.38f, 8.22f, -14.88f, "동북쪽 방 북동향"),
            new Mount(1.63f, -14.88f, 0.13f, -17.47f, "중앙 홀 남서향"),
            new Mount(-3.88f, -10.88f, -1.63f, -14.77f, "현관 홀 남동향"),
            // </auto-mounts-1f>
        };

        /// <summary>
        /// 2층 설치 지점 좌표표 (S15P21D205-1091). 앞 두 개는 2층 파쇄기 <c>Shredder_B</c> (-0.2, 6.1, -25.1) 를
        /// 양쪽에서 보는 고정 지점이고, 나머지는 <c>2. Auto Place</c> 가 쓴다.
        /// </summary>
        private static readonly Mount[] UpstairsMounts =
        {
            new Mount(3.2f, -26.3f, -0.2f, -25.1f, "2층 파쇄기 동쪽"),
            new Mount(-3.6f, -26.3f, -0.2f, -25.1f, "2층 파쇄기 서쪽"),
            // <auto-mounts-2f>
            // 아직 자동 배치를 돌리지 않았다. 2F/2. Auto Place 가 이 자리를 채운다.
            // </auto-mounts-2f>
        };

        private static readonly FloorPlan Ground = new FloorPlan
        {
            Key = "1f",
            Label = "1층",
            // 스폰 y 1.4 는 캡슐 중심이고 바닥 자체는 약 1.01 이다.
            FloorY = 1.01f, FloorMin = 0.6f, FloorMax = 1.45f,
            ProbeY = 4.4f,
            CoverY0 = -1f, CoverY1 = 5.5f,
            // 남쪽 양문 방, 비밀 책장문 뒤, 계단 남서쪽 작은 방 (문은 전부 열어 두기로 함).
            AssumedOpenSeeds = new[] { new Vector2(1.3f, -30.9f), new Vector2(8.6f, -30.6f), new Vector2(-3.6f, -28.9f) },
            PinnedMounts = 2,
            Mounts = GroundMounts,
            Zone = GroundZone,
        };

        /// <summary>
        /// 2층 (S15P21D205-1091). 바닥 5.5, 천고는 2026-09-21 에 4.5 m 로 올렸다(README 8절).
        /// 탐침 9.0 은 대기 구역 바닥 콜라이더 <c>WaitingBound_Floor</c> (9.2) 아래다 - 그 위에서 쏘면
        /// 그 판에 먼저 맞아 2층 전체가 벽으로 나온다.
        /// </summary>
        private static readonly FloorPlan Upstairs = new FloorPlan
        {
            Key = "2f",
            Label = "2층",
            FloorY = 5.5f, FloorMin = 5.1f, FloorMax = 5.95f,
            ProbeY = 9.0f,
            // 위는 다락 차단 판 Boundary_AtticFloor(9.8). 그 위는 대기 구역이라 하이라이트 대상이 아니다.
            CoverY0 = 5.5f, CoverY1 = 9.8f,
            // 남쪽 양문 3쌍 뒤의 방. 동쪽(x 12.5)·북쪽(z -12) 양문은 발코니로 나가는 문이라 넣지 않는다
            // (열면 낙하 검증을 다시 해야 한다, README 6절).
            AssumedOpenSeeds = new[] { new Vector2(1.25f, -30.9f), new Vector2(-6.2f, -30.9f), new Vector2(8.75f, -30.9f) },
            PinnedMounts = 2,
            Mounts = UpstairsMounts,
            Zone = UpstairsZone,
        };

        private static readonly FloorPlan[] Floors = { Ground, Upstairs };

        /// <summary>지금 다루는 층. 메뉴가 고르고, 마지막에 고른 층이 남는다.</summary>
        private static FloorPlan Current = Ground;

        private static float FloorY => Current.FloorY;
        private static float FloorMin => Current.FloorMin;
        private static float FloorMax => Current.FloorMax;
        private static float ProbeY => Current.ProbeY;
        private static Vector2[] AssumedOpenSeeds => Current.AssumedOpenSeeds;
        private static int PinnedMounts => Current.PinnedMounts;

        /// <summary>지금 고른 층의 좌표표.</summary>
        public static Mount[] Mounts => Current.Mounts;

        /// <summary>높이로 층을 고른다. 1층 카메라는 천장 아래 5.41 까지 올라가므로 2층 바닥에서 가른다.</summary>
        private static FloorPlan PlanFor(float y) => y < Upstairs.FloorY ? Ground : Upstairs;

        /// <summary>카메라가 맡는 층. 층을 적어 둔 카메라는 그 값이 기준이고, 없으면 높이로 고른다.</summary>
        private static FloorPlan PlanOf(HighlightCctvCamera camera) =>
            camera.HasFloor
                ? Floors.FirstOrDefault(f => f.CoverY0 <= camera.FloorY0 && camera.FloorY0 < f.CoverY1) ?? PlanFor(camera.transform.position.y)
                : PlanFor(camera.transform.position.y);

        /// <summary>이 가지에 달린 카메라 가운데 지금 층 것만.</summary>
        private static HighlightCctvCamera[] OnCurrentFloor(GameObject root) =>
            root.GetComponentsInChildren<HighlightCctvCamera>().Where(c => PlanOf(c) == Current).ToArray();

        /// <summary>메뉴가 층을 고르고 같은 일을 시킨다.</summary>
        private static void OnFloor(FloorPlan plan, Action action)
        {
            Current = plan;
            action();
        }

        private enum CellKind : byte { Void, Wall, Blocked, Floor }

        private sealed class Grid
        {
            public int Width, Height;
            public CellKind[] Kind;
            public bool[] Reachable;
            /// <summary>가림 상자 안쪽 0.1 m 보다 깊이 있는 칸. 런타임 규칙상 어느 카메라도 볼 수 없다.</summary>
            public bool[] Structural;
            public float[] FloorHeight;
            public float[] Ceiling;
            public int[] Coverage;
            public int Index(int cx, int cz) => cz * Width + cx;
            public float WorldX(int cx) => X0 + (cx + 0.5f) * Cell;
            public float WorldZ(int cz) => Z0 + (cz + 0.5f) * Cell;
            public int Count(Func<int, bool> predicate)
            {
                var n = 0;
                for (var i = 0; i < Kind.Length; i++) if (predicate(i)) n++;
                return n;
            }
        }

        private readonly struct Occluder
        {
            public Occluder(Bounds bounds, string name) { Bounds = bounds; Name = name; }
            public Bounds Bounds { get; }
            public string Name { get; }
        }

        [MenuItem(MenuRoot + "1F/1. Bake Map")]
        public static void BakeGroundMap() => OnFloor(Ground, BakeMap);

        [MenuItem(MenuRoot + "2F/1. Bake Map")]
        public static void BakeUpstairsMap() => OnFloor(Upstairs, BakeMap);

        private static void BakeMap()
        {
            RequireScene();
            var grid = Scan();
            var occluders = CollectOccluders();
            var texture = Draw(grid, occluders, null, showCoverage: false);
            var png = Save(texture, $"cctv-{Current.Key}-map.png");
            Debug.Log($"[MansionCctv] {Current.Label} 지도 저장: {png} (닿는 칸 {grid.Count(i => grid.Reachable[i])}, 천장 {CeilingSummary(grid)})");
        }

        /// <summary>
        /// 처음 <see cref="PinnedMounts"/>개(파쇄기 카메라)만 고정하고 나머지를 새로 고른다.
        /// 탐욕으로 고른 뒤 카메라를 하나씩 다른 후보로 바꿔 보며 전체 사각을 줄이고, 혼자 보는 칸이 없는 카메라는 뺀다.
        /// </summary>
        [MenuItem(MenuRoot + "1F/2. Auto Place (Greedy + Swap)")]
        public static void AutoPlaceGround() => OnFloor(Ground, () => AutoPlace(false));

        [MenuItem(MenuRoot + "2F/2. Auto Place (Greedy + Swap)")]
        public static void AutoPlaceUpstairs() => OnFloor(Upstairs, () => AutoPlace(false));

        /// <summary>좌표표(<see cref="Mounts"/>)는 그대로 두고, 그것이 못 보는 칸만 채우는 지점을 고른다.</summary>
        [MenuItem(MenuRoot + "1F/2b. Auto Fill From Mounts")]
        public static void AutoFillGround() => OnFloor(Ground, () => AutoPlace(true));

        [MenuItem(MenuRoot + "2F/2b. Auto Fill From Mounts")]
        public static void AutoFillUpstairs() => OnFloor(Upstairs, () => AutoPlace(true));

        private static void AutoPlace(bool keepMounts)
        {
            RequireScene();
            var started = DateTime.Now;
            var grid = Scan();
            var occluders = CollectOccluders();
            var fixedCount = keepMounts ? Mounts.Length : Math.Min(PinnedMounts, Mounts.Length);
            var fixedRoot = BuildCameras(out _, fixedCount);
            var fixedCameras = fixedRoot.GetComponentsInChildren<HighlightCctvCamera>();
            List<Pick> picks;
            int candidateCount;
            try { picks = Greedy(grid, occluders, fixedCameras, out candidateCount); }
            finally { UnityEngine.Object.DestroyImmediate(fixedRoot); }
            MarkStructural(grid, occluders);
            var sb = new StringBuilder();
            var inv = CultureInfo.InvariantCulture;
            var reachable = grid.Count(i => grid.Reachable[i]);
            var remaining = grid.Count(i => grid.Reachable[i] && grid.Coverage[i] == 0);
            sb.AppendLine(keepMounts ? $"# 저택 {Current.Label} CCTV 자동 채움 (좌표표 고정)" : $"# 저택 {Current.Label} CCTV 자동 배치 (탐욕 + 교환)");
            sb.AppendLine();
            sb.AppendLine($"생성: {started:yyyy-MM-dd HH:mm} · 고정 카메라 {fixedCameras.Length} · 후보 지점 {candidateCount} × 방향 {AutoPlaceHeadings} × 초점 거리 {AutoPlaceFocusDistances.Length} · 닿는 칸 {reachable} · 남은 사각 {remaining} ({100f * remaining / Math.Max(1, reachable):F1}%) · 소요 {(DateTime.Now - started).TotalSeconds:F0}초");
            sb.AppendLine();
            sb.AppendLine("| # | 설치 (x, y, z) | 초점 (x, z) | 방향 | 초점 거리 | 보이는 칸 | 이 카메라만 보는 칸 |");
            sb.AppendLine("| --- | --- | --- | --- | --- | --- | --- |");
            foreach (var p in picks)
                sb.AppendLine($"| {p.index:00} | ({p.position.x.ToString("0.00", inv)}, {p.position.y.ToString("0.00", inv)}, {p.position.z.ToString("0.00", inv)}) | ({p.focus.x.ToString("0.00", inv)}, {p.focus.z.ToString("0.00", inv)}) | {p.heading:F0}° | {Vector3.Distance(new Vector3(p.position.x, 0f, p.position.z), new Vector3(p.focus.x, 0f, p.focus.z)):F1} m | {p.visible} | {p.gain} |");
            sb.AppendLine();
            sb.AppendLine(keepMounts ? "## Mounts 뒤에 덧붙일 표" : $"## Mounts 의 {fixedCount + 1}번째부터 바꿔 넣을 표");
            sb.AppendLine();
            sb.AppendLine("```csharp");
            foreach (var line in MountLines(picks)) sb.AppendLine(line);
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("## 남은 사각 묶음");
            sb.AppendLine();
            var clusters = Clusters(grid, i => grid.Reachable[i] && grid.Coverage[i] == 0 && !grid.Structural[i]);
            if (clusters.Count == 0) sb.AppendLine("없음.");
            foreach (var cluster in clusters.Take(20))
            {
                var xs = cluster.Select(i => grid.WorldX(i % grid.Width)).ToArray();
                var zs = cluster.Select(i => grid.WorldZ(i / grid.Width)).ToArray();
                sb.AppendLine($"- {cluster.Count}칸 x {xs.Min():F1}~{xs.Max():F1} z {zs.Min():F1}~{zs.Max():F1}");
            }
            var md = Path.Combine(ProjectRoot(), OutputFolder, $"cctv-{Current.Key}-autoplace.md");
            File.WriteAllText(md, sb.ToString(), new UTF8Encoding(false));
            var cameras = Mounts.Take(fixedCount).Select(m =>
                {
                    var position = MountPosition(m.X, m.Z, null, null, out var floor);
                    return (position, new Vector3(m.FocusX, floor + FocusAboveFloor, m.FocusZ), m.Fov);
                })
                .Concat(picks.Select(p => (p.position, p.focus, DefaultFov))).ToArray();
            if (!keepMounts) WriteMountsToSource(fixedCount, picks);
            var root = BuildCameraObjects(cameras);
            try
            {
                var texture = Draw(grid, occluders, root.GetComponentsInChildren<HighlightCctvCamera>(), showCoverage: true);
                var png = Save(texture, $"cctv-{Current.Key}-autoplace.png");
                Debug.Log($"[MansionCctv] 자동 {(keepMounts ? "채움" : "배치")} {picks.Count}대, 남은 사각 {remaining}/{reachable} -> {md}, {png}");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [MenuItem(MenuRoot + "1F/3. Check Coverage")]
        public static void CheckGroundCoverage() => OnFloor(Ground, CheckCoverage);

        [MenuItem(MenuRoot + "2F/3. Check Coverage")]
        public static void CheckUpstairsCoverage() => OnFloor(Upstairs, CheckCoverage);

        private static void CheckCoverage()
        {
            RequireScene();
            var grid = Scan();
            var occluders = CollectOccluders();
            var root = BuildCameras(out var notes);
            try
            {
                var cameras = root.GetComponentsInChildren<HighlightCctvCamera>();
                var perCamera = Evaluate(grid, occluders, cameras);
                MarkStructural(grid, occluders);
                var texture = Draw(grid, occluders, cameras, showCoverage: true);
                var png = Save(texture, $"cctv-{Current.Key}-coverage.png");
                var report = Report(grid, occluders, cameras, perCamera, notes);
                var md = Path.Combine(ProjectRoot(), OutputFolder, $"cctv-{Current.Key}-coverage.md");
                File.WriteAllText(md, report, new UTF8Encoding(false));
                var reachable = grid.Count(i => grid.Reachable[i]);
                var covered = grid.Count(i => grid.Reachable[i] && grid.Coverage[i] > 0);
                Debug.Log($"[MansionCctv] 사각 검사: 카메라 {cameras.Length}, 닿는 칸 {reachable}, 보이는 칸 {covered} ({(reachable == 0 ? 0 : 100f * covered / reachable):F1}%) -> {png}, {md}");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [MenuItem(MenuRoot + "1F/4. Save Prefab (this floor)")]
        public static void SaveGroundPrefab() => OnFloor(Ground, SavePrefab);

        [MenuItem(MenuRoot + "2F/4. Save Prefab (this floor)")]
        public static void SaveUpstairsPrefab() => OnFloor(Upstairs, SavePrefab);

        /// <summary>
        /// 이 층 카메라만 좌표표로 다시 만들고 <b>다른 층 카메라는 프리팹에 있던 그대로 옮긴다.</b>
        /// 프리팹 하나에 두 층이 함께 있으므로, 통째로 덮어쓰면 다른 층의 손본 지점이 사라진다.
        /// </summary>
        private static void SavePrefab()
        {
            RequireScene();
            if (Mounts.Length == 0) throw new InvalidOperationException($"{Current.Label} 좌표표(Mounts)가 비어 있습니다.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
                Debug.LogWarning($"[MansionCctv] 기존 {PrefabPath} 의 {Current.Label} 카메라를 좌표표로 덮어씁니다. 씬에서 손으로 옮긴 {Current.Label} 카메라가 있었다면 사라집니다.");
            var root = BuildCameras(out var notes);
            try
            {
                foreach (var note in notes) Debug.LogWarning("[MansionCctv] " + note);
                var kept = CopyOtherFloors(root);
                Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[MansionCctv] {Current.Label} CCTV 지점 {Mounts.Length}개 저장" +
                          (kept > 0 ? $", 다른 층 {kept}개는 그대로 둠" : "") +
                          $" (총 {root.transform.childCount}): {PrefabPath}. 번호는 9. 로 다시 매기세요.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        /// <summary>저장된 프리팹에서 지금 층이 아닌 카메라를 <paramref name="root"/> 아래로 그대로 옮긴다.</summary>
        private static int CopyOtherFloors(GameObject root)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null) return 0;
            var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            var kept = 0;
            try
            {
                foreach (var camera in contents.GetComponentsInChildren<HighlightCctvCamera>())
                {
                    if (PlanOf(camera) == Current) continue;
                    var copy = UnityEngine.Object.Instantiate(camera.gameObject);
                    copy.name = camera.gameObject.name;
                    copy.transform.SetParent(root.transform, true);
                    kept++;
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            return kept;
        }

        private static void RequireScene()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.name != SceneName)
                throw new InvalidOperationException($"{SceneName} 씬을 열고 실행하세요. (현재: {scene.name})");
        }

        private static int ProbeMask()
        {
            var mask = Physics.DefaultRaycastLayers;
            foreach (var name in new[] { "Player", "Carryable", "Item Preview", "FirstPersonView" })
            {
                var layer = LayerMask.NameToLayer(name);
                if (layer >= 0) mask &= ~(1 << layer);
            }
            return mask;
        }

        /// <summary>격자 칸마다 바닥·가구·벽을 나누고, 스폰에서 걸어 닿는 칸을 표시한다.</summary>
        private static Grid Scan()
        {
            var grid = new Grid
            {
                Width = Mathf.RoundToInt((X1 - X0) / Cell),
                Height = Mathf.RoundToInt((Z1 - Z0) / Cell)
            };
            grid.Kind = new CellKind[grid.Width * grid.Height];
            grid.Reachable = new bool[grid.Kind.Length];
            grid.Structural = new bool[grid.Kind.Length];
            grid.FloorHeight = new float[grid.Kind.Length];
            grid.Ceiling = new float[grid.Kind.Length];
            grid.Coverage = new int[grid.Kind.Length];
            var mask = ProbeMask();
            var halfExtents = new Vector3(0.12f, 0.75f, 0.12f);
            for (var cz = 0; cz < grid.Height; cz++)
            for (var cx = 0; cx < grid.Width; cx++)
            {
                var i = grid.Index(cx, cz);
                var origin = new Vector3(grid.WorldX(cx), ProbeY, grid.WorldZ(cz));
                grid.Ceiling[i] = float.NaN;
                if (!Physics.Raycast(origin, Vector3.down, out var hit, ProbeY - FloorMin + 0.5f, mask, QueryTriggerInteraction.Ignore))
                {
                    grid.Kind[i] = CellKind.Void;
                    grid.FloorHeight[i] = float.NaN;
                    continue;
                }
                grid.FloorHeight[i] = hit.point.y;
                if (hit.point.y > FloorMax + 1.2f) { grid.Kind[i] = CellKind.Wall; continue; }
                if (hit.point.y > FloorMax || hit.point.y < FloorMin) { grid.Kind[i] = CellKind.Blocked; continue; }
                var centre = new Vector3(origin.x, hit.point.y + 1.0f, origin.z);
                var blocked = Physics.OverlapBox(centre, halfExtents, Quaternion.identity, mask, QueryTriggerInteraction.Ignore).Length > 0;
                grid.Kind[i] = blocked ? CellKind.Blocked : CellKind.Floor;
                if (Physics.Raycast(new Vector3(origin.x, hit.point.y + 0.1f, origin.z), Vector3.up,
                    out var ceiling, 8f, mask, QueryTriggerInteraction.Ignore)) grid.Ceiling[i] = ceiling.point.y;
            }
            MarkReachable(grid);
            return grid;
        }

        /// <summary>스폰·파쇄기 자리와 열어 두기로 한 방의 씨앗에서 4방향으로 퍼져 닿는 바닥 칸을 표시한다.</summary>
        private static void MarkReachable(Grid grid)
        {
            var seeds = Landmarks().Select(t => new Vector2(t.position.x, t.position.z)).Concat(AssumedOpenSeeds);
            var stack = new Stack<int>();
            foreach (var seed in seeds)
            {
                var start = NearestFloor(grid, seed);
                if (start < 0 || grid.Reachable[start]) continue;
                grid.Reachable[start] = true;
                stack.Push(start);
                while (stack.Count > 0)
                {
                    var i = stack.Pop();
                    var cx = i % grid.Width; var cz = i / grid.Width;
                    foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    {
                        var nx = cx + dx; var nz = cz + dz;
                        if (nx < 0 || nz < 0 || nx >= grid.Width || nz >= grid.Height) continue;
                        var j = grid.Index(nx, nz);
                        if (grid.Reachable[j] || grid.Kind[j] != CellKind.Floor ||
                            Mathf.Abs(grid.FloorHeight[j] - grid.FloorHeight[i]) > StepHeight) continue;
                        grid.Reachable[j] = true;
                        stack.Push(j);
                    }
                }
            }
        }

        private static int NearestFloor(Grid grid, Vector2 point)
        {
            var cx = Mathf.FloorToInt((point.x - X0) / Cell);
            var cz = Mathf.FloorToInt((point.y - Z0) / Cell);
            var best = -1; var bestDistance = int.MaxValue;
            for (var dz = -6; dz <= 6; dz++)
            for (var dx = -6; dx <= 6; dx++)
            {
                var nx = cx + dx; var nz = cz + dz;
                if (nx < 0 || nz < 0 || nx >= grid.Width || nz >= grid.Height) continue;
                var i = grid.Index(nx, nz);
                if (grid.Kind[i] != CellKind.Floor) continue;
                var d = dx * dx + dz * dz;
                if (d < bestDistance) { bestDistance = d; best = i; }
            }
            return best;
        }

        /// <summary>런타임 <c>CaptureCctvOccluders</c> 와 같은 조건으로 정적 가림 상자를 모은다.</summary>
        private static List<Occluder> CollectOccluders()
        {
            var result = new List<Occluder>();
            var mask = Physics.DefaultRaycastLayers;
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                if (!renderer.enabled || renderer.forceRenderingOff || !renderer.gameObject.activeInHierarchy ||
                    renderer.bounds.size.y < 0.5f ||
                    (mask & (1 << renderer.gameObject.layer)) == 0 ||
                    renderer.GetComponentInParent<CarryableItem>() != null ||
                    renderer.GetComponentInParent<Animator>() != null ||
                    renderer.sharedMaterial != null && renderer.sharedMaterial.renderQueue > 2500) continue;
                var b = renderer.bounds;
                // 1층 시선 높이와 겹치지 않는 상자(2층·다락·지하)는 어차피 광선에 닿지 않는다.
                if (b.max.y < FloorY + 0.3f || b.min.y > FloorY + MaxMountAboveFloor + 0.5f) continue;
                if (b.max.x < X0 - 1f || b.min.x > X1 + 1f || b.max.z < Z0 - 1f || b.min.z > Z1 + 1f) continue;
                result.Add(new Occluder(b, renderer.name));
            }
            return result;
        }

        private static Vector3 MountPosition(float x, float z, List<string> notes, string label) => MountPosition(x, z, notes, label, out _);

        /// <summary>천장 0.3 m 아래, 최대 바닥 위 4.4 m. 천장을 못 찾으면 바닥 위 3.0 m.</summary>
        private static Vector3 MountPosition(float x, float z, List<string> notes, string label, out float floor)
        {
            var mask = ProbeMask();
            floor = FloorY;
            if (Physics.Raycast(new Vector3(x, ProbeY, z), Vector3.down, out var floorHit, ProbeY - FloorMin + 0.5f, mask, QueryTriggerInteraction.Ignore)
                && floorHit.point.y >= FloorMin && floorHit.point.y <= FloorMax) floor = floorHit.point.y;
            float mountY;
            if (Physics.Raycast(new Vector3(x, floor + 1f, z), Vector3.up, out var ceilingHit, 8f, mask, QueryTriggerInteraction.Ignore))
            {
                mountY = Mathf.Min(ceilingHit.point.y - CeilingClearance, floor + MaxMountAboveFloor);
                if (ceilingHit.point.y - floor < 3.6f) notes?.Add($"{label}: 천장 {ceilingHit.point.y:F2} 이 낮아 설치 높이 {mountY:F2}");
            }
            else
            {
                mountY = floor + FallbackMountAboveFloor;
                notes?.Add($"{label}: 천장을 찾지 못해 바닥 위 {FallbackMountAboveFloor} m 로 둠");
            }
            var position = new Vector3(x, mountY, z);
            if (Physics.CheckSphere(position, 0.12f, mask, QueryTriggerInteraction.Ignore))
                notes?.Add($"{label}: 설치 지점 ({x}, {mountY:F2}, {z}) 이 콜라이더 안에 있음");
            return position;
        }

        /// <summary>좌표표를 실제 지점 트랜스폼으로 만든다. 저장과 검사가 같은 것을 본다.</summary>
        private static GameObject BuildCameras(out List<string> notes, int count = -1)
        {
            notes = new List<string>();
            if (count < 0 || count > Mounts.Length) count = Mounts.Length;
            var cameras = new (Vector3 position, Vector3 focus, float fov)[count];
            for (var i = 0; i < count; i++)
            {
                var m = Mounts[i];
                var position = MountPosition(m.X, m.Z, notes, $"CAM {i + 1:00} {m.Area}", out var floor);
                cameras[i] = (position, new Vector3(m.FocusX, floor + FocusAboveFloor, m.FocusZ), m.Fov);
            }
            var root = BuildCameraObjects(cameras);
            var markers = root.GetComponentsInChildren<HighlightCctvCamera>();
            for (var i = 0; i < markers.Length; i++) markers[i].Configure($"CAM {i + 1:00} · {Mounts[i].Area}", Mounts[i].Fov);
            return root;
        }

        private static GameObject BuildCameraObjects((Vector3 position, Vector3 focus, float fov)[] cameras)
        {
            var root = new GameObject("Mansion CCTV");
            for (var i = 0; i < cameras.Length; i++)
            {
                var marker = new GameObject($"CCTV {i + 1:00}");
                marker.transform.SetParent(root.transform, false);
                marker.transform.position = cameras[i].position;
                marker.transform.LookAt(cameras[i].focus);
                var mount = marker.AddComponent<HighlightCctvCamera>();
                mount.Configure($"CAM {i + 1:00}", cameras[i].fov);
                // 런타임이 이 지점을 다른 층 장면에 쓰지 않게 맡는 높이를 함께 적는다.
                mount.ConfigureFloor(Current.CoverY0, Current.CoverY1);
            }
            return root;
        }

        private static bool InFrustum(Vector3 origin, Quaternion inverseRotation, float fov, Vector3 point)
        {
            var local = inverseRotation * (point - origin);
            if (local.z <= 0f || local.z > MaxViewDistance) return false;
            var halfHeight = local.z * Mathf.Tan(fov * Mathf.Deg2Rad * 0.5f) * FrustumShrink;
            return Mathf.Abs(local.y) <= halfHeight && Mathf.Abs(local.x) <= halfHeight * Aspect;
        }

        private static bool LineClear(Vector3 origin, Vector3 point, List<Occluder> occluders)
        {
            var ray = new Ray(origin, (point - origin).normalized);
            var distance = Vector3.Distance(origin, point);
            foreach (var o in occluders)
            {
                var bounds = o.Bounds;
                if (!bounds.Contains(origin) && bounds.IntersectRay(ray, out var entry) &&
                    entry > 0.05f && entry < distance - 0.1f) return false;
            }
            return true;
        }

        private static bool CanSee(HighlightCctvCamera camera, Vector3 point, List<Occluder> occluders)
        {
            var origin = camera.transform.position;
            return InFrustum(origin, Quaternion.Inverse(camera.transform.rotation), camera.FieldOfView, point) &&
                   LineClear(origin, point, occluders);
        }

        private static List<Occluder> Nearby(List<Occluder> occluders, Vector3 origin) =>
            occluders.Where(o => o.Bounds.SqrDistance(origin) <= MaxViewDistance * MaxViewDistance).ToList();

        private static Vector3 Centre(Grid grid, int i) =>
            new Vector3(grid.WorldX(i % grid.Width), grid.FloorHeight[i] + 0.9f, grid.WorldZ(i / grid.Width));

        /// <summary>카메라별로 보이는 칸 수를 세고 격자에 겹침 수를 적는다. 몸통 중심 또는 상체가 보이면 된다.</summary>
        private static int[] Evaluate(Grid grid, List<Occluder> occluders, HighlightCctvCamera[] cameras)
        {
            var perCamera = new int[cameras.Length];
            var nearby = cameras.Select(c => Nearby(occluders, c.transform.position)).ToArray();
            for (var i = 0; i < grid.Kind.Length; i++)
            {
                if (!grid.Reachable[i]) continue;
                var centre = Centre(grid, i);
                var upper = centre + Vector3.up * 0.5f;
                for (var c = 0; c < cameras.Length; c++)
                {
                    if (!CanSee(cameras[c], centre, nearby[c]) && !CanSee(cameras[c], upper, nearby[c])) continue;
                    grid.Coverage[i]++;
                    perCamera[c]++;
                }
            }
            return perCamera;
        }

        /// <summary>
        /// 못 보는 칸 가운데 몸통·상체 지점이 모두 <b>낮은</b> 가림 상자 안쪽(면에서 0.1 m 이상)에 있는 칸을 표시한다.
        /// 런타임은 광선이 상자에 들어간 뒤 0.1 m 안에 대상이 없으면 가린 것으로 보므로, 피아노·의자처럼 카메라보다 낮은
        /// 상자 안의 칸은 카메라를 더 놓아도 보이지 않는다. 계단·벽 높이 책장처럼 키 큰 상자는 카메라가 그 상자 안(위쪽 빈 공간)에
        /// 있으면 무시되므로(<c>bounds.Contains(origin)</c>) 여기서 세지 않는다. 그 칸은 그냥 사각이다.
        /// </summary>
        private static void MarkStructural(Grid grid, List<Occluder> occluders)
        {
            var lowest = FloorY + MinCeilingAboveFloor;
            bool Inside(Vector3 point)
            {
                foreach (var o in occluders)
                {
                    var b = o.Bounds;
                    if (b.max.y > lowest) continue;
                    if (b.size.x < 0.3f || b.size.z < 0.3f || b.Contains(new Vector3(point.x, b.center.y, point.z)) == false) continue;
                    if (point.x - b.min.x > 0.1f && b.max.x - point.x > 0.1f && point.z - b.min.z > 0.1f && b.max.z - point.z > 0.1f &&
                        point.y - b.min.y > 0.1f && b.max.y - point.y > 0.1f) return true;
                }
                return false;
            }
            for (var i = 0; i < grid.Kind.Length; i++)
            {
                grid.Structural[i] = false;
                if (!grid.Reachable[i] || grid.Coverage[i] > 0) continue;
                var centre = Centre(grid, i);
                grid.Structural[i] = Inside(centre) && Inside(centre + Vector3.up * 0.5f);
            }
        }

        // ---- 자동 배치 ----

        private readonly struct Pick
        {
            public Pick(int index, Vector3 position, Vector3 focus, float heading, int visible, int gain)
            {
                this.index = index; this.position = position; this.focus = focus; this.heading = heading; this.visible = visible; this.gain = gain;
            }
            public readonly int index;
            public readonly Vector3 position;
            public readonly Vector3 focus;
            public readonly float heading;
            public readonly int visible;
            public readonly int gain;
        }

        private sealed class Option
        {
            public Vector3 Position, Focus;
            public float Heading;
            public int[] Cells;
        }

        /// <summary>
        /// 벽에서 0.75 m 안쪽의 닿는 칸을 0.5 m 간격으로 후보로 잡고, 남은 사각을 가장 많이 덮는
        /// (후보, 방향, 초점 거리)를 차례로 고른다(탐욕). 그 뒤 카메라 하나를 다른 후보로 바꿔 사각이
        /// 줄어들면 바꾸는 것을 반복하고(교환), 혼자 보는 칸이 없는 카메라는 뺀다.
        /// 결과 겹침 수는 <see cref="Grid.Coverage"/> 에 남긴다(고정 카메라 포함).
        /// </summary>
        private static List<Pick> Greedy(Grid grid, List<Occluder> occluders, HighlightCctvCamera[] fixedCameras, out int candidateCount, int maxTotal = AutoPlaceMaxCameras)
        {
            if (fixedCameras.Length > 0) Evaluate(grid, occluders, fixedCameras);
            var candidates = new List<(Vector3 position, float floor)>();
            for (var cz = 0; cz < grid.Height; cz += 2)
            for (var cx = 0; cx < grid.Width; cx += 2)
            {
                var i = grid.Index(cx, cz);
                if (!grid.Reachable[i] || !NearWall(grid, cx, cz)) continue;
                var position = MountPosition(grid.WorldX(cx), grid.WorldZ(cz), null, null, out var floor);
                if (position.y - grid.FloorHeight[i] < MinCeilingAboveFloor) continue;
                if (Physics.CheckSphere(position, 0.12f, ProbeMask(), QueryTriggerInteraction.Ignore)) continue;
                candidates.Add((position, floor));
            }
            candidateCount = candidates.Count;
            var reachableCells = new List<int>();
            for (var i = 0; i < grid.Kind.Length; i++) if (grid.Reachable[i]) reachableCells.Add(i);

            // 시선 판정은 방향과 무관하니 후보 위치마다 한 번만 계산해 둔다. 1: 몸통, 2: 상체.
            var options = new List<Option>();
            var total = candidates.Count;
            try
            {
                for (var c = 0; c < candidates.Count; c++)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("Mansion CCTV", $"후보 {c + 1}/{total} 시선 계산", (float)c / total))
                        throw new OperationCanceledException();
                    var origin = candidates[c].position;
                    var nearby = Nearby(occluders, origin);
                    var visible = new Dictionary<int, byte>();
                    foreach (var i in reachableCells)
                    {
                        var centre = Centre(grid, i);
                        if ((centre - origin).sqrMagnitude > MaxViewDistance * MaxViewDistance) continue;
                        var flags = (LineClear(origin, centre, nearby) ? 1 : 0) | (LineClear(origin, centre + Vector3.up * 0.5f, nearby) ? 2 : 0);
                        if (flags != 0) visible[i] = (byte)flags;
                    }
                    var floor = candidates[c].floor;
                    foreach (var focusDistance in AutoPlaceFocusDistances)
                    for (var h = 0; h < AutoPlaceHeadings; h++)
                    {
                        var heading = 360f * h / AutoPlaceHeadings;
                        var direction = new Vector3(Mathf.Sin(heading * Mathf.Deg2Rad), 0f, Mathf.Cos(heading * Mathf.Deg2Rad));
                        var focus = origin + direction * focusDistance;
                        focus.y = floor + FocusAboveFloor;
                        var inverse = Quaternion.Inverse(Quaternion.LookRotation(focus - origin));
                        var cells = new List<int>();
                        foreach (var pair in visible)
                        {
                            var centre = Centre(grid, pair.Key);
                            if ((pair.Value & 1) != 0 && InFrustum(origin, inverse, DefaultFov, centre) ||
                                (pair.Value & 2) != 0 && InFrustum(origin, inverse, DefaultFov, centre + Vector3.up * 0.5f))
                                cells.Add(pair.Key);
                        }
                        if (cells.Count > 0) options.Add(new Option { Position = origin, Focus = focus, Heading = heading, Cells = cells.ToArray() });
                    }
                }
            }
            finally { EditorUtility.ClearProgressBar(); }

            // 탐욕.
            var coverage = grid.Coverage;
            var chosen = new List<Option>();
            int Gain(Option option)
            {
                var gain = 0;
                foreach (var i in option.Cells) if (coverage[i] == 0) gain++;
                return gain;
            }
            void Add(Option option) { foreach (var i in option.Cells) coverage[i]++; }
            void Remove(Option option) { foreach (var i in option.Cells) coverage[i]--; }
            while (chosen.Count + fixedCameras.Length < maxTotal)
            {
                Option best = null; var bestGain = 0;
                foreach (var option in options)
                {
                    var gain = Gain(option);
                    if (gain > bestGain) { bestGain = gain; best = option; }
                }
                if (best == null || bestGain < AutoPlaceMinGain) break;
                Add(best);
                chosen.Add(best);
            }

            // 교환: 카메라 하나를 빼고 그 자리에 가장 많이 덮는 후보를 넣는다. 사각이 줄지 않으면 원래대로 둔다.
            try
            {
                for (var sweep = 0; sweep < 6; sweep++)
                {
                    var improved = false;
                    for (var k = 0; k < chosen.Count; k++)
                    {
                        EditorUtility.DisplayProgressBar("Mansion CCTV", $"교환 {sweep + 1}회차 카메라 {k + 1}/{chosen.Count}", (float)k / chosen.Count);
                        Remove(chosen[k]);
                        var best = chosen[k]; var bestGain = Gain(chosen[k]);
                        foreach (var option in options)
                        {
                            var gain = Gain(option);
                            if (gain > bestGain) { bestGain = gain; best = option; }
                        }
                        Add(best);
                        if (!ReferenceEquals(best, chosen[k])) { chosen[k] = best; improved = true; }
                    }
                    if (!improved) break;
                }
            }
            finally { EditorUtility.ClearProgressBar(); }

            // 혼자 보는 칸이 없는 카메라는 뺀다.
            for (var k = chosen.Count - 1; k >= 0; k--)
            {
                var unique = 0;
                foreach (var i in chosen[k].Cells) if (coverage[i] == 1) unique++;
                if (unique > 0) continue;
                Remove(chosen[k]);
                chosen.RemoveAt(k);
            }

            var picks = new List<Pick>();
            foreach (var option in chosen.OrderBy(o => o.Position.z).ThenBy(o => o.Position.x))
            {
                var unique = 0;
                foreach (var i in option.Cells) if (coverage[i] == 1) unique++;
                picks.Add(new Pick(fixedCameras.Length + picks.Count + 1, option.Position, option.Focus, option.Heading, option.Cells.Length, unique));
            }
            return picks;
        }

        private static bool NearWall(Grid grid, int cx, int cz)
        {
            for (var dz = -3; dz <= 3; dz++)
            for (var dx = -3; dx <= 3; dx++)
            {
                var nx = cx + dx; var nz = cz + dz;
                if (nx < 0 || nz < 0 || nx >= grid.Width || nz >= grid.Height) continue;
                if (grid.Kind[grid.Index(nx, nz)] == CellKind.Wall) return true;
            }
            return false;
        }

        /// <summary>카메라 정면 광선이 허리 높이(바닥 위 0.7 m)와 만나는 점. 좌표표 없이도 초점을 보고서에 적기 위한 것이다.</summary>
        private static Vector3 FocusOnFloor(Transform camera)
        {
            var forward = camera.forward;
            if (forward.y >= -0.01f) return camera.position + forward * 5f;
            var t = (camera.position.y - (FloorY + FocusAboveFloor)) / -forward.y;
            return camera.position + forward * t;
        }

        private const string PreviewName = "Mansion CCTV Preview";

        /// <summary>
        /// 좌표표가 아니라 저장된 프리팹의 카메라로 사각을 검사한다. 씬 미리보기에서 카메라를 옮겨
        /// 프리팹에 적용(Overrides > Apply)한 뒤 확인할 때 쓴다. 이 경우 프리팹이 기준이고 좌표표는 기록이다.
        /// </summary>
        [MenuItem(MenuRoot + "1F/3b. Check Coverage (Saved Prefab)")]
        public static void CheckGroundPrefabCoverage() => OnFloor(Ground, CheckPrefabCoverage);

        [MenuItem(MenuRoot + "2F/3b. Check Coverage (Saved Prefab)")]
        public static void CheckUpstairsPrefabCoverage() => OnFloor(Upstairs, CheckPrefabCoverage);

        private static void CheckPrefabCoverage()
        {
            RequireScene();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) throw new InvalidOperationException(PrefabPath + " 이 없습니다. 4. Save 로 먼저 만드세요.");
            var grid = Scan();
            var occluders = CollectOccluders();
            var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                // 다른 층 카메라는 이 층 격자를 볼 일이 없다. 표에 0 칸짜리 줄로 끼는 것만 막는다.
                var cameras = OnCurrentFloor(root);
                var perCamera = Evaluate(grid, occluders, cameras);
                MarkStructural(grid, occluders);
                var texture = Draw(grid, occluders, cameras, showCoverage: true);
                var png = Save(texture, $"cctv-{Current.Key}-coverage.png");
                var report = Report(grid, occluders, cameras, perCamera, new List<string> { "이 보고서는 좌표표가 아니라 저장된 프리팹 " + PrefabPath + " 기준이다." });
                var md = Path.Combine(ProjectRoot(), OutputFolder, $"cctv-{Current.Key}-coverage.md");
                File.WriteAllText(md, report, new UTF8Encoding(false));
                var reachable = grid.Count(i => grid.Reachable[i]);
                var covered = grid.Count(i => grid.Reachable[i] && grid.Coverage[i] > 0);
                Debug.Log($"[MansionCctv] {Current.Label} 프리팹 사각 검사: 카메라 {cameras.Length}, 닿는 칸 {reachable}, 보이는 칸 {covered} ({(reachable == 0 ? 0 : 100f * covered / reachable):F1}%) -> {png}, {md}");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        /// <summary>
        /// 저장된 프리팹을 씬에 한 번 놓아 카메라 위치·방향을 눈으로 본다. 다시 누르면 지운다.
        /// 씬 뷰에서 모든 지점에 시야 부채꼴과 이름이 그려진다(<see cref="HighlightCctvGizmos"/>).
        /// 미리보기를 둔 채 씬을 저장하지 않는다. 런타임은 Resources 에서 따로 인스턴스화하므로 겹친다.
        /// </summary>
        [MenuItem(MenuRoot + "5. Toggle Prefab Preview In Scene")]
        public static void TogglePreview()
        {
            RequireScene();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var existing = scene.GetRootGameObjects().FirstOrDefault(g => g.name == PreviewName);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
                RemoveEye();
                Debug.Log("[MansionCctv] 미리보기를 지웠습니다.");
                return;
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) throw new InvalidOperationException(PrefabPath + " 이 없습니다. 4. Save 로 먼저 만드세요.");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = PreviewName;
            Undo.RegisterCreatedObjectUndo(instance, "Mansion CCTV Preview");
            Selection.activeGameObject = instance;
            SceneView.lastActiveSceneView?.FrameSelected();
            Debug.Log("[MansionCctv] 미리보기를 놓았습니다. 카메라를 옮겼으면 Overrides > Apply All 로 프리팹에 적용한 뒤 3b 로 검사하고, 씬을 저장하기 전에 이 메뉴로 미리보기를 지우세요.");
        }

        // ---- 저장된 프리팹을 직접 고치는 메뉴 ----

        private const int FillMaxTotal = 32;

        /// <summary>
        /// 저장된 프리팹의 카메라는 그대로 두고, 그것이 못 보는 칸을 채우는 지점을 골라 <b>프리팹에 바로 추가</b>한다.
        /// 씬에서 손으로 고친 뒤 빈 곳만 메울 때 쓴다. 좌표표(<see cref="Mounts"/>)는 건드리지 않는다.
        /// 추가된 카메라는 미리보기(5번)에서 바로 보이고, 마음에 안 들면 지우고 Apply 하면 된다.
        /// </summary>
        [MenuItem(MenuRoot + "1F/2c. Auto Fill Saved Prefab (adds cameras)")]
        public static void AutoFillGroundPrefab() => OnFloor(Ground, AutoFillPrefab);

        [MenuItem(MenuRoot + "2F/2c. Auto Fill Saved Prefab (adds cameras)")]
        public static void AutoFillUpstairsPrefab() => OnFloor(Upstairs, AutoFillPrefab);

        private static void AutoFillPrefab()
        {
            RequireScene();
            var started = DateTime.Now;
            var grid = Scan();
            var occluders = CollectOccluders();
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                // 채울 곳을 세는 것은 이 층 카메라 기준이다. 다른 층 카메라는 이 격자를 보지 못한다.
                var fixedCameras = OnCurrentFloor(root);
                var picks = Greedy(grid, occluders, fixedCameras, out var candidateCount, FillMaxTotal);
                MarkStructural(grid, occluders);
                var names = MountNames(picks);
                var existing = fixedCameras.Length;
                // 번호는 프리팹 전체에서 이어 붙인다. 두 층이 같은 이름을 갖지 않게 하고, 정리는 9. 가 한다.
                var numbered = root.GetComponentsInChildren<HighlightCctvCamera>().Length;
                for (var i = 0; i < picks.Count; i++)
                {
                    var p = picks[i];
                    var number = numbered + i + 1;
                    var marker = new GameObject($"CCTV {number:00}");
                    marker.transform.SetParent(root.transform, false);
                    marker.transform.position = p.position;
                    marker.transform.LookAt(p.focus);
                    var mount = marker.AddComponent<HighlightCctvCamera>();
                    mount.Configure($"CAM {number:00} · {names[i]}", DefaultFov);
                    mount.ConfigureFloor(Current.CoverY0, Current.CoverY1);
                }
                if (picks.Count > 0) PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                var reachable = grid.Count(i => grid.Reachable[i]);
                var remaining = grid.Count(i => grid.Reachable[i] && grid.Coverage[i] == 0 && !grid.Structural[i]);
                var sb = new StringBuilder();
                sb.AppendLine($"# 저택 {Current.Label} CCTV 프리팹 자동 채움");
                sb.AppendLine();
                sb.AppendLine($"생성: {started:yyyy-MM-dd HH:mm} · 기존 카메라 {existing} · 추가 {picks.Count} · 후보 {candidateCount} · 남은 사각 {remaining} ({100f * remaining / Math.Max(1, reachable):F1}%) · 소요 {(DateTime.Now - started).TotalSeconds:F0}초");
                sb.AppendLine();
                foreach (var p in picks) sb.AppendLine($"- CCTV {numbered + p.index - fixedCameras.Length:00}: ({p.position.x:F2}, {p.position.y:F2}, {p.position.z:F2}) → ({p.focus.x:F2}, {p.focus.z:F2}), 보이는 칸 {p.visible}, 이 카메라만 {p.gain}");
                File.WriteAllText(Path.Combine(ProjectRoot(), OutputFolder, $"cctv-{Current.Key}-autoplace.md"), sb.ToString(), new UTF8Encoding(false));
                var texture = Draw(grid, occluders, OnCurrentFloor(root), showCoverage: true);
                var png = Save(texture, $"cctv-{Current.Key}-autoplace.png");
                Debug.Log($"[MansionCctv] {Current.Label} 프리팹에 {picks.Count}대 추가({Current.Label} 총 {existing + picks.Count}), 남은 사각 {remaining}/{reachable} -> {png}. 미리보기(5번)를 켜 두었다면 바로 보입니다. 확인 후 3b 로 다시 검사하세요.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        /// <summary>
        /// 저장된 프리팹의 카메라를 아래층부터, 층 안에서는 남쪽→북쪽 순으로 CCTV 01.. 로 다시 번호 매긴다.
        /// 이름의 "CAM NN · " 앞부분만 바꾼다. 층을 안 적어 둔 카메라에는 높이로 고른 층을 함께 적는다.
        /// </summary>
        [MenuItem(MenuRoot + "9. Renumber Saved Prefab Cameras")]
        public static void RenumberPrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var cameras = root.GetComponentsInChildren<HighlightCctvCamera>()
                    .OrderBy(c => Array.IndexOf(Floors, PlanOf(c)))
                    .ThenBy(c => c.transform.position.z).ThenBy(c => c.transform.position.x).ToArray();
                for (var i = 0; i < cameras.Length; i++)
                {
                    var location = cameras[i].LocationName;
                    var separator = location.IndexOf(" · ", StringComparison.Ordinal);
                    var zone = separator >= 0 ? location.Substring(separator + 3) : location.StartsWith("CAM ") ? ZoneName(cameras[i].transform.position) : location;
                    cameras[i].Configure($"CAM {i + 1:00} · {zone}", cameras[i].FieldOfView);
                    cameras[i].name = $"CCTV {i + 1:00}";
                    cameras[i].transform.SetSiblingIndex(i);
                    // 손으로 복제한 카메라는 층이 비어 있다. 런타임이 다른 층 장면에 쓰지 않게 여기서 채운다.
                    if (!cameras[i].HasFloor)
                    {
                        var plan = PlanFor(cameras[i].transform.position.y);
                        cameras[i].ConfigureFloor(plan.CoverY0, plan.CoverY1);
                    }
                }
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                var perFloor = string.Join(", ", Floors.Select(f => $"{f.Label} {cameras.Count(c => PlanOf(c) == f)}대"));
                Debug.Log($"[MansionCctv] 프리팹 카메라 {cameras.Length}대를 CAM 01~{cameras.Length:00} 으로 다시 매겼습니다 ({perFloor}).");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // ---- 자동 배치 결과를 좌표표로 ----

        /// <summary>그 높이의 층이 쓰는 방 이름.</summary>
        private static string ZoneName(Vector3 position) => PlanFor(position.y).Zone(position);

        /// <summary>1층 방 이름. 지도(cctv-1f-map.png)에서 읽은 벽 위치 기준이며 CAM 이름에만 쓴다.</summary>
        private static string GroundZone(Vector3 position)
        {
            var x = position.x; var z = position.z;
            if (z > -12.2f) return "현관 홀";
            if (x < -5f) return z > -21f ? "북서쪽 방" : "남서쪽 방";
            if (x > 4.9f) return z > -19.5f ? "동북쪽 방" : z > -29.5f ? "동남쪽 방" : "비밀 책장문 방";
            if (x < -2.8f && z < -27.3f) return "계단 남서쪽 작은 방";
            if (z < -29.5f) return "남쪽 양문 방";
            if (z > -19.5f) return "중앙 홀";
            if (x > 2.4f) return "중앙 남동 복도";
            if (x < -2.8f) return "계단 서쪽";
            return "계단 남쪽 복도";
        }

        /// <summary>
        /// 2층 자리 이름. 2층 방 이름은 아직 정하지 않아 방위로 부른다 - 시점을 하나씩 보며
        /// <c>HighlightCctvCamera > Location Name</c> 에서 방 이름으로 고쳐 쓴다.
        /// </summary>
        private static string UpstairsZone(Vector3 position)
        {
            var northSouth = position.z > -16.5f ? "북" : position.z > -25.5f ? "" : "남";
            var eastWest = position.x > 4.5f ? "동" : position.x > -4.5f ? "" : "서";
            var where = northSouth + eastWest;
            return where.Length == 0 ? "2층 중앙" : "2층 " + where + "쪽";
        }

        private static string HeadingName(float heading)
        {
            var names = new[] { "북향", "북동향", "동향", "남동향", "남향", "남서향", "서향", "북서향" };
            return names[Mathf.RoundToInt(((heading % 360f) + 360f) % 360f / 45f) % 8];
        }

        /// <summary>방 + 바라보는 방향 이름. 같은 이름이 겹치면 번호를 붙인다.</summary>
        private static List<string> MountNames(List<Pick> picks)
        {
            var names = new List<string>();
            var used = new Dictionary<string, int>();
            foreach (var p in picks)
            {
                var name = $"{ZoneName(p.position)} {HeadingName(p.heading)}";
                used[name] = used.TryGetValue(name, out var n) ? n + 1 : 1;
                if (used[name] > 1) name += $" {used[name]}";
                names.Add(name);
            }
            return names;
        }

        private static List<string> MountLines(List<Pick> picks)
        {
            var inv = CultureInfo.InvariantCulture;
            var names = MountNames(picks);
            var lines = new List<string>();
            for (var i = 0; i < picks.Count; i++)
            {
                var p = picks[i];
                lines.Add($"            new Mount({p.position.x.ToString("0.00", inv)}f, {p.position.z.ToString("0.00", inv)}f, {p.focus.x.ToString("0.00", inv)}f, {p.focus.z.ToString("0.00", inv)}f, \"{names[i]}\"),");
            }
            return lines;
        }

        /// <summary>
        /// 이 파일의 <c>Mounts</c> 에서 <c>&lt;auto-mounts&gt;</c> 표식 사이를 새 결과로 바꿔 쓴다.
        /// 고정 항목(파쇄기)은 표식 밖에 있어 그대로 남는다. Unity 가 다시 컴파일한 뒤 3·4 번 메뉴를 쓴다.
        /// </summary>
        private static void WriteMountsToSource(int fixedCount, List<Pick> picks)
        {
            var path = Path.Combine(ProjectRoot(), "Assets/_Game/Editor/MansionCctvPlanner.cs");
            var source = File.ReadAllText(path, Encoding.UTF8);
            var begin = source.IndexOf(Current.MountsBegin, StringComparison.Ordinal);
            var end = begin < 0 ? -1 : source.IndexOf(Current.MountsEnd, begin, StringComparison.Ordinal);
            if (begin < 0 || end < 0 || end < begin)
            {
                Debug.LogWarning($"[MansionCctv] {Current.Label} 좌표표 표식({Current.MountsBegin.Trim()})을 찾지 못해 소스를 고치지 않았습니다. 보고서의 표를 손으로 옮기세요.");
                return;
            }
            var body = new StringBuilder();
            body.AppendLine(Current.MountsBegin);
            body.AppendLine($"            // 2. Auto Place {DateTime.Now:yyyy-MM-dd HH:mm} 결과 {picks.Count}개 (고정 {fixedCount}개 뒤). 남쪽에서 북쪽 순. 이름은 방 + 바라보는 방향.");
            foreach (var line in MountLines(picks)) body.AppendLine(line);
            body.Append(Current.MountsEnd);
            source = source.Substring(0, begin) + body + source.Substring(end + Current.MountsEnd.Length);
            File.WriteAllText(path, source, new UTF8Encoding(false));
            AssetDatabase.ImportAsset("Assets/_Game/Editor/MansionCctvPlanner.cs");
            Debug.Log($"[MansionCctv] {Current.Label} 좌표표 {picks.Count}개를 MansionCctvPlanner.cs 에 썼습니다. 다시 컴파일된 뒤 3. Check Coverage → 4. Save 를 누르세요.");
        }

        // ---- 카메라 시점으로 보기 ----

        private const string EyeName = "CCTV Eye";
        private static HighlightCctvCamera lookingThrough;

        /// <summary>
        /// 선택한 CCTV 지점(또는 그 부모/자식)의 위치·방향·시야각을 Scene 뷰에 맞추고, 같은 시점의 임시 카메라
        /// <c>CCTV Eye</c>(저장 안 됨, depth 100)를 두어 Game 뷰가 16:9 로 그 화면을 그리게 한다.
        /// 지점을 옮기면 임시 카메라가 따라간다. 미리보기를 지우면 함께 사라진다.
        /// </summary>
        [MenuItem(MenuRoot + "6. Look Through Selected CCTV %#&8")]
        public static void LookThroughSelected()
        {
            var selected = Selection.activeGameObject;
            var camera = selected == null ? null
                : selected.GetComponent<HighlightCctvCamera>() ?? selected.GetComponentInParent<HighlightCctvCamera>() ?? selected.GetComponentInChildren<HighlightCctvCamera>();
            if (camera == null) camera = lookingThrough != null ? lookingThrough : AllCctv().FirstOrDefault();
            if (camera == null) throw new InvalidOperationException("씬에 CCTV 지점이 없습니다. 5. Toggle Prefab Preview 로 먼저 놓으세요.");
            LookThrough(camera);
        }

        [MenuItem(MenuRoot + "7. Look Through Next CCTV %#&9")]
        public static void LookThroughNext()
        {
            var all = AllCctv().ToList();
            if (all.Count == 0) throw new InvalidOperationException("씬에 CCTV 지점이 없습니다. 5. Toggle Prefab Preview 로 먼저 놓으세요.");
            var index = lookingThrough == null ? -1 : all.IndexOf(lookingThrough);
            LookThrough(all[(index + 1) % all.Count]);
        }

        /// <summary>
        /// Scene 뷰를 원하는 구도로 움직인 뒤 누르면, 마지막에 시점으로 본(또는 선택한) CCTV 지점을 그 Scene 뷰
        /// 카메라 위치·방향으로 옮긴다. Unity 의 GameObject > Align With View(Ctrl+Shift+F)와 같고 되돌리기가 된다.
        /// </summary>
        [MenuItem(MenuRoot + "6b. Move CCTV To Scene View %#&7")]
        public static void MoveCctvToSceneView()
        {
            var selected = Selection.activeGameObject;
            var camera = selected == null ? null
                : selected.GetComponent<HighlightCctvCamera>() ?? selected.GetComponentInParent<HighlightCctvCamera>();
            if (camera == null) camera = lookingThrough;
            if (camera == null) throw new InvalidOperationException("옮길 CCTV 지점을 먼저 선택하거나 6 번으로 시점을 보세요.");
            var view = SceneView.lastActiveSceneView;
            if (view == null || view.camera == null) throw new InvalidOperationException("Scene 뷰가 없습니다.");
            Undo.RecordObject(camera.transform, "Move CCTV To Scene View");
            camera.transform.SetPositionAndRotation(view.camera.transform.position, view.camera.transform.rotation);
            PrefabUtility.RecordPrefabInstancePropertyModifications(camera.transform);
            lookingThrough = camera;
            Selection.activeGameObject = camera.gameObject;
            var tilt = -Mathf.Asin(Mathf.Clamp(camera.transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
            Debug.Log($"[MansionCctv] {camera.LocationName} 을 Scene 뷰 시점 ({camera.transform.position.x:F2}, {camera.transform.position.y:F2}, {camera.transform.position.z:F2}), 내려보는 각 {tilt:F0}° 로 옮겼습니다. Overrides > Apply All 로 프리팹에 적용하세요.");
        }

        [MenuItem(MenuRoot + "8. Stop Looking Through (Remove Eye)")]
        public static void StopLookingThrough() => RemoveEye();

        /// <summary>
        /// 저장된 프리팹의 카메라마다 맡는 층을 적어 둔다. 층이 비어 있는 카메라는 높이로 고른다.
        /// 층을 적기 전에 만든 프리팹에 한 번 쓰고, 그 뒤로는 저장·번호 매김이 알아서 채운다.
        /// </summary>
        [MenuItem(MenuRoot + "0. Tag Saved Prefab Cameras With Floor")]
        public static void TagPrefabFloors()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var cameras = root.GetComponentsInChildren<HighlightCctvCamera>();
                var changed = 0;
                foreach (var camera in cameras)
                {
                    var plan = PlanFor(camera.transform.position.y);
                    if (camera.HasFloor && Mathf.Approximately(camera.FloorY0, plan.CoverY0) &&
                        Mathf.Approximately(camera.FloorY1, plan.CoverY1)) continue;
                    camera.ConfigureFloor(plan.CoverY0, plan.CoverY1);
                    changed++;
                }
                if (changed > 0) PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                var perFloor = string.Join(", ", Floors.Select(f => $"{f.Label} {cameras.Count(c => PlanOf(c) == f)}대"));
                Debug.Log($"[MansionCctv] 카메라 {cameras.Length}대 중 {changed}대에 층을 적었습니다 ({perFloor}).");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static IEnumerable<HighlightCctvCamera> AllCctv()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            return scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<HighlightCctvCamera>(true)).OrderBy(c => c.name);
        }

        private static void LookThrough(HighlightCctvCamera camera)
        {
            lookingThrough = camera;
            // 이 카메라의 층을 지금 층으로 삼는다. 보고서와 초점 높이가 보고 있는 층을 따라간다.
            Current = PlanOf(camera);
            var eye = GameObject.Find(EyeName);
            if (eye == null)
            {
                eye = new GameObject(EyeName) { hideFlags = HideFlags.DontSave };
                var output = eye.AddComponent<Camera>();
                output.depth = 100f;
                output.nearClipPlane = 0.1f;
                output.farClipPlane = 120f;
                EditorApplication.update -= FollowEye;
                EditorApplication.update += FollowEye;
            }
            eye.GetComponent<Camera>().fieldOfView = camera.FieldOfView;
            eye.transform.SetPositionAndRotation(camera.transform.position, camera.transform.rotation);
            var view = SceneView.lastActiveSceneView;
            if (view != null)
            {
                view.AlignViewToObject(eye.transform);
                view.cameraSettings.fieldOfView = camera.FieldOfView;
                view.Repaint();
            }
            Selection.activeGameObject = camera.gameObject;
            Debug.Log($"[MansionCctv] {camera.LocationName} 시점. Scene 뷰는 이 시점으로 맞췄고 Game 뷰는 CCTV Eye(16:9)가 그립니다. Ctrl+Shift+Alt+9 로 다음 카메라, 8 번 메뉴로 끝냅니다.");
        }

        private static void FollowEye()
        {
            var eye = GameObject.Find(EyeName);
            if (eye == null || lookingThrough == null) { EditorApplication.update -= FollowEye; return; }
            if (eye.transform.position == lookingThrough.transform.position && eye.transform.rotation == lookingThrough.transform.rotation) return;
            eye.transform.SetPositionAndRotation(lookingThrough.transform.position, lookingThrough.transform.rotation);
            eye.GetComponent<Camera>().fieldOfView = lookingThrough.FieldOfView;
        }

        private static void RemoveEye()
        {
            EditorApplication.update -= FollowEye;
            lookingThrough = null;
            var eye = GameObject.Find(EyeName);
            if (eye != null) UnityEngine.Object.DestroyImmediate(eye);
        }

        // ---- 보고서 ----

        private static string Report(Grid grid, List<Occluder> occluders, HighlightCctvCamera[] cameras, int[] perCamera, List<string> notes)
        {
            var sb = new StringBuilder();
            var inv = CultureInfo.InvariantCulture;
            var reachable = grid.Count(i => grid.Reachable[i]);
            var covered = grid.Count(i => grid.Reachable[i] && grid.Coverage[i] > 0);
            var twice = grid.Count(i => grid.Reachable[i] && grid.Coverage[i] > 1);
            var unreachable = grid.Count(i => grid.Kind[i] == CellKind.Floor && !grid.Reachable[i]);
            var structural = grid.Count(i => grid.Reachable[i] && grid.Coverage[i] == 0 && grid.Structural[i]);
            sb.AppendLine($"# 저택 {Current.Label} CCTV 사각 검사");
            sb.AppendLine();
            sb.AppendLine($"생성: {DateTime.Now:yyyy-MM-dd HH:mm} · 격자 {Cell} m · 범위 x[{X0}, {X1}] z[{Z0}, {Z1}] · 층 {Current.Label} (바닥 {Current.FloorY}, 맡는 높이 {Current.CoverY0}~{Current.CoverY1}) · 가림 판정 = 런타임과 같은 정적 렌더러 경계 상자");
            sb.AppendLine();
            sb.AppendLine($"- 플레이 구역(스폰·파쇄기·열어 둔 방에서 걸어 닿는 칸): {reachable} ({reachable * Cell * Cell:F1} m²) · 닿지 않아 뺀 바닥 칸 {unreachable}");
            sb.AppendLine($"- 카메라 1대 이상에 보이는 칸: {covered} ({(reachable == 0 ? 0 : 100f * covered / reachable):F1}%)");
            sb.AppendLine($"- 2대 이상에 보이는 칸: {twice} ({(reachable == 0 ? 0 : 100f * twice / reachable):F1}%)");
            sb.AppendLine($"- 사각 칸: {reachable - covered - structural} · 가림 상자 안이라 어느 카메라도 못 보는 칸: {structural} ({structural * Cell * Cell:F1} m²)");
            sb.AppendLine($"- {Current.Label} 천장 높이: {CeilingSummary(grid)}");
            sb.AppendLine();
            sb.AppendLine("## 카메라");
            sb.AppendLine();
            sb.AppendLine("| CAM | 위치 | 설치 (x, y, z) | 초점 (x, z) | 내려보는 각 | 시야각 | 보이는 칸 | 이 카메라만 보는 칸 |");
            sb.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- |");
            for (var c = 0; c < cameras.Length; c++)
            {
                var t = cameras[c].transform;
                var origin = t.position;
                var tilt = -Mathf.Asin(Mathf.Clamp(t.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
                var focus = FocusOnFloor(t);
                var nearby = Nearby(occluders, origin);
                var unique = 0;
                for (var i = 0; i < grid.Kind.Length; i++)
                {
                    if (!grid.Reachable[i] || grid.Coverage[i] != 1) continue;
                    var centre = Centre(grid, i);
                    if (CanSee(cameras[c], centre, nearby) || CanSee(cameras[c], centre + Vector3.up * 0.5f, nearby)) unique++;
                }
                sb.AppendLine($"| {c + 1:00} | {cameras[c].LocationName} | ({origin.x.ToString("0.0", inv)}, {origin.y.ToString("0.00", inv)}, {origin.z.ToString("0.0", inv)}) | ({focus.x.ToString("0.0", inv)}, {focus.z.ToString("0.0", inv)}) | {tilt:F0}° | {cameras[c].FieldOfView:F0}° | {perCamera[c]} | {unique} |");
            }
            if (notes.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("## 설치 지점 참고");
                sb.AppendLine();
                foreach (var note in notes) sb.AppendLine("- " + note);
            }
            sb.AppendLine();
            sb.AppendLine("## 사각 묶음 (큰 것부터, 최대 40개)");
            sb.AppendLine();
            var clusters = Clusters(grid, i => grid.Reachable[i] && grid.Coverage[i] == 0 && !grid.Structural[i]);
            if (clusters.Count == 0) sb.AppendLine("없음.");
            else
            {
                sb.AppendLine("| # | 칸 | 면적 m² | x 범위 | z 범위 | 중심 (x, z) |");
                sb.AppendLine("| --- | --- | --- | --- | --- | --- |");
                var n = 0;
                foreach (var cluster in clusters.Take(40))
                {
                    n++;
                    var xs = cluster.Select(i => grid.WorldX(i % grid.Width)).ToArray();
                    var zs = cluster.Select(i => grid.WorldZ(i / grid.Width)).ToArray();
                    sb.AppendLine($"| {n} | {cluster.Count} | {cluster.Count * Cell * Cell:F2} | {xs.Min():F1} ~ {xs.Max():F1} | {zs.Min():F1} ~ {zs.Max():F1} | ({xs.Average():F1}, {zs.Average():F1}) |");
                }
            }
            sb.AppendLine();
            sb.AppendLine("## 가림 상자 안 사각 (카메라를 더 놓아도 안 보임)");
            sb.AppendLine();
            var inside = Clusters(grid, i => grid.Reachable[i] && grid.Coverage[i] == 0 && grid.Structural[i]);
            if (inside.Count == 0) sb.AppendLine("없음.");
            foreach (var cluster in inside.Take(15))
            {
                var xs = cluster.Select(i => grid.WorldX(i % grid.Width)).ToArray();
                var zs = cluster.Select(i => grid.WorldZ(i / grid.Width)).ToArray();
                var name = occluders.FirstOrDefault(o => o.Bounds.Contains(new Vector3(xs.Average(), o.Bounds.center.y, zs.Average())) && o.Bounds.size.x * o.Bounds.size.z < 100f).Name;
                sb.AppendLine($"- {cluster.Count}칸 x {xs.Min():F1}~{xs.Max():F1} z {zs.Min():F1}~{zs.Max():F1} ({name ?? "?"})");
            }
            sb.AppendLine();
            sb.AppendLine("## 닿지 않아 뺀 바닥 묶음 (큰 것부터, 최대 15개)");
            sb.AppendLine();
            var excluded = Clusters(grid, i => grid.Kind[i] == CellKind.Floor && !grid.Reachable[i]);
            if (excluded.Count == 0) sb.AppendLine("없음.");
            else
            {
                sb.AppendLine("| # | 칸 | x 범위 | z 범위 |");
                sb.AppendLine("| --- | --- | --- | --- |");
                var n = 0;
                foreach (var cluster in excluded.Take(15))
                {
                    n++;
                    var xs = cluster.Select(i => grid.WorldX(i % grid.Width)).ToArray();
                    var zs = cluster.Select(i => grid.WorldZ(i / grid.Width)).ToArray();
                    sb.AppendLine($"| {n} | {cluster.Count} | {xs.Min():F1} ~ {xs.Max():F1} | {zs.Min():F1} ~ {zs.Max():F1} |");
                }
            }
            sb.AppendLine();
            sb.AppendLine("## 발자국이 큰 가림 상자 (런타임이 시선을 막는다고 보는 것, 큰 것부터 12개)");
            sb.AppendLine();
            sb.AppendLine("| 이름 | x 범위 | z 범위 | y 범위 | 발자국 m² |");
            sb.AppendLine("| --- | --- | --- | --- | --- |");
            foreach (var o in occluders.OrderByDescending(o => o.Bounds.size.x * o.Bounds.size.z).Take(12))
            {
                var b = o.Bounds;
                sb.AppendLine($"| {o.Name} | {b.min.x:F1} ~ {b.max.x:F1} | {b.min.z:F1} ~ {b.max.z:F1} | {b.min.y:F2} ~ {b.max.y:F2} | {b.size.x * b.size.z:F1} |");
            }
            sb.AppendLine();
            sb.AppendLine($"## {Current.Label} 기준점");
            sb.AppendLine();
            foreach (var t in Landmarks())
                sb.AppendLine($"- {t.name}: ({t.position.x.ToString("0.0", inv)}, {t.position.y.ToString("0.0", inv)}, {t.position.z.ToString("0.0", inv)})");
            return sb.ToString();
        }

        private static List<List<int>> Clusters(Grid grid, Func<int, bool> member)
        {
            var seen = new bool[grid.Kind.Length];
            var clusters = new List<List<int>>();
            var stack = new Stack<int>();
            for (var start = 0; start < grid.Kind.Length; start++)
            {
                if (seen[start] || !member(start)) continue;
                var cluster = new List<int>();
                stack.Push(start); seen[start] = true;
                while (stack.Count > 0)
                {
                    var i = stack.Pop();
                    cluster.Add(i);
                    var cx = i % grid.Width; var cz = i / grid.Width;
                    foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    {
                        var nx = cx + dx; var nz = cz + dz;
                        if (nx < 0 || nz < 0 || nx >= grid.Width || nz >= grid.Height) continue;
                        var j = grid.Index(nx, nz);
                        if (seen[j] || !member(j)) continue;
                        seen[j] = true; stack.Push(j);
                    }
                }
                clusters.Add(cluster);
            }
            return clusters.OrderByDescending(c => c.Count).ToList();
        }

        private static IEnumerable<Transform> Landmarks()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            return scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                // 층마다 이 층에 선 것만. 1층 스폰(y 1.4)과 2층 스폰(y 6.06)이 같은 이름을 쓴다.
                .Where(t => (t.name.StartsWith("SpawnPoint_") || t.name == "ShredderSpot") &&
                            t.position.y > FloorY - 0.5f && t.position.y < FloorY + 2.5f)
                .OrderBy(t => t.name);
        }

        private static string CeilingSummary(Grid grid)
        {
            var heights = grid.Ceiling.Where((h, i) => grid.Reachable[i] && !float.IsNaN(h)).OrderBy(h => h).ToArray();
            if (heights.Length == 0) return "천장을 찾지 못함";
            return $"최저 {heights[0]:F2} · 중앙값 {heights[heights.Length / 2]:F2} · 최고 {heights[heights.Length - 1]:F2} (바닥 {FloorY})";
        }

        // ---- 그림 ----

        private static Texture2D Draw(Grid grid, List<Occluder> occluders, HighlightCctvCamera[] cameras, bool showCoverage)
        {
            var w = grid.Width * PixelsPerCell;
            var h = grid.Height * PixelsPerCell;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color32[w * h];
            for (var cz = 0; cz < grid.Height; cz++)
            for (var cx = 0; cx < grid.Width; cx++)
            {
                var i = grid.Index(cx, cz);
                Color32 colour = grid.Kind[i] switch
                {
                    CellKind.Void => new Color32(200, 210, 225, 255),
                    CellKind.Wall => new Color32(40, 40, 45, 255),
                    CellKind.Blocked => new Color32(160, 160, 160, 255),
                    _ => grid.Reachable[i] ? new Color32(250, 250, 250, 255) : new Color32(225, 235, 215, 255)
                };
                if (showCoverage && grid.Reachable[i])
                    colour = grid.Coverage[i] == 0 ? (grid.Structural[i] ? new Color32(240, 170, 60, 255) : new Color32(230, 60, 60, 255))
                        : grid.Coverage[i] == 1 ? new Color32(190, 230, 150, 255)
                        : new Color32(110, 200, 110, 255);
                FillCell(pixels, w, cx, cz, colour);
            }
            // 가림 상자 발자국: 런타임이 시선을 막는다고 보는 범위를 연한 빗금으로 보인다. 큰 것은 보고서로 뺀다.
            foreach (var o in occluders)
            {
                var b = o.Bounds;
                if (b.min.y > FloorY + 1.6f || b.size.x * b.size.z > 30f) continue;
                var cx0 = Mathf.Clamp(Mathf.FloorToInt((b.min.x - X0) / Cell), 0, grid.Width - 1);
                var cx1 = Mathf.Clamp(Mathf.FloorToInt((b.max.x - X0) / Cell), 0, grid.Width - 1);
                var cz0 = Mathf.Clamp(Mathf.FloorToInt((b.min.z - Z0) / Cell), 0, grid.Height - 1);
                var cz1 = Mathf.Clamp(Mathf.FloorToInt((b.max.z - Z0) / Cell), 0, grid.Height - 1);
                for (var cz = cz0; cz <= cz1; cz++)
                for (var cx = cx0; cx <= cx1; cx++)
                {
                    if (grid.Kind[grid.Index(cx, cz)] != CellKind.Floor) continue;
                    var px = cx * PixelsPerCell; var pz = cz * PixelsPerCell;
                    for (var k = 0; k < PixelsPerCell; k++) Blend(pixels, w, px + k, pz + k, new Color32(90, 90, 120, 90));
                }
            }
            // 1 m 격자, 5 m 굵은 선과 좌표 숫자.
            for (var x = Mathf.CeilToInt(X0); x <= Mathf.FloorToInt(X1); x++)
            {
                var px = Mathf.RoundToInt((x - X0) / Cell * PixelsPerCell);
                var major = x % 5 == 0;
                for (var py = 0; py < h; py++) Blend(pixels, w, px, py, major ? new Color32(0, 0, 0, 140) : new Color32(0, 0, 0, 40));
                if (major) DrawNumber(pixels, w, px + 3, 3, x, new Color32(0, 0, 0, 255));
            }
            for (var z = Mathf.CeilToInt(Z0); z <= Mathf.FloorToInt(Z1); z++)
            {
                var py = Mathf.RoundToInt((z - Z0) / Cell * PixelsPerCell);
                var major = z % 5 == 0;
                for (var px = 0; px < w; px++) Blend(pixels, w, px, py, major ? new Color32(0, 0, 0, 140) : new Color32(0, 0, 0, 40));
                if (major) DrawNumber(pixels, w, 3, py + 3, z, new Color32(0, 0, 0, 255));
            }
            // 기준점: 스폰 노랑, 파쇄기 자홍.
            foreach (var t in Landmarks())
            {
                var colour = t.name == "ShredderSpot" ? new Color32(220, 40, 200, 255) : new Color32(240, 200, 30, 255);
                FillCircle(pixels, w, ToPixel(t.position), 5, colour);
            }
            if (cameras != null)
            {
                for (var c = 0; c < cameras.Length; c++)
                {
                    var t = cameras[c].transform;
                    var origin = ToPixel(t.position);
                    var flat = new Vector3(t.forward.x, 0f, t.forward.z);
                    if (flat.sqrMagnitude < 1e-4f) flat = Vector3.forward;
                    flat.Normalize();
                    var halfHeight = Mathf.Tan(cameras[c].FieldOfView * Mathf.Deg2Rad * 0.5f) * FrustumShrink;
                    var halfHorizontal = Mathf.Atan(halfHeight * Aspect) * Mathf.Rad2Deg;
                    var reach = MaxViewDistance / Cell * PixelsPerCell;
                    foreach (var sign in new[] { -1f, 1f })
                    {
                        var dir = Quaternion.Euler(0f, sign * halfHorizontal, 0f) * flat;
                        DrawLine(pixels, w, origin, origin + new Vector2(dir.x, dir.z) * reach, new Color32(30, 90, 220, 150));
                    }
                    DrawLine(pixels, w, origin, origin + new Vector2(flat.x, flat.z) * (PixelsPerCell * 6), new Color32(30, 90, 220, 255));
                    FillCircle(pixels, w, origin, 5, new Color32(30, 90, 220, 255));
                    DrawNumber(pixels, w, Mathf.RoundToInt(origin.x) + 7, Mathf.RoundToInt(origin.y) + 4, c + 1, new Color32(10, 40, 160, 255));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        private static Vector2 ToPixel(Vector3 world) =>
            new Vector2((world.x - X0) / Cell * PixelsPerCell, (world.z - Z0) / Cell * PixelsPerCell);

        private static void FillCell(Color32[] pixels, int w, int cx, int cz, Color32 colour)
        {
            for (var y = 0; y < PixelsPerCell; y++)
            for (var x = 0; x < PixelsPerCell; x++)
                pixels[(cz * PixelsPerCell + y) * w + cx * PixelsPerCell + x] = colour;
        }

        private static void Blend(Color32[] pixels, int w, int x, int y, Color32 colour)
        {
            var h = pixels.Length / w;
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            var i = y * w + x;
            var a = colour.a / 255f;
            var p = pixels[i];
            pixels[i] = new Color32(
                (byte)Mathf.RoundToInt(p.r + (colour.r - p.r) * a),
                (byte)Mathf.RoundToInt(p.g + (colour.g - p.g) * a),
                (byte)Mathf.RoundToInt(p.b + (colour.b - p.b) * a), 255);
        }

        private static void FillCircle(Color32[] pixels, int w, Vector2 centre, int radius, Color32 colour)
        {
            for (var dy = -radius; dy <= radius; dy++)
            for (var dx = -radius; dx <= radius; dx++)
                if (dx * dx + dy * dy <= radius * radius)
                    Blend(pixels, w, Mathf.RoundToInt(centre.x) + dx, Mathf.RoundToInt(centre.y) + dy, colour);
        }

        private static void DrawLine(Color32[] pixels, int w, Vector2 from, Vector2 to, Color32 colour)
        {
            var h = pixels.Length / w;
            var steps = Mathf.CeilToInt(Vector2.Distance(from, to));
            for (var s = 0; s <= steps; s++)
            {
                var p = Vector2.Lerp(from, to, steps == 0 ? 0f : (float)s / steps);
                var x = Mathf.RoundToInt(p.x); var y = Mathf.RoundToInt(p.y);
                if (x < 0 || y < 0 || x >= w || y >= h) return;
                Blend(pixels, w, x, y, colour);
            }
        }

        // 3x5 숫자 글꼴. 좌표와 카메라 번호를 그림에 적기 위한 최소한의 것이다.
        private static readonly string[] Digits =
        {
            "111101101101111", "010110010010111", "111001111100111", "111001111001111", "101101111001001",
            "111100111001111", "111100111101111", "111001001001001", "111101111101111", "111101111001111"
        };

        private static void DrawNumber(Color32[] pixels, int w, int x, int y, int value, Color32 colour)
        {
            const int scale = 2;
            var text = value.ToString(CultureInfo.InvariantCulture);
            var cursor = x;
            foreach (var ch in text)
            {
                if (ch == '-')
                {
                    for (var k = 0; k < 3 * scale; k++)
                    for (var s = 0; s < scale; s++) Blend(pixels, w, cursor + k, y + 2 * scale + s, colour);
                    cursor += 4 * scale;
                    continue;
                }
                var glyph = Digits[ch - '0'];
                for (var row = 0; row < 5; row++)
                for (var col = 0; col < 3; col++)
                {
                    if (glyph[row * 3 + col] != '1') continue;
                    for (var sy = 0; sy < scale; sy++)
                    for (var sx = 0; sx < scale; sx++)
                        Blend(pixels, w, cursor + col * scale + sx, y + (4 - row) * scale + sy, colour);
                }
                cursor += 4 * scale;
            }
        }

        private static string Save(Texture2D texture, string fileName)
        {
            var folder = Path.Combine(ProjectRoot(), OutputFolder);
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, fileName);
            var bytes = texture.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(texture);
            try { File.WriteAllBytes(path, bytes); }
            catch (IOException e)
            {
                // 미리보기 프로그램이 그림을 메모리 매핑으로 잡고 있으면 덮어쓰기가 막힌다(Win32 1224). 다른 이름으로 쓴다.
                var fallback = Path.Combine(folder, Path.GetFileNameWithoutExtension(fileName) + "-" + DateTime.Now.ToString("HHmmss") + ".png");
                File.WriteAllBytes(fallback, bytes);
                Debug.LogWarning($"[MansionCctv] {fileName} 을 덮어쓸 수 없어 {Path.GetFileName(fallback)} 으로 저장했습니다. ({e.Message})");
                return fallback;
            }
            return path;
        }

        private static string ProjectRoot() => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    }

    /// <summary>씬 뷰에 CCTV 지점의 시야 부채꼴과 이름을 항상 그린다. 저택 미리보기와 마트 프리팹 둘 다에 적용된다.</summary>
    public static class HighlightCctvGizmos
    {
        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected | GizmoType.Pickable)]
        private static void Draw(HighlightCctvCamera camera, GizmoType type)
        {
            var t = camera.transform;
            var selected = (type & GizmoType.Selected) != 0;
            var colour = selected ? new Color(1f, 0.6f, 0.1f) : new Color(0.1f, 0.8f, 1f);
            const float depth = 4f;
            var halfHeight = depth * Mathf.Tan(camera.FieldOfView * Mathf.Deg2Rad * 0.5f);
            var halfWidth = halfHeight * 16f / 9f;
            var centre = t.position + t.forward * depth;
            var corners = new[]
            {
                centre + t.up * halfHeight - t.right * halfWidth, centre + t.up * halfHeight + t.right * halfWidth,
                centre - t.up * halfHeight + t.right * halfWidth, centre - t.up * halfHeight - t.right * halfWidth
            };
            Gizmos.color = colour;
            Gizmos.DrawSphere(t.position, 0.15f);
            for (var i = 0; i < 4; i++)
            {
                Gizmos.DrawLine(t.position, corners[i]);
                Gizmos.DrawLine(corners[i], corners[(i + 1) % 4]);
            }
            Gizmos.color = new Color(colour.r, colour.g, colour.b, 0.5f);
            Gizmos.DrawLine(t.position, t.position + t.forward * 12f);
            var style = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = colour } };
            Handles.Label(t.position + Vector3.up * 0.3f, camera.LocationName, style);
        }
    }
}
