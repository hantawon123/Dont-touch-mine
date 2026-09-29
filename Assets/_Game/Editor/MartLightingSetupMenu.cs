using System.Collections.Generic;
using System.Linq;
using Game.Client.Interactions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Game.Editor
{
    /// <summary>
    /// 마트 매치 맵(Supermarket) 조명 준비·베이크 메뉴. 로비의 <see cref="LobbyLightingSetupMenu"/>를 마트 규모에 맞게 옮긴 것.
    /// </summary>
    /// <remarks>
    /// 로비 도구를 그대로 못 쓰는 이유: (1) 경계 안 52×75 m로 로비(13×12 m)의 25배라 라이트맵 해상도를 낮춰야 하고,
    /// (2) 씬이 한 루트 아래가 아니라 평평(루트 9천여 개)하고 경계가 6면이 아닌 박스 15개 합집합이며,
    /// (3) 고정 소품에 ContributeGI가 하나도 없어 굽기 전에 표시해야 한다.
    /// <list type="number">
    /// <item>Mark Static: 경계 안 고정 소품(Carryable·Rigidbody 아님)에 ContributeGI·ReflectionProbeStatic·OccludeeStatic.</item>
    /// <item>Convert Lights: 태양광 Mixed, 스팟·포인트 Baked(부드러운 그림자), 창고 구역(x &lt; -40) 포인트 세기 절반.</item>
    /// <item>Setup: 조명 설정 에셋(8 texels/m, 4096), 라이트 프로브 격자, 리플렉션 프로브 격자.</item>
    /// <item>Post-Process: 로비 프로필 복제 → 마트 볼륨, Synty 데모 볼륨 제거, 카메라 PP + SMAA.</item>
    /// <item>Bake / Clear / Report.</item>
    /// </list>
    /// 재베이크 규칙은 로비와 같다: 고정물(선반·벽·전등·Static 플래그) 변경 뒤 반드시 5번 다시 실행. 들 수 있는 상품은 동적이라 무관.
    /// </remarks>
    public static class MartLightingSetupMenu
    {
        private const string MenuRoot = "Game/Match Map/Lighting/";
        private const string ScenePath = "Assets/_Game/Content/Scenes/Supermarket.unity";
        private const string LightingFolder = "Assets/_Game/Content/Lighting";
        private const string LightingSettingsPath = LightingFolder + "/MartLighting.lighting";
        private const string LobbyPostProfilePath = LightingFolder + "/LobbyPostProcess.asset";
        private const string PostProfilePath = LightingFolder + "/MartPostProcess.asset";
        private const string EnvironmentRootName = "MartEnvironment";
        private const string BoundaryName = "Boundary";
        private const string ProbeGroupName = "MartLightProbes";
        private const string ReflectionRootName = "Probes";
        private const string PostVolumeName = "Mart Post Volume";
        private const string SyntyDemoVolumeName = "Global Volume";

        // 라이트 프로브: 로비(1.5 m)보다 넓게. 52×75 m → 약 15×22×3 = 1,000개 안팎.
        private const float ProbeSpacing = 3.5f;
        private static readonly float[] ProbeHeights = { 0.4f, 1.6f, 3.2f };
        private const float ProbeWallMargin = 0.6f;
        private const float ProbeSolidRadius = 0.2f;

        // 리플렉션 프로브 격자(가로×세로 칸 수). 칸마다 Baked 박스 프로브 하나.
        private const int ReflectionColumns = 3;
        private const int ReflectionRows = 4;
        private const int ReflectionResolution = 128;

        /// <summary>이 x보다 서쪽은 창고·하역장.</summary>
        private const float WarehouseMaxX = -40f;
        private const float WarehouseIntensityScale = 0.5f;

        /// <summary>
        /// 창고를 어둡게 둘지. 처음엔 숨기기 유리 구역으로 어둡게 했지만 사용자 확인 결과 "무서운 분위기"가 되어
        /// 2026-09-11 밝게 바꿈(false). true로 되돌리면 창고 포인트 세기 절반·채움 라이트 제외가 다시 적용된다.
        /// </summary>
        private const bool DimWarehouse = false;

        /// <summary>실시간 복귀 시 환경광 세기. 기본 1보다 조금 올려 "베이크 전보다 밝게"(사용자 요청 2026-09-11).</summary>
        private const float RealtimeAmbientIntensity = 1.3f;

        /// <summary>천장 스팟(형광등 역할) 베이크 세기. 팩 기본 2~3은 52×75 m 매장을 굽기엔 어두워서 올린다.</summary>
        private const float BakedSpotIntensity = 5f;

        /// <summary>
        /// 베이크용 환경광. 실내라 스카이박스 빛이 거의 못 들어와 1차 베이크가 전체적으로 어두웠다 →
        /// 형광등 매장처럼 균일한 밑바탕 밝기를 평면 환경광으로 준다.
        /// </summary>
        private static readonly Color BakedAmbient = new(0.55f, 0.57f, 0.60f, 1f);

        /// <summary>간접광(튕긴 빛) 세기. 1차 베이크가 그늘진 곳이 많아 2배로. 설정 에셋이 이미 있어도 매번 덧씌운다.</summary>
        private const float IndirectIntensity = 2f;

        private const int CarryableLayer = 7;

        // ------------------------------------------------------------------ 0. 라이트맵 UV

        /// <summary>
        /// 베이크 대상 메시에 라이트맵 UV(UV2)를 만든다. Synty FBX는 기본으로 UV2가 없어서(3,712/3,757개) 1차 베이크가
        /// 아틀라스 UV0로 구워져 라이트맵이 거의 비어 있었다. FBX는 임포터의 Generate Lightmap UVs를 켜서 재임포트,
        /// 분해 도구가 만든 메시 에셋(Gen_*)은 <see cref="Unwrapping.GenerateSecondaryUVSet(Mesh)"/>로 직접 만든다.
        /// </summary>
        [MenuItem(MenuRoot + "0. Generate Lightmap UVs For Baked Meshes")]
        public static void GenerateLightmapUVs()
        {
            var scene = EnsureSceneOpen();
            if (!scene.IsValid())
            {
                return;
            }

            var importerPaths = new HashSet<string>();
            var meshAssets = new HashSet<Mesh>();
            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (renderer.gameObject.scene != scene ||
                    !GameObjectUtility.AreStaticEditorFlagsSet(renderer.gameObject, StaticEditorFlags.ContributeGI))
                {
                    continue;
                }

                var filter = renderer.GetComponent<MeshFilter>();
                var mesh = filter != null ? filter.sharedMesh : null;
                if (mesh == null || mesh.HasVertexAttribute(VertexAttribute.TexCoord1))
                {
                    continue;
                }

                var path = AssetDatabase.GetAssetPath(mesh);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                if (AssetImporter.GetAtPath(path) is ModelImporter)
                {
                    importerPaths.Add(path);
                }
                else if (path.EndsWith(".asset"))
                {
                    meshAssets.Add(mesh);
                }
            }

            var generated = 0;
            foreach (var mesh in meshAssets)
            {
                Unwrapping.GenerateSecondaryUVSet(mesh);
                EditorUtility.SetDirty(mesh);
                generated++;
            }

            AssetDatabase.SaveAssets();

            var reimported = 0;
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var path in importerPaths)
                {
                    var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                    if (importer.generateSecondaryUV)
                    {
                        continue;
                    }

                    importer.generateSecondaryUV = true;
                    importer.SaveAndReimport();
                    reimported++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            Debug.Log($"[Mart Lighting] 라이트맵 UV 생성: FBX 임포터 {reimported}개(총 {importerPaths.Count}) 재임포트, 메시 에셋 {generated}개 직접 생성. " +
                      "재임포트가 끝나면 5번(Bake)을 다시 실행하세요.");
        }

        // ------------------------------------------------------------------ 1. 정적 플래그

        [MenuItem(MenuRoot + "1. Mark Fixed Props Static For Baking")]
        public static void MarkStaticForBaking()
        {
            var scene = EnsureSceneOpen();
            if (!scene.IsValid())
            {
                return;
            }

            var hasBoundary = TryGetBoundary(out var boundary);
            var marked = 0;
            var skippedOutside = 0;
            var skippedMovable = 0;
            const StaticEditorFlags bakeFlags =
                StaticEditorFlags.ContributeGI | StaticEditorFlags.ReflectionProbeStatic | StaticEditorFlags.OccludeeStatic;

            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (renderer.gameObject.scene != scene || renderer.gameObject.name.StartsWith("["))
                {
                    continue;
                }

                if (IsMovable(renderer.transform))
                {
                    skippedMovable++;
                    continue;
                }

                if (hasBoundary && !boundary.Contains(renderer.bounds.center))
                {
                    skippedOutside++;
                    continue;
                }

                var flags = GameObjectUtility.GetStaticEditorFlags(renderer.gameObject);
                if ((flags & bakeFlags) == bakeFlags)
                {
                    continue;
                }

                Undo.RecordObject(renderer.gameObject, "Mark Static For Baking");
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, flags | bakeFlags);
                marked++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Mart Lighting] ContributeGI 표시 {marked}개 (경계 밖 제외 {skippedOutside}, 움직이는 것 제외 {skippedMovable}). 씬 저장됨.");
        }

        // ------------------------------------------------------------------ 1b. 지붕 빛 통과

        private const string RoofPassFolder = "Assets/_Game/Content/Lighting";
        private static readonly string[] RoofNameHints = { "Roof", "Ceiling", "Skylight" };
        /// <summary>이 높이(m) 위에 있는 바닥 메시(위층 바닥 = 아래층 천장)도 지붕으로 본다.</summary>
        private const float RoofFloorMinY = 4.5f;

        /// <summary>
        /// 지붕·천장 렌더러의 Cast Shadows를 끈다. 베이크 계산에서 하늘빛(환경광)이 지붕을 통과해 실내로 들어오므로,
        /// 실시간 환경광처럼 균일하게 밝은 실내를 베이크로도 얻는다(사용자 선택 2026-09-11). 태양광 실시간 그림자는 영향 없음.
        /// 다시 실행하면 같은 결과. 되돌리려면 <see cref="RestoreRoofShadows"/>.
        /// </summary>
        [MenuItem(MenuRoot + "1b. Let Sky Light Through Roof (Roof Cast Shadows Off)")]
        public static void LetSkyThroughRoof()
        {
            var count = SetRoofShadowCasting(ShadowCastingMode.Off);
            Debug.Log($"[Mart Lighting] 지붕·천장 렌더러 {count}개 Cast Shadows Off → 베이크에서 하늘빛이 실내로 들어옵니다. 다음: 3 → 4 → 5.");
        }

        [MenuItem(MenuRoot + "1c. Restore Roof Cast Shadows")]
        public static void RestoreRoofShadows()
        {
            var count = SetRoofShadowCasting(ShadowCastingMode.On);
            Debug.Log($"[Mart Lighting] 지붕·천장 렌더러 {count}개 Cast Shadows On.");
        }

        private static int SetRoofShadowCasting(ShadowCastingMode mode)
        {
            var scene = EnsureSceneOpen();
            if (!scene.IsValid())
            {
                return 0;
            }

            var count = 0;
            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (renderer.gameObject.scene != scene || IsMovable(renderer.transform))
                {
                    continue;
                }

                var name = renderer.name;
                var isRoof = RoofNameHints.Any(h => name.Contains(h)) ||
                             (name.Contains("Floor") && renderer.bounds.min.y > RoofFloorMinY);
                if (!isRoof || renderer.shadowCastingMode == mode)
                {
                    continue;
                }

                Undo.RecordObject(renderer, "Roof Shadow Casting");
                renderer.shadowCastingMode = mode;
                count++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return count;
        }

        // ------------------------------------------------------------------ 2. 조명 모드

        [MenuItem(MenuRoot + "2. Convert Lights (Sun Mixed, Others Baked, Warehouse Dim)")]
        public static void ConvertLights()
        {
            var scene = EnsureSceneOpen();
            if (!scene.IsValid())
            {
                return;
            }

            var mixed = 0;
            var baked = 0;
            var dimmed = 0;
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (light.gameObject.scene != scene)
                {
                    continue;
                }

                Undo.RecordObject(light, "Convert Mart Lights");
                if (light.type == LightType.Directional)
                {
                    light.lightmapBakeType = LightmapBakeType.Mixed;
                    light.shadows = LightShadows.Soft;
                    mixed++;
                    continue;
                }

                light.lightmapBakeType = LightmapBakeType.Baked;
                light.shadows = LightShadows.Soft; // Baked 라이트도 shadows가 None이면 베이크 그림자가 안 생긴다
                if (light.type == LightType.Spot)
                {
                    light.intensity = BakedSpotIntensity;
                }

                baked++;

                var isDimmed = light.name.EndsWith(" (Dim)");
                if (DimWarehouse && light.type == LightType.Point && light.transform.position.x < WarehouseMaxX && !isDimmed)
                {
                    light.intensity *= WarehouseIntensityScale;
                    light.name += " (Dim)"; // 두 번 실행해도 다시 반으로 줄지 않게 표시
                    dimmed++;
                }
                else if (!DimWarehouse && isDimmed)
                {
                    light.intensity /= WarehouseIntensityScale; // 어둡게 했던 것을 되돌린다
                    light.name = light.name.Substring(0, light.name.Length - " (Dim)".Length);
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Mart Lighting] Mixed {mixed}, Baked {baked}, 창고 어둡게 {dimmed}. 씬 저장됨.");
        }

        // ------------------------------------------------------------------ 2b. 전등 소품마다 베이크 라이트

        private const string FixtureLightsRootName = "Lights";
        private const string FixtureLightPrefix = "FixtureLight_";
        private static readonly string[] FixturePrefixes =
        {
            "SM_Prop_Lighting_Ceiling", "SM_Prop_Lighting_Spotlight", "SM_Prop_Wall_Light", "SM_Prop_Lighting_Wall",
        };
        private const float FixtureLightRange = 13f;
        private const float FixtureLightIntensity = 5f;
        private const float FixtureLightDrop = 0.35f; // 전등 중심에서 이만큼 아래에 광원을 둔다(갓 안에 갇히지 않게)
        private static readonly Color FixtureLightColor = new(1f, 0.965f, 0.91f, 1f);

        /// <summary>
        /// 경계 안 전등 소품(천장 바·스팟·벽등)마다 Baked 포인트 라이트를 하나씩 만든다.
        /// 마트는 지붕이 닫힌 건물이라 환경광(스카이박스)이 실내로 들어오지 않아, 팩에 든 조명 14개만으로 구우면
        /// 3,900 m² 매장이 어둡다(2차 베이크 라이트맵 평균 0.08). 전등 소품 위치(약 50개)를 실제 광원으로 쓴다.
        /// 다시 실행하면 기존 FixtureLight_*를 지우고 새로 만든다.
        /// </summary>
        [MenuItem(MenuRoot + "2b. Add Baked Lights At Light Fixtures")]
        public static void AddFixtureLights()
        {
            var scene = EnsureSceneOpen();
            if (!scene.IsValid())
            {
                return;
            }

            if (!TryGetBoundary(out var boundary))
            {
                Debug.LogError("[Mart Lighting] 경계가 없어 전등 라이트를 만들 수 없습니다.");
                return;
            }

            var root = GameObject.Find(EnvironmentRootName);
            var lightsRoot = root.transform.Find(FixtureLightsRootName);
            if (lightsRoot == null)
            {
                var go = new GameObject(FixtureLightsRootName);
                Undo.RegisterCreatedObjectUndo(go, "Create Lights Root");
                go.transform.SetParent(root.transform, false);
                lightsRoot = go.transform;
            }

            foreach (var old in lightsRoot.GetComponentsInChildren<Light>(true).ToArray())
            {
                if (old.name.StartsWith(FixtureLightPrefix))
                {
                    Undo.DestroyObjectImmediate(old.gameObject);
                }
            }

            var created = 0;
            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (renderer.gameObject.scene != scene || !FixturePrefixes.Any(p => renderer.name.StartsWith(p)))
                {
                    continue;
                }

                var center = renderer.bounds.center;
                if (!boundary.Contains(center) || IsMovable(renderer.transform))
                {
                    continue;
                }

                var go = new GameObject($"{FixtureLightPrefix}{created:D3}");
                Undo.RegisterCreatedObjectUndo(go, "Create Fixture Light");
                go.transform.SetParent(lightsRoot, false);
                go.transform.position = center + Vector3.down * Mathf.Min(FixtureLightDrop, renderer.bounds.extents.y + 0.1f);
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.lightmapBakeType = LightmapBakeType.Baked;
                light.range = FixtureLightRange;
                light.intensity = FixtureLightIntensity;
                light.color = FixtureLightColor;
                light.shadows = LightShadows.Soft;
                created++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Mart Lighting] 전등 소품 기준 Baked 포인트 라이트 {created}개 생성({FixtureLightsRootName} 아래). 씬 저장됨. 다음: 5번(Bake).");
        }

        // ------------------------------------------------------------------ 2c. 어두운 구역 채움 라이트

        private const string FillLightPrefix = "FillLight_";
        private const float FillSampleSpacing = 6f;
        private const float FillSampleHeight = 1.0f;
        private const float FillCoverageFactor = 0.3f; // 라이트 범위의 이 비율 안에 들면 "비춰진다"고 본다. 0.3 = 사실상 6 m 격자로 천장 전등을 고르게 단다(사용자 요청: 밝은 매장)
        private const float FillLightRange = 11f;
        private const float FillLightIntensity = 3.5f;
        private const float FillLightBelowCeiling = 0.5f;
        private const float FillLightDefaultHeight = 4.5f;

        /// <summary>
        /// 경계 안 바닥을 격자로 훑어 어느 실내 라이트(스팟·포인트)의 범위에도 안 들어가는 지점에 Baked 포인트 라이트를 추가한다.
        /// <see cref="DimWarehouse"/>가 켜져 있을 때만 창고 구역(x &lt; -40) 제외. 벽 속 지점은 건너뛴다. 다시 실행하면 FillLight_*를 지우고 새로 만든다.
        /// </summary>
        [MenuItem(MenuRoot + "2c. Add Fill Lights In Dark Areas")]
        public static void AddFillLights()
        {
            var scene = EnsureSceneOpen();
            if (!scene.IsValid())
            {
                return;
            }

            if (!TryGetBoundary(out var boundary))
            {
                Debug.LogError("[Mart Lighting] 경계가 없어 채움 라이트를 만들 수 없습니다.");
                return;
            }

            var root = GameObject.Find(EnvironmentRootName);
            var lightsRoot = root.transform.Find(FixtureLightsRootName);
            if (lightsRoot == null)
            {
                var go = new GameObject(FixtureLightsRootName);
                Undo.RegisterCreatedObjectUndo(go, "Create Lights Root");
                go.transform.SetParent(root.transform, false);
                lightsRoot = go.transform;
            }

            foreach (var old in lightsRoot.GetComponentsInChildren<Light>(true).ToArray())
            {
                if (old.name.StartsWith(FillLightPrefix))
                {
                    Undo.DestroyObjectImmediate(old.gameObject);
                }
            }

            var existing = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(l => l.gameObject.scene == scene && l.type != LightType.Directional)
                .Select(l => (pos: l.transform.position, range: l.range * FillCoverageFactor))
                .ToList();

            var layerMask = Physics.DefaultRaycastLayers & ~(1 << CarryableLayer);
            var hits = new Collider[8];
            var floorY = Mathf.Max(0f, boundary.min.y);
            var created = 0;
            var sampled = 0;
            var covered = 0;
            for (var x = boundary.min.x + FillSampleSpacing * 0.5f; x < boundary.max.x; x += FillSampleSpacing)
            {
                if (DimWarehouse && x < WarehouseMaxX)
                {
                    continue;
                }

                for (var z = boundary.min.z + FillSampleSpacing * 0.5f; z < boundary.max.z; z += FillSampleSpacing)
                {
                    var sample = new Vector3(x, floorY + FillSampleHeight, z);
                    // 벽·가구 속이거나 바닥이 없는 지점(건물 밖)은 건너뛴다.
                    if (Physics.OverlapSphereNonAlloc(sample, 0.3f, hits, layerMask, QueryTriggerInteraction.Ignore) > 0)
                    {
                        continue;
                    }

                    if (!Physics.Raycast(sample, Vector3.down, out var floorHit, 3f, layerMask, QueryTriggerInteraction.Ignore))
                    {
                        continue;
                    }

                    sampled++;
                    var lit = false;
                    foreach (var (pos, range) in existing)
                    {
                        if ((pos - sample).sqrMagnitude <= range * range)
                        {
                            lit = true;
                            break;
                        }
                    }

                    if (lit)
                    {
                        covered++;
                        continue;
                    }

                    var lightY = Physics.Raycast(sample, Vector3.up, out var ceilingHit, 12f, layerMask, QueryTriggerInteraction.Ignore)
                        ? ceilingHit.point.y - FillLightBelowCeiling
                        : floorHit.point.y + FillLightDefaultHeight;
                    var lightPos = new Vector3(x, Mathf.Max(floorHit.point.y + 2.5f, lightY), z);

                    var go = new GameObject($"{FillLightPrefix}{created:D3}");
                    Undo.RegisterCreatedObjectUndo(go, "Create Fill Light");
                    go.transform.SetParent(lightsRoot, false);
                    go.transform.position = lightPos;
                    var light = go.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.lightmapBakeType = LightmapBakeType.Baked;
                    light.range = FillLightRange;
                    light.intensity = FillLightIntensity;
                    light.color = FixtureLightColor;
                    light.shadows = LightShadows.Soft;
                    created++;
                    existing.Add((lightPos, FillLightRange * FillCoverageFactor));
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Mart Lighting] 바닥 표본 {sampled}개 중 비춰진 {covered}, 채움 라이트 {created}개 추가(창고 {(DimWarehouse ? "제외" : "포함")}). 씬 저장됨. 다음: 5번(Bake).");
        }

        // ------------------------------------------------------------------ 3. 설정·프로브

        [MenuItem(MenuRoot + "3. Setup Lighting Settings, Light Probes, Reflection Probes")]
        public static void SetupLighting()
        {
            var scene = EnsureSceneOpen();
            if (!scene.IsValid())
            {
                return;
            }

            if (!TryGetBoundary(out var boundary))
            {
                EditorUtility.DisplayDialog("Mart Lighting",
                    $"'{EnvironmentRootName}/{BoundaryName}' 아래 BoxCollider가 없습니다. 경계를 먼저 배치하세요.", "확인");
                return;
            }

            var settings = GetOrCreateLightingSettings();
            settings.indirectScale = IndirectIntensity;
            EditorUtility.SetDirty(settings);
            Lightmapping.lightingSettings = settings;

            // 환경광: 지붕 빛 통과(1b) 방식에서는 실시간 룩과 같은 스카이박스 환경광 ×1.3을 그대로 굽는다.
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = RealtimeAmbientIntensity;

            var probeCount = BuildLightProbes(boundary);
            var reflectionCount = BuildReflectionProbes(boundary);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Mart Lighting] Setup done. settings={LightingSettingsPath} ({settings.lightmapResolution} texels/m, max {settings.lightmapMaxSize}), " +
                      $"lightProbes={probeCount}, reflectionProbes={reflectionCount}. 다음: 4번(포스트프로세스) → 5번(Bake).");
            LogReport();
        }

        // ------------------------------------------------------------------ 4. 포스트프로세스

        [MenuItem(MenuRoot + "4. Setup Post-Process Volume And Camera")]
        public static void SetupPostProcess()
        {
            var scene = EnsureSceneOpen();
            if (!scene.IsValid())
            {
                return;
            }

            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostProfilePath);
            if (profile == null)
            {
                if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(LobbyPostProfilePath) == null)
                {
                    Debug.LogError($"[Mart Lighting] 로비 프로필 {LobbyPostProfilePath} 이 없어 복제할 수 없습니다.");
                    return;
                }

                AssetDatabase.CopyAsset(LobbyPostProfilePath, PostProfilePath);
                profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostProfilePath);
                Debug.Log($"[Mart Lighting] {LobbyPostProfilePath} → {PostProfilePath} 복제.");
            }

            // Synty 데모 씬에서 따라온 전역 볼륨(팩 Demo 폴더 프로필 참조)은 제거한다.
            foreach (var volume in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (volume.gameObject.scene != scene || volume.name == PostVolumeName)
                {
                    continue;
                }

                var path = volume.sharedProfile != null ? AssetDatabase.GetAssetPath(volume.sharedProfile) : string.Empty;
                if (volume.name == SyntyDemoVolumeName || path.StartsWith("Assets/Synty/"))
                {
                    var removedName = volume.name;
                    Undo.DestroyObjectImmediate(volume.gameObject);
                    Debug.Log($"[Mart Lighting] 데모 볼륨 제거: {removedName} ({path})");
                }
            }

            var volumeObject = GameObject.Find(PostVolumeName);
            if (volumeObject == null)
            {
                volumeObject = new GameObject(PostVolumeName);
                Undo.RegisterCreatedObjectUndo(volumeObject, "Create Mart Post Volume");
                SceneManager.MoveGameObjectToScene(volumeObject, scene);
            }

            var postVolume = volumeObject.GetComponent<Volume>();
            if (postVolume == null)
            {
                postVolume = volumeObject.AddComponent<Volume>();
            }

            postVolume.isGlobal = true;
            postVolume.priority = 0f;
            postVolume.sharedProfile = profile;

            var camera = Camera.main;
            if (camera != null && camera.gameObject.scene == scene)
            {
                var data = camera.GetUniversalAdditionalCameraData();
                Undo.RecordObject(data, "Mart Camera Post-Process");
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                data.antialiasingQuality = AntialiasingQuality.Medium;
            }
            else
            {
                Debug.LogWarning("[Mart Lighting] 씬에 Main Camera가 없어 카메라 PP 설정은 건너뜀.");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Mart Lighting] 포스트프로세스 설정 완료: {PostVolumeName} → {profile.name}, 카메라 PP + SMAA Medium. 씬 저장됨.");
        }

        // ------------------------------------------------------------------ 5~7. 베이크·정리·보고

        [MenuItem(MenuRoot + "5. Bake Lighting (Lightmaps + Reflection + Light Probes)")]
        public static void Bake()
        {
            var scene = EnsureSceneOpen();
            if (!scene.IsValid())
            {
                return;
            }

            if (Lightmapping.isRunning)
            {
                Debug.LogWarning("[Mart Lighting] 이미 베이크가 진행 중입니다.");
                return;
            }

            if (Lightmapping.lightingSettings == null ||
                AssetDatabase.GetAssetPath(Lightmapping.lightingSettings) != LightingSettingsPath)
            {
                Lightmapping.lightingSettings = GetOrCreateLightingSettings();
            }

            LogReport();
            Lightmapping.bakeCompleted -= OnBakeCompleted;
            Lightmapping.bakeCompleted += OnBakeCompleted;

            var started = Lightmapping.BakeAsync();
            Debug.Log(started
                ? "[Mart Lighting] Bake started. 완료되면 콘솔에 결과가 출력되고 씬이 저장됩니다."
                : "[Mart Lighting] Bake failed to start. Lighting 창의 오류를 확인하세요.");
        }

        [MenuItem(MenuRoot + "6. Clear Baked Data")]
        public static void ClearBaked()
        {
            var scene = EnsureSceneOpen();
            if (!scene.IsValid())
            {
                return;
            }

            Lightmapping.Clear();
            Lightmapping.ClearLightingDataAsset();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Mart Lighting] Baked data cleared.");
        }

        [MenuItem(MenuRoot + "Report Lighting State")]
        public static void Report()
        {
            LogReport();
        }

        private static void OnBakeCompleted()
        {
            Lightmapping.bakeCompleted -= OnBakeCompleted;
            var scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            var dataAsset = Lightmapping.lightingDataAsset;
            Debug.Log($"[Mart Lighting] Bake completed. lightmaps={LightmapSettings.lightmaps.Length}, " +
                      $"lightingData={(dataAsset != null ? AssetDatabase.GetAssetPath(dataAsset) : "none")}. 씬 저장됨.");
        }

        // ------------------------------------------------------------------ 7. 베이크 없이 실시간으로 되돌리기

        /// <summary>
        /// 베이크 전 상태(실시간 조명 + 스카이박스 환경광)로 되돌린다. 실시간 환경광은 지붕에 가려지지 않아 실내가
        /// 균일하게 밝게 보인다(베이크는 지붕을 제대로 계산해 어두워짐). 그림자·접촉 음영은 사라지지만 밝고 단순한 룩.
        /// 베이크 데이터 삭제, 자동 생성 라이트(FixtureLight_·FillLight_) 제거, 팩 조명 Realtime 복구, ContributeGI 해제.
        /// </summary>
        [MenuItem(MenuRoot + "7. Revert To Realtime Lighting (No Bake)")]
        public static void RevertToRealtime()
        {
            var scene = EnsureSceneOpen();
            if (!scene.IsValid())
            {
                return;
            }

            if (Lightmapping.isRunning)
            {
                Lightmapping.Cancel();
            }

            Lightmapping.Clear();
            Lightmapping.ClearLightingDataAsset();

            var removed = 0;
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None).ToArray())
            {
                if (light.gameObject.scene != scene)
                {
                    continue;
                }

                if (light.name.StartsWith(FixtureLightPrefix) || light.name.StartsWith(FillLightPrefix))
                {
                    Undo.DestroyObjectImmediate(light.gameObject);
                    removed++;
                    continue;
                }

                Undo.RecordObject(light, "Revert Light To Realtime");
                light.lightmapBakeType = LightmapBakeType.Realtime;
                if (light.type != LightType.Directional)
                {
                    light.shadows = LightShadows.None;
                }
            }

            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = RealtimeAmbientIntensity;
            // 베이크 데이터를 지우면 스카이박스 환경광 프로브도 사라져 실내가 어두워진다(평균 밝기 34). 즉시 다시 만든다(→ 94).
            DynamicGI.UpdateEnvironment();

            // 베이크용으로 만든 프로브는 데이터가 지워지면 검게 비치거나 무의미하므로 함께 치운다(3번 메뉴로 다시 만들 수 있음).
            var probeGroup = GameObject.Find(ProbeGroupName);
            if (probeGroup != null)
            {
                Undo.DestroyObjectImmediate(probeGroup);
            }

            var envRoot = GameObject.Find(EnvironmentRootName);
            var probesRoot = envRoot != null ? envRoot.transform.Find(ReflectionRootName) : null;
            if (probesRoot != null)
            {
                foreach (var probe in probesRoot.GetComponentsInChildren<ReflectionProbe>(true).ToArray())
                {
                    Undo.DestroyObjectImmediate(probe.gameObject);
                }
            }

            var cleared = 0;
            const StaticEditorFlags bakeFlags = StaticEditorFlags.ContributeGI | StaticEditorFlags.ReflectionProbeStatic;
            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (renderer.gameObject.scene != scene)
                {
                    continue;
                }

                var flags = GameObjectUtility.GetStaticEditorFlags(renderer.gameObject);
                if ((flags & bakeFlags) == 0)
                {
                    continue;
                }

                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, flags & ~bakeFlags);
                cleared++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Mart Lighting] 실시간 조명으로 되돌림: 자동 라이트 {removed}개 제거, ContributeGI 해제 {cleared}개, 베이크 데이터 삭제, 환경광 Skybox. 씬 저장됨.");
        }

        // ------------------------------------------------------------------ 내부

        private static Scene EnsureSceneOpen()
        {
            var active = SceneManager.GetActiveScene();
            if (active.path == ScenePath)
            {
                return active;
            }

            if (!EditorUtility.DisplayDialog("Mart Lighting",
                    $"활성 씬이 Supermarket이 아닙니다.\n{ScenePath} 를 열고 진행할까요?", "열기", "취소"))
            {
                return default;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return default;
            }

            return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        private static bool IsMovable(Transform transform)
        {
            return transform.GetComponentInParent<CarryableItem>(true) != null ||
                   transform.GetComponentInParent<Rigidbody>(true) != null;
        }

        /// <summary>경계 박스 15개의 합집합. 로비처럼 6면 이름이 정해져 있지 않으므로 바운드로 잰다.</summary>
        private static bool TryGetBoundary(out Bounds boundary)
        {
            boundary = default;
            var root = GameObject.Find(EnvironmentRootName);
            var boundaryRoot = root != null ? root.transform.Find(BoundaryName) : null;
            if (boundaryRoot == null)
            {
                return false;
            }

            var found = false;
            foreach (var box in boundaryRoot.GetComponentsInChildren<BoxCollider>(true))
            {
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

            return found;
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
                name = "MartLighting",
                bakedGI = true,
                realtimeGI = false,
                lightmapper = LightingSettings.Lightmapper.ProgressiveGPU,
                mixedBakeMode = MixedLightingMode.Shadowmask,
                directionalityMode = LightmapsMode.CombinedDirectional,
                // 해상도: 경계 안 52×75 m + 고정 소품 1만 3천 개. 로비(20)의 절반 이하, 최대 크기는 두 배.
                lightmapResolution = 8f,
                lightmapMaxSize = 4096,
                lightmapPadding = 2,
                lightmapCompression = LightmapCompression.NormalQuality,
                // 샘플/바운스: 넓은 맵이라 로비(32/512/3)보다 가볍게 시작. 얼룩이 보이면 올린다.
                directSampleCount = 32,
                indirectSampleCount = 256,
                environmentSampleCount = 128,
                maxBounces = 2,
                lightProbeSampleCountMultiplier = 2f,
                ao = true,
                aoMaxDistance = 0.5f,
                aoExponentDirect = 0f,
                aoExponentIndirect = 1f,
                filteringMode = LightingSettings.FilterMode.Auto,
            };

            AssetDatabase.CreateAsset(settings, LightingSettingsPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Mart Lighting] Created {LightingSettingsPath}");
            return settings;
        }

        /// <summary>경계 안 격자에 라이트 프로브를 놓는다. 벽·선반 속(콜라이더 안)은 뺀다. 상품(Carryable 레이어)은 무시.</summary>
        private static int BuildLightProbes(Bounds boundary)
        {
            var existing = GameObject.Find(ProbeGroupName);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
            }

            var go = new GameObject(ProbeGroupName);
            Undo.RegisterCreatedObjectUndo(go, "Build Mart Light Probes");
            var group = go.AddComponent<LightProbeGroup>();

            var minX = boundary.min.x + ProbeWallMargin;
            var maxX = boundary.max.x - ProbeWallMargin;
            var minZ = boundary.min.z + ProbeWallMargin;
            var maxZ = boundary.max.z - ProbeWallMargin;
            var countX = Mathf.Max(2, Mathf.RoundToInt((maxX - minX) / ProbeSpacing) + 1);
            var countZ = Mathf.Max(2, Mathf.RoundToInt((maxZ - minZ) / ProbeSpacing) + 1);
            var floorY = Mathf.Max(0f, boundary.min.y);
            var hits = new Collider[8];
            var layerMask = Physics.DefaultRaycastLayers & ~(1 << CarryableLayer);

            var positions = new List<Vector3>();
            for (var ix = 0; ix < countX; ix++)
            {
                var x = Mathf.Lerp(minX, maxX, ix / (float)(countX - 1));
                for (var iz = 0; iz < countZ; iz++)
                {
                    var z = Mathf.Lerp(minZ, maxZ, iz / (float)(countZ - 1));
                    foreach (var h in ProbeHeights)
                    {
                        var p = new Vector3(x, floorY + h, z);
                        if (p.y > boundary.max.y - 0.3f)
                        {
                            continue;
                        }

                        var count = Physics.OverlapSphereNonAlloc(p, ProbeSolidRadius, hits, layerMask, QueryTriggerInteraction.Ignore);
                        var insideSolid = false;
                        for (var i = 0; i < count; i++)
                        {
                            if (hits[i].transform.parent != null && hits[i].transform.parent.name == BoundaryName)
                            {
                                continue; // 경계 박스는 막힘으로 치지 않음
                            }

                            insideSolid = true;
                            break;
                        }

                        if (!insideSolid)
                        {
                            positions.Add(p);
                        }
                    }
                }
            }

            group.probePositions = positions.ToArray();
            return positions.Count;
        }

        /// <summary>경계를 가로×세로 칸으로 나눠 칸마다 Baked 박스 리플렉션 프로브를 놓는다.</summary>
        private static int BuildReflectionProbes(Bounds boundary)
        {
            var root = GameObject.Find(EnvironmentRootName);
            var probesRoot = root.transform.Find(ReflectionRootName);
            if (probesRoot == null)
            {
                var go = new GameObject(ReflectionRootName);
                Undo.RegisterCreatedObjectUndo(go, "Create Probes Root");
                go.transform.SetParent(root.transform, false);
                probesRoot = go.transform;
            }

            foreach (var old in probesRoot.GetComponentsInChildren<ReflectionProbe>(true).ToArray())
            {
                Undo.DestroyObjectImmediate(old.gameObject);
            }

            var cell = new Vector3(boundary.size.x / ReflectionColumns, boundary.size.y, boundary.size.z / ReflectionRows);
            var floorY = Mathf.Max(0f, boundary.min.y);
            var height = boundary.max.y - floorY;
            var created = 0;
            for (var cx = 0; cx < ReflectionColumns; cx++)
            {
                for (var cz = 0; cz < ReflectionRows; cz++)
                {
                    var center = new Vector3(
                        boundary.min.x + cell.x * (cx + 0.5f),
                        floorY + Mathf.Min(1.8f, height * 0.5f),
                        boundary.min.z + cell.z * (cz + 0.5f));
                    var go = new GameObject($"ReflectionProbe_{cx}_{cz}");
                    Undo.RegisterCreatedObjectUndo(go, "Create Reflection Probe");
                    go.transform.SetParent(probesRoot, false);
                    go.transform.position = center;
                    var probe = go.AddComponent<ReflectionProbe>();
                    probe.mode = ReflectionProbeMode.Baked;
                    probe.resolution = ReflectionResolution;
                    probe.size = new Vector3(cell.x + 1f, height, cell.z + 1f);
                    probe.center = new Vector3(0f, floorY + height * 0.5f - center.y, 0f);
                    probe.boxProjection = true;
                    probe.importance = 1;
                    created++;
                }
            }

            return created;
        }

        private static void LogReport()
        {
            var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var byMode = lights.GroupBy(l => l.lightmapBakeType).Select(g => $"{g.Key}={g.Count()}");
            var renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var contributeGI = renderers.Count(r =>
                GameObjectUtility.AreStaticEditorFlagsSet(r.gameObject, StaticEditorFlags.ContributeGI));
            var probes = GameObject.Find(ProbeGroupName)?.GetComponent<LightProbeGroup>();
            var reflection = Object.FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var settingsName = Lightmapping.TryGetLightingSettings(out var settings) && settings != null ? settings.name : "default";

            Debug.Log("[Mart Lighting] Report: " +
                      $"lights={lights.Length} ({string.Join(", ", byMode)}), " +
                      $"renderers={renderers.Length} contributeGI={contributeGI}, " +
                      $"lightProbes={(probes != null ? probes.probePositions.Length : 0)}, " +
                      $"reflectionProbes={reflection.Length}, lightmaps={LightmapSettings.lightmaps.Length}, settings={settingsName}");
        }
    }
}
