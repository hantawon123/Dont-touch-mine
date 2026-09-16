using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Editor
{
    // Applies to both the editor Build button and Jenkins, without changing desktop quality.
    public sealed class WebGlShadowBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.WebGL) return;

            var expected = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>(
                "Assets/Settings/WebGL_RPAsset.asset");
            QualitySettings.GetRenderPipelineAssetsForPlatform<UnityEngine.Rendering.RenderPipelineAsset>(
                "WebGL", out var pipelines, out var allOverridden);
            if (expected == null || !allOverridden || pipelines.Count != 1 || !pipelines.Contains(expected) ||
                QualitySettings.GetActiveQualityLevelsForPlatformCount("WebGL") != 1)
                throw new BuildFailedException("WebGL must include only its dedicated WebGL quality and pipeline.");

            var settings = new SerializedObject(expected);
            foreach (var field in new[] { "m_MainLightShadowsSupported", "m_AdditionalLightShadowsSupported",
                "m_AnyShadowsSupported", "m_SoftShadowsSupported" })
                if (settings.FindProperty(field).boolValue)
                    throw new BuildFailedException("WebGL realtime shadows must remain disabled: " + field);

            var renderers = settings.FindProperty("m_RendererDataList");
            for (var i = 0; i < renderers.arraySize; i++)
            {
                var renderer = new SerializedObject(renderers.GetArrayElementAtIndex(i).objectReferenceValue);
                var features = renderer.FindProperty("m_RendererFeatures");
                for (var j = 0; j < features.arraySize; j++)
                {
                    var feature = features.GetArrayElementAtIndex(j).objectReferenceValue;
                    if (feature != null && feature.GetType().Name == "ScreenSpaceShadows")
                        throw new BuildFailedException("Remove Screen Space Shadows from the WebGL renderer, including inactive features.");
                }
            }
        }
    }
    public static class WebBuild
    {
        // Run with -batchmode -quit -buildTarget WebGL -executeMethod Game.Editor.WebBuild.Build.
        public static void Build()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
                throw new BuildFailedException("Install WebGL Build Support for this Unity version.");

            var output = Environment.GetEnvironmentVariable("WEBGL_OUTPUT") ?? "Builds/WebGL";
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled)
                .Select(scene => scene.path).ToArray();
            if (scenes.Length == 0) throw new BuildFailedException("No enabled build scenes.");
            if (!File.Exists("Assets/Photon/Fusion/Resources/PhotonAppSettings.asset"))
                throw new BuildFailedException("Restore PhotonAppSettings.asset before building.");

            var compression = PlayerSettings.WebGL.compressionFormat;
            var fallback = PlayerSettings.WebGL.decompressionFallback;
            var hashes = PlayerSettings.WebGL.nameFilesAsHashes;
            var version = PlayerSettings.bundleVersion;
            var codeGeneration = PlayerSettings.GetIl2CppCodeGeneration(NamedBuildTarget.WebGL);
#if UNITY_WEBGL
            var webOptimization = UnityEditor.WebGL.UserBuildSettings.codeOptimization;
#endif
            try
            {
                PlayerSettings.bundleVersion = Environment.GetEnvironmentVariable("WEBGL_REVISION") ?? version;
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
                PlayerSettings.WebGL.decompressionFallback = false;
                PlayerSettings.WebGL.nameFilesAsHashes = true;
                // Normal player builds must not inherit a previous editor's fast-build setting.
                PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.WebGL,
                    UnityEditor.Build.Il2CppCodeGeneration.OptimizeSpeed);
#if UNITY_WEBGL
                UnityEditor.WebGL.UserBuildSettings.codeOptimization = UnityEditor.WebGL.WasmCodeOptimization.RuntimeSpeed;
#endif
                if (Environment.GetEnvironmentVariable("WEBGL_FAST_BUILD") == "1")
                {
                    PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.WebGL,
                        UnityEditor.Build.Il2CppCodeGeneration.OptimizeSize);
#if UNITY_WEBGL
                    UnityEditor.WebGL.UserBuildSettings.codeOptimization = UnityEditor.WebGL.WasmCodeOptimization.BuildTimes;
#endif
                }
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = output,
                    target = BuildTarget.WebGL,
                    options = Environment.GetEnvironmentVariable("WEBGL_PROFILE_BUILD") == "1"
                        ? BuildOptions.Development : BuildOptions.None
                });
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/webgl-build-report.json", JsonUtility.ToJson(new BuildMetrics
                {
                    revision = PlayerSettings.bundleVersion,
                    unityVersion = Application.unityVersion,
                    codeGeneration = PlayerSettings.GetIl2CppCodeGeneration(NamedBuildTarget.WebGL).ToString(),
#if UNITY_WEBGL
                    webOptimization = UnityEditor.WebGL.UserBuildSettings.codeOptimization.ToString(),
#endif
                    result = report.summary.result.ToString(),
                    seconds = report.summary.totalTime.TotalSeconds,
                    steps = report.steps.Select(step => new StepMetrics
                    {
                        name = step.name, depth = step.depth, seconds = step.duration.TotalSeconds
                    }).ToArray()
                }, true));
                if (report.summary.result != BuildResult.Succeeded)
                    throw new BuildFailedException($"WebGL build failed: {report.summary.totalErrors} errors.");
                File.WriteAllText(Path.Combine(output, "version.txt"), PlayerSettings.bundleVersion);
            }
            finally
            {
                PlayerSettings.WebGL.compressionFormat = compression;
                PlayerSettings.WebGL.decompressionFallback = fallback;
                PlayerSettings.WebGL.nameFilesAsHashes = hashes;
                PlayerSettings.bundleVersion = version;
                PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.WebGL, codeGeneration);
#if UNITY_WEBGL
                UnityEditor.WebGL.UserBuildSettings.codeOptimization = webOptimization;
#endif
            }
        }

        [Serializable]
        private sealed class BuildMetrics
        {
            public string revision, unityVersion, codeGeneration, webOptimization, result;
            public double seconds;
            public StepMetrics[] steps;
        }

        [Serializable]
        private sealed class StepMetrics
        {
            public string name;
            public int depth;
            public double seconds;
        }
    }
}
