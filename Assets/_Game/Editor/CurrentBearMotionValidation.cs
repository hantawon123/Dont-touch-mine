using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Batch validation of the actual CharacterTest character and imported clips.</summary>
    public static class CurrentBearMotionValidation
    {
        [Serializable] private class Result
        {
            public int states;
            public int uniqueClips;
            public int sampledFrames;
            public int bodyVertices;
            public string scope = "Unity Editor imported clips sampled on the scene character; not Play Mode";
            public List<string> errors = new();
            public List<string> checkedStates = new();
        }

        public static void Run()
        {
            var args = Environment.GetCommandLineArgs();
            var outputIndex = Array.IndexOf(args, "-bearReport");
            var output = outputIndex >= 0 ? args[outputIndex + 1] : "Logs/current-bear-unity-validation.json";
            var result = new Result();
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var scene = EditorSceneManager.OpenScene("Assets/Scenes/CharacterTest.unity");
                if (!CharacterTestPreviewSetup.ApplyBlenderPreview())
                    throw new InvalidOperationException("Could not configure CharacterTest.");
                var original = scene.GetRootGameObjects().Single(o => o.name == "SmoothBear");
                var controller = (AnimatorController)original.GetComponentInChildren<Animator>().runtimeAnimatorController;
                var states = controller.layers[0].stateMachine.states.Select(s => s.state).ToArray();
                result.states = states.Length;
                result.uniqueClips = states.Select(s => s.motion).Distinct().Count();
                var clone = UnityEngine.Object.Instantiate(original);
                clone.name = "CurrentBear_QA_Only";
                foreach (var behaviour in clone.GetComponentsInChildren<MonoBehaviour>(true))
                    behaviour.enabled = false;
                clone.GetComponentInChildren<Animator>().enabled = false;
                var skins = clone.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var body = skins.Single(s => s.name == "Body");
                result.bodyVertices = body.sharedMesh.vertexCount;
                if (result.bodyVertices < 1537)
                    result.errors.Add("Unexpected body vertex count.");
                foreach (var name in new[] { "Belly_Breath", "Crouch_Groin_Flat" })
                    if (body.sharedMesh.GetBlendShapeIndex(name) < 0)
                        result.errors.Add("Missing corrective shape: " + name);
                var baked = new Mesh();
                var verts = new List<Vector3>();
                foreach (var state in states)
                {
                    if (state.motion is not AnimationClip clip)
                    {
                        result.errors.Add("Missing clip: " + state.name);
                        continue;
                    }
                    foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                    {
                        var target = string.IsNullOrEmpty(binding.path) ? clone.transform : clone.transform.Find(binding.path);
                        if (target == null)
                            result.errors.Add(state.name + ": unresolved binding " + binding.path);
                    }
                    var count = Mathf.CeilToInt(clip.length * 30);
                    Vector3[] first = null;
                    for (var frame = 0; frame <= count; frame++)
                    {
                        foreach (var skin in skins)
                            for (var i = 0; i < skin.sharedMesh.blendShapeCount; i++)
                                skin.SetBlendShapeWeight(i, 0);
                        clip.SampleAnimation(clone, Mathf.Min(frame / 30f, clip.length));
                        body.BakeMesh(baked);
                        baked.GetVertices(verts);
                        if (verts.Any(v => !Finite(v.x) || !Finite(v.y) || !Finite(v.z) || v.sqrMagnitude > 400))
                            result.errors.Add(state.name + ": invalid deformation at frame " + frame);
                        if (frame == 0)
                            first = verts.ToArray();
                        if (frame == count && clip.isLooping && first != null)
                        {
                            var error = 0f;
                            for (var i = 0; i < first.Length; i++)
                                error = Mathf.Max(error, Vector3.Distance(first[i], verts[i]));
                            if (error > .002f)
                                result.errors.Add(state.name + ": loop closure error " + error);
                        }
                        result.sampledFrames++;
                    }
                    result.checkedStates.Add(state.name);
                    Debug.Log("CURRENT_BEAR_CHECKED " + state.name);
                }
                UnityEngine.Object.DestroyImmediate(baked);
                UnityEngine.Object.DestroyImmediate(clone);
                if (result.errors.Count == 0)
                {
                    EditorSceneManager.SaveScene(scene);
                    AssetDatabase.SaveAssets();
                }
            }
            catch (Exception exception)
            {
                result.errors.Add(exception.ToString());
                Debug.LogException(exception);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            File.WriteAllText(output, JsonUtility.ToJson(result, true));
            Debug.Log($"CURRENT_BEAR_RESULT states={result.states} clips={result.uniqueClips} frames={result.sampledFrames} errors={result.errors.Count}");
            EditorApplication.Exit(result.errors.Count == 0 ? 0 : 1);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
