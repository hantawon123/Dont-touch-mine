using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Game.Client.Intro;

namespace Game.Editor.Intro
{
    /// <summary>
    /// 메뉴 Tools > Intro 에서 인트로 씬을 통째로 만들어주는 에디터 스크립트.
    ///
    /// 조명 모드가 두 가지다.
    ///   A) 스프라이트 전용  : 렌더 파이프라인과 무관하게 동작. 3D URP 프로젝트에 그대로 넣어도 됨.
    ///   B) Light2D 사용     : URP 렌더러 목록에 2D Renderer 가 있어야 한다.
    ///                         없으면 자동으로 A 로 폴백하고 로그를 남긴다.
    ///
    /// 스프라이트는 이미 8프레임 개별 PNG 로 나뉘어 있어서 Slice 작업이 필요 없다.
    /// (Unity 버전별로 달라지는 슬라이스 API 를 아예 쓰지 않기 위함)
    /// </summary>
    public static class IntroSceneBuilder
    {
        const string SpriteDir = "Assets/_Game/Content/UI/Intro";
        const string FrameDir  = "Assets/_Game/Content/UI/Intro/Frames";
        const string MatDir    = "Assets/_Game/Content/UI/Intro/Materials";
        const string AudioPath = "Assets/Intro/Audio/intro_audio_mix_10s.wav";
        const string ScenePath = "Assets/_Game/Content/Scenes/Intro.unity";
        const string NextScene = "Home";   // 인트로가 끝나면 갈 씬 (Game.Client.Home.HomeMenuPresenter.HomeSceneName)

        // ── 화면 규격 : 1 유닛 = 100px, Ortho Size 5.4 -> 19.2 x 10.8 유닛 ──
        const float PPU = 100f, OrthoSize = 5.4f, ScreenW = 19.2f;

        // ── 레이아웃 (월드 좌표) ──
        const float GroundY  = -3.60f;
        const float MoonY    = -1.60f;
        const float MoonDiam =  5.20f;
        const float GlowDiam = 16.50f;

        const float LoopWidth = 27.20f;
        const float LoopDur   =  5.00f;
        const float SpawnEdge = 13.40f;   // 오른쪽 바깥에서 등장
        const int   Direction = -1;       // 오른쪽 -> 왼쪽

        const int OrderGlow = 0, OrderMoon = 10, OrderBack = 20,
                  OrderThief = 30, OrderFront = 40, OrderVignette = 90, OrderFade = 100;
        const float FadeDur   = 0.70f;    // 마지막 0.7초 동안 배경색으로 페이드아웃 (총 길이 5초에 포함)

        static readonly Color BgColor    = new Color32(0x03, 0x08, 0x13, 0xFF);
        static readonly Color BackGround = new Color32(0x0D, 0x16, 0x2A, 0xFF);
        static readonly Color MoonLightC = new Color32(0xFF, 0xF6, 0xE0, 0xFF);
        static readonly Color AmbientC   = new Color32(0x5A, 0x7F, 0xB4, 0xFF);

        // (스프라이트 이름, 화면상 키(유닛), 무리 오프셋, 프레임 위상)  offset 클수록 앞(왼쪽)
        static readonly (string name, float height, float offset, int phase)[] Cast =
        {
            ("thief_C_run", 3.00f, 5.45f, 3),   // 선두
            ("thief_A_run", 2.72f, 2.85f, 6),
            ("thief_B_run", 2.40f, 0.00f, 1),   // 후미
        };

        // PNG 안에서 캐릭터가 실제로 차지하는 세로 픽셀
        static readonly Dictionary<string, float> CharPixelHeight = new Dictionary<string, float>
        {
            { "thief_A_run", 205f }, { "thief_B_run", 183f }, { "thief_C_run", 220f },
        };


        // ═══════════════════════════════════════════════════════════
        [MenuItem("Tools/Intro/1) 스프라이트 임포트 설정", priority = 10)]
        public static void ConfigureImporters()
        {
            foreach (var n in CharPixelHeight.Keys)
                for (int i = 0; i < 8; i++)
                    Setup($"{FrameDir}/{n}_{i}.png", new Vector2(0.5f, 0.059f));   // 피벗 = 발밑

            Setup($"{SpriteDir}/moon_disc.png", new Vector2(0.5f, 0.5f));
            Setup($"{SpriteDir}/moon_glow.png", new Vector2(0.5f, 0.5f));
            if (System.IO.File.Exists($"{SpriteDir}/moon_glow_linear.png"))
                Setup($"{SpriteDir}/moon_glow_linear.png", new Vector2(0.5f, 0.5f));
            Setup($"{SpriteDir}/vignette.png",  new Vector2(0.5f, 0.5f));
            Setup($"{SpriteDir}/ground_front.png", new Vector2(0.5f, 0.5f), tileable: true);
            Setup($"{SpriteDir}/ground_back.png",  new Vector2(0.5f, 0.5f), tileable: true);

            SetupAudio();

            AssetDatabase.Refresh();
            Debug.Log("[Intro] 스프라이트 임포트 설정 완료.");
        }

