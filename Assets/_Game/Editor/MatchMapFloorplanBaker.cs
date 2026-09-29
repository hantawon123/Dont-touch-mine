using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.Editor
{
    /// <summary>
    /// 매치 맵을 위에서 내려다본 평면도 PNG 로 굽는다 (S15P21D205-1010).
    ///
    /// 관리 화면 분석 탭의 히트맵이 좌표를 격자로 그리는데, 바닥이 빈 회색이라 그 칸이 맵의
    /// 어디인지 알 수 없다. 이 도구가 그 바닥을 만든다.
    ///
    /// <para>
    /// <b>그림과 격자가 어긋나지 않는 것이 이 도구의 요점이다.</b> 그래서 PNG 옆에 그 그림이
    /// 덮는 월드 사각형을 같은 이름의 JSON 으로 함께 쓴다. 관리 화면은 그 값을 읽어 격자를
    /// 맞추므로, 사람이 두 곳에 같은 숫자를 적을 일이 없다. 어긋나면 사람이 벽 안에 서 있는
    /// 것처럼 보이는데 그건 틀렸다는 티가 안 나서 더 나쁘다.
    /// </para>
    ///
    /// <para>
    /// <b>천장을 잘라야 한다.</b> 그냥 위에서 찍으면 지붕만 나온다. 직교 카메라를 맵 위에
    /// 두고 near 클립을 잘라 낼 높이까지 밀어, 그 아래만 찍는다.
    /// </para>
    /// </summary>
    public sealed class MatchMapFloorplanBaker : EditorWindow
    {
        private const string MenuPath = "Game/Match Map/Bake Analytics Floorplan...";

        /// <summary>경계 콜라이더는 실측 바깥으로 튀어나와 있어 재는 대상에서 뺀다.</summary>
        private const string BoundaryName = "Boundary";

        /// <summary>프로젝트 루트 기준 출력 폴더. 관리 화면이 /admin/maps/ 로 읽는 자리다.</summary>
        private const string DefaultOutput = "backend/app/src/main/resources/static/admin/maps";

        /// <summary>한 변의 픽셀 상한. 넘으면 GPU 와 브라우저 양쪽에서 부담이 된다.</summary>
        private const int MaxPixels = 4096;

        /// <summary>
        /// 배경색과 다른 픽셀이 이 비율보다 적으면 <b>아무것도 안 찍힌 것</b>으로 본다 (S15P21D205-1085).
        ///
        /// <para>
        /// 2026-09-19 저택 첫 굽기가 배경색 한 가지와 마젠타 180 픽셀뿐인 PNG 를 냈다. 셰이더가 아직
        /// 컴파일 중이면 그 물건은 그려지지 않고 조용히 빠지는데, 결과가 "빈 맵"처럼 보여서 히트맵에
        /// 깔고 나서야 이상한 줄 알게 된다. 그런 그림은 쓰지 않는 편이 낫다.
        /// </para>
        /// </summary>
        private const float MinInkRatio = 0.01f;

        /// <summary>셰이더 컴파일을 기다리는 상한(초). 이 안에 안 끝나면 그냥 찍고 빈 그림 검사에 맡긴다.</summary>
        private const double ShaderWaitSeconds = 120;

        private GameObject root;
        private string mapId = "supermarket";
        private string outputFolder = DefaultOutput;
        private float cutHeight = 3f;
        private int pixelsPerMeter = 24;
        private Color background = new Color(0.97f, 0.97f, 0.98f, 1f);

        /// <summary>
        /// 이 그림이 어느 층인지 (S15P21D205-1085). 저택처럼 층이 x·z 로 겹치는 맵에서만 켠다.
        ///
        /// <para>
        /// 켜면 y 범위를 JSON 에 함께 쓰고, 관리 화면이 그 범위 밖의 좌표를 이 그림 위에 그리지
        /// 않는다. 저택은 본관 1층 위에 2층이 있고 그 위 다락이 <b>대기 구역</b>이라, 층을 안 나누면
        /// 자기 차례를 기다리며 몇 분씩 서 있는 다락이 제일 뜨거운 칸이 된다.
        /// </para>
        /// </summary>
        private bool oneFloor;
        private string floorLabel = "1층";
        private float floorY0;
        private float floorY1 = 5f;

        /// <summary>
        /// 자주 굽는 맵·층의 값 (S15P21D205-1085, 2층 1092).
        ///
        /// <para>
        /// 실제로 구운 값이며 <c>maps/&lt;id&gt;.json</c> 에 그대로 남아 있다. 저택은 층이 겹쳐
        /// 층마다 파일이 따로다 - 그림 하나에 두 층을 담으면 히트맵이 사람을 벽 안에 세운다.
        /// </para>
        /// </summary>
        private readonly struct Preset
        {
            public Preset(string name, string mapId, float x0, float z0, float x1, float z1,
                float cut, string floorLabel = null, float floorY0 = 0f, float floorY1 = 0f)
            {
                Name = name; MapId = mapId; X0 = x0; Z0 = z0; X1 = x1; Z1 = z1;
                Cut = cut; FloorLabel = floorLabel; FloorY0 = floorY0; FloorY1 = floorY1;
            }

            public string Name { get; }
            public string MapId { get; }
            public float X0 { get; }
            public float Z0 { get; }
            public float X1 { get; }
            public float Z1 { get; }
            public float Cut { get; }
            public string FloorLabel { get; }
            public float FloorY0 { get; }
            public float FloorY1 { get; }
        }

        private static readonly Preset[] Presets =
        {
            new Preset("마트", "supermarket", -26f, -42f, 22f, 9.5f, 3f),
            // 저택 1층: 자를 높이는 2층 바닥 5.5 아래, 층 범위는 1층 바닥 1.01 ~ 2층 바닥.
            new Preset("저택 1층", "mansion", -15f, -34f, 15.2f, -8.5f, 3f, "1층", 0.7f, 5f),
            // 저택 2층: 자를 높이는 2층 천장(바닥 5.5 + 천고 4.5) 아래, 층 범위는 다락 차단 판 9.8 까지.
            new Preset("저택 2층", "mansion-2f", -15f, -34f, 15.2f, -8.5f, 9.5f, "2층", 5f, 9.8f),
        };

        private void Apply(Preset preset)
        {
            mapId = preset.MapId;
            x0 = preset.X0; z0 = preset.Z0; x1 = preset.X1; z1 = preset.Z1;
            cutHeight = preset.Cut;
            oneFloor = !string.IsNullOrEmpty(preset.FloorLabel);
            if (oneFloor)
            {
                floorLabel = preset.FloorLabel;
                floorY0 = preset.FloorY0;
                floorY1 = preset.FloorY1;
            }
            measured = x1 > x0 && z1 > z0;
            measureNote = $"「{preset.Name}」 값을 넣었습니다. 환경 루트와 씬이 그 맵인지 확인하고 구우세요.";
            GUI.FocusControl(null);
        }

        private bool measured;
        private float x0, z0, x1, z1, floorY, roofY;
        private string measureNote = string.Empty;

        [MenuItem(MenuPath)]
        public static void Open()
        {
            var window = GetWindow<MatchMapFloorplanBaker>(true, "Analytics Floorplan", true);
            window.minSize = new Vector2(460, 320);
            window.PickRoot();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "관리 화면 분석 탭의 히트맵 바닥에 깔 평면도를 굽습니다.\n" +
                "PNG 와 함께 그 그림이 덮는 월드 사각형을 JSON 으로 씁니다. 관리 화면이 그 값을 읽습니다.",
                MessageType.None);

            EditorGUI.BeginChangeCheck();
            root = (GameObject)EditorGUILayout.ObjectField("환경 루트", root, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck()) measured = false;

            mapId = EditorGUILayout.TextField("맵 id", mapId);
            outputFolder = EditorGUILayout.TextField("출력 폴더", outputFolder);

            // 창을 다시 열면 맵 id 가 기본값으로 돌아간다. 2026-09-19 에 씬과 범위만 저택으로 바꾸고
            // 구워서 마트 그림을 덮어쓴 적이 있다(S15P21D205-1085). 값을 한 번에 넣는 단추를 둔다.
            EditorGUILayout.LabelField("자주 굽는 값", EditorStyles.miniBoldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                foreach (var preset in Presets)
                    if (GUILayout.Button(preset.Name)) Apply(preset);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("굽는 범위", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "이 사각형이 그림의 범위이면서 히트맵 격자의 범위가 됩니다. "
                + "좁으면 그 밖의 좌표가 조용히 버려지고 화면에는 '거기 아무도 안 갔다'로 보입니다. "
                + "넓으면 여백이 생길 뿐이니 넓은 쪽이 안전합니다.",
                MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("경계로 재기")) Measure(false);
                if (GUILayout.Button("씬 전체로 재기")) Measure(true);
            }
            if (!string.IsNullOrEmpty(measureNote)) EditorGUILayout.HelpBox(measureNote, MessageType.Info);

            // 재기는 출발점일 뿐이고 최종 값은 사람이 정합니다. 맵마다 경계 오브젝트가 무엇을
            // 감싸고 있는지가 달라서, 잰 값을 그대로 믿으면 반쪽만 구워집니다.
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("가로 x");
                x0 = EditorGUILayout.FloatField(x0);
                EditorGUILayout.LabelField("~", GUILayout.Width(12));
                x1 = EditorGUILayout.FloatField(x1);
                EditorGUILayout.LabelField($"{x1 - x0:F1} m", GUILayout.Width(60));
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("세로 z");
                z0 = EditorGUILayout.FloatField(z0);
                EditorGUILayout.LabelField("~", GUILayout.Width(12));
                z1 = EditorGUILayout.FloatField(z1);
                EditorGUILayout.LabelField($"{z1 - z0:F1} m", GUILayout.Width(60));
            }
            measured = x1 > x0 && z1 > z0;
            if (!measured) EditorGUILayout.HelpBox("가로와 세로 모두 오른쪽 값이 더 커야 합니다.", MessageType.Warning);

            EditorGUILayout.Space();
            cutHeight = EditorGUILayout.FloatField(
                new GUIContent("잘라 낼 높이 y", "이 높이보다 위는 찍지 않습니다. 지붕과 천장 조명을 지우는 값입니다."),
                cutHeight);
            pixelsPerMeter = EditorGUILayout.IntSlider("1 m 당 픽셀", pixelsPerMeter, 4, 64);
            background = EditorGUILayout.ColorField("배경", background);

            EditorGUILayout.Space();
            oneFloor = EditorGUILayout.ToggleLeft(
                new GUIContent("한 층만 담은 그림",
                    "층이 x·z 로 겹치는 맵에서 켭니다. 이 y 범위 밖의 좌표는 히트맵이 버립니다."),
                oneFloor);
            if (oneFloor)
            {
                EditorGUILayout.HelpBox(
                    "저택처럼 위아래로 겹치는 맵입니다. 여기 적은 y 범위를 JSON 에 함께 써서 다른 층의 "
                    + "좌표가 이 그림 위에 얹히지 않게 합니다. 위층이 대기 구역이면 특히 중요합니다 - "
                    + "가만히 서 있는 사람들이 제일 뜨거운 칸이 됩니다.",
                    MessageType.None);
                floorLabel = EditorGUILayout.TextField("층 이름", floorLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PrefixLabel("층 높이 y");
                    floorY0 = EditorGUILayout.FloatField(floorY0);
                    EditorGUILayout.LabelField("~", GUILayout.Width(12));
                    floorY1 = EditorGUILayout.FloatField(floorY1);
                }
                if (floorY1 <= floorY0)
                {
                    EditorGUILayout.HelpBox("층 높이도 오른쪽 값이 더 커야 합니다.", MessageType.Warning);
                }
            }

            if (measured)
            {
                var (w, h) = PixelSize();
                EditorGUILayout.LabelField("그림 크기", $"{w} x {h} px");
            }

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(!measured))
            {
                if (GUILayout.Button("굽기", GUILayout.Height(28))) Bake();
            }
        }

        /// <summary>선택한 것을 쓰고, 없으면 씬에서 환경 루트로 보이는 것을 찾는다.</summary>
        private void PickRoot()
        {
            root = Selection.activeGameObject;
            if (root != null) return;

            foreach (var name in new[] { "MartEnvironment", "SupermarketEnvironment", "Environment" })
            {
                var found = GameObject.Find(name);
                if (found == null) continue;
                root = found;
                return;
            }

            // 루트를 못 찾으면 씬 전체를 잰다. 씬의 최상위 오브젝트를 모두 훑는 경로는 Measure 에 있다.
            var scene = EditorSceneManager.GetActiveScene();
            mapId = string.IsNullOrEmpty(scene.name) ? mapId : scene.name.ToLowerInvariant();
        }

        /// <summary>
        /// 렌더러의 XZ 범위를 잰다. 경계 콜라이더 아래와 꺼진 렌더러는 뺀다.
        ///
        /// <para>
        /// 이 값이 그림의 범위이자 히트맵 격자의 범위가 된다. 그래서 <b>놀 수 있는 곳보다 넓게</b>
        /// 잡아야 한다. 좁게 잡으면 그 밖의 좌표가 격자에서 통째로 버려지는데, 화면에는 그냥
        /// "거기 아무도 안 갔다"로 보인다.
        /// </para>
        /// </summary>
        private void Measure(bool wholeScene)
        {
            // 경계 오브젝트는 맵마다 감싸는 것이 다르다. 마트에서는 대기 구역만 감싸고 있어서
            // 그대로 믿으면 반쪽만 구워진다. 그래서 잰 값은 출발점이고 최종 값은 사람이 정한다.
            if ((!wholeScene && TryMeasureBoundary(out var bounds, out var source))
                || TryMeasureRenderers(out bounds, out source))
            {
                x0 = bounds.min.x;
                x1 = bounds.max.x;
                z0 = bounds.min.z;
                z1 = bounds.max.z;
                floorY = bounds.min.y;
                roofY = bounds.max.y;
                measured = true;
                measureNote = $"{source} 기준으로 채웠습니다. 아는 지점과 맞는지 보고 고치세요. "
                              + $"이 씬의 렌더러는 y {floorY:F1} ~ {roofY:F1} 에 있습니다.";

                // 처음 열었을 때 쓸 만한 값을 넣어 준다. 사람이 보고 고치면 된다.
                if (cutHeight <= floorY || cutHeight >= roofY) cutHeight = Mathf.Round((floorY + roofY) * 0.5f);
                return;
            }

            measured = false;
            EditorUtility.DisplayDialog(
                "Analytics Floorplan",
                "잴 것을 찾지 못했습니다. '" + BoundaryName + "' 오브젝트가 있는 루트를 고르거나, "
                + "렌더러가 딸린 오브젝트를 고르세요.",
                "확인");
        }

        /// <summary>
        /// 경계 콜라이더의 범위.
        ///
        /// <para>
        /// 마트는 소품이 씬 루트에 평평하게 놓여 있고 환경 루트 아래에는 경계와 조명만 있다.
        /// 그래서 루트 아래 렌더러를 재면 아무것도 안 나온다. 경계는 도달 영역의 껍질을 따라
        /// 세운 것이라 오히려 이쪽이 <b>놀 수 있는 곳</b>에 더 가깝다.
        /// </para>
        /// </summary>
        private bool TryMeasureBoundary(out Bounds bounds, out string source)
        {
            bounds = default;
            source = string.Empty;

            var boundary = root != null ? root.transform.Find(BoundaryName) : null;
            if (boundary == null)
            {
                var found = GameObject.Find(BoundaryName);
                boundary = found != null ? found.transform : null;
            }
            if (boundary == null) return false;

            var colliders = boundary.GetComponentsInChildren<Collider>(true);
            if (colliders.Length == 0) return false;

            bounds = colliders[0].bounds;
            foreach (var c in colliders) bounds.Encapsulate(c.bounds);
            source = $"경계 콜라이더 {colliders.Length:N0}개";
            return true;
        }

        /// <summary>
        /// 경계가 없을 때 쓰는 폴백. 루트 아래 렌더러, 그것도 없으면 씬 전체 렌더러.
        ///
        /// <para>
        /// 씬 전체로 가면 놀 수 있는 곳 밖의 데모 구조물까지 들어와 범위가 넓어질 수 있다.
        /// 넓은 것은 여백이 생길 뿐이지만, 너무 넓으면 히트맵이 구석에 몰려 작아진다. 그때는
        /// 경계를 먼저 만드는 편이 낫다.
        /// </para>
        /// </summary>
        private bool TryMeasureRenderers(out Bounds bounds, out string source)
        {
            bounds = default;
            source = string.Empty;

            var renderers = Scoped(root).Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
            if (renderers.Length == 0 && root != null) renderers = Scoped(null).Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
            if (renderers.Length == 0) return false;

            bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            source = $"렌더러 {renderers.Length:N0}개";
            return true;
        }

        private static IEnumerable<Renderer> Scoped(GameObject scope)
        {
            var all = scope != null
                ? scope.GetComponentsInChildren<Renderer>(true)
                : EditorSceneManager.GetActiveScene().GetRootGameObjects()
                    .SelectMany(go => go.GetComponentsInChildren<Renderer>(true)).ToArray();
            return all.Where(r => !IsUnder(r.transform, BoundaryName));
        }

        private static bool IsUnder(Transform node, string ancestorName)
        {
            for (var t = node; t != null; t = t.parent)
            {
                if (t.name == ancestorName) return true;
            }
            return false;
        }

        private (int width, int height) PixelSize()
        {
            var w = Mathf.RoundToInt((x1 - x0) * pixelsPerMeter);
            var h = Mathf.RoundToInt((z1 - z0) * pixelsPerMeter);
            var over = Mathf.Max(w, h) / (float)MaxPixels;
            if (over > 1f)
            {
                w = Mathf.RoundToInt(w / over);
                h = Mathf.RoundToInt(h / over);
            }
            return (Mathf.Max(1, w), Mathf.Max(1, h));
        }

        /// <summary>
        /// 직교 카메라로 한 장 찍어 PNG 와 JSON 을 쓴다.
        ///
        /// <para>
        /// 카메라는 지붕 위에 두고 near 클립을 <see cref="cutHeight"/> 까지 밀어 그 아래만 찍는다.
        /// 카메라 자체를 잘라 낼 높이에 두지 않는 이유는, 그러면 그 높이에 걸친 물건이 카메라
        /// 안쪽에 들어와 이상하게 잘리기 때문이다.
        /// </para>
        /// </summary>
        private void Bake()
        {
            var (width, height) = PixelSize();
            var centerX = (x0 + x1) * 0.5f;
            var centerZ = (z0 + z1) * 0.5f;
            // 카메라 높이는 잰 지붕이 아니라 잘라 낼 높이로 정한다. 범위를 손으로 넓히면 잰 값이
            // 더 이상 맞지 않는데, 어차피 잘라 낼 높이 위는 안 찍으므로 그 기준만 있으면 된다.
            const float Above = 50f;
            const float Below = 300f;
            var top = cutHeight + Above;

            var holder = new GameObject("~AnalyticsFloorplanCamera") { hideFlags = HideFlags.HideAndDontSave };
            RenderTexture texture = null;
            var previous = RenderTexture.active;

            // GPU 상주 드로어를 굽는 동안만 끈다 (S15P21D205-1085).
            //
            // PC 품질 설정이 2026-09-16 에 이것을 켰다(S15P21D205-994). 정적 소품을 GPU 가 직접 그리는
            // 경로라, 여기처럼 손으로 만들어 Camera.Render() 로 한 장 찍는 카메라에 그 배치가 따라오는지
            // 확인하지 못했다. 마트를 멀쩡히 구운 2026-09-15 에는 꺼져 있었으므로, 굽는 동안만 끄고
            // 되돌린다. 에셋의 값은 그대로다.
            //
            // <b>빈 그림의 원인은 이쪽이 아니었다.</b> 아래 셰이더 주석을 보라.
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            var drawerMode = pipeline != null ? pipeline.gpuResidentDrawerMode : GPUResidentDrawerMode.Disabled;
            if (pipeline != null) pipeline.gpuResidentDrawerMode = GPUResidentDrawerMode.Disabled;

            try
            {
                var camera = holder.AddComponent<Camera>();
                camera.transform.SetPositionAndRotation(
                    new Vector3(centerX, top, centerZ), Quaternion.Euler(90f, 0f, 0f));
                camera.orthographic = true;
                // 직교 크기는 세로(z) 반지름이다. 가로는 aspect 가 정한다.
                camera.orthographicSize = (z1 - z0) * 0.5f;
                camera.aspect = (x1 - x0) / (z1 - z0);
                camera.nearClipPlane = Above;
                camera.farClipPlane = Above + Below;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = background;
                camera.allowHDR = false;
                camera.allowMSAA = false;

                // 후처리는 끈다. 노출과 색보정이 들어가면 같은 맵을 두 번 구웠을 때 그림이 달라진다.
                var urp = camera.GetUniversalAdditionalCameraData();
                if (urp != null)
                {
                    urp.renderPostProcessing = false;
                    urp.renderShadows = false;
                }

                texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
                {
                    antiAliasing = 4
                };
                camera.targetTexture = texture;

                // 두 번 찍는다 (S15P21D205-1085).
                //
                // 아직 컴파일되지 않은 셰이더를 쓰는 물건은 그려지지 않고 <b>조용히 빠진다.</b>
                // 2026-09-19 에 스크립트가 다시 컴파일된 직후 구운 저택이 두 번 다 배경색뿐인 그림으로
                // 나왔고, 로그를 보면 두 번 모두 그 직전에 셰이더 컴파일러가 여섯 개 떴다. 몇 분 뒤
                // 같은 씬을 다시 구웠을 때는 컴파일러가 뜨지 않았고 98 % 가 칠해졌다.
                //
                // 동기 컴파일로 바꾸는 것만으로는 부족하다. 이미 비동기로 요청되어 큐에 들어간 변형은
                // 그 순간 준비되지 않기 때문이다. 그래서 한 번 찍어 필요한 변형을 요청하게 만들고,
                // 컴파일이 끝나기를 기다린 다음 진짜 한 장을 찍는다. 무엇이 필요한지는 한 번 찍어
                // 보기 전에는 알 수 없어서 기다릴 대상도 알 수 없다.
                var wasAsync = ShaderUtil.allowAsyncCompilation;
                ShaderUtil.allowAsyncCompilation = false;
                try
                {
                    camera.Render();
                    WaitForShaders();
                    camera.Render();
                }
                finally
                {
                    ShaderUtil.allowAsyncCompilation = wasAsync;
                }

                RenderTexture.active = texture;
                var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();

                // 빈 그림은 쓰지 않는다. 쓰면 히트맵 바닥이 그냥 회색이 되는데, 그건 "이 맵은 원래
                // 이렇게 생겼나"로 읽혀서 틀렸다는 티가 안 난다.
                var ink = InkRatio(image, background);
                if (ink < MinInkRatio)
                {
                    DestroyImmediate(image);
                    var inView = CountRenderersInView();
                    Debug.LogError(
                        $"[Floorplan] {mapId}: 배경만 찍혔습니다(칠해진 픽셀 {ink:P2}). "
                        + $"범위 안 렌더러 {inView:N0}개. 파일은 쓰지 않았습니다.");
                    EditorUtility.DisplayDialog(
                        "Analytics Floorplan",
                        "아무것도 안 찍혀서 파일을 쓰지 않았습니다.\n\n"
                        + $"칠해진 픽셀 {ink:P2}, 범위 안 렌더러 {inView:N0}개.\n\n"
                        + (inView > 0
                            ? "렌더러는 범위 안에 있습니다. 그리는 쪽 문제입니다 - 한 번 더 구워 보고, "
                              + "그래도 같으면 씬 뷰 카메라로 이 범위가 보이는지 확인하세요."
                            : "범위 안에 렌더러가 없습니다. 가로·세로 범위나 잘라 낼 높이를 확인하세요."),
                        "확인");
                    return;
                }

                var folder = Path.Combine(ProjectRoot(), outputFolder);
                Directory.CreateDirectory(folder);
                var png = Path.Combine(folder, mapId + ".png");
                File.WriteAllBytes(png, image.EncodeToPNG());
                DestroyImmediate(image);

                File.WriteAllText(Path.Combine(folder, mapId + ".json"), Sidecar(width, height));

                Debug.Log(
                    $"[Floorplan] {mapId}: {width}x{height} px, " +
                    $"x[{x0:F2}, {x1:F2}] z[{z0:F2}, {z1:F2}], 잘라 낸 높이 {cutHeight:F2}, " +
                    $"칠해진 픽셀 {ink:P1} -> {png}");
                EditorUtility.RevealInFinder(png);
            }
            finally
            {
                if (pipeline != null) pipeline.gpuResidentDrawerMode = drawerMode;
                RenderTexture.active = previous;
                if (texture != null)
                {
                    texture.Release();
                    DestroyImmediate(texture);
                }
                DestroyImmediate(holder);
            }
        }

        /// <summary>
        /// 배경색과 다른 픽셀의 비율. 굽은 그림이 비었는지 보는 유일한 값이다.
        ///
        /// <para>
        /// 안티에일리어싱 때문에 딱 맞아떨어지지 않으므로 채널마다 6 만큼은 같은 색으로 본다.
        /// </para>
        /// </summary>
        /// <summary>
        /// 남은 셰이더 컴파일을 기다린다. 에디터가 멈춰 있는 동안이므로 상한을 둔다 - 여기서 포기해도
        /// 아래의 빈 그림 검사가 결과를 붙잡는다.
        /// </summary>
        private static void WaitForShaders()
        {
            var until = DateTime.UtcNow.AddSeconds(ShaderWaitSeconds);
            while (ShaderUtil.anythingCompiling && DateTime.UtcNow < until)
            {
                System.Threading.Thread.Sleep(50);
            }
        }

        private static float InkRatio(Texture2D image, Color background)
        {
            var pixels = image.GetPixels32();
            if (pixels.Length == 0) return 0f;

            var bg = (Color32)background;
            var ink = 0;
            foreach (var p in pixels)
            {
                if (Mathf.Abs(p.r - bg.r) > 6 || Mathf.Abs(p.g - bg.g) > 6 || Mathf.Abs(p.b - bg.b) > 6) ink++;
            }
            return ink / (float)pixels.Length;
        }

        /// <summary>
        /// 범위 안, 잘라 낼 높이 아래에 있는 렌더러 수.
        ///
        /// <para>
        /// 빈 그림이 나왔을 때 "범위가 틀렸나"와 "그리는 쪽이 틀렸나"를 가르는 값이다. 둘은 화면에서
        /// 똑같이 회색 바닥으로 보이지만 고치는 곳이 다르다.
        /// </para>
        /// </summary>
        private int CountRenderersInView()
        {
            var box = new Bounds();
            box.SetMinMax(new Vector3(x0, -1000f, z0), new Vector3(x1, cutHeight, z1));

            var seen = 0;
            foreach (var r in Scoped(null))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (box.Intersects(r.bounds)) seen++;
            }
            return seen;
        }

        /// <summary>
        /// 그림이 덮는 월드 사각형. 관리 화면이 이것을 읽어 격자를 맞춘다.
        ///
        /// <para>
        /// 손으로 쓴다. JsonUtility 는 float 를 현재 문화권으로 찍어서, 소수점이 쉼표인 환경에서
        /// 구우면 브라우저가 못 읽는 JSON 이 나온다.
        /// </para>
        /// </summary>
        private string Sidecar(int width, int height)
        {
            string n(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
            var bakedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
            var scene = EditorSceneManager.GetActiveScene().name;

            // 층은 켰을 때만 씁니다. 없으면 관리 화면이 높이로 거르지 않습니다 - 한 층짜리 맵에서
            // 0 ~ 0 같은 범위가 들어가면 모든 좌표가 버려져 빈 격자로 보입니다.
            var floor = oneFloor && floorY1 > floorY0
                ? "  \"floor\": { \"label\": \"" + floorLabel + "\", "
                  + "\"y0\": " + n(floorY0) + ", \"y1\": " + n(floorY1) + " },\n"
                : string.Empty;

            return "{\n"
                   + "  \"mapId\": \"" + mapId + "\",\n"
                   + "  \"extent\": [" + n(x0) + ", " + n(z0) + ", " + n(x1) + ", " + n(z1) + "],\n"
                   + "  \"cutHeight\": " + n(cutHeight) + ",\n"
                   + floor
                   + "  \"pixels\": [" + width + ", " + height + "],\n"
                   + "  \"scene\": \"" + scene + "\",\n"
                   + "  \"bakedAt\": \"" + bakedAt + "\"\n"
                   + "}\n";
        }

        /// <summary>Assets 의 부모. 출력이 Unity 프로젝트 밖(backend/)이라 이 기준이 필요하다.</summary>
        private static string ProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }
    }
}
