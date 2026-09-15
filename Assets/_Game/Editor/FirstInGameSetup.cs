using System.Linq;
using Game.Client.Character;
using Game.Core.Players;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// First Generic 클립으로 인게임 PlayerAnimator를 채우고
    /// PlayerCharacter Visual을 First 메시로 교체한다.
    /// </summary>
    public static class FirstInGameSetup
    {
        private const string IdlePath = "Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Idle.fbx";
        private const string ControllerPath = "Assets/_Game/Content/Animations/PlayerAnimator.controller";
        private const string PlayerPrefabPath = "Assets/_Game/Content/Prefabs/PlayerCharacter.prefab";
        private const string CatalogPath = "Assets/_Game/Content/Config/AvatarPartCatalog.asset";
        private const string IdleState = "Idle";
        private static readonly Vector3 VisualScale = new(0.55f, 0.55f, 0.55f);
        private static readonly MotionDefinition[] Motions =
        {
            new(IdlePath, IdleState, 60, true),
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
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Crouch_Idle.fbx", "Crouch_Idle", 60, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Crouch_Walk_Forward.fbx", "Crouch_Walk_Forward", 36, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Crouch_Start.fbx", "Crouch_Start", 24, false),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Crouch_End.fbx", "Crouch_End", 24, false),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Crouch_Walk_Left.fbx", "Crouch_Walk_Left", 36, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Crouch_Walk_Right.fbx", "Crouch_Walk_Right", 36, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Crouch_Walk_Back.fbx", "Crouch_Walk_Back", 36, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Pickup_Low.fbx", "Pickup_Low", 60, false),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Pickup_Crouch.fbx", "Pickup_Crouch", 48, false),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Pickup_Prone.fbx", "Pickup_Prone", 48, false),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_Idle.fbx", "Carry_Idle", 60, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_Walk_Forward.fbx", "Carry_Walk_Forward", 24, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_Walk_Back.fbx", "Carry_Walk_Back", 24, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_Walk_Left.fbx", "Carry_Walk_Left", 24, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_Walk_Right.fbx", "Carry_Walk_Right", 24, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_Run_Forward.fbx", "Carry_Run_Forward", 19, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_Run_Back.fbx", "Carry_Run_Back", 19, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_Run_Left.fbx", "Carry_Run_Left", 19, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_Run_Right.fbx", "Carry_Run_Right", 19, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_Crouch_Idle.fbx", "Carry_Crouch_Idle", 60, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_Crouch_Walk_Forward.fbx", "Carry_Crouch_Walk_Forward", 36, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_Crouch_Walk_Back.fbx", "Carry_Crouch_Walk_Back", 36, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_Crouch_Walk_Left.fbx", "Carry_Crouch_Walk_Left", 36, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_Crouch_Walk_Right.fbx", "Carry_Crouch_Walk_Right", 36, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_Prone_Idle.fbx", "Carry_Prone_Idle", 60, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_Crawl_Forward.fbx", "Carry_Crawl_Forward", 36, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_Crawl_Back.fbx", "Carry_Crawl_Back", 36, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_Crawl_Left.fbx", "Carry_Crawl_Left", 36, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_Crawl_Right.fbx", "Carry_Crawl_Right", 36, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_PutDown_Low.fbx", "PutDown_Low", 60, false),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_PutDown_Crouch.fbx", "PutDown_Crouch", 48, false),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_PutDown_Prone.fbx", "PutDown_Prone", 48, false),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Throw.fbx", "Throw", 24, false),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Prone_Start.fbx", "Prone_Start", 24, false),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Prone_Idle.fbx", "Prone_Idle", 60, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Crawl_Forward.fbx", "Crawl_Forward", 36, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Crawl_Back.fbx", "Crawl_Back", 36, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Crawl_Left.fbx", "Crawl_Left", 36, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Crawl_Right.fbx", "Crawl_Right", 36, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Prone_End.fbx", "Prone_End", 24, false),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Crouch_To_Prone.fbx", "Crouch_To_Prone", 36, false),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Prone_To_Crouch.fbx", "Prone_To_Crouch", 36, false),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Punch.fbx", "Punch", 24, false),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Punch_Walk.fbx", "Punch_Walk", 24, false),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Punch_Run.fbx", "Punch_Run", 24, false),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Punch_Crouch.fbx", "Punch_Crouch", 24, false),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Punch_Crouch_Walk.fbx", "Punch_Crouch_Walk", 24, false),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Punch_Left.fbx", "Punch_Left", 24, false),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Punch_Left_Walk.fbx", "Punch_Left_Walk", 24, false),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Punch_Left_Run.fbx", "Punch_Left_Run", 24, false),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Punch_Left_Crouch.fbx", "Punch_Left_Crouch", 24, false),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Punch_Left_Crouch_Walk.fbx", "Punch_Left_Crouch_Walk", 24, false),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Punch_Combo.fbx", "Punch_Combo", 24, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Punch_Combo_Walk.fbx", "Punch_Combo_Walk", 24, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Punch_Combo_Run.fbx", "Punch_Combo_Run", 120, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Punch_Combo_Crouch.fbx", "Punch_Combo_Crouch", 24, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Punch_Combo_Crouch_Walk.fbx", "Punch_Combo_Crouch_Walk", 72, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Hit.fbx", "Hit", 30, false),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Hit_Walk.fbx", "Hit_Walk", 30, false),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Hit_Run.fbx", "Hit_Run", 30, false),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Hit_Crouch.fbx", "Hit_Crouch", 30, false),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Hit_Crouch_Walk.fbx", "Hit_Crouch_Walk", 30, false),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Stun_Start.fbx", "Stun_Start", 66, false),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Stun_Idle.fbx", "Stun_Idle", 60, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Stun_End.fbx", "Stun_End", 36, false),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_Jump.fbx", "Carry_Jump", 32, false),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_Land.fbx", "Carry_Land", 20, false),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_TwoHands.fbx", "Carry_TwoHands", 60, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_PutUp_TwoHands.fbx", "PutUp_TwoHands", 20, false),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_PutDown_TwoHands.fbx", "PutDown_TwoHands", 20, false),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Throw_TwoHands.fbx", "Throw_TwoHands", 24, false),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Throw_TwoHands_Walk.fbx", "Throw_TwoHands_Walk", 24, false),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Throw_TwoHands_Run.fbx", "Throw_TwoHands_Run", 24, false),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Throw_TwoHands_Crouch.fbx", "Throw_TwoHands_Crouch", 24, false),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Throw_TwoHands_Crouch_Walk.fbx", "Throw_TwoHands_Crouch_Walk", 24, false),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Throw_TwoHands_Prone.fbx", "Throw_TwoHands_Prone", 24, false),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Throw_TwoHands_Crawl.fbx", "Throw_TwoHands_Crawl", 24, false),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_TwoHands_Crouch_Start.fbx", "Carry_TwoHands_Crouch_Start", 24, false),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_TwoHands_Crouch_End.fbx", "Carry_TwoHands_Crouch_End", 24, false),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_TwoHands_Prone_Start.fbx", "Carry_TwoHands_Prone_Start", 24, false),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_TwoHands_Prone_End.fbx", "Carry_TwoHands_Prone_End", 24, false),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_TwoHands_Crouch_To_Prone.fbx", "Carry_TwoHands_Crouch_To_Prone", 36, false),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_TwoHands_Prone_To_Crouch.fbx", "Carry_TwoHands_Prone_To_Crouch", 36, false),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_PutUp_TwoHands_Crouch.fbx", "PutUp_TwoHands_Crouch", 20, false),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_PutDown_TwoHands_Crouch.fbx", "PutDown_TwoHands_Crouch", 20, false),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_PutUp_TwoHands_Prone.fbx", "PutUp_TwoHands_Prone", 20, false),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_PutDown_TwoHands_Prone.fbx", "PutDown_TwoHands_Prone", 20, false),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_TwoHands_Walk_Forward.fbx", "Carry_TwoHands_Walk_Forward", 24, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_TwoHands_Walk_Back.fbx", "Carry_TwoHands_Walk_Back", 24, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_TwoHands_Walk_Left.fbx", "Carry_TwoHands_Walk_Left", 24, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_TwoHands_Walk_Right.fbx", "Carry_TwoHands_Walk_Right", 24, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_TwoHands_Run_Forward.fbx", "Carry_TwoHands_Run_Forward", 19, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_TwoHands_Run_Back.fbx", "Carry_TwoHands_Run_Back", 19, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_TwoHands_Run_Left.fbx", "Carry_TwoHands_Run_Left", 19, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_TwoHands_Run_Right.fbx", "Carry_TwoHands_Run_Right", 19, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_TwoHands_Crouch_Idle.fbx", "Carry_TwoHands_Crouch_Idle", 60, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_TwoHands_Crouch_Walk_Forward.fbx", "Carry_TwoHands_Crouch_Walk_Forward", 36, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_TwoHands_Crouch_Walk_Back.fbx", "Carry_TwoHands_Crouch_Walk_Back", 36, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_TwoHands_Crouch_Walk_Left.fbx", "Carry_TwoHands_Crouch_Walk_Left", 36, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_TwoHands_Crouch_Walk_Right.fbx", "Carry_TwoHands_Crouch_Walk_Right", 36, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_TwoHands_Prone_Idle.fbx", "Carry_TwoHands_Prone_Idle", 60, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_TwoHands_Crawl_Forward.fbx", "Carry_TwoHands_Crawl_Forward", 36, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_TwoHands_Crawl_Back.fbx", "Carry_TwoHands_Crawl_Back", 36, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_TwoHands_Crawl_Left.fbx", "Carry_TwoHands_Crawl_Left", 36, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_TwoHands_Crawl_Right.fbx", "Carry_TwoHands_Crawl_Right", 36, true),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_TwoHands_Jump.fbx", "Carry_TwoHands_Jump", 32, false),
            new("Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Carry_TwoHands_Land.fbx", "Carry_TwoHands_Land", 20, false),
        };

        private static bool appliedThisDomain;

        [InitializeOnLoadMethod]
        private static void BuildAfterReload()
        {
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    Apply();
                }
            };
        }

        [MenuItem("Game/Setup/Apply First In-Game Character")]
        public static void ApplyFromMenu()
        {
            appliedThisDomain = false;
            if (!Apply())
            {
                EditorUtility.DisplayDialog("First In-Game", "First 클립 또는 Player 프리팹을 찾지 못했습니다.", "OK");
                return;
            }

            EditorUtility.DisplayDialog("First In-Game", "인게임 캐릭터에 First 모습과 클립을 연결했습니다.", "OK");
        }

        public static void ApplyFromBatch()
        {
            appliedThisDomain = false;
            if (!Apply())
            {
                throw new System.InvalidOperationException(
                    "First in-game apply failed: missing First clip or Player prefab.");
            }
        }

        public static bool Apply()
        {
            if (appliedThisDomain)
            {
                return true;
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(IdlePath) == null)
            {
                return false;
            }

            var idle = LoadClip(IdlePath, IdleState);
            if (idle == null || !ConfigureController(idle))
            {
                return false;
            }

            if (!AssignVisual())
            {
                return false;
            }

            appliedThisDomain = true;
            return true;
        }

        private static bool ConfigureController(AnimationClip idle)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }

            if (controller.parameters.All(parameter => parameter.name != "Speed"))
            {
                controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            }

            var machine = controller.layers[0].stateMachine;
            // Rebind every state on reload so replaced FBX sub-assets (including
            // crawl blend-shape curves) cannot leave the controller pointing at
            // an older imported clip.
            BindState(machine, IdleState, idle);
            foreach (var motion in Motions)
            {
                var clip = LoadClip(motion.Path, motion.State);
                if (clip == null)
                {
                    Debug.LogWarning($"First In-Game: missing clip {motion.State} at {motion.Path}");
                    continue;
                }

                BindState(machine, motion.State, clip);
            }

            BindState(machine, "Punch", LoadClip(
                "Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Punch.fbx",
                "Punch") ?? idle);
            BindState(machine, "Stunned", LoadClip(
                "Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Stun_Idle.fbx",
                "Stun_Idle") ?? idle);
            BindState(machine, "Locomotion", idle);
            BindState(machine, "CrouchMove", LoadClip(
                "Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Crouch_Idle.fbx",
                "Crouch_Idle") ?? idle);
            BindState(machine, "Crawl", LoadClip(
                "Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Crawl_Forward.fbx",
                "Crawl_Forward") ?? idle);
            BindState(machine, "Airborne", LoadClip(
                "Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Fall.fbx",
                "Fall") ?? idle);

            machine.defaultState = machine.states.Select(entry => entry.state)
                .First(state => state.name == IdleState);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);
            return true;
        }

        private static bool IsControllerReady(AnimatorStateMachine machine, AnimationClip idle)
        {
            var states = machine.states.Select(entry => entry.state).ToArray();
            return states.Any(state => state.name == IdleState && state.motion == idle) &&
                   states.Any(state => state.name == "Punch") &&
                   states.Any(state => state.name == "Punch_Left") &&
                   states.Any(state => state.name == "Punch_Combo_Crouch_Walk") &&
                   states.Any(state => state.name == "Punch_Crouch") &&
                   states.Any(state => state.name == "Hit_Walk") &&
                   states.Any(state => state.name == "Stunned") &&
                   states.Any(state => state.name == "Throw") &&
                   states.Any(state => state.name == "Throw_TwoHands") &&
                   states.Any(state => state.name == "Throw_TwoHands_Walk") &&
                   states.Any(state => state.name == "Throw_TwoHands_Prone") &&
                   states.Any(state => state.name == "Walk_Left") &&
                   states.Any(state => state.name == "Jump") &&
                   states.Any(state => state.name == "Carry_TwoHands") &&
                   states.Any(state => state.name == "PutUp_TwoHands") &&
                   states.Any(state => state.name == "PutDown_TwoHands") &&
                   states.Any(state => state.name == "Carry_TwoHands_Jump") &&
                   states.Any(state => state.name == "Carry_TwoHands_Land") &&
                   states.Any(state => state.name == "Carry_TwoHands_Crouch_Start") &&
                   states.Any(state => state.name == "Crouch_Idle") &&
                   states.Any(state => state.name == "Stun_Idle") &&
                   states.Any(state => state.name == "Pickup_Crouch") &&
                   states.Any(state => state.name == "PutDown_Prone") &&
                   states.Any(state => state.name == "Prone_Idle");
        }

        private static void BindState(AnimatorStateMachine machine, string name, AnimationClip clip)
        {
            var state = machine.states.Select(entry => entry.state).FirstOrDefault(entry => entry.name == name)
                ?? machine.AddState(name);
            state.motion = clip;
            state.writeDefaultValues = false;
            state.speed = 1f;
        }

        private static AnimationClip LoadClip(string path, string state) =>
            AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .FirstOrDefault(clip => clip.name == state);

        private static bool AssignVisual()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(IdlePath);
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            var catalog = AssetDatabase.LoadAssetAtPath<AvatarPartCatalog>(CatalogPath);
            if (model == null || controller == null)
            {
                return false;
            }

            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                var visual = root.transform.Find("Visual");
                if (IsFirstVisual(visual, model, controller) &&
                    HasBodyColorTarget(root) &&
                    visual.localScale == VisualScale)
                {
                    ApplyProjectMaterials(visual.gameObject);
                    PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                    return true;
                }

                if (IsFirstVisual(visual, model, null))
                {
                    visual.localScale = VisualScale;
                    BindAnimator(visual.gameObject, controller);
                    WireAppearance(root, visual.gameObject, catalog);
                    ApplyProjectMaterials(visual.gameObject);
                    PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                    return true;
                }

                if (visual != null)
                {
                    Object.DestroyImmediate(visual.gameObject);
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                instance.name = "Visual";
                instance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                instance.transform.localScale = VisualScale;
                BindAnimator(instance, controller);
                WireAppearance(root, instance, catalog);
                ApplyProjectMaterials(instance);
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void WireAppearance(GameObject root, GameObject visual, AvatarPartCatalog catalog)
        {
            var applier = root.GetComponent<AvatarAppearanceApplier>() ?? root.AddComponent<AvatarAppearanceApplier>();
            var body = visual.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .FirstOrDefault(renderer => renderer.name == "Body")
                ?? visual.GetComponentInChildren<SkinnedMeshRenderer>(true);
            var so = new SerializedObject(applier);
            so.FindProperty("catalog").objectReferenceValue = catalog;
            var targets = so.FindProperty("targets");
            targets.arraySize = body == null ? 0 : 1;
            if (body != null)
            {
                var element = targets.GetArrayElementAtIndex(0);
                element.FindPropertyRelative("category").enumValueIndex = (int)AvatarPartCategory.BodyColor;
                element.FindPropertyRelative("targetRenderer").objectReferenceValue = body;
                element.FindPropertyRelative("materialIndex").intValue = 0;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool IsFirstVisual(
            Transform visual,
            GameObject model,
            RuntimeAnimatorController controller)
        {
            if (visual == null ||
                PrefabUtility.GetCorrespondingObjectFromSource(visual.gameObject) != model)
            {
                return false;
            }

            if (controller == null)
            {
                return true;
            }

            var animator = visual.GetComponentInChildren<Animator>(true);
            return animator != null &&
                   animator.runtimeAnimatorController == controller &&
                   !animator.applyRootMotion;
        }

        private static bool HasBodyColorTarget(GameObject root)
        {
            var applier = root.GetComponent<AvatarAppearanceApplier>();
            if (applier == null)
            {
                return false;
            }

            var so = new SerializedObject(applier);
            var targets = so.FindProperty("targets");
            return so.FindProperty("catalog").objectReferenceValue != null &&
                   targets.arraySize == 1 &&
                   targets.GetArrayElementAtIndex(0)
                       .FindPropertyRelative("targetRenderer").objectReferenceValue != null;
        }

        private static void BindAnimator(GameObject visual, RuntimeAnimatorController controller)
        {
            var animator = visual.GetComponent<Animator>() ?? visual.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                animator = visual.AddComponent<Animator>();
            }

            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.runtimeAnimatorController = controller;
        }

        private static void ApplyProjectMaterials(GameObject visual)
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

            foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
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
            }
        }

        private readonly struct MotionDefinition
        {
            public MotionDefinition(string path, string state, float lastFrame, bool loop)
            {
                Path = path;
                State = state;
                LastFrame = lastFrame;
                Loop = loop;
            }

            public string Path { get; }
            public string State { get; }
            public float LastFrame { get; }
            public bool Loop { get; }
        }
    }
}
