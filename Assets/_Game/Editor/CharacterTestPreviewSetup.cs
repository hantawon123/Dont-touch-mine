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
        private const string PreviewPath = "Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Idle.fbx";
        private const string ControllerPath = "Assets/Scenes/CharacterTest/First/FirstCharacterPreview.controller";
        private const string ScenePath = "Assets/Scenes/CharacterTest.unity";
        private const string PreviewName = "FirstPlayerCapsule";
        private const string Tag = "first-motion-preview-v2";
        private const string IdleState = "Idle";
        private static readonly MotionDefinition[] MotionDefinitions =
        {
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Walk_Forward.fbx", "Walk_Forward", 24, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Walk_Back.fbx", "Walk_Back", 24, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Walk_Left.fbx", "Walk_Left", 24, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Walk_Right.fbx", "Walk_Right", 24, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Run_Forward.fbx", "Run_Forward", 19, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Run_Back.fbx", "Run_Back", 19, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Run_Left.fbx", "Run_Left", 19, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Run_Right.fbx", "Run_Right", 19, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Jump.fbx", "Jump", 32, false),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Fall.fbx", "Fall", 48, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Land.fbx", "Land", 20, false),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Crouch_Idle.fbx", "Crouch_Idle", 60, true, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Crouch_Start.fbx", "Crouch_Start", 24, false, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Crouch_End.fbx", "Crouch_End", 24, false, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Crouch_Walk_Forward.fbx", "Crouch_Walk_Forward", 36, true, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Crouch_Walk_Back.fbx", "Crouch_Walk_Back", 36, true, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Crouch_Walk_Left.fbx", "Crouch_Walk_Left", 36, true, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Crouch_Walk_Right.fbx", "Crouch_Walk_Right", 36, true, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Prone_Idle.fbx", "Prone_Idle", 60, true, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Prone_Start.fbx", "Prone_Start", 24, false, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Prone_End.fbx", "Prone_End", 24, false, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Crawl_Forward.fbx", "Crawl_Forward", 36, true, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Crawl_Back.fbx", "Crawl_Back", 36, true, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Crawl_Left.fbx", "Crawl_Left", 36, true, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Crawl_Right.fbx", "Crawl_Right", 36, true, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Crouch_To_Prone.fbx", "Crouch_To_Prone", 36, false, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Prone_To_Crouch.fbx", "Prone_To_Crouch", 36, false, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_Idle.fbx", "Carry_Idle", 60, true, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_Walk_Forward.fbx", "Carry_Walk_Forward", 24, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_Walk_Back.fbx", "Carry_Walk_Back", 24, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_Walk_Left.fbx", "Carry_Walk_Left", 24, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_Walk_Right.fbx", "Carry_Walk_Right", 24, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_Run_Forward.fbx", "Carry_Run_Forward", 19, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_Run_Back.fbx", "Carry_Run_Back", 19, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_Run_Left.fbx", "Carry_Run_Left", 19, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_Run_Right.fbx", "Carry_Run_Right", 19, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_Crouch_Idle.fbx", "Carry_Crouch_Idle", 60, true, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_Crouch_Walk_Forward.fbx", "Carry_Crouch_Walk_Forward", 36, true, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_Crouch_Walk_Back.fbx", "Carry_Crouch_Walk_Back", 36, true, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_Crouch_Walk_Left.fbx", "Carry_Crouch_Walk_Left", 36, true, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_Crouch_Walk_Right.fbx", "Carry_Crouch_Walk_Right", 36, true, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_Prone_Idle.fbx", "Carry_Prone_Idle", 60, true, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_Crawl_Forward.fbx", "Carry_Crawl_Forward", 36, true, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_Crawl_Back.fbx", "Carry_Crawl_Back", 36, true, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_Crawl_Left.fbx", "Carry_Crawl_Left", 36, true, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_Crawl_Right.fbx", "Carry_Crawl_Right", 36, true, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_Jump.fbx", "Carry_Jump", 32, false, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_Land.fbx", "Carry_Land", 20, false, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_TwoHands.fbx", "Carry_TwoHands", 60, true, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_PutUp_TwoHands.fbx", "PutUp_TwoHands", 20, false, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_PutDown_TwoHands.fbx", "PutDown_TwoHands", 20, false, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_TwoHands_Walk_Forward.fbx", "Carry_TwoHands_Walk_Forward", 24, true, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_TwoHands_Walk_Back.fbx", "Carry_TwoHands_Walk_Back", 24, true, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_TwoHands_Walk_Left.fbx", "Carry_TwoHands_Walk_Left", 24, true, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_TwoHands_Walk_Right.fbx", "Carry_TwoHands_Walk_Right", 24, true, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_TwoHands_Run_Forward.fbx", "Carry_TwoHands_Run_Forward", 19, true, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_TwoHands_Run_Back.fbx", "Carry_TwoHands_Run_Back", 19, true, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_TwoHands_Run_Left.fbx", "Carry_TwoHands_Run_Left", 19, true, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_TwoHands_Run_Right.fbx", "Carry_TwoHands_Run_Right", 19, true, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_TwoHands_Crouch_Idle.fbx", "Carry_TwoHands_Crouch_Idle", 60, true, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_TwoHands_Crouch_Walk_Forward.fbx", "Carry_TwoHands_Crouch_Walk_Forward", 36, true, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_TwoHands_Crouch_Walk_Back.fbx", "Carry_TwoHands_Crouch_Walk_Back", 36, true, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_TwoHands_Crouch_Walk_Left.fbx", "Carry_TwoHands_Crouch_Walk_Left", 36, true, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_TwoHands_Crouch_Walk_Right.fbx", "Carry_TwoHands_Crouch_Walk_Right", 36, true, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_TwoHands_Prone_Idle.fbx", "Carry_TwoHands_Prone_Idle", 60, true, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_TwoHands_Crawl_Forward.fbx", "Carry_TwoHands_Crawl_Forward", 36, true, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_TwoHands_Crawl_Back.fbx", "Carry_TwoHands_Crawl_Back", 36, true, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_TwoHands_Crawl_Left.fbx", "Carry_TwoHands_Crawl_Left", 36, true, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_TwoHands_Crawl_Right.fbx", "Carry_TwoHands_Crawl_Right", 36, true, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_TwoHands_Jump.fbx", "Carry_TwoHands_Jump", 32, false, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_TwoHands_Land.fbx", "Carry_TwoHands_Land", 20, false, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Throw_TwoHands.fbx", "Throw_TwoHands", 24, false, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Throw_TwoHands_Walk.fbx", "Throw_TwoHands_Walk", 24, false, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Throw_TwoHands_Run.fbx", "Throw_TwoHands_Run", 24, false, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Throw_TwoHands_Crouch.fbx", "Throw_TwoHands_Crouch", 24, false, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Throw_TwoHands_Crouch_Walk.fbx", "Throw_TwoHands_Crouch_Walk", 24, false, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Throw_TwoHands_Prone.fbx", "Throw_TwoHands_Prone", 24, false, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Throw_TwoHands_Crawl.fbx", "Throw_TwoHands_Crawl", 24, false, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_TwoHands_Crouch_Start.fbx", "Carry_TwoHands_Crouch_Start", 24, false, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_TwoHands_Crouch_End.fbx", "Carry_TwoHands_Crouch_End", 24, false, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_TwoHands_Prone_Start.fbx", "Carry_TwoHands_Prone_Start", 24, false, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_TwoHands_Prone_End.fbx", "Carry_TwoHands_Prone_End", 24, false, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_TwoHands_Crouch_To_Prone.fbx", "Carry_TwoHands_Crouch_To_Prone", 36, false, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_TwoHands_Prone_To_Crouch.fbx", "Carry_TwoHands_Prone_To_Crouch", 36, false, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_PutUp_TwoHands_Crouch.fbx", "PutUp_TwoHands_Crouch", 20, false, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_PutDown_TwoHands_Crouch.fbx", "PutDown_TwoHands_Crouch", 20, false, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_PutUp_TwoHands_Prone.fbx", "PutUp_TwoHands_Prone", 20, false, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_PutDown_TwoHands_Prone.fbx", "PutDown_TwoHands_Prone", 20, false, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Pickup_Low.fbx", "Pickup_Low", 60, false, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Pickup_Crouch.fbx", "Pickup_Crouch", 48, false, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Pickup_Prone.fbx", "Pickup_Prone", 48, false, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_PutDown_Low.fbx", "PutDown_Low", 60, false, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_PutDown_Crouch.fbx", "PutDown_Crouch", 48, false, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_PutDown_Prone.fbx", "PutDown_Prone", 48, false, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Throw.fbx", "Throw", 24, false),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Punch.fbx", "Punch", 24, false, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Punch_Walk.fbx", "Punch_Walk", 24, false, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Punch_Run.fbx", "Punch_Run", 24, false, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Punch_Crouch.fbx", "Punch_Crouch", 24, false, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Punch_Crouch_Walk.fbx", "Punch_Crouch_Walk", 24, false, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Punch_Left.fbx", "Punch_Left", 24, false, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Punch_Left_Walk.fbx", "Punch_Left_Walk", 24, false, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Punch_Left_Run.fbx", "Punch_Left_Run", 24, false, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Punch_Left_Crouch.fbx", "Punch_Left_Crouch", 24, false, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Punch_Left_Crouch_Walk.fbx", "Punch_Left_Crouch_Walk", 24, false, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Punch_Combo.fbx", "Punch_Combo", 24, true, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Punch_Combo_Walk.fbx", "Punch_Combo_Walk", 24, true, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Punch_Combo_Run.fbx", "Punch_Combo_Run", 120, true, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Punch_Combo_Crouch.fbx", "Punch_Combo_Crouch", 24, true, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Punch_Combo_Crouch_Walk.fbx", "Punch_Combo_Crouch_Walk", 72, true, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Hit.fbx", "Hit", 30, false, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Hit_Walk.fbx", "Hit_Walk", 30, false, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Hit_Run.fbx", "Hit_Run", 30, false, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Hit_Crouch.fbx", "Hit_Crouch", 30, false, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Hit_Crouch_Walk.fbx", "Hit_Crouch_Walk", 30, false, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Stun_Start.fbx", "Stun_Start", 66, false, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Stun_Idle.fbx", "Stun_Idle", 60, true, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Stun_End.fbx", "Stun_End", 36, false, true),
        };

        [InitializeOnLoadMethod]
        private static void BuildAfterReload()
        {
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
            if (!ApplyBlenderPreview())
            {
                EditorUtility.DisplayDialog(
                    "CharacterTest",
                    "First 캐릭터 FBX가 없습니다.",
                    "OK");
                return;
            }

            EditorUtility.DisplayDialog(
                "CharacterTest",
                "First 캐릭터의 전체 동작을 연결했습니다. Play 하세요.",
                "OK");
        }

        public static bool ApplyBlenderPreview()
        {
            var importer = AssetImporter.GetAtPath(PreviewPath) as ModelImporter;
            if (importer == null)
            {
                return false;
            }

            ConfigurePreview(importer);
            foreach (var motion in MotionDefinitions)
            {
                var motionImporter = AssetImporter.GetAtPath(motion.Path) as ModelImporter;
                if (motionImporter == null)
                {
                    return false;
                }

                ConfigureMotion(motionImporter, motion);
            }

            ConfigureController();
            AssignToScene();
            return true;
        }

        private static void ConfigurePreview(ModelImporter importer)
        {
            if (importer.userData == Tag &&
                importer.animationType == ModelImporterAnimationType.Generic &&
                importer.avatarSetup == ModelImporterAvatarSetup.CreateFromThisModel)
            {
                return;
            }

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.importBlendShapes = true;
            importer.importAnimation = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.clipAnimations = new[]
            {
                new ModelImporterClipAnimation
                {
                    name = IdleState,
                    takeName = importer.defaultClipAnimations.First().takeName,
                    firstFrame = 0,
                    lastFrame = 60,
                    loopTime = true,
                    keepOriginalOrientation = true,
                    keepOriginalPositionY = true,
                    keepOriginalPositionXZ = true,
                }
            };
            importer.userData = Tag;
            importer.SaveAndReimport();
        }

        private static void ConfigureMotion(ModelImporter importer, MotionDefinition motion)
        {
            if (importer.userData == Tag &&
                importer.animationType == ModelImporterAnimationType.Generic &&
                importer.avatarSetup == ModelImporterAvatarSetup.CreateFromThisModel &&
                importer.clipAnimations.Length == 1 &&
                importer.clipAnimations[0].name == motion.State &&
                Mathf.Approximately(importer.clipAnimations[0].lastFrame, motion.LastFrame) &&
                importer.clipAnimations[0].loopTime == motion.Loop &&
                importer.importBlendShapes == motion.ImportBlendShapes)
            {
                return;
            }

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.importAnimation = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = motion.ImportBlendShapes;
            var takeName = "Scene";
            var takes = importer.defaultClipAnimations;
            if (takes != null && takes.Length > 0)
            {
                takeName = takes[0].takeName;
            }

            importer.clipAnimations = new[]
            {
                new ModelImporterClipAnimation
                {
                    name = motion.State,
                    takeName = takeName,
                    firstFrame = 0,
                    lastFrame = motion.LastFrame,
                    loopTime = motion.Loop,
                    keepOriginalOrientation = true,
                    keepOriginalPositionY = true,
                    keepOriginalPositionXZ = true,
                }
            };
            importer.userData = Tag;
            importer.SaveAndReimport();
        }

        private static void ConfigureController()
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath(PreviewPath)
                .OfType<AnimationClip>().First(c => c.name == IdleState);
            var bellyBinding = AnimationUtility.GetCurveBindings(clip)
                .FirstOrDefault(b => b.propertyName == "blendShape.Belly_Breath");
            var bellyCurve = AnimationUtility.GetEditorCurve(clip, bellyBinding);
            var bellyRange = bellyCurve == null || bellyCurve.keys.Length == 0
                ? 0f
                : bellyCurve.keys.Max(key => key.value) - bellyCurve.keys.Min(key => key.value);
            if (bellyRange < 90f)
            {
                throw new System.InvalidOperationException("First idle FBX must contain animated belly expansion (0 to 100).");
            }

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }

            var machine = controller.layers[0].stateMachine;
            var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == IdleState)
                ?? machine.AddState(IdleState);
            state.motion = clip;
            state.writeDefaultValues = false;
            foreach (var motion in MotionDefinitions)
            {
                var clipMotion = AssetDatabase.LoadAllAssetsAtPath(motion.Path)
                    .OfType<AnimationClip>().FirstOrDefault(clip => clip.name == motion.State);
                if (clipMotion == null)
                {
                    Debug.LogWarning($"CharacterTest: missing clip {motion.State} at {motion.Path}");
                    continue;
                }
                var motionState = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == motion.State)
                    ?? machine.AddState(motion.State);
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

            driver.ConfigureMotions(new[] { IdleState }.Concat(MotionDefinitions.Select(motion => motion.State)).ToArray());
            EditorUtility.SetDirty(driver);
            PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
            ApplyProjectMaterials(preview);

            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void ApplyProjectMaterials(GameObject preview)
        {
            var body = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Game/Content/Materials/MAT_Capsule_Character.mat");
            var outline = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Game/Content/Materials/MAT_Outline.mat");
            var hood = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Game/Content/Materials/MAT_Hood.mat");
            var eyeWhite = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Game/Content/Materials/MAT_EyeWhite.mat");
            var eyePupil = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Game/Content/Materials/MAT_EyePupil.mat");
            var mouth = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Game/Content/Materials/MAT_Mouth.mat");

            foreach (var renderer in preview.GetComponentsInChildren<Renderer>(true))
            {
                switch (renderer.name)
                {
                    case "Body" when body != null:
                        renderer.sharedMaterials = outline != null
                            ? new[] { body, outline }
                            : new[] { body };
                        break;
                    case "Hood" when hood != null:
                        renderer.sharedMaterial = hood;
                        break;
                    case "Eye_White_L":
                    case "Eye_White_R":
                        if (eyeWhite != null)
                        {
                            renderer.sharedMaterial = eyeWhite;
                        }
                        break;
                    case "Eye_Pupil_L":
                    case "Eye_Pupil_R":
                        if (eyePupil != null)
                        {
                            renderer.sharedMaterial = eyePupil;
                        }
                        break;
                    case "Mouth_Smile" when mouth != null:
                        renderer.sharedMaterial = mouth;
                        break;
                }

                EditorUtility.SetDirty(renderer);
            }
        }

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

        private sealed class MotionDefinition
        {
            public MotionDefinition(string path, string state, float lastFrame, bool loop, bool importBlendShapes = false)
            {
                Path = path;
                State = state;
                LastFrame = lastFrame;
                Loop = loop;
                ImportBlendShapes = importBlendShapes;
            }

            public string Path { get; }
            public string State { get; }
            public float LastFrame { get; }
            public bool Loop { get; }
            public bool ImportBlendShapes { get; }
        }
    }
}
