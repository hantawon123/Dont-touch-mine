using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Blender에서 보낸 감정·피격 FBX를 native .anim으로 옮기고 PlayerAnimator에 붙인다.
    /// Hit가 바뀌면 자세별 파생 클립도 CarryTwoHandsHitClipBaker로 다시 굽는다.
    /// </summary>
    public static class SmoothBearEmoteClipImporter
    {
        private const string EmoteFolder = SmoothBearAssets.Root + "/EmoteSource";
        private const string CombatFolder = SmoothBearAssets.Root + "/CombatSource";
        private const string AnimFolder = SmoothBearAssets.Root + "/Animations";
        private const string HitClip = "Hit";

        private static readonly (string Folder, string Name, bool Loop)[] Clips =
        {
            (EmoteFolder, "Emote_Wave", false),
            (EmoteFolder, "Emote_Taunt", false),
            (EmoteFolder, "Emote_Insult", false),
            (EmoteFolder, "Emote_Chicken", true),
            (EmoteFolder, "Emote_HipHop", true),
            (EmoteFolder, "Emote_Spin", true),
            (CombatFolder, HitClip, false),
            (CombatFolder, "Stun_Start", false)
        };

        private static string[] ClipNames => System.Array.ConvertAll(Clips, clip => clip.Name);

        private static bool importQueued;

        [InitializeOnLoadMethod]
        private static void ImportIfMissing()
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

                var missing = ClipNames.Any(name => SmoothBearAssets.LoadClip(name) == null);
                var stale = Clips.Any(FbxNewerThanAnim);
                if (!missing && !stale)
                {
                    return;
                }

                if (Clips.Any(clip => AssetImporter.GetAtPath(clip.Folder + "/" + clip.Name + ".fbx") != null))
                {
                    Import();
                }
            };
        }

        private static bool FbxNewerThanAnim((string Folder, string Name, bool Loop) clip)
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var fbxPath = Path.Combine(projectRoot, clip.Folder.Replace('/', Path.DirectorySeparatorChar), clip.Name + ".fbx");
            var animPath = Path.Combine(projectRoot, AnimFolder.Replace('/', Path.DirectorySeparatorChar), clip.Name + ".anim");
            if (!File.Exists(fbxPath) || !File.Exists(animPath))
            {
                return File.Exists(fbxPath);
            }

            return File.GetLastWriteTimeUtc(fbxPath) > File.GetLastWriteTimeUtc(animPath);
        }

        private static void OnPostprocessAllAssets(
            string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (importQueued || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            var changed = false;
            foreach (var path in importedAssets)
            {
                if ((path.StartsWith(EmoteFolder, System.StringComparison.Ordinal) ||
                     path.StartsWith(CombatFolder, System.StringComparison.Ordinal)) &&
                    path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                {
                    changed = true;
                    break;
                }
            }

            if (!changed)
            {
                return;
            }

            importQueued = true;
            EditorApplication.delayCall += () =>
            {
                try
                {
                    Import();
                }
                finally
                {
                    importQueued = false;
                }
            };
        }

        private sealed class EmoteFbxPostprocessor : AssetPostprocessor
        {
            private static void OnPostprocessAllAssets(
                string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
            {
                SmoothBearEmoteClipImporter.OnPostprocessAllAssets(
                    importedAssets, deletedAssets, movedAssets, movedFromAssetPaths);
            }
        }

        [MenuItem("Game/Setup/Import SmoothBear Emote Clips")]
        public static void ImportFromMenu()
        {
            var count = Import();
            EditorUtility.DisplayDialog(
                "SmoothBear Emotes",
                count == ClipNames.Length
                    ? "감정 클립을 연결했습니다."
                    : $"일부만 연결됐습니다 ({count}/{ClipNames.Length}). FBX를 먼저 보내세요.",
                "OK");
        }

        public static void ImportFromBatch()
        {
            var count = Import();
            if (count != ClipNames.Length)
            {
                throw new System.InvalidOperationException(
                    $"Expected {ClipNames.Length} emote clips, imported {count}.");
            }

            EditorApplication.Exit(0);
        }

        public static int Import()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var imported = 0;
            var hitChanged = false;
            foreach (var clip in Clips)
            {
                if (ImportOne(clip.Folder, clip.Name, clip.Loop))
                {
                    imported++;
                    hitChanged |= clip.Name == HitClip;
                }
            }

            if (imported == 0)
            {
                return 0;
            }

            BindController(SmoothBearAssets.PlayerControllerPath);
            BindController(SmoothBearAssets.PreviewControllerPath);
            AssetDatabase.SaveAssets();
            if (hitChanged)
            {
                // Hit_Walk/Run/Crouch, Hit_Prone/Crawl and Carry_TwoHands_Hit* are layered on Hit.
                CarryTwoHandsHitClipBaker.Bake();
            }

            return imported;
        }

        private static bool ImportOne(string folder, string name, bool loop)
        {
            var fbxPath = folder + "/" + name + ".fbx";
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogWarning("Missing emote FBX " + fbxPath);
                return false;
            }

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
            importer.importAnimation = true;
            importer.importBlendShapes = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();

            var source = AssetDatabase.LoadAllAssetsAtPath(fbxPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(clip => clip != null && !clip.name.StartsWith("__preview", System.StringComparison.Ordinal));
            if (source == null)
            {
                Debug.LogWarning("No animation clip in " + fbxPath);
                return false;
            }

            var clip = new AnimationClip
            {
                name = name,
                frameRate = source.frameRate,
                legacy = false
            };
            var settings = AnimationUtility.GetAnimationClipSettings(source);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            foreach (var binding in AnimationUtility.GetCurveBindings(source))
            {
                AnimationUtility.SetEditorCurve(clip, binding, AnimationUtility.GetEditorCurve(source, binding));
            }

            clip.EnsureQuaternionContinuity();
            var dest = AnimFolder + "/" + name + ".anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(dest);
            if (existing != null)
            {
                EditorUtility.CopySerialized(clip, existing);
                existing.name = name;
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(clip);
                clip = existing;
            }
            else
            {
                AssetDatabase.CreateAsset(clip, dest);
            }

            AssetDatabase.ImportAsset(dest, ImportAssetOptions.ForceSynchronousImport);
            clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(dest);
            BakeVolumeBones(clip);
            EditorUtility.SetDirty(clip);
            Debug.Log($"Imported {name} length={clip.length:0.###}s");
            return true;
        }

        private static void BindController(string path)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null || controller.layers.Length == 0)
            {
                return;
            }

            var machine = controller.layers[0].stateMachine;
            foreach (var name in ClipNames)
            {
                var clip = SmoothBearAssets.LoadClip(name);
                if (clip == null)
                {
                    continue;
                }

                var state = machine.states.Select(entry => entry.state).FirstOrDefault(entry => entry.name == name)
                    ?? machine.AddState(name);
                state.motion = clip;
                state.writeDefaultValues = false;
                state.speed = 1f;
            }

            EditorUtility.SetDirty(controller);
        }

        private static void BakeVolumeBones(AnimationClip clip)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(SmoothBearAssets.ModelPath);
            if (model == null)
            {
                return;
            }

            var instance = Object.Instantiate(model);
            try
            {
                var animator = instance.GetComponent<Animator>();
                if (animator != null)
                {
                    animator.enabled = false;
                }

                var supports = instance.GetComponentsInChildren<Transform>()
                    .Where(transform => transform.name.StartsWith("Volume_"))
                    .ToArray();
                if (supports.Length == 0)
                {
                    return;
                }

                var channels = new[]
                {
                    "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w",
                    "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z",
                    "m_LocalScale.x", "m_LocalScale.y", "m_LocalScale.z"
                };
                var count = Mathf.CeilToInt(clip.length * clip.frameRate);
                var keys = supports.Select(_ => channels.Select(__ => new System.Collections.Generic.List<Keyframe>()).ToArray()).ToArray();
                var previous = new Quaternion[supports.Length];
                for (var frame = 0; frame <= count; frame++)
                {
                    var time = Mathf.Min(frame / clip.frameRate, clip.length);
                    clip.SampleAnimation(instance, time);
                    for (var j = 0; j < supports.Length; j++)
                    {
                        var helper = supports[j];
                        var child = helper.parent.Find(helper.name.Substring(7));
                        if (child == null)
                        {
                            continue;
                        }

                        var path = AnimationUtility.CalculateTransformPath(helper, instance.transform);
                        var rest = model.transform.Find(path);
                        if (rest == null)
                        {
                            continue;
                        }

                        var delta = Quaternion.Inverse(rest.localRotation) * child.localRotation;
                        var q = rest.localRotation * Quaternion.Slerp(Quaternion.identity, delta, 0.5f);
                        if (frame > 0 && Quaternion.Dot(previous[j], q) < 0)
                        {
                            q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                        }

                        previous[j] = q;
                        var p = child.localPosition;
                        var s = child.localScale;
                        float[] values = { q.x, q.y, q.z, q.w, p.x, p.y, p.z, s.x, s.y, s.z };
                        for (var k = 0; k < channels.Length; k++)
                        {
                            keys[j][k].Add(new Keyframe(time, values[k]));
                        }
                    }
                }

                for (var j = 0; j < supports.Length; j++)
                {
                    for (var k = 0; k < channels.Length; k++)
                    {
                        if (keys[j][k].Count == 0)
                        {
                            continue;
                        }

                        var curve = new AnimationCurve(keys[j][k].ToArray());
                        AnimationUtility.SetEditorCurve(
                            clip,
                            EditorCurveBinding.FloatCurve(
                                AnimationUtility.CalculateTransformPath(supports[j], instance.transform),
                                typeof(Transform),
                                channels[k]),
                            curve);
                    }
                }

                clip.EnsureQuaternionContinuity();
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
