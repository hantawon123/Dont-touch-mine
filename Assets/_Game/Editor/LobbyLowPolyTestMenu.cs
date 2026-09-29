using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Editor
{
    /// <summary>
    /// 로비(지하실)를 로우폴리 스타일로 바꿔 보는 테스트 씬을 만든다.
    /// 메인 맵을 로우폴리로 갈 때 대기실도 같은 톤으로 바꿀 수 있는지 미리 보기 위한 것.
    /// </summary>
    /// <remarks>
    /// 원본 씬·프리팹·에셋 팩은 건드리지 않는다. <see cref="SourceScene"/>을 복제한 뒤
    /// 환경(LobbyBasementEnvironment) 아래 렌더러에만 다음을 적용한다.
    /// <list type="bullet">
    /// <item>재질: 원본 텍스처의 평균색을 바탕으로, 원본 텍스처를 <see cref="TextureBlend"/> 비율만
    /// 섞은 새 텍스처를 만들어 붙인다. 노멀·메탈릭·AO 맵은 제거하고 매끈함을 낮춘다.</item>
    /// <item>메시: 삼각형마다 정점을 분리해 면 노멀을 주는 각진(flat shading) 복사본으로 교체한다.
    /// UV2(라이트맵 UV)를 그대로 복사하므로 베이크된 라이트맵이 유지된다.</item>
    /// <item>데칼(포스터·전단)은 그림 자체가 내용이라 건너뛴다.</item>
    /// </list>
    /// 생성물은 <see cref="OutputRoot"/> 아래에 모이고 메뉴 3번으로 한 번에 지울 수 있다.
    /// </remarks>
    public static class LobbyLowPolyTestMenu
    {
        private const string SourceScene = "Assets/_Game/Content/Scenes/Lobby.unity";
        private const string TestScene = "Assets/_Game/Content/Scenes/LobbyLowPolyTest.unity";
        private const string OutputRoot = "Assets/_Game/Content/LowPolyTest";
        private const string EnvironmentRootName = "LobbyBasementEnvironment";
        private const string DecalsGroupName = "Decals";

        // 기준: Playground의 LowPolyInterior2 — 팔레트 텍스처 1장, 면마다 단색, 매끈함 0.1, 노멀맵 없음.
        // 재질 평균색은 소품 전체가 한 색으로 뭉개져 탁했다. 대신 같은 평면의 삼각형 묶음마다 UV를
        // 한 점으로 모아 원본 텍스처를 팔레트처럼 쓴다: 면 하나 = 색 하나, 부위별 색은 유지된다.
        //
        // 두 프로파일: 벽·기둥·천장·바닥(BasementModules)은 벽돌·나무 무늬가 방의 인상이라 원본 UV와
        // 텍스처를 살리고 노멀맵·광택만 뺀다(사용자 피드백: 구조물까지 단색이면 너무 로우폴리). 소품은 팔레트.
        private readonly struct Profile
        {
            public Profile(string name, float textureBlend, int textureSize)
            {
                Name = name;
                TextureBlend = textureBlend;
                TextureSize = textureSize;
            }

            public string Name { get; }

            /// <summary>0 = 면 단색(팔레트), 1 = 원본 UV 그대로.</summary>
            public float TextureBlend { get; }

            /// <summary>생성 텍스처 크기. 팔레트는 세부 무늬(나사·얼룩)가 걸리지 않게 작게 뭉갠다.</summary>
            public int TextureSize { get; }
        }

        private static readonly Profile StructureProfile = new("Structure", 1f, 512);
        private static readonly Profile PropProfile = new("Prop", 0f, 64);
        private const string StructureGroupName = "BasementModules";
        private const float Smoothness = 0.1f;

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int BumpMapId = Shader.PropertyToID("_BumpMap");
        private static readonly int MetallicGlossMapId = Shader.PropertyToID("_MetallicGlossMap");
        private static readonly int OcclusionMapId = Shader.PropertyToID("_OcclusionMap");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int GlossinessId = Shader.PropertyToID("_Glossiness");
        private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        private static readonly int AlphaClipId = Shader.PropertyToID("_AlphaClip");

        [MenuItem("Game/Lobby/LowPoly Test/1 Create Test Scene (Flat Color + Faceted)")]
        public static void CreateTestScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TestScene) != null)
            {
                AssetDatabase.DeleteAsset(TestScene);
            }

            // 이전 생성물이 쌓이지 않게 비운다.
            if (AssetDatabase.IsValidFolder(OutputRoot))
            {
                AssetDatabase.DeleteAsset(OutputRoot);
            }

            if (!AssetDatabase.CopyAsset(SourceScene, TestScene))
            {
                Debug.LogError($"[LowPolyTest] 씬 복제 실패: {SourceScene} -> {TestScene}");
                return;
            }

            EnsureFolder(OutputRoot);
            EnsureFolder(OutputRoot + "/Textures");
            EnsureFolder(OutputRoot + "/Materials");
            EnsureFolder(OutputRoot + "/Meshes");

            var scene = EditorSceneManager.OpenScene(TestScene, OpenSceneMode.Single);
            var environment = GameObject.Find(EnvironmentRootName);
            if (environment == null)
            {
                Debug.LogError($"[LowPolyTest] '{EnvironmentRootName}'를 찾지 못했다.");
                return;
            }

            // 프리팹 인스턴스 오버라이드가 수백 개 쌓이는 것을 피하고, 원본 프리팹과의 연결을 끊는다.
            if (PrefabUtility.IsAnyPrefabInstanceRoot(environment))
            {
                PrefabUtility.UnpackPrefabInstance(environment, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }

            var materialCache = new Dictionary<(Material, string), Material>();
            var meshCache = new Dictionary<(Mesh, string), Mesh>();
            var stats = new Stats();

            var renderers = environment.GetComponentsInChildren<Renderer>(true);
            try
            {
                for (var index = 0; index < renderers.Length; index++)
                {
                    var renderer = renderers[index];
                    EditorUtility.DisplayProgressBar("LowPoly Test", renderer.name, (float)index / renderers.Length);

                    if (IsUnder(renderer.transform, DecalsGroupName))
                    {
                        stats.SkippedDecals++;
                        continue;
                    }

                    if (renderer is MeshRenderer meshRenderer)
                    {
                        var profile = IsUnder(renderer.transform, StructureGroupName) ? StructureProfile : PropProfile;
                        ConvertMeshRenderer(meshRenderer, profile, materialCache, meshCache, stats);
                    }
                    else
                    {
                        stats.SkippedOther++;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log(
                $"[LowPolyTest] 완료: 렌더러 {stats.ConvertedRenderers}개 변환, 재질 {materialCache.Count}개, " +
                $"메시 {meshCache.Count}개 생성. 건너뜀: 데칼 {stats.SkippedDecals}, 알파클립 재질 {stats.KeptAlphaClipMaterials}, " +
                $"기타 렌더러 {stats.SkippedOther}. 씬: {TestScene}");
        }

        [MenuItem("Game/Lobby/LowPoly Test/2 Open Test Scene")]
        public static void OpenTestScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TestScene) == null)
            {
                Debug.LogWarning("[LowPolyTest] 테스트 씬이 없다. 메뉴 1번을 먼저 실행.");
                return;
            }

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(TestScene, OpenSceneMode.Single);
            }
        }

        [MenuItem("Game/Lobby/LowPoly Test/3 Delete Test Scene And Generated Assets")]
        public static void DeleteGenerated()
        {
            if (!EditorUtility.DisplayDialog("LowPoly Test 삭제",
                    $"{TestScene}\n{OutputRoot}\n\n위 씬과 생성 에셋을 모두 삭제합니다. 원본 Lobby 씬은 그대로입니다.",
                    "삭제", "취소"))
            {
                return;
            }

            if (EditorSceneManager.GetActiveScene().path == TestScene)
            {
                EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Single);
            }

            AssetDatabase.DeleteAsset(TestScene);
            AssetDatabase.DeleteAsset(OutputRoot);
            AssetDatabase.Refresh();
            Debug.Log("[LowPolyTest] 테스트 씬과 생성 에셋을 삭제했다.");
        }

        // ---- 실제 입장 흐름으로 테스트 -------------------------------------------
        // 방 생성 → 로비 입장은 NetworkScenes 에셋의 lobby 씬(빌드 인덱스)을 로드한다.
        // 테스트 씬을 빌드 목록에 넣고 NetworkScenes가 그것을 가리키게 바꾸면 같은 흐름으로 걸어 볼 수 있다.
        // NetworkScenes.asset은 커밋 대상이라 테스트가 끝나면 반드시 5번으로 되돌린다.

        private const string NetworkScenesPath = "Assets/_Game/Content/Settings/NetworkScenes.asset";

        [MenuItem("Game/Lobby/LowPoly Test/4 Use Test Scene As Lobby (Play via Home)")]
        public static void UseTestSceneAsLobby()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TestScene) == null)
            {
                Debug.LogWarning("[LowPolyTest] 테스트 씬이 없다. 메뉴 1번을 먼저 실행.");
                return;
            }

            EnsureInBuildSettings(TestScene, true);
            PointNetworkScenesLobbyTo(TestScene);
            Debug.Log("[LowPolyTest] 로비 씬을 테스트 씬으로 바꿨다. Home에서 방을 만들어 입장하면 로우폴리 로비가 열린다. 끝나면 메뉴 5번으로 복구.");
        }

        [MenuItem("Game/Lobby/LowPoly Test/5 Restore Original Lobby")]
        public static void RestoreOriginalLobby()
        {
            PointNetworkScenesLobbyTo(SourceScene);
            EnsureInBuildSettings(TestScene, false);
            Debug.Log("[LowPolyTest] 로비 씬 참조를 원본 Lobby로 되돌리고 테스트 씬을 빌드 목록에서 뺐다.");
        }

        private static void PointNetworkScenesLobbyTo(string scenePath)
        {
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(NetworkScenesPath);
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            if (asset == null || sceneAsset == null)
            {
                Debug.LogError($"[LowPolyTest] NetworkScenes({asset != null}) 또는 씬({scenePath}) 을 찾지 못했다.");
                return;
            }

            using var serialized = new SerializedObject(asset);
            serialized.FindProperty("_lobbyScene").objectReferenceValue = sceneAsset;
            serialized.FindProperty("_lobbyScenePath").stringValue = scenePath;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
        }

        private static void EnsureInBuildSettings(string scenePath, bool present)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            var index = scenes.FindIndex(s => s.path == scenePath);
            if (present && index < 0)
            {
                scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            }
            else if (present && !scenes[index].enabled)
            {
                scenes[index].enabled = true;
            }
            else if (!present && index >= 0)
            {
                scenes.RemoveAt(index);
            }
            else
            {
                return;
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void ConvertMeshRenderer(MeshRenderer renderer, Profile profile,
            Dictionary<(Material, string), Material> materialCache, Dictionary<(Mesh, string), Mesh> meshCache, Stats stats)
        {
            var filter = renderer.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
            {
                filter.sharedMesh = GetOrCreateFacetedMesh(filter.sharedMesh, profile, meshCache);
            }

            var materials = renderer.sharedMaterials;
            var changed = false;
            for (var index = 0; index < materials.Length; index++)
            {
                var original = materials[index];
                if (original == null)
                {
                    continue;
                }

                if (IsAlphaClipped(original))
                {
                    stats.KeptAlphaClipMaterials++;
                    continue;
                }

                materials[index] = GetOrCreateFlatMaterial(original, profile, materialCache);
                changed = true;
            }

            if (changed)
            {
                renderer.sharedMaterials = materials;
            }

            stats.ConvertedRenderers++;
        }

        // ---- 재질 ---------------------------------------------------------------

        private static Material GetOrCreateFlatMaterial(Material original, Profile profile, Dictionary<(Material, string), Material> cache)
        {
            var key = (original, profile.Name);
            if (cache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var flat = new Material(original) { name = $"{original.name}_{profile.Name}" };

            var baseMapId = flat.HasProperty(BaseMapId) ? BaseMapId : flat.HasProperty(MainTexId) ? MainTexId : -1;
            if (baseMapId != -1)
            {
                var source = original.GetTexture(baseMapId);
                if (source != null)
                {
                    var palette = CreatePaletteTexture(source, $"{original.name}_{profile.Name}", profile.TextureSize);
                    if (palette != null)
                    {
                        flat.SetTexture(baseMapId, palette);
                    }
                }
            }

            ClearTexture(flat, BumpMapId, "_NORMALMAP");
            ClearTexture(flat, MetallicGlossMapId, "_METALLICSPECGLOSSMAP");
            ClearTexture(flat, OcclusionMapId, "_OCCLUSIONMAP");

            if (flat.HasProperty(SmoothnessId))
            {
                flat.SetFloat(SmoothnessId, Smoothness);
            }

            if (flat.HasProperty(GlossinessId))
            {
                flat.SetFloat(GlossinessId, Smoothness);
            }

            if (flat.HasProperty(MetallicId))
            {
                flat.SetFloat(MetallicId, 0f);
            }

            var path = AssetDatabase.GenerateUniqueAssetPath($"{OutputRoot}/Materials/{Sanitize(flat.name)}.mat");
            AssetDatabase.CreateAsset(flat, path);
            cache[key] = flat;
            return flat;
        }

        private static void ClearTexture(Material material, int propertyId, string keyword)
        {
            if (material.HasProperty(propertyId))
            {
                material.SetTexture(propertyId, null);
            }

            material.DisableKeyword(keyword);
        }

        private static bool IsAlphaClipped(Material material)
        {
            return material.HasProperty(AlphaClipId) && material.GetFloat(AlphaClipId) > 0.5f;
        }

        /// <summary>
        /// 원본 텍스처를 <see cref="PaletteSize"/>로 단계적으로 줄여(박스 필터) 뭉갠 팔레트 텍스처를
        /// PNG 에셋으로 저장한다. 면마다 무게중심 UV 한 점을 찍으므로 세부 무늬가 아닌 부위 색만 남는다.
        /// Blit을 쓰므로 원본이 Read/Write 불가여도 된다.
        /// </summary>
        private static Texture2D CreatePaletteTexture(Texture source, string materialName, int paletteSize)
        {
            var previous = RenderTexture.active;
            RenderTexture current = null;
            Texture2D readable;
            try
            {
                // 한 번에 크게 줄이면 바이리니어가 픽셀을 건너뛰어 노이즈가 남는다. 절반씩 줄인다.
                var size = Mathf.Max(paletteSize, Mathf.NextPowerOfTwo(Mathf.Max(source.width, source.height)));
                Texture input = source;
                while (size > paletteSize)
                {
                    size = Mathf.Max(paletteSize, size / 2);
                    var next = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                    next.filterMode = FilterMode.Bilinear;
                    Graphics.Blit(input, next);
                    if (current != null) RenderTexture.ReleaseTemporary(current);
                    current = next;
                    input = next;
                }

                if (current == null)
                {
                    current = RenderTexture.GetTemporary(paletteSize, paletteSize, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                    Graphics.Blit(source, current);
                }

                RenderTexture.active = current;
                readable = new Texture2D(paletteSize, paletteSize, TextureFormat.RGBA32, false);
                readable.ReadPixels(new Rect(0, 0, paletteSize, paletteSize), 0, 0);
                readable.Apply();
            }
            finally
            {
                RenderTexture.active = previous;
                if (current != null) RenderTexture.ReleaseTemporary(current);
            }

            var path = AssetDatabase.GenerateUniqueAssetPath($"{OutputRoot}/Textures/{Sanitize(materialName)}_Palette.png");
            File.WriteAllBytes(path, readable.EncodeToPNG());
            Object.DestroyImmediate(readable);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.sRGBTexture = true;
                importer.mipmapEnabled = true;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ---- 메시 ---------------------------------------------------------------

        private static Mesh GetOrCreateFacetedMesh(Mesh source, Profile profile, Dictionary<(Mesh, string), Mesh> cache)
        {
            var key = (source, profile.Name);
            if (cache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var faceted = CreateFacetedMesh(source, profile.TextureBlend);
            var path = AssetDatabase.GenerateUniqueAssetPath($"{OutputRoot}/Meshes/{Sanitize(source.name)}_{profile.Name}.asset");
            AssetDatabase.CreateAsset(faceted, path);
            cache[key] = faceted;
            return faceted;
        }

        /// <summary>
        /// 삼각형마다 정점 3개를 새로 만들어 면 노멀을 준다. UV0·UV2·정점색은 원본 정점에서 복사하므로
        /// 텍스처와 라이트맵이 그대로 맞는다.
        /// </summary>
        private static Mesh CreateFacetedMesh(Mesh source, float textureBlend)
        {
            var srcVertices = source.vertices;
            var srcUv = source.uv;
            var srcUv2 = source.uv2;
            var srcColors = source.colors;
            var hasUv = srcUv != null && srcUv.Length == srcVertices.Length;
            var hasUv2 = srcUv2 != null && srcUv2.Length == srcVertices.Length;
            var hasColors = srcColors != null && srcColors.Length == srcVertices.Length;

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uv = new List<Vector2>();
            var uv2 = new List<Vector2>();
            var colors = new List<Color>();
            var submeshes = new List<int[]>();

            for (var sub = 0; sub < source.subMeshCount; sub++)
            {
                var triangles = source.GetTriangles(sub);
                var indices = new int[triangles.Length];
                var triangleCount = triangles.Length / 3;
                var faceNormals = new Vector3[triangleCount];
                for (var tri = 0; tri < triangleCount; tri++)
                {
                    var a = srcVertices[triangles[tri * 3]];
                    var b = srcVertices[triangles[tri * 3 + 1]];
                    var c = srcVertices[triangles[tri * 3 + 2]];
                    var normal = Vector3.Cross(b - a, c - a);
                    faceNormals[tri] = normal.sqrMagnitude > 1e-12f ? normal.normalized : Vector3.up;
                }

                // 같은 평면에서 변을 공유하는 삼각형들(상자 한 면, 벽 한 판)은 한 색으로 칠한다.
                // 삼각형마다 따로 찍으면 한 면이 대각선으로 두 톤이 되고 벽에 줄무늬가 생긴다.
                // 원본 UV를 그대로 쓰는 프로파일이면 묶음 계산을 건너뛴다.
                var groupUv = hasUv && textureBlend < 0.999f
                    ? ComputeCoplanarGroupUvs(srcVertices, srcUv, triangles, faceNormals)
                    : null;

                for (var t = 0; t < triangles.Length; t += 3)
                {
                    var tri = t / 3;
                    var normal = faceNormals[tri];
                    var flatUv = groupUv != null ? groupUv[tri] : Vector2.zero;

                    for (var k = 0; k < 3; k++)
                    {
                        var srcIndex = triangles[t + k];
                        indices[t + k] = vertices.Count;
                        vertices.Add(srcVertices[srcIndex]);
                        normals.Add(normal);
                        if (hasUv) uv.Add(groupUv != null ? Vector2.Lerp(flatUv, srcUv[srcIndex], textureBlend) : srcUv[srcIndex]);
                        if (hasUv2) uv2.Add(srcUv2[srcIndex]);
                        if (hasColors) colors.Add(srcColors[srcIndex]);
                    }
                }

                submeshes.Add(indices);
            }

            var mesh = new Mesh
            {
                name = source.name + "_Faceted",
                indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            if (hasUv) mesh.SetUVs(0, uv);
            if (hasUv2) mesh.SetUVs(1, uv2);
            if (hasColors) mesh.SetColors(colors);
            mesh.subMeshCount = submeshes.Count;
            for (var sub = 0; sub < submeshes.Count; sub++)
            {
                mesh.SetTriangles(submeshes[sub], sub, false);
            }

            mesh.RecalculateBounds();
            if (hasUv)
            {
                mesh.RecalculateTangents();
            }

            return mesh;
        }

        /// <summary>
        /// 변을 공유하고(정점 위치 기준) 법선이 거의 같은 삼각형들을 묶어, 묶음마다 면적 가중 평균 UV를 돌려준다.
        /// 정점이 UV 이음새로 갈라져 있어도 위치가 같으면 같은 변으로 본다.
        /// </summary>
        private static Vector2[] ComputeCoplanarGroupUvs(Vector3[] positions, Vector2[] uvs, int[] triangles, Vector3[] faceNormals)
        {
            var triangleCount = triangles.Length / 3;
            var parent = new int[triangleCount];
            for (var i = 0; i < triangleCount; i++) parent[i] = i;

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

            // 위치를 0.1 mm 단위로 양자화해 정점 id를 만들고, 변 = (작은 id, 큰 id).
            var positionIds = new Dictionary<Vector3Int, int>();
            var vertexId = new int[positions.Length];
            for (var i = 0; i < positions.Length; i++)
            {
                var p = positions[i] * 10000f;
                var key = new Vector3Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), Mathf.RoundToInt(p.z));
                if (!positionIds.TryGetValue(key, out var id))
                {
                    id = positionIds.Count;
                    positionIds[key] = id;
                }

                vertexId[i] = id;
            }

            var edgeOwner = new Dictionary<long, int>();
            for (var tri = 0; tri < triangleCount; tri++)
            {
                for (var e = 0; e < 3; e++)
                {
                    var a = vertexId[triangles[tri * 3 + e]];
                    var b = vertexId[triangles[tri * 3 + (e + 1) % 3]];
                    var key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    if (edgeOwner.TryGetValue(key, out var other))
                    {
                        if (Vector3.Dot(faceNormals[tri], faceNormals[other]) > 0.999f)
                        {
                            Union(tri, other);
                        }
                    }
                    else
                    {
                        edgeOwner[key] = tri;
                    }
                }
            }

            var sumUv = new Dictionary<int, Vector2>();
            var sumWeight = new Dictionary<int, float>();
            for (var tri = 0; tri < triangleCount; tri++)
            {
                var i0 = triangles[tri * 3];
                var i1 = triangles[tri * 3 + 1];
                var i2 = triangles[tri * 3 + 2];
                var area = Vector3.Cross(positions[i1] - positions[i0], positions[i2] - positions[i0]).magnitude * 0.5f + 1e-6f;
                var centroid = (uvs[i0] + uvs[i1] + uvs[i2]) / 3f;
                var root = Find(tri);
                sumUv[root] = (sumUv.TryGetValue(root, out var s) ? s : Vector2.zero) + centroid * area;
                sumWeight[root] = (sumWeight.TryGetValue(root, out var w) ? w : 0f) + area;
            }

            var result = new Vector2[triangleCount];
            for (var tri = 0; tri < triangleCount; tri++)
            {
                var root = Find(tri);
                result[tri] = sumUv[root] / sumWeight[root];
            }

            return result;
        }

        // ---- 유틸 ---------------------------------------------------------------

        private static bool IsUnder(Transform transform, string ancestorName)
        {
            for (var current = transform; current != null; current = current.parent)
            {
                if (current.name == ancestorName)
                {
                    return true;
                }
            }

            return false;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, name);
        }

        private static string Sanitize(string name)
        {
            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(invalid, '_');
            }

            return name.Trim();
        }

        private sealed class Stats
        {
            public int ConvertedRenderers;
            public int SkippedDecals;
            public int SkippedOther;
            public int KeptAlphaClipMaterials;
        }
    }
}
