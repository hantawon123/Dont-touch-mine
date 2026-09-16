using System.Linq;
using Game.Client.Character;
using Game.Core.Players;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// SmoothBear Generic 클립으로 인게임 PlayerAnimator를 채우고
    /// PlayerCharacter Visual을 SmoothBear 메시로 교체한다.
    /// </summary>
    public static class FirstInGameSetup
    {
        private const string IdlePath = SmoothBearAssets.ModelPath;
        private const string ControllerPath = "Assets/_Game/Content/Animations/PlayerAnimator.controller";
        private const string PlayerPrefabPath = "Assets/_Game/Content/Prefabs/PlayerCharacter.prefab";
        private const string CatalogPath = "Assets/_Game/Content/Config/AvatarPartCatalog.asset";
        private const string IdleState = "Idle";
        /// <summary>
        /// Visual을 새로 끼울 때만 쓰는 기본 크기. 이미 붙어 있는 Visual의 크기는 건드리지 않는다 —
        /// 크기는 PlayerCharacter 프리팹에서 팀이 조정하는 값이고(2026-09-16 0.65 확정), 이 스크립트가
        /// 도메인 리로드마다 되돌리면 그 조정이 계속 사라진다.
        /// </summary>
        private static readonly Vector3 VisualScale = new(0.65f, 0.65f, 0.65f);
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

        [MenuItem("Game/Setup/Apply SmoothBear In-Game Character")]
        public static void ApplyFromMenu()
        {
            appliedThisDomain = false;
            if (!Apply())
            {
                EditorUtility.DisplayDialog("SmoothBear In-Game", "SmoothBear 클립 또는 Player 프리팹을 찾지 못했습니다.", "OK");
                return;
            }

            EditorUtility.DisplayDialog("SmoothBear In-Game", "인게임 캐릭터에 SmoothBear 모습과 클립을 연결했습니다.", "OK");
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

            var idle = SmoothBearAssets.LoadClip(IdleState);
            if (idle == null || !ConfigureController(idle))
            {
                return false;
            }

            if (!AssignVisual())
            {
                return false;
            }

            SmoothBearAssets.SaveCharacterPrefab();
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
            foreach (var clip in SmoothBearAssets.LoadClips())
                BindState(machine, clip.name, clip);

            BindState(machine, "Punch", SmoothBearAssets.LoadClip("Punch") ?? idle);
            BindState(machine, "Stunned", SmoothBearAssets.LoadClip("Stun_Idle") ?? idle);
            BindState(machine, "Locomotion", idle);
            BindState(machine, "CrouchMove", SmoothBearAssets.LoadClip("Crouch_Idle") ?? idle);
            BindState(machine, "Crawl", SmoothBearAssets.LoadClip("Crawl_Forward") ?? idle);
            BindState(machine, "Airborne", SmoothBearAssets.LoadClip("Fall") ?? idle);

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
                    HasBodyColorTarget(root))
                {
                    WireAppearance(root, visual.gameObject, catalog);
                    ApplyProjectMaterials(visual.gameObject);
                    PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                    return true;
                }

                if (IsFirstVisual(visual, model, null))
                {
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
            SmoothBearAssets.WireAppearance(root, visual, catalog);
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
                   so.FindProperty("hoodMesh").objectReferenceValue != null && targets.arraySize == 2 &&
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

        private static void ApplyProjectMaterials(GameObject visual) => SmoothBearAssets.ApplyMaterials(visual);


    }
}
