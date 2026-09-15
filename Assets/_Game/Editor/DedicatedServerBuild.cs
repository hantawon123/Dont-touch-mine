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
            var revision = Environment.GetEnvironmentVariable("WEBGL_REVISION");
            if (string.IsNullOrWhiteSpace(revision)) throw new BuildFailedException("WEBGL_REVISION is required for a matching server/client pair.");
            var output = Environment.GetEnvironmentVariable("GAME_SERVER_OUTPUT") ?? "Builds/Server";
            var oldVersion = PlayerSettings.bundleVersion;
            var oldBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Server);
            var oldOptimization = PlayerSettings.dedicatedServerOptimizations;
            try
            {
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
                PlayerSettings.bundleVersion = oldVersion;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Server, oldBackend);
                PlayerSettings.dedicatedServerOptimizations = oldOptimization;
            }
        }
    }
}