        static void SetupAudio()
        {
            var ai = AssetImporter.GetAtPath(AudioPath) as AudioImporter;
            if (ai == null) { Debug.LogError($"[Intro] 오디오 파일 없음: {AudioPath}"); return; }

            var settings = ai.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            settings.preloadAudioData = true;
            ai.defaultSampleSettings = settings;
            ai.forceToMono = false;
            EditorUtility.SetDirty(ai);
            ai.SaveAndReimport();
        }

        static void Setup(string path, Vector2 pivot, bool tileable = false)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) { Debug.LogError($"[Intro] 파일 없음: {path}"); return; }

            ti.textureType         = TextureImporterType.Sprite;
            ti.spriteImportMode    = SpriteImportMode.Single;
            ti.spritePivot         = pivot;
            ti.spritePixelsPerUnit = PPU;
            ti.filterMode          = FilterMode.Bilinear;
            ti.mipmapEnabled       = false;
            ti.alphaIsTransparency = true;
            ti.wrapMode            = tileable ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;

            var ps = ti.GetDefaultPlatformTextureSettings();
            ps.textureCompression = TextureImporterCompression.Uncompressed;  // 실루엣 계단현상 방지
            ps.maxTextureSize     = 2048;
            ti.SetPlatformTextureSettings(ps);

            // 피벗을 Custom 으로 확정 + Tiled 드로우 모드를 위한 Full Rect
            var st = new TextureImporterSettings();
            ti.ReadTextureSettings(st);
            st.spriteAlignment = (int)SpriteAlignment.Custom;
            st.spritePivot     = pivot;
            st.spriteMeshType  = SpriteMeshType.FullRect;
            ti.SetTextureSettings(st);

