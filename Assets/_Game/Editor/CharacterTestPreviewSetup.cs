using Game.Client;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Editor
{
    public static class CharacterTestPreviewSetup
    {
        private const string PreviewPath = SmoothBearAssets.ModelPath;
        private const string ControllerPath = SmoothBearAssets.PreviewControllerPath;
        internal const string ScenePath = "Assets/Scenes/CharacterTest.unity";
        private const string PlayOnceKey = "Game.CharacterTest.PlayOnce";
        private const string PreviewName = "SmoothBear";
        private const string IdleState = "Idle";
        private static readonly string[] MotionNames =
        {
            "Walk_Forward",
            "Walk_Back",
            "Walk_Left",
            "Walk_Right",
            "Run_Forward",
            "Run_Back",
            "Run_Left",
            "Run_Right",
            "Jump",
            "Fall",
            "Land",
            "Crouch_Idle",
            "Crouch_Start",
            "Crouch_End",
            "Crouch_Walk_Forward",
            "Crouch_Walk_Back",
            "Crouch_Walk_Left",
            "Crouch_Walk_Right",
            "Prone_Idle",
            "Prone_Start",
            "Prone_End",
            "Crawl_Forward",
            "Crawl_Back",
            "Crawl_Left",
            "Crawl_Right",
            "Crouch_To_Prone",
            "Prone_To_Crouch",
            "Carry_Idle",
            "Carry_Walk_Forward",
            "Carry_Walk_Back",
            "Carry_Walk_Left",
            "Carry_Walk_Right",
            "Carry_Run_Forward",
            "Carry_Run_Back",
            "Carry_Run_Left",
            "Carry_Run_Right",
            "Carry_Crouch_Idle",
            "Carry_Crouch_Walk_Forward",
            "Carry_Crouch_Walk_Back",
            "Carry_Crouch_Walk_Left",
            "Carry_Crouch_Walk_Right",
            "Carry_Prone_Idle",
            "Carry_Crawl_Forward",
            "Carry_Crawl_Back",
            "Carry_Crawl_Left",
            "Carry_Crawl_Right",
            "Carry_Jump",
            "Carry_Land",
            "Carry_TwoHands",
            "PutUp_TwoHands",
            "PutDown_TwoHands",
            "Carry_TwoHands_Walk_Forward",
            "Carry_TwoHands_Walk_Back",
            "Carry_TwoHands_Walk_Left",
            "Carry_TwoHands_Walk_Right",
            "Carry_TwoHands_Run_Forward",
            "Carry_TwoHands_Run_Back",
            "Carry_TwoHands_Run_Left",
            "Carry_TwoHands_Run_Right",
            "Carry_TwoHands_Crouch_Idle",
            "Carry_TwoHands_Crouch_Walk_Forward",
            "Carry_TwoHands_Crouch_Walk_Back",
            "Carry_TwoHands_Crouch_Walk_Left",
            "Carry_TwoHands_Crouch_Walk_Right",
            "Carry_TwoHands_Prone_Idle",
            "Carry_TwoHands_Crawl_Forward",
            "Carry_TwoHands_Crawl_Back",
            "Carry_TwoHands_Crawl_Left",
            "Carry_TwoHands_Crawl_Right",
            "Carry_TwoHands_Jump",
            "Carry_TwoHands_Land",
            "Throw_TwoHands",
            "Throw_TwoHands_Walk",
            "Throw_TwoHands_Run",
            "Throw_TwoHands_Crouch",
            "Throw_TwoHands_Crouch_Walk",
            "Throw_TwoHands_Prone",
            "Throw_TwoHands_Crawl",
            "Carry_TwoHands_Crouch_Start",
            "Carry_TwoHands_Crouch_End",
            "Carry_TwoHands_Prone_Start",
            "Carry_TwoHands_Prone_End",
            "Carry_TwoHands_Crouch_To_Prone",
            "Carry_TwoHands_Prone_To_Crouch",
            "PutUp_TwoHands_Crouch",
            "PutDown_TwoHands_Crouch",
            "PutUp_TwoHands_Prone",
            "PutDown_TwoHands_Prone",
            "Pickup_Low",
            "Pickup_Crouch",
            "Pickup_Prone",
            "PutDown_Low",
            "PutDown_Crouch",
            "PutDown_Prone",
            "Throw",
            "Punch",
            "Punch_Walk",
            "Punch_Run",
            "Punch_Crouch",
            "Punch_Crouch_Walk",
            "Punch_Left",
            "Punch_Left_Walk",
            "Punch_Left_Run",
            "Punch_Left_Crouch",
            "Punch_Left_Crouch_Walk",
            "Punch_Combo",
            "Punch_Combo_Walk",
            "Punch_Combo_Run",
            "Punch_Combo_Crouch",
            "Punch_Combo_Crouch_Walk",
            "Hit",
            "Hit_Walk",
            "Hit_Run",
            "Hit_Crouch",
            "Hit_Crouch_Walk",
            "Hit_Prone",
            "Hit_Crawl",
            "Carry_TwoHands_Hit",
            "Carry_TwoHands_Hit_Walk",
            "Carry_TwoHands_Hit_Run",
            "Carry_TwoHands_Hit_Crouch",
            "Carry_TwoHands_Hit_Crouch_Walk",
            "Carry_TwoHands_Hit_Prone",
            "Carry_TwoHands_Hit_Crawl",
            "Stun_Start",
            "Stun_Idle",
            "Stun_End",
            "Emote_Wave",
            "Emote_Taunt",
            "Emote_Insult",
            "Emote_Chicken",
            "Emote_HipHop",
            "Emote_Spin",
        };

        [InitializeOnLoadMethod]
        private static void BuildAfterReload()
        {
            // CI must build committed assets, not run interactive authoring setup.
            if (Application.isBatchMode) return;
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    ApplyBlenderPreview();
                }
            };
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            if (scene.path == ScenePath && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                ApplyBlenderPreview();
            }
        }

        [MenuItem("Game/Setup/Apply CharacterTest Blender Preview")]
        public static void ApplyFromMenu()
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
            {
                EditorSceneManager.OpenScene(ScenePath);
            }

            if (!ApplyBlenderPreview())
            {
                EditorUtility.DisplayDialog(
                    "CharacterTest",
                    "SmoothBear 모델 또는 애니메이션이 없습니다.",
                    "OK");
                return;
            }

            EditorUtility.DisplayDialog(
                "CharacterTest",
                "SmoothBear 캐릭터의 전체 동작을 연결했습니다. Play 하세요.",
                "OK");
        }

        [MenuItem("Game/Setup/Play CharacterTest")]
        public static void PlayCharacterTest()
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
            {
                EditorSceneManager.OpenScene(ScenePath);
            }

            ApplyBlenderPreview();
            RequestPlayOverride();
            EditorApplication.isPlaying = true;
        }

        internal static void RequestPlayOverride()
        {
            SessionState.SetBool(PlayOnceKey, true);
        }

        internal static bool TryGetPlayOverride(out SceneAsset scene)
        {
            scene = null;
            if (!SessionState.GetBool(PlayOnceKey, false))
            {
                return false;
            }

            scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            return scene != null;
        }

        internal static void ClearPlayOverride()
        {
            SessionState.SetBool(PlayOnceKey, false);
        }

        public static bool ApplyBlenderPreview()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PreviewPath) == null ||
                SmoothBearAssets.LoadClip(IdleState) == null)
            {
                return false;
            }

            ConfigureController();
            AssignToScene();
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.IsValid() && scene.path == ScenePath && scene.isDirty)
            {
                EditorSceneManager.SaveScene(scene);
            }

            return true;
        }





        private static void ConfigureController()
        {
            var clip = SmoothBearAssets.LoadClip(IdleState);
            var bellyBinding = AnimationUtility.GetCurveBindings(clip)
                .FirstOrDefault(b => b.propertyName == "blendShape.Belly_Breath");
            var bellyCurve = AnimationUtility.GetEditorCurve(clip, bellyBinding);
            var bellyRange = bellyCurve == null || bellyCurve.keys.Length == 0
                ? 0f
                : bellyCurve.keys.Max(key => key.value) - bellyCurve.keys.Min(key => key.value);
            if (bellyRange < 90f)
            {
                throw new System.InvalidOperationException("SmoothBear idle clip must contain animated belly expansion (0 to 100).");
            }

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }

            var machine = controller.layers[0].stateMachine;
            controller.name = "SmoothBearPreview";
            var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == IdleState)
                ?? machine.AddState(IdleState);
            state.motion = clip;
            state.writeDefaultValues = false;
            foreach (var name in MotionNames)
            {
                var clipMotion = SmoothBearAssets.LoadClip(name);
                if (clipMotion == null)
                {
                    Debug.LogWarning($"CharacterTest: missing clip {name}");
                    continue;
                }
                var motionState = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == name)
                    ?? machine.AddState(name);
                motionState.motion = clipMotion;
                motionState.writeDefaultValues = false;
            }
            machine.defaultState = state;
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);
        }

        private static void AssignToScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != ScenePath)
            {
                return;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PreviewPath);
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            if (prefab == null || controller == null)
            {
                return;
            }

            DestroyNamed("FirstPlayerCapsule");
            DestroyNamed("PlayerCapsule");
            DestroyNamed("PlayerCapsule_BakedIdle");
            DestroyNamed("BlenderPreview");

            var preview = FindNamed(PreviewName);
            if (preview != null &&
                PrefabUtility.GetCorrespondingObjectFromSource(preview) != prefab)
            {
                Object.DestroyImmediate(preview);
                preview = null;
            }

            if (preview == null)
            {
                preview = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                preview.name = PreviewName;
                preview.transform.SetPositionAndRotation(
                    new Vector3(0f, 0f, -3.59f),
                    Quaternion.identity);
            }

            preview.SetActive(true);

            var animator = preview.GetComponent<Animator>();
            if (animator == null)
            {
                animator = preview.GetComponentInChildren<Animator>(true);
            }

            if (animator != null)
            {
                animator.enabled = true;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.runtimeAnimatorController = controller;
            }

            var driver = preview.GetComponent<CharacterTestPreviewDriver>();
            if (driver == null)
            {
                driver = preview.AddComponent<CharacterTestPreviewDriver>();
            }

            driver.ConfigureMotions(new[] { IdleState }.Concat(MotionNames).ToArray());
            EditorUtility.SetDirty(driver);
            if (animator != null)
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
            }

            PrefabUtility.RecordPrefabInstancePropertyModifications(driver);
            ApplyProjectMaterials(preview);

            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void ApplyProjectMaterials(GameObject preview) => SmoothBearAssets.ApplyMaterials(preview);

        private static GameObject FindNamed(string name)
        {
            foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name == name)
                {
                    return root;
                }
            }

            return null;
        }

        private static void DestroyNamed(string name)
        {
            var found = FindNamed(name);
            if (found != null)
            {
                Object.DestroyImmediate(found);
            }
        }


    }
}
