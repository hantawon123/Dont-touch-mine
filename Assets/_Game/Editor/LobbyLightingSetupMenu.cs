using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Editor
{
    /// <summary>
    /// 로비(지하실) 씬의 조명 베이크 준비와 실행을 메뉴로 묶는다.
    /// 1) LightingSettings 에셋 생성·적용, 2) 방 내부 라이트 프로브 격자 생성, 3) 베이크.
    /// 환경(소품·벽·전등)이 바뀌면 3번만 다시 실행해 재베이크한다.
    /// </summary>
    public static class LobbyLightingSetupMenu
    {
        private const string MenuRoot = "Game/Lobby/Lighting/";
        private const string LobbyScenePath = "Assets/_Game/Content/Scenes/Lobby.unity";
        private const string LightingFolder = "Assets/_Game/Content/Lighting";
        private const string LightingSettingsPath = LightingFolder + "/LobbyLighting.lighting";
        private const string EnvironmentRootName = "LobbyBasementEnvironment";
        private const string ProbeGroupName = "LobbyLightProbes";

        // 프로브 격자: 방 내부(경계 콜라이더 안쪽)에 수평 1.5 m, 높이 4단
        private const float ProbeSpacing = 1.5f;
        private static readonly float[] ProbeHeights = { 0.3f, 1.5f, 3.0f, 4.8f };
        private const float ProbeWallMargin = 0.5f;

        [MenuItem(MenuRoot + "1. Setup Lighting Settings And Light Probes")]
        public static void SetupLighting()
        {
            var scene = EnsureLobbySceneOpen();
            if (!scene.IsValid())
            {
                return;
            }

            var settings = GetOrCreateLightingSettings();
            Lightmapping.lightingSettings = settings;

            var root = GameObject.Find(EnvironmentRootName);
            if (root == null)
            {
                EditorUtility.DisplayDialog("Lobby Lighting",
                    $"씬에 '{EnvironmentRootName}' 오브젝트가 없습니다. 지하실 프리팹이 배치된 Lobby 씬에서 실행하세요.",
                    "확인");
                return;
            }

            var probeCount = BuildLightProbes(root);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[Lobby Lighting] Setup done. settings={LightingSettingsPath}, " +
                      $"lightmapper={settings.lightmapper}, resolution={settings.lightmapResolution} texels/unit, " +
                      $"maxSize={settings.lightmapMaxSize}, mixed={settings.mixedBakeMode}, probes={probeCount}. " +
                      "다음: 'Game/Lobby/Lighting/2. Bake' 실행.");
            LogReport(root);
        }

        [MenuItem(MenuRoot + "2. Bake Lighting (Lightmaps + Reflection + Light Probes)")]
        public static void Bake()
        {
            var scene = EnsureLobbySceneOpen();
            if (!scene.IsValid())
            {
                return;
            }

            if (Lightmapping.isRunning)
            {
                Debug.LogWarning("[Lobby Lighting] 이미 베이크가 진행 중입니다.");
                return;
            }

            if (Lightmapping.lightingSettings == null ||
                AssetDatabase.GetAssetPath(Lightmapping.lightingSettings) != LightingSettingsPath)
            {
                Lightmapping.lightingSettings = GetOrCreateLightingSettings();
            }

            var root = GameObject.Find(EnvironmentRootName);
            if (root != null)
            {
                LogReport(root);
            }

            Lightmapping.bakeCompleted -= OnBakeCompleted;
            Lightmapping.bakeCompleted += OnBakeCompleted;

            var started = Lightmapping.BakeAsync();
            Debug.Log(started
                ? "[Lobby Lighting] Bake started. 완료되면 콘솔에 결과가 출력됩니다."
                : "[Lobby Lighting] Bake failed to start. Lighting 창의 오류를 확인하세요.");
        }

        [MenuItem(MenuRoot + "3. Clear Baked Data")]
        public static void ClearBaked()
        {
            var scene = EnsureLobbySceneOpen();
            if (!scene.IsValid())
            {
                return;
            }

            Lightmapping.Clear();
            Lightmapping.ClearLightingDataAsset();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Lobby Lighting] Baked data cleared.");
        }

        [MenuItem(MenuRoot + "Report Lighting State")]
        public static void Report()
        {
            var root = GameObject.Find(EnvironmentRootName);
            if (root == null)
            {
                Debug.LogWarning($"[Lobby Lighting] '{EnvironmentRootName}' not found in active scene.");
                return;
            }

            LogReport(root);
        }

        private static void OnBakeCompleted()
        {
            Lightmapping.bakeCompleted -= OnBakeCompleted;
            var scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            var lightmaps = LightmapSettings.lightmaps;
            var dataAsset = Lightmapping.lightingDataAsset;
            Debug.Log($"[Lobby Lighting] Bake completed. lightmaps={lightmaps.Length}, " +
                      $"lightingData={(dataAsset != null ? AssetDatabase.GetAssetPath(dataAsset) : "none")}. 씬 저장됨.");
        }

        private static Scene EnsureLobbySceneOpen()
        {
            var active = SceneManager.GetActiveScene();
            if (active.path == LobbyScenePath)
            {
                return active;
            }

            if (!EditorUtility.DisplayDialog("Lobby Lighting",
                    $"활성 씬이 Lobby가 아닙니다.\n{LobbyScenePath} 를 열고 진행할까요?",
                    "열기", "취소"))
            {
                return default;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return default;
            }

            return EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);
        }

        private static LightingSettings GetOrCreateLightingSettings()
        {
            var existing = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingSettingsPath);
            if (existing != null)
            {
                return existing;
            }

            if (!AssetDatabase.IsValidFolder(LightingFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Game/Content", "Lighting");
            }

            var settings = new LightingSettings
            {
                name = "LobbyLighting",
                // GI
                bakedGI = true,
                realtimeGI = false,
                lightmapper = LightingSettings.Lightmapper.ProgressiveGPU,
                mixedBakeMode = MixedLightingMode.Shadowmask,
                directionalityMode = LightmapsMode.CombinedDirectional,
                // 해상도: 13×12 m 실내 + 소품 650개. 팩 기본(40)은 과해서 절반으로
                lightmapResolution = 20f,
                lightmapMaxSize = 2048,
                lightmapPadding = 2,
                lightmapCompression = LightmapCompression.NormalQuality,
                // 샘플/바운스: 팩 설정 기준(32/512/3)
                directSampleCount = 32,
                indirectSampleCount = 512,
                environmentSampleCount = 256,
                maxBounces = 3,
                lightProbeSampleCountMultiplier = 4f,
                // AO: 좁은 실내라 짙은 접촉 그림자
                ao = true,
                aoMaxDistance = 0.5f,
                aoExponentDirect = 0f,
                aoExponentIndirect = 1f,
                filteringMode = LightingSettings.FilterMode.Auto,
            };

            AssetDatabase.CreateAsset(settings, LightingSettingsPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Lobby Lighting] Created {LightingSettingsPath}");
            return settings;
        }

        /// <summary>
        /// 경계 콜라이더(Boundary) 안쪽 부피에 격자 프로브를 깔고, 벽·소품 속에 박힌 점은 제외한다.
        /// </summary>
        private static int BuildLightProbes(GameObject root)
        {
            var bounds = MeasureInterior(root);

            var existing = GameObject.Find(ProbeGroupName);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
            }

            var go = new GameObject(ProbeGroupName);
            Undo.RegisterCreatedObjectUndo(go, "Build Lobby Light Probes");
            var group = go.AddComponent<LightProbeGroup>();

            var solids = root.GetComponentsInChildren<Collider>(true)
                .Where(c => c.enabled && !c.isTrigger && !c.transform.IsChildOf(root.transform.Find("Boundary")))
                .ToArray();

            var positions = new System.Collections.Generic.List<Vector3>();
            var minX = bounds.min.x + ProbeWallMargin;
            var maxX = bounds.max.x - ProbeWallMargin;
            var minZ = bounds.min.z + ProbeWallMargin;
            var maxZ = bounds.max.z - ProbeWallMargin;
            var countX = Mathf.Max(2, Mathf.RoundToInt((maxX - minX) / ProbeSpacing) + 1);
            var countZ = Mathf.Max(2, Mathf.RoundToInt((maxZ - minZ) / ProbeSpacing) + 1);

            for (var ix = 0; ix < countX; ix++)
            {
                var x = Mathf.Lerp(minX, maxX, ix / (float)(countX - 1));
                for (var iz = 0; iz < countZ; iz++)
                {
                    var z = Mathf.Lerp(minZ, maxZ, iz / (float)(countZ - 1));
                    foreach (var h in ProbeHeights)
                    {
                        var p = new Vector3(x, bounds.min.y + h, z);
                        if (p.y > bounds.max.y - 0.3f)
                        {
                            continue;
                        }
                        if (IsInsideSolid(p, solids))
                        {
                            continue;
                        }
                        positions.Add(p);
                    }
                }
            }

            group.probePositions = positions.ToArray();
            return positions.Count;
        }

        private static bool IsInsideSolid(Vector3 p, Collider[] solids)
        {
            foreach (var c in solids)
            {
                if (!c.bounds.Contains(p))
                {
                    continue;
                }
                // 메시 콜라이더(비볼록)는 ClosestPoint 미지원 → 바운드로만 판정
                if (c is MeshCollider mc && !mc.convex)
                {
                    if (c.bounds.size.y < 0.3f)
                    {
                        continue; // 바닥 판 같은 얇은 메시는 프로브를 막지 않음
                    }
                    return true;
                }
                if ((c.ClosestPoint(p) - p).sqrMagnitude < 1e-4f)
                {
                    return true;
                }
            }
            return false;
        }

        private static Bounds MeasureInterior(GameObject root)
        {
            var boundary = root.transform.Find("Boundary");
            if (boundary != null && boundary.childCount == 6)
            {
                float Face(string name, System.Func<BoxCollider, float> f)
                {
                    var c = boundary.Find(name).GetComponent<BoxCollider>();
                    return f(c);
                }

                var minX = Face("Bound_West", c => c.transform.TransformPoint(c.center).x + c.size.x * 0.5f);
                var maxX = Face("Bound_East", c => c.transform.TransformPoint(c.center).x - c.size.x * 0.5f);
                var minZ = Face("Bound_South", c => c.transform.TransformPoint(c.center).z + c.size.z * 0.5f);
                var maxZ = Face("Bound_North", c => c.transform.TransformPoint(c.center).z - c.size.z * 0.5f);
                var minY = Face("Bound_Floor", c => c.transform.TransformPoint(c.center).y + c.size.y * 0.5f);
                var maxY = Face("Bound_Ceiling", c => c.transform.TransformPoint(c.center).y - c.size.y * 0.5f);
                var b = new Bounds();
                b.SetMinMax(new Vector3(minX, Mathf.Max(0f, minY), minZ), new Vector3(maxX, maxY, maxZ));
                return b;
            }

            var renderers = root.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled).ToArray();
            var rb = renderers[0].bounds;
            foreach (var r in renderers)
            {
                rb.Encapsulate(r.bounds);
            }
            return rb;
        }

        private static void LogReport(GameObject root)
        {
            var lights = root.GetComponentsInChildren<Light>(true);
            var byMode = lights.GroupBy(l => l.lightmapBakeType)
                .Select(g => $"{g.Key}={g.Count()}");
            var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            var contributeGI = renderers.Count(r =>
                GameObjectUtility.AreStaticEditorFlagsSet(r.gameObject, StaticEditorFlags.ContributeGI));
            var probes = GameObject.Find(ProbeGroupName)?.GetComponent<LightProbeGroup>();
            var reflection = root.GetComponentsInChildren<ReflectionProbe>(true);

            Debug.Log("[Lobby Lighting] Report: " +
                      $"lights={lights.Length} ({string.Join(", ", byMode)}), " +
                      $"renderers={renderers.Length} contributeGI={contributeGI}, " +
                      $"lightProbes={(probes != null ? probes.probePositions.Length : 0)}, " +
                      $"reflectionProbes={reflection.Length} ({string.Join(", ", reflection.Select(r => r.mode))}), " +
                      $"lightmaps={LightmapSettings.lightmaps.Length}, " +
                      $"settings={(Lightmapping.lightingSettings != null ? Lightmapping.lightingSettings.name : "default")}");
        }
    }
}
