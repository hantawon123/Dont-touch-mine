using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Softens the outer underarm silhouette shared by two-handed carry clips.
    /// </summary>
    public static class CarryTwoHandsPoseSmoother
    {
        private const float ShoulderRestBlend = 0.055f;
        private const float UpperArmRestBlend = 0.035f;
        private const float VolumeArmBlend = 0.58f;

        private static readonly string[] ArmPaths =
        {
            "DGN_Armature/Hips/Spine/Shoulder.L",
            "DGN_Armature/Hips/Spine/Shoulder.R",
            "DGN_Armature/Hips/Spine/Shoulder.L/UpperArm.L",
            "DGN_Armature/Hips/Spine/Shoulder.R/UpperArm.R",
        };

        private static readonly string[] RotationChannels =
        {
            "m_LocalRotation.x",
            "m_LocalRotation.y",
            "m_LocalRotation.z",
            "m_LocalRotation.w",
        };

        [MenuItem("Game/Setup/양손 들기 겨드랑이 매끈하게 다듬기", false, 2)]
        public static void SmoothFromMenu()
        {
            Smooth();
            CharacterTestPreviewSetup.ApplyBlenderPreview();
            EditorUtility.DisplayDialog("Carry pose", "양손으로 들 때 겨드랑이 외곽선을 매끈하게 다듬었습니다.", "OK");
        }

        public static void Smooth()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(SmoothBearAssets.ModelPath);
            if (model == null)
            {
                throw new InvalidOperationException("Missing SmoothBear model.");
            }

            var clips = SmoothBearAssets.LoadClips()
                .Where(clip => clip.name.StartsWith("Carry_TwoHands", StringComparison.Ordinal))
                .ToArray();
            if (clips.Length == 0)
            {
                throw new InvalidOperationException("No two-handed carry clips found.");
            }

            var instance = UnityEngine.Object.Instantiate(model);
            var animator = instance.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.enabled = false;
            }

            try
            {
                foreach (var clip in clips)
                {
                    SmoothClip(clip, instance, model);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }

            AssetDatabase.SaveAssets();
        }

        private static void SmoothClip(AnimationClip clip, GameObject instance, GameObject model)
        {
            var keys = ArmPaths.Select(_ => RotationChannels.Select(__ => new List<Keyframe>()).ToArray()).ToArray();
            var previous = new Quaternion[ArmPaths.Length];
            var frameCount = Mathf.CeilToInt(clip.length * clip.frameRate);

            for (var frame = 0; frame <= frameCount; frame++)
            {
                var time = Mathf.Min(frame / clip.frameRate, clip.length);
                clip.SampleAnimation(instance, time);

                for (var arm = 0; arm < ArmPaths.Length; arm++)
                {
                    var transform = instance.transform.Find(ArmPaths[arm]);
                    var rest = model.transform.Find(ArmPaths[arm]).localRotation;
                    var blend = arm < 2 ? ShoulderRestBlend : UpperArmRestBlend;
                    var rotation = Quaternion.Slerp(transform.localRotation, rest, blend);
                    if (frame > 0 && Quaternion.Dot(previous[arm], rotation) < 0f)
                    {
                        rotation = new Quaternion(-rotation.x, -rotation.y, -rotation.z, -rotation.w);
                    }

                    previous[arm] = rotation;
                    var values = new[] { rotation.x, rotation.y, rotation.z, rotation.w };
                    for (var channel = 0; channel < RotationChannels.Length; channel++)
                    {
                        keys[arm][channel].Add(new Keyframe(time, values[channel]));
                    }
                }
            }

            for (var arm = 0; arm < ArmPaths.Length; arm++)
            {
                for (var channel = 0; channel < RotationChannels.Length; channel++)
                {
                    AnimationUtility.SetEditorCurve(
                        clip,
                        EditorCurveBinding.FloatCurve(ArmPaths[arm], typeof(Transform), RotationChannels[channel]),
                        ToLinearCurve(keys[arm][channel]));
                }
            }

            clip.EnsureQuaternionContinuity();
            RebuildUpperArmVolumeBones(clip, instance, model);
            EditorUtility.SetDirty(clip);
        }

        private static void RebuildUpperArmVolumeBones(AnimationClip clip, GameObject instance, GameObject model)
        {
            var supports = new[]
            {
                instance.transform.Find("DGN_Armature/Hips/Spine/Shoulder.L/Volume_UpperArm.L"),
                instance.transform.Find("DGN_Armature/Hips/Spine/Shoulder.R/Volume_UpperArm.R"),
            };
            if (supports.Any(support => support == null))
            {
                throw new InvalidOperationException("Missing upper-arm volume bones.");
            }

            var keys = supports.Select(_ => RotationChannels.Select(__ => new List<Keyframe>()).ToArray()).ToArray();
            var previous = new Quaternion[supports.Length];
            var frameCount = Mathf.CeilToInt(clip.length * clip.frameRate);
            for (var frame = 0; frame <= frameCount; frame++)
            {
                var time = Mathf.Min(frame / clip.frameRate, clip.length);
                clip.SampleAnimation(instance, time);
                for (var index = 0; index < supports.Length; index++)
                {
                    var support = supports[index];
                    var child = support.parent.Find(support.name.Substring("Volume_".Length));
                    var path = AnimationUtility.CalculateTransformPath(support, instance.transform);
                    var rest = model.transform.Find(path).localRotation;
                    var delta = Quaternion.Inverse(rest) * child.localRotation;
                    var rotation = rest * Quaternion.Slerp(Quaternion.identity, delta, VolumeArmBlend);
                    if (frame > 0 && Quaternion.Dot(previous[index], rotation) < 0f)
                    {
                        rotation = new Quaternion(-rotation.x, -rotation.y, -rotation.z, -rotation.w);
                    }

                    previous[index] = rotation;
                    var values = new[] { rotation.x, rotation.y, rotation.z, rotation.w };
                    for (var channel = 0; channel < RotationChannels.Length; channel++)
                    {
                        keys[index][channel].Add(new Keyframe(time, values[channel]));
                    }
                }
            }

            for (var index = 0; index < supports.Length; index++)
            {
                var path = AnimationUtility.CalculateTransformPath(supports[index], instance.transform);
                for (var channel = 0; channel < RotationChannels.Length; channel++)
                {
                    AnimationUtility.SetEditorCurve(
                        clip,
                        EditorCurveBinding.FloatCurve(path, typeof(Transform), RotationChannels[channel]),
                        ToLinearCurve(keys[index][channel]));
                }
            }

            clip.EnsureQuaternionContinuity();
        }

        private static AnimationCurve ToLinearCurve(List<Keyframe> keys)
        {
            return new AnimationCurve(keys.ToArray());
        }
    }
}