            EditorUtility.SetDirty(ti);
            ti.SaveAndReimport();
        }


        // ═══════════════════════════════════════════════════════════
        [MenuItem("Tools/Intro/2) 씬 구성 — 스프라이트 전용 (파이프라인 무관)", priority = 20)]
        public static void BuildSpritesOnly() => Build(false);

        [MenuItem("Tools/Intro/3) 씬 구성 — Light2D 사용 (2D Renderer 필요)", priority = 21)]
        public static void BuildLit() => Build(true);

        [MenuItem("Tools/Intro/4) 씬 저장 + Build Settings 맨 앞에 등록", priority = 30)]
        public static void SaveAndRegister()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[Intro] Play 중에는 씬을 저장할 수 없습니다. Play 를 끄고 다시 실행하세요.");
                return;
            }
            if (GameObject.Find("IntroScene") == null)
            {
                Debug.LogWarning("[Intro] 현재 씬에 IntroScene 이 없습니다. 2) 또는 3) 메뉴를 먼저 실행하세요.");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                Debug.LogError($"[Intro] 씬 저장 실패: {ScenePath}");
                return;
            }

            // Build Settings: 인트로를 index 0 에, 나머지 순서는 그대로 (Home 이 그 다음이 된다)
            var list = EditorBuildSettings.scenes
                .Where(e => e.path != ScenePath)
                .ToList();
            list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();

            bool hasNext = list.Any(e => e.enabled && e.path.EndsWith($"/{NextScene}.unity"));
            Debug.Log($"[Intro] 저장 완료: {ScenePath}\n" +
                      $"Build Settings 첫 씬 = Intro, 종료 후 '{NextScene}' 로드" +
                      (hasNext ? "" : $"  ⚠ '{NextScene}' 씬이 Build Settings 에 없습니다!"));
        }

        [MenuItem("Tools/Intro/진단 : 이 프로젝트에서 Light2D 를 쓸 수 있나?", priority = 40)]
        public static void Diagnose()
        {
            int idx = Find2DRendererIndex(out string detail);
            Debug.Log($"[Intro] 색공간 = {PlayerSettings.colorSpace}. " +
                      (PlayerSettings.colorSpace == ColorSpace.Linear
                          ? "달무리는 moon_glow_linear.png 를 사용합니다 (감마 합성 영상과 밝기 일치)."
                          : "달무리는 moon_glow.png 를 사용합니다."));
            Debug.Log(idx >= 0
                ? $"[Intro] 2D Renderer 를 찾았습니다 (index {idx}). 3) 메뉴로 Light2D 모드를 쓸 수 있습니다.\n{detail}"
                : $"[Intro] 2D Renderer 가 없습니다. 2) 스프라이트 전용 모드를 쓰거나, " +
                  $"URP Asset 의 Renderer List 에 2D Renderer 를 추가하세요.\n{detail}");
        }


        static void Build(bool wantLight2D)
        {
            int rendererIndex = -1;
            if (wantLight2D)
            {
                rendererIndex = Find2DRendererIndex(out string detail);
                if (rendererIndex < 0)
                {
                    Debug.LogWarning("[Intro] 2D Renderer 가 없어 Light2D 모드를 쓸 수 없습니다. " +
                                     "스프라이트 전용 모드로 진행합니다.\n" + detail);
                    wantLight2D = false;
                }
            }

            var old = GameObject.Find("IntroScene");
            if (old != null &&
                !EditorUtility.DisplayDialog("Intro", "기존 IntroScene 을 지우고 다시 만들까요?", "다시 만들기", "취소"))
                return;
            if (old != null) Object.DestroyImmediate(old);

            // Sprite-Unlit-Default 는 3D(Universal) 렌더러에서도 정상 동작한다.
            var unlit = MakeMaterial("Sprite-Unlit", "Universal Render Pipeline/2D/Sprite-Unlit-Default",
                                                    "Sprites/Default");
            var backMat = unlit;
            if (wantLight2D)
                backMat = MakeMaterial("Sprite-Lit", "Universal Render Pipeline/2D/Sprite-Lit-Default",
                                                    "Sprites/Default");

            var root = new GameObject("IntroScene");
            var ctrl = root.AddComponent<IntroLoopController>();
            ctrl.loopDuration = LoopDur;
            ctrl.autoFinish   = true;      // 한 바퀴(5초) 돌면 종료
            ctrl.loopCount    = 1;
            ctrl.allowSkip    = false;     // 스킵 불가 — 항상 5초를 다 본다

            // 종료(한 바퀴 완주) -> Home 단독 로드
            var exit = root.AddComponent<IntroSceneExit>();
            exit.nextSceneName = NextScene;

            // 영상 두 바퀴(10초)에 맞춰 만든 밤 앰비언스 + 발소리 믹스.
            // 현재 인트로는 첫 바퀴(5초)만 재생하고, 마지막 FadeDur 동안 화면과 함께 소리를 줄인다.
            var audioGo = NewChild(root, "IntroAudio", Vector3.zero);
            var audio = audioGo.AddComponent<AudioSource>();
            audio.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioPath);
            audio.playOnAwake = true;
            audio.loop = true;
            audio.spatialBlend = 0f;
            audio.volume = 0.8f;
            var audioFade = audioGo.AddComponent<IntroAudioFade>();
            audioFade.baseVolume = 0.8f;
            audioFade.fadeDuration = FadeDur;

            // ── 카메라 ──
            var camGo = new GameObject("IntroCamera");
            camGo.transform.SetParent(root.transform);
            camGo.transform.position = new Vector3(0, 0, -10);
            var cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();   // 없으면 매 프레임 "no audio listeners" 경고가 찍힌다
            cam.orthographic     = true;
            cam.orthographicSize = OrthoSize;
            cam.clearFlags       = CameraClearFlags.SolidColor;
            cam.backgroundColor  = BgColor;
            cam.depth            = 10;          // 씬에 다른 카메라가 있어도 인트로가 위로 오게
            if (wantLight2D) SetCameraRenderer(cam, rendererIndex);

            // ── 달 : 달무리 + 원반 (+ 선택적으로 가운데 고정 조명) ──
            var moon = NewChild(root, "Moon", new Vector3(0, MoonY, 0));
            var glow = Spr(moon, "MoonGlow", LoadGlow(), GlowDiam / (1024f / PPU),
                           OrderGlow, unlit, new Color(1, 1, 1, 0.42f));
            var disc = Spr(moon, "MoonDisc", Load("moon_disc"), MoonDiam / (1024f / PPU),
                           OrderMoon, unlit, Color.white);

            if (wantLight2D) AddLights(root, moon, glow, disc);

            // ── 바닥 ──
            Ground(root, "Ground_Back",  Load("ground_back"),  GroundY + 0.34f,
                   new Vector2(28f, 2.6f), OrderBack,  backMat, BackGround);
            Ground(root, "Ground_Front", Load("ground_front"), GroundY,
                   new Vector2(24f, 3.2f), OrderFront, unlit,   Color.black);

            // ── 도둑 3인 ──
            var crew = NewChild(root, "Thieves", Vector3.zero);
            foreach (var (name, height, offset, phase) in Cast)
            {
                var frames = LoadFrames(name);
                if (frames.Length != 8)
                {
                    Debug.LogError($"[Intro] {name} 프레임이 {frames.Length}장. " +
                                   $"{FrameDir} 확인 후 1) 메뉴를 먼저 실행하세요.");
                    continue;
                }

                var go = NewChild(crew, name, new Vector3(SpawnEdge, GroundY, 0));
                go.transform.localScale = Vector3.one * (height / (CharPixelHeight[name] / PPU));

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite       = frames[0];
                sr.material     = unlit;          // 조명 영향 X -> 항상 새까만 실루엣
                sr.color        = Color.black;
                sr.sortingOrder = OrderThief;

                var an = go.AddComponent<SpriteSheetAnimator>();
                an.frames      = frames;
                an.fps         = 8 * 12 / LoopDur;   // 19.2fps = 5초에 12사이클
                an.phaseOffset = phase;

                var rn = go.AddComponent<ThiefRunner>();
                rn.loopWidth   = LoopWidth;
                rn.speed       = LoopWidth / LoopDur;
                rn.startOffset = offset;
                rn.spawnEdge   = SpawnEdge;
                rn.direction   = Direction;
                rn.groundY     = GroundY;
            }

            Spr(root, "Vignette", Load("vignette"), ScreenW / (1920f / PPU),
                OrderVignette, unlit, Color.black);

            // ── 페이드아웃 : 마지막 FadeDur 초 동안 배경색으로 덮고, 다 덮이면 Home 로드 ──
            var fade = Spr(root, "FadeOut", null, 1f, OrderFade, unlit, new Color(BgColor.r, BgColor.g, BgColor.b, 0f));
            var fo = fade.AddComponent<IntroFadeOut>();   // 스프라이트는 Awake 에서 whiteTexture 로 생성
            fo.fadeDuration = FadeDur;
            fo.color        = BgColor;

            Selection.activeGameObject = root;
            Debug.Log($"[Intro] 씬 구성 완료 ({(wantLight2D ? "Light2D 모드" : "스프라이트 전용 모드")}). Play 로 확인하세요.");
        }


        // ── URP 2D Renderer 탐색 (타입을 직접 참조하지 않고 이름으로 확인) ──
        static int Find2DRendererIndex(out string detail)
        {
            detail = "";
            var rp = GraphicsSettings.defaultRenderPipeline;
            if (rp == null) { detail = "렌더 파이프라인이 Built-in 입니다 (URP 아님)."; return -1; }

            var so = new SerializedObject(rp);
            var list = so.FindProperty("m_RendererDataList");
            if (list == null || !list.isArray) { detail = $"URP Asset 을 읽지 못했습니다: {rp.name}"; return -1; }

            var names = new List<string>();
            int found = -1;
            for (int i = 0; i < list.arraySize; i++)
            {
                var o = list.GetArrayElementAtIndex(i).objectReferenceValue;
                string tn = o != null ? o.GetType().Name : "(null)";
                names.Add($"  [{i}] {tn}");
                if (found < 0 && tn == "Renderer2DData") found = i;
            }
            detail = $"현재 URP Asset: {rp.name}\n렌더러 목록:\n{string.Join("\n", names)}";
            return found;
        }

        static void SetCameraRenderer(Camera cam, int index)
        {
            // UniversalAdditionalCameraData.SetRenderer(int) 를 리플렉션으로 호출
            var t = System.Type.GetType(
                "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
            if (t == null) return;
            var data = cam.GetComponent(t) ?? cam.gameObject.AddComponent(t);
            t.GetMethod("SetRenderer")?.Invoke(data, new object[] { index });
        }

        static void AddLights(GameObject root, GameObject moon,
                              GameObject glow, GameObject disc)
        {
            var lt = System.Type.GetType(
                "UnityEngine.Rendering.Universal.Light2D, Unity.RenderPipelines.Universal.Runtime");
            if (lt == null) { Debug.LogWarning("[Intro] Light2D 타입을 찾지 못했습니다."); return; }

            // 전역광
            var g = NewChild(root, "GlobalLight2D", Vector3.zero).AddComponent(lt);
            SetProp(g, "lightType", 4);          // Global
            SetProp(g, "intensity", 0.16f);
            SetProp(g, "color", AmbientC);

            // 달빛 : 가운데 고정
            var m = NewChild(moon, "MoonLight2D", Vector3.zero).AddComponent(lt);
            SetProp(m, "lightType", 0);          // Point
            SetProp(m, "color", MoonLightC);
            SetProp(m, "intensity", 2.4f);
            SetProp(m, "pointLightInnerRadius", 1.2f);
            SetProp(m, "pointLightOuterRadius", 11.0f);
            SetProp(m, "falloffIntensity", 0.7f);
            // ShadowCaster2D 는 어디에도 붙이지 않는다 (요구사항: 그림자 없음)

            var mg = moon.AddComponent<MoonGlow>();
            mg.glowSprite = glow.GetComponent<SpriteRenderer>();
            mg.discSprite = disc.GetComponent<SpriteRenderer>();
            mg.lightObject = m as Component;
            mg.period = LoopDur;
        }

        static void SetProp(Object target, string prop, object value)
        {
            var p = target.GetType().GetProperty(prop);
            if (p == null) { Debug.LogWarning($"[Intro] Light2D.{prop} 없음 (URP 버전 차이)"); return; }
            if (p.PropertyType.IsEnum) value = System.Enum.ToObject(p.PropertyType, value);
            p.SetValue(target, value);
        }


        // ── 헬퍼 ──
        static GameObject NewChild(GameObject parent, string name, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform);
            go.transform.localPosition = localPos;
            return go;
        }

        static GameObject Spr(GameObject parent, string name, Sprite s, float scale,
                              int order, Material mat, Color color)
        {
            var go = NewChild(parent, name, Vector3.zero);
            go.transform.localScale = Vector3.one * scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s; sr.material = mat; sr.color = color; sr.sortingOrder = order;
            return go;
        }

        /// <summary>지면 윗면이 surfaceY 에 정확히 오도록 타일드 스프라이트를 배치.</summary>
        static void Ground(GameObject parent, string name, Sprite s, float surfaceY,
                           Vector2 size, int order, Material mat, Color color)
        {
            const float SurfaceRatio = 0.42f;                 // 텍스처에서 지면선의 세로 위치
            float offset = size.y * (0.5f - SurfaceRatio);    // 중심 -> 지면선 거리

            var go = NewChild(parent, name, new Vector3(0, surfaceY - offset, 0));
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s; sr.material = mat; sr.color = color; sr.sortingOrder = order;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.tileMode = SpriteTileMode.Continuous;
            sr.size     = size;
        }

        static Material MakeMaterial(string name, string shaderName, string fallbackShader)
        {
            if (!System.IO.Directory.Exists(MatDir))
            {
                System.IO.Directory.CreateDirectory(MatDir);
                AssetDatabase.Refresh();
            }
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;

            var sh = Shader.Find(shaderName) ?? Shader.Find(fallbackShader);
            if (sh == null) { Debug.LogError($"[Intro] 셰이더를 못 찾음: {shaderName}"); return null; }

            m = new Material(sh) { name = name };
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static Sprite Load(string n) => AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteDir}/{n}.png");

        /// <summary>
        /// 달무리 스프라이트 선택.
        /// 프리뷰 영상은 감마 공간에서 합성했는데, 프로젝트가 Linear 색공간이면 같은 알파가
        /// 리니어로 섞여 중심이 1.6배, 중간부가 3배 가까이 밝아진다.
        /// 그래서 Linear 프로젝트에서는 알파를 미리 sRGB->Linear 로 변환해 둔
        /// moon_glow_linear.png (docs/intro/tools/bake_linear.py 산출물) 를 쓴다.
        /// </summary>
        static Sprite LoadGlow()
        {
            if (PlayerSettings.colorSpace != ColorSpace.Linear) return Load("moon_glow");

            var linear = Load("moon_glow_linear");
            if (linear != null) return linear;

            Debug.LogWarning("[Intro] Linear 색공간인데 moon_glow_linear.png 가 없어 moon_glow.png 를 씁니다. " +
                             "달무리가 영상보다 밝게 보이면 docs/intro/tools/bake_linear.py 를 실행하세요.");
            return Load("moon_glow");
        }

        static Sprite[] LoadFrames(string n) =>
            Enumerable.Range(0, 8)
                      .Select(i => AssetDatabase.LoadAssetAtPath<Sprite>($"{FrameDir}/{n}_{i}.png"))
                      .Where(s => s != null)
                      .ToArray();
    }
}
