using System.Linq;
using Game.Client.Match;
using Game.Client.Players;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Game.Editor
{
    /// <summary>
    /// 엔딩 무대 도구.
    /// 1) Result 씬에 유치장 환경·카메라·EndingStage를 배치하고 스코프에 연결한다.
    /// 2) 조립 씬(EndingBuild)에 임시 아바타 복제본을 세워 구도를 확인한다.
    /// </summary>
    public static class EndingStageSetupMenu
    {
        private const string MenuRoot = "Game/Ending/";
        private const string ResultScenePath = "Assets/_Game/Content/Scenes/Result.unity";
        private const string EnvironmentPrefabPath = "Assets/_Game/Content/Prefabs/EndingHoldingEnvironment.prefab";
        private const string CharacterPrefabPath = "Assets/_Game/Content/Prefabs/PlayerCharacter.prefab";
        private const string StageName = "EndingStage";
        private const string PreviewRootName = "EndingPreviewAvatars";

        // 인게임 맵과 겹치지 않게 무대를 아래로 내려 둔다. 카메라는 무대를 따라간다.
        private static readonly Vector3 StageOffset = new(0f, -300f, 0f);
        private const float StageCameraDepth = 5f;
        // 2026-09-08 C안: 앵커 (0, 1.75, 6.6) / 피치 4° / 화각 40. 캐릭터가 화면 높이의 32~45%.
        private const float StageCameraFov = 40f;

        [MenuItem(MenuRoot + "1. Place Ending Stage In Result Scene")]
        public static void PlaceStageInResultScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ResultScenePath, OpenSceneMode.Single);

            var old = scene.GetRootGameObjects().FirstOrDefault(g => g.name == StageName);
            if (old != null) Undo.DestroyObjectImmediate(old);

            var stageGo = new GameObject(StageName);
            stageGo.transform.position = StageOffset;
            var stage = stageGo.AddComponent<EndingStage>();

            var envPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnvironmentPrefabPath);
            var env = (GameObject)PrefabUtility.InstantiatePrefab(envPrefab, scene);
            env.transform.SetParent(stageGo.transform, false);
            env.transform.localPosition = Vector3.zero;

            var camGo = new GameObject("EndingCamera");
            camGo.transform.SetParent(stageGo.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = StageCameraFov;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            cam.depth = StageCameraDepth;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.06f, 0.08f);
            cam.enabled = false; // 프레젠터가 켠다
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            var anchor = env.transform.Find("EndingCameraAnchor");
            var escape = env.transform.Find("EscapeSpawnPoints");
            var arrest = env.transform.Find("ArrestSpawnPoints");
            var visuals = new GameObject("Visuals").transform;
            visuals.SetParent(stageGo.transform, false);
            stage.Wire(cam, anchor, escape, arrest, visuals);
            if (anchor != null) camGo.transform.SetPositionAndRotation(anchor.position, anchor.rotation);

            var scope = scene.GetRootGameObjects().Select(g => g.GetComponentInChildren<Game.Bootstrap.ResultLifetimeScope>(true)).FirstOrDefault(s => s != null);
            if (scope == null)
            {
                Debug.LogError("[Ending] ResultLifetimeScope not found in Result scene.");
            }
            else
            {
                var so = new SerializedObject(scope);
                so.FindProperty("endingStage").objectReferenceValue = stage;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = stageGo;
            Debug.Log($"[Ending] Stage placed in Result scene at {StageOffset}: env, camera(depth {StageCameraDepth}), " +
                      $"escape slots {stage.EscapeSlotCount}, arrest slots {stage.ArrestSlotCount}, scope wired={scope != null}.");
        }

        [MenuItem(MenuRoot + "2. Preview Avatars In Active Scene")]
        public static void PreviewAvatars()
        {
            var scene = SceneManager.GetActiveScene();
            ClearPreview();
            var env = GameObject.Find("EndingHoldingEnvironment");
            if (env == null)
            {
                EditorUtility.DisplayDialog("Ending Preview", "활성 씬에 EndingHoldingEnvironment가 없습니다.", "확인");
                return;
            }

            var character = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefabPath);
            var root = new GameObject(PreviewRootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            var source = (GameObject)PrefabUtility.InstantiatePrefab(character, scene);
            source.SetActive(false);
            var count = 0;
            try
            {
                foreach (var group in new[] { "EscapeSpawnPoints", "ArrestSpawnPoints" })
                {
                    var g = env.transform.Find(group);
                    if (g == null) continue;
                    foreach (Transform slot in g)
                    {
                        var visual = new ReplayVisual(source.transform, root.transform);
                        visual.Target.name = $"Preview_{slot.name}";
                        visual.Target.SetPositionAndRotation(slot.position, slot.rotation);
                        visual.Target.gameObject.SetActive(true);
                        foreach (var r in visual.Target.GetComponentsInChildren<Renderer>(true)) r.forceRenderingOff = false;
                        count++;
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(source);
            }

            Selection.activeGameObject = root;
            Debug.Log($"[Ending] Preview avatars placed: {count}. 저장하지 말고 확인 후 'Clear Preview Avatars'로 지우세요.");
        }

        [MenuItem(MenuRoot + "3. Clear Preview Avatars")]
        public static void ClearPreview()
        {
            var root = GameObject.Find(PreviewRootName);
            if (root != null) Object.DestroyImmediate(root);
        }
    }
}
