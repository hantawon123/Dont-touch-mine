using System;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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

        private GameObject root;
        private string mapId = "supermarket";
        private string outputFolder = DefaultOutput;
        private float cutHeight = 3f;
        private int pixelsPerMeter = 24;
        private Color background = new Color(0.97f, 0.97f, 0.98f, 1f);

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

            EditorGUILayout.Space();
            if (GUILayout.Button("범위 재기")) Measure();

            if (measured)
            {
                EditorGUILayout.LabelField("가로(x)", $"{x0:F2} ~ {x1:F2}  ({x1 - x0:F1} m)");
                EditorGUILayout.LabelField("세로(z)", $"{z0:F2} ~ {z1:F2}  ({z1 - z0:F1} m)");
                EditorGUILayout.LabelField("바닥 / 지붕", $"y {floorY:F2} ~ {roofY:F2}");
                if (!string.IsNullOrEmpty(measureNote)) EditorGUILayout.HelpBox(measureNote, MessageType.Info);
            }

            EditorGUILayout.Space();
            cutHeight = EditorGUILayout.FloatField(
                new GUIContent("잘라 낼 높이 y", "이 높이보다 위는 찍지 않습니다. 지붕과 천장 조명을 지우는 값입니다."),
                cutHeight);
            pixelsPerMeter = EditorGUILayout.IntSlider("1 m 당 픽셀", pixelsPerMeter, 4, 64);
            background = EditorGUILayout.ColorField("배경", background);

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
        private void Measure()
        {
            var renderers = (root != null
                    ? root.GetComponentsInChildren<Renderer>(true)
                    : EditorSceneManager.GetActiveScene().GetRootGameObjects()
                        .SelectMany(go => go.GetComponentsInChildren<Renderer>(true)).ToArray())
                .Where(r => r.enabled && r.gameObject.activeInHierarchy)
                .Where(r => !IsUnder(r.transform, BoundaryName))
                .ToArray();

            if (renderers.Length == 0)
            {
                measured = false;
                EditorUtility.DisplayDialog("Analytics Floorplan", "잴 렌더러가 없습니다. 환경 루트를 고르세요.", "확인");
                return;
            }

            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);

            x0 = bounds.min.x;
            x1 = bounds.max.x;
            z0 = bounds.min.z;
            z1 = bounds.max.z;
            floorY = bounds.min.y;
            roofY = bounds.max.y;
            measured = true;
            measureNote = $"렌더러 {renderers.Length:N0}개 기준입니다. 잘라 낼 높이는 지붕({roofY:F1})보다 낮아야 합니다.";

            // 처음 열었을 때 쓸 만한 값을 넣어 준다. 사람이 보고 고치면 된다.
            if (cutHeight <= floorY || cutHeight >= roofY) cutHeight = Mathf.Round((floorY + roofY) * 0.5f);
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
            var top = roofY + 10f;

            var holder = new GameObject("~AnalyticsFloorplanCamera") { hideFlags = HideFlags.HideAndDontSave };
            RenderTexture texture = null;
            var previous = RenderTexture.active;

            try
            {
                var camera = holder.AddComponent<Camera>();
                camera.transform.SetPositionAndRotation(
                    new Vector3(centerX, top, centerZ), Quaternion.Euler(90f, 0f, 0f));
                camera.orthographic = true;
                // 직교 크기는 세로(z) 반지름이다. 가로는 aspect 가 정한다.
                camera.orthographicSize = (z1 - z0) * 0.5f;
                camera.aspect = (x1 - x0) / (z1 - z0);
                camera.nearClipPlane = Mathf.Max(0.01f, top - cutHeight);
                camera.farClipPlane = top - floorY + 10f;
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
                camera.Render();

                RenderTexture.active = texture;
                var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();

                var folder = Path.Combine(ProjectRoot(), outputFolder);
                Directory.CreateDirectory(folder);
                var png = Path.Combine(folder, mapId + ".png");
                File.WriteAllBytes(png, image.EncodeToPNG());
                DestroyImmediate(image);

                File.WriteAllText(Path.Combine(folder, mapId + ".json"), Sidecar(width, height));

                Debug.Log(
                    $"[Floorplan] {mapId}: {width}x{height} px, " +
                    $"x[{x0:F2}, {x1:F2}] z[{z0:F2}, {z1:F2}], 잘라 낸 높이 {cutHeight:F2} -> {png}");
                EditorUtility.RevealInFinder(png);
            }
            finally
            {
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

            return "{\n"
                   + "  \"mapId\": \"" + mapId + "\",\n"
                   + "  \"extent\": [" + n(x0) + ", " + n(z0) + ", " + n(x1) + ", " + n(z1) + "],\n"
                   + "  \"cutHeight\": " + n(cutHeight) + ",\n"
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
