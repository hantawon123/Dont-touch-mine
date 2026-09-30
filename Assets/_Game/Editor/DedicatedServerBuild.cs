using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Editor
{
    public static class DedicatedServerBuild
    {
        public static void Build()
        {
            var revision = Environment.GetEnvironmentVariable("GAME_REVISION")
                ?? Environment.GetEnvironmentVariable("WEBGL_REVISION");
            if (string.IsNullOrWhiteSpace(revision)) throw new BuildFailedException("GAME_REVISION is required for a matching server/client pair.");
            var output = Environment.GetEnvironmentVariable("GAME_SERVER_OUTPUT") ?? "Builds/Server";
            var oldVersion = PlayerSettings.bundleVersion;
            var oldBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Server);
            var oldOptimization = PlayerSettings.dedicatedServerOptimizations;
            // These visual-only models have no MeshCollider users. Keep all bones,
            // animation and bounds; strip headless CPU geometry and visual morph targets.
            var modelMetadata = new[] {
                "Assets/_Game/Content/Characters/SmoothBear/SmoothBear.fbx",
                "Assets/_Game/Content/Characters/SmoothBear/Hoods/AnimalHoods.fbx"
            }.Select(path => (path, bytes: File.ReadAllBytes(path + ".meta"))).ToArray();
            try
            {
                foreach (var model in modelMetadata)
                {
                    var importer = AssetImporter.GetAtPath(model.path) as ModelImporter;
                    if (importer == null) throw new BuildFailedException("Missing server visual model: " + model.path);
                    importer.isReadable = false;
                    importer.importBlendShapes = false;
                    importer.SaveAndReimport();
                    var imported = (ModelImporter)AssetImporter.GetAtPath(model.path);
                    if (imported.isReadable || imported.importBlendShapes)
                        throw new BuildFailedException("Server visual mesh import retained CPU data: " + model.path);
                }
                PlayerSettings.bundleVersion = revision;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Server, ScriptingImplementation.Mono2x);
                PlayerSettings.dedicatedServerOptimizations = true;
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                    locationPathName = Path.Combine(output, "GameServer.x86_64"),
                    target = BuildTarget.StandaloneLinux64,
                    subtarget = (int)StandaloneBuildSubtarget.Server,
                    options = BuildOptions.None
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new BuildFailedException($"Dedicated server build failed: {report.summary.totalErrors} errors.");
                File.WriteAllText(Path.Combine(output, "version.txt"), revision);
            }
            finally
            {
                // Restore exact authored metadata, including after a failed build.
                // Future client builds keep their original import settings.
                foreach (var model in modelMetadata)
                {
                    File.WriteAllBytes(model.path + ".meta", model.bytes);
                    AssetDatabase.ImportAsset(model.path, ImportAssetOptions.ForceUpdate);
                }
                PlayerSettings.bundleVersion = oldVersion;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Server, oldBackend);
                PlayerSettings.dedicatedServerOptimizations = oldOptimization;
            }
        }
    }
}
