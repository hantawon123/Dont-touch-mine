using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class ServerFlowBuild
{
    public static void BuildWebGL()
    {
        PlayerSettings.companyName = "KeepItValidation";
        PlayerSettings.productName = "ServerFlow988";
        PlayerSettings.bundleVersion = Environment.GetEnvironmentVariable("SERVER_FLOW_VERSION") ?? "988-local-v1";
        PlayerSettings.runInBackground = true;
        Environment.SetEnvironmentVariable("WEBGL_REVISION", PlayerSettings.bundleVersion);
        Game.Editor.WebBuild.Build();
    }

    public static void Build()
    {
        BuildStandalone(BuildTarget.StandaloneWindows64, "ServerFlow988.exe", false);
    }

    public static void BuildLinux()
    {
        BuildStandalone(BuildTarget.StandaloneLinux64, "ServerFlow988.x86_64", true);
    }

    private static void BuildStandalone(BuildTarget target, string executable, bool server)
    {
        var output = Environment.GetEnvironmentVariable("SERVER_FLOW_OUTPUT");
        if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("SERVER_FLOW_OUTPUT required");
        PlayerSettings.companyName = "KeepItValidation";
        PlayerSettings.productName = "ServerFlow988";
        PlayerSettings.bundleVersion = Environment.GetEnvironmentVariable("SERVER_FLOW_VERSION") ?? "988-local-v1";
        PlayerSettings.runInBackground = true;
        PlayerSettings.SetScriptingBackend(server ? NamedBuildTarget.Server : NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        PlayerSettings.dedicatedServerOptimizations = server;
        var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            target = target,
            subtarget = (int)(server ? StandaloneBuildSubtarget.Server : StandaloneBuildSubtarget.Player),
            locationPathName = Path.Combine(output, executable),
            options = BuildOptions.Development
        });
        Debug.Log($"[Flow988Build] {result.summary.result} errors={result.summary.totalErrors}");
        if (result.summary.result != BuildResult.Succeeded) throw new BuildFailedException("988 validation build failed");
        File.WriteAllText(Path.Combine(output, "version.txt"), PlayerSettings.bundleVersion);
    }
}
