using System;
using System.IO;
using Fusion;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ProbeBuild
{
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        EditorSceneManager.SaveScene(scene, "Assets/Probe.unity");
        new GameObject("TopologyProbe").AddComponent<TopologyProbe>();
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.transform.position = new Vector3(0, -0.5f, 0); floor.transform.localScale = new Vector3(10, 1, 10);
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "ServerPhysics"; cube.transform.position = new Vector3(0, 2, 0);
        cube.AddComponent<Rigidbody>(); cube.AddComponent<NetworkObject>(); cube.AddComponent<ProbeState>();
        var camera = new GameObject("Camera").AddComponent<Camera>();
        camera.transform.position = new Vector3(0, 3, -7); camera.transform.LookAt(Vector3.zero);
        new GameObject("Light").AddComponent<Light>().type = LightType.Directional;
        EditorSceneManager.SaveScene(scene, "Assets/Probe.unity");
        scene = EditorSceneManager.OpenScene("Assets/Probe.unity");
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Probe.unity", true) };
        PlayerSettings.runInBackground = true;
        PlayerSettings.SetPreloadedAssets(Array.Empty<UnityEngine.Object>());
        PlayerSettings.companyName = "KeepItProbe"; PlayerSettings.productName = "Topology987";
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        PlayerSettings.WebGL.template = "APPLICATION:Minimal";
        PlayerSettings.WebGL.memorySize = 128;
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        var target = EditorUserBuildSettings.activeBuildTarget;
        var output = Environment.GetEnvironmentVariable("PROBE_OUTPUT");
        if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("PROBE_OUTPUT required");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { "Assets/Probe.unity" }, target = target,
            locationPathName = target == BuildTarget.WebGL ? output : Path.Combine(output, "Probe.exe"),
            options = BuildOptions.Development
        });
        Debug.Log($"[ProbeBuild] {target} {report.summary.result} errors={report.summary.totalErrors}");
        if (report.summary.result != BuildResult.Succeeded) throw new Exception("Probe build failed");
    }
}
