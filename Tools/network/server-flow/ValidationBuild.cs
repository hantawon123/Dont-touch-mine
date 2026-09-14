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
        PlayerSettings.bundleVersion = "988-local-v1";
        PlayerSettings.runInBackground = true;
        Environment.SetEnvironmentVariable("WEBGL_REVISION", PlayerSettings.bundleVersion);
        Game.Editor.WebBuild.Build();
    }

    public static void Build()
    {
        var output = Environment.GetEnvironmentVariable("SERVER_FLOW_OUTPUT");
        if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("SERVER_FLOW_OUTPUT required");
        PlayerSettings.companyName = "KeepItValidation";
        PlayerSettings.productName = "ServerFlow988";
        PlayerSettings.bundleVersion = "988-local-v1";
        PlayerSettings.runInBackground = true;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            target = BuildTarget.StandaloneWindows64,
            locationPathName = Path.Combine(output, "ServerFlow988.exe"),
            options = BuildOptions.Development
        });
        Debug.Log($"[Flow988Build] {result.summary.result} errors={result.summary.totalErrors}");
        if (result.summary.result != BuildResult.Succeeded) throw new BuildFailedException("988 validation build failed");
    }
}
