using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Editor
{
    public static class ClientBuild
    {
        public static void Build()
        {
            var revision = Environment.GetEnvironmentVariable("CLIENT_REVISION");
            if (string.IsNullOrWhiteSpace(revision)) throw new BuildFailedException("CLIENT_REVISION is required.");
            var output = Environment.GetEnvironmentVariable("CLIENT_OUTPUT") ?? "Builds/Client/Game.exe";
            var version = PlayerSettings.bundleVersion;
            try
            {
                PlayerSettings.bundleVersion = revision;
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                    locationPathName = output,
                    target = BuildTarget.StandaloneWindows64,
                    subtarget = (int)StandaloneBuildSubtarget.Player,
                    options = Environment.GetEnvironmentVariable("CLIENT_PROFILE_BUILD") == "1"
                        ? BuildOptions.Development : BuildOptions.None
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new BuildFailedException($"Client build failed: {report.summary.totalErrors} errors.");
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(output)), "version.txt"), revision);
            }
            finally { PlayerSettings.bundleVersion = version; }
        }
    }
}
