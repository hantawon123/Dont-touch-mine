using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Hit를 자세 베이스 위에 얹어 자세별 Hit 클립을 만든다.
    /// - Hit_Walk/Run/Crouch/Crouch_Walk: 상체는 Hit 그대로, 하체·골반은 이동 베이스
    /// - Hit_Prone/Hit_Crawl, Carry_TwoHands_Hit*: 기존 부분 가중치 오버레이
    /// Hit.anim이 바뀌면 SmoothBearEmoteClipImporter가 이 베이커를 다시 돌린다.
    /// </summary>
    public static class CarryTwoHandsHitClipBaker
    {
        private static readonly Job[] Jobs =
        {
            new("Hit_Walk", "Hit", "Walk_Forward", false, fullUpperBody: true),
            new("Hit_Run", "Hit", "Run_Forward", false, fullUpperBody: true),
            new("Hit_Crouch", "Hit", "Crouch_Idle", false, fullUpperBody: true),
            new("Hit_Crouch_Walk", "Hit", "Crouch_Walk_Forward", false, fullUpperBody: true),
            new("Hit_Prone", "Hit", "Prone_Idle", false),
            new("Hit_Crawl", "Hit", "Crawl_Forward", false),
            new("Carry_TwoHands_Hit", "Hit", "Carry_TwoHands", true),
            new("Carry_TwoHands_Hit_Walk", "Hit_Walk", "Carry_TwoHands_Walk_Forward", true),
            new("Carry_TwoHands_Hit_Run", "Hit_Run", "Carry_TwoHands_Run_Forward", true),
            new("Carry_TwoHands_Hit_Crouch", "Hit_Crouch", "Carry_TwoHands_Crouch_Idle", true),
            new("Carry_TwoHands_Hit_Crouch_Walk", "Hit_Crouch_Walk", "Carry_TwoHands_Crouch_Walk_Forward", true),
            new("Carry_TwoHands_Hit_Prone", "Hit", "Carry_TwoHands_Prone_Idle", true),
            new("Carry_TwoHands_Hit_Crawl", "Hit", "Carry_TwoHands_Crawl_Forward", true),
        };

        private static readonly Dictionary<string, float> HitOverlay =
            new()
            {
                ["Spine"] = 0.45f,
                ["Neck"] = 0.35f,
                ["Head"] = 0.30f,
                ["Shoulder.L"] = 0.35f,
                ["Shoulder.R"] = 1f,
                ["UpperArm.L"] = 0.45f,
                ["UpperArm.R"] = 1f,
                ["Arm.L"] = 0.45f,
                ["Arm.R"] = 1f,
                ["Hand.L"] = 0.35f,
                ["Hand.R"] = 1f,
            };

        [InitializeOnLoadMethod]
        private static void BakeMissingClips()
        {
            if (Application.isBatchMode)
            {
                return;
            }

            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    return;
                }

                if (Jobs.All(job => SmoothBearAssets.LoadClip(job.Output) != null))
                {
                    return;
                }

                try
                {
                    Bake();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            };
        }

        [MenuItem("Game/Setup/들고 있을 때 Hit 클립 굽기", false, 1)]
        [MenuItem("Game/Setup/엎드렸을 때 Hit 클립 굽기", false, 1)]
        [MenuItem("Tools/들고 있을 때 Hit 클립 굽기", false, 1)]
        [MenuItem("Tools/엎드렸을 때 Hit 클립 굽기", false, 1)]
        public static void BakeFromMenu()
        {
            Bake();
            if (UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path !=
                CharacterTestPreviewSetup.ScenePath)
            {
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                    CharacterTestPreviewSetup.ScenePath);
            }

            CharacterTestPreviewSetup.ApplyBlenderPreview();
            EditorUtility.DisplayDialog(
                "Carry Hit",
                "엎드림·들고 엎드림 Hit 클립을 구웠습니다.",
                "OK");
        }

        public static void BakeFromBatch()
        {
            Bake();
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                CharacterTestPreviewSetup.ScenePath);
            CharacterTestPreviewSetup.ApplyBlenderPreview();
            EditorApplication.Exit(0);
        }

        public static void Bake()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(SmoothBearAssets.ModelPath);
            if (model == null)
            {
                throw new InvalidOperationException("Missing SmoothBear model.");
            }

            var instance = UnityEngine.Object.Instantiate(model);
            instance.GetComponent<Animator>().enabled = false;
            try
            {
                foreach (var job in Jobs)
                {
                    BakeClip(job.Output, job.Hit, job.Base, job.KeepCarryArms, job.FullUpperBody, instance, model);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }

            AssetDatabase.SaveAssets();
            BindControllers();
        }

        private static void BindControllers()
        {
            foreach (var path in new[]
            {
                SmoothBearAssets.PlayerControllerPath,
                SmoothBearAssets.PreviewControllerPath,
            })
            {
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                if (controller == null)
                {
                    continue;
                }

                var machine = controller.layers[0].stateMachine;
                foreach (var job in Jobs)
                {
                    var clip = SmoothBearAssets.LoadClip(job.Output);
                    if (clip == null)
                    {
                        continue;
                    }

                    var state = machine.states.Select(entry => entry.state)
                            .FirstOrDefault(entry => entry.name == job.Output)
                        ?? machine.AddState(job.Output);
                    state.motion = clip;
                    state.writeDefaultValues = false;
                    state.speed = 1f;
                }

                EditorUtility.SetDirty(controller);
            }

            AssetDatabase.SaveAssets();
        }

        private static void BakeClip(
            string outputName,
            string hitName,
            string baseName,
            bool keepCarryArms,
            bool fullUpperBody,
            GameObject instance,
            GameObject model)
        {
            var hit = SmoothBearAssets.LoadClip(hitName);
            var carry = SmoothBearAssets.LoadClip(baseName);
            if (hit == null || carry == null)
            {
                throw new InvalidOperationException($"Missing {hitName} or {baseName}.");
            }

            var clip = new AnimationClip
            {
                name = outputName,
                frameRate = hit.frameRate,
                legacy = false,
            };
            var settings = AnimationUtility.GetAnimationClipSettings(hit);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            var bones = instance.GetComponentsInChildren<Transform>(true)
                .Where(bone => bone != instance.transform)
                .ToArray();
            var channels = new[]
            {
                "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w",
                "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z",
                "m_LocalScale.x", "m_LocalScale.y", "m_LocalScale.z",
            };
            var keys = bones.Select(_ => channels.Select(__ => new List<Keyframe>()).ToArray()).ToArray();
            var previous = new Quaternion[bones.Length];
            var body = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Single(renderer => renderer.name == "Body");
            var bodyPath = AnimationUtility.CalculateTransformPath(body.transform, instance.transform);
            var shapeNames = Enumerable.Range(0, body.sharedMesh.blendShapeCount)
                .Select(index => body.sharedMesh.GetBlendShapeName(index))
                .ToArray();
            var shapeKeys = shapeNames.Select(_ => new List<Keyframe>()).ToArray();
            var frameCount = Mathf.CeilToInt(hit.length * hit.frameRate);

            for (var frame = 0; frame <= frameCount; frame++)
            {
                var time = Mathf.Min(frame / hit.frameRate, hit.length);
                carry.SampleAnimation(instance, carry.length <= 0f ? 0f : time % carry.length);
                var carryPose = Capture(bones);
                var carryShapes = CaptureShapes(body, shapeNames);
                hit.SampleAnimation(instance, time);
                var hitPose = Capture(bones);

                for (var index = 0; index < bones.Length; index++)
                {
                    var mixed = Mix(bones[index].name, carryPose[index], hitPose[index], keepCarryArms, fullUpperBody);
                    if (frame > 0 && Quaternion.Dot(previous[index], mixed.Rotation) < 0f)
                    {
                        mixed = mixed.Flipped();
                    }

                    previous[index] = mixed.Rotation;
                    float[] values =
                    {
                        mixed.Rotation.x, mixed.Rotation.y, mixed.Rotation.z, mixed.Rotation.w,
                        mixed.Position.x, mixed.Position.y, mixed.Position.z,
                        mixed.Scale.x, mixed.Scale.y, mixed.Scale.z,
                    };
                    for (var channel = 0; channel < channels.Length; channel++)
                    {
                        keys[index][channel].Add(new Keyframe(time, values[channel]));
                    }
                }

                for (var shape = 0; shape < shapeNames.Length; shape++)
                {
                    shapeKeys[shape].Add(new Keyframe(time, carryShapes[shape]));
                }
            }

            for (var index = 0; index < bones.Length; index++)
            {
                var path = AnimationUtility.CalculateTransformPath(bones[index], instance.transform);
                for (var channel = 0; channel < channels.Length; channel++)
                {
                    AnimationUtility.SetEditorCurve(
                        clip,
                        EditorCurveBinding.FloatCurve(path, typeof(Transform), channels[channel]),
                        ToCurve(keys[index][channel]));
                }
            }

            for (var shape = 0; shape < shapeNames.Length; shape++)
            {
                AnimationUtility.SetEditorCurve(
                    clip,
                    EditorCurveBinding.FloatCurve(
                        bodyPath,
                        typeof(SkinnedMeshRenderer),
                        "blendShape." + shapeNames[shape]),
                    ToCurve(shapeKeys[shape]));
            }

            clip.EnsureQuaternionContinuity();
            BakeVolumeBones(clip, instance, model);

            var dest = SmoothBearAssets.Root + "/Animations/" + outputName + ".anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(dest);
            if (existing != null)
            {
                EditorUtility.CopySerialized(clip, existing);
                EditorUtility.SetDirty(existing);
                UnityEngine.Object.DestroyImmediate(clip);
            }
            else
            {
                AssetDatabase.CreateAsset(clip, dest);
            }
        }

        private static Pose[] Capture(Transform[] bones)
        {
            var poses = new Pose[bones.Length];
            for (var index = 0; index < bones.Length; index++)
            {
                poses[index] = new Pose(
                    bones[index].localPosition,
                    bones[index].localRotation,
                    bones[index].localScale);
            }

            return poses;
        }

        private static float[] CaptureShapes(SkinnedMeshRenderer body, string[] names)
        {
            var values = new float[names.Length];
            for (var index = 0; index < names.Length; index++)
            {
                values[index] = body.GetBlendShapeWeight(index);
            }

            return values;
        }

        private static Pose Mix(string bone, Pose carry, Pose hit, bool keepCarryArms, bool fullUpperBody)
        {
            if (keepCarryArms && IsCarryArm(bone))
            {
                return carry;
            }

            if (fullUpperBody && (HitOverlay.ContainsKey(bone) || IsCarryArm(bone)))
            {
                // Whole torso and both arms straight from Hit; hips and legs follow the base.
                return hit;
            }

            if (!HitOverlay.TryGetValue(bone, out var weight))
            {
                if (!keepCarryArms && IsCarryArm(bone))
                {
                    weight = 1f;
                }
                else
                {
                    return carry;
                }
            }

            var rotation = Quaternion.Slerp(carry.Rotation, hit.Rotation, weight);
            if (Quaternion.Dot(carry.Rotation, rotation) < 0f)
            {
                rotation = new Quaternion(-rotation.x, -rotation.y, -rotation.z, -rotation.w);
            }

            return new Pose(
                Vector3.Lerp(carry.Position, hit.Position, weight),
                rotation,
                Vector3.Lerp(carry.Scale, hit.Scale, weight));
        }

        private static bool IsCarryArm(string bone) =>
            bone is "Shoulder.L" or "Shoulder.R" or "UpperArm.L" or "UpperArm.R"
                or "Arm.L" or "Arm.R" or "Hand.L" or "Hand.R" ||
            bone.StartsWith("Finger_", StringComparison.Ordinal);

        private static AnimationCurve ToCurve(List<Keyframe> keys)
        {
            var curve = new AnimationCurve(keys.ToArray());
            for (var index = 0; index < curve.length; index++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, index, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(curve, index, AnimationUtility.TangentMode.Linear);
            }

            return curve;
        }

        private static void BakeVolumeBones(AnimationClip clip, GameObject instance, GameObject model)
        {
            var supports = instance.GetComponentsInChildren<Transform>()
                .Where(bone => bone.name.StartsWith("Volume_", StringComparison.Ordinal))
                .ToArray();
            if (supports.Length == 0)
            {
                return;
            }

            var channels = new[]
            {
                "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w",
                "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z",
                "m_LocalScale.x", "m_LocalScale.y", "m_LocalScale.z",
            };
            var frameCount = Mathf.CeilToInt(clip.length * clip.frameRate);
            var keys = supports.Select(_ => channels.Select(__ => new List<Keyframe>()).ToArray()).ToArray();
            var previous = new Quaternion[supports.Length];
            for (var frame = 0; frame <= frameCount; frame++)
            {
                var time = Mathf.Min(frame / clip.frameRate, clip.length);
                clip.SampleAnimation(instance, time);
                for (var index = 0; index < supports.Length; index++)
                {
                    var helper = supports[index];
                    var child = helper.parent.Find(helper.name.Substring("Volume_".Length));
                    var path = AnimationUtility.CalculateTransformPath(helper, instance.transform);
                    var rest = model.transform.Find(path).localRotation;
                    var delta = Quaternion.Inverse(rest) * child.localRotation;
                    var rotation = rest * Quaternion.Slerp(Quaternion.identity, delta, 0.5f);
                    if (frame > 0 && Quaternion.Dot(previous[index], rotation) < 0f)
                    {
                        rotation = new Quaternion(-rotation.x, -rotation.y, -rotation.z, -rotation.w);
                    }

                    previous[index] = rotation;
                    var position = child.localPosition;
                    var scale = child.localScale;
                    float[] values =
                    {
                        rotation.x, rotation.y, rotation.z, rotation.w,
                        position.x, position.y, position.z,
                        scale.x, scale.y, scale.z,
                    };
                    for (var channel = 0; channel < channels.Length; channel++)
                    {
                        keys[index][channel].Add(new Keyframe(time, values[channel]));
                    }
                }
            }

            for (var index = 0; index < supports.Length; index++)
            {
                var path = AnimationUtility.CalculateTransformPath(supports[index], instance.transform);
                for (var channel = 0; channel < channels.Length; channel++)
                {
                    AnimationUtility.SetEditorCurve(
                        clip,
                        EditorCurveBinding.FloatCurve(path, typeof(Transform), channels[channel]),
                        ToCurve(keys[index][channel]));
                }
            }

            clip.EnsureQuaternionContinuity();
        }

        private readonly struct Job
        {
            public Job(string output, string hit, string @base, bool keepCarryArms, bool fullUpperBody = false)
            {
                Output = output;
                Hit = hit;
                Base = @base;
                KeepCarryArms = keepCarryArms;
                FullUpperBody = fullUpperBody;
            }

            public string Output { get; }
            public string Hit { get; }
            public string Base { get; }
            public bool KeepCarryArms { get; }
            public bool FullUpperBody { get; }
        }

        private readonly struct Pose
        {
            public Pose(Vector3 position, Quaternion rotation, Vector3 scale)
            {
                Position = position;
                Rotation = rotation;
                Scale = scale;
            }

            public Vector3 Position { get; }
            public Quaternion Rotation { get; }
            public Vector3 Scale { get; }

            public Pose Flipped() => new(
                Position,
                new Quaternion(-Rotation.x, -Rotation.y, -Rotation.z, -Rotation.w),
                Scale);
        }
    }
}
