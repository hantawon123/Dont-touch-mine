using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace Game.Bootstrap
{
    // Local diagnostic only: opt in with ?renderprobe=1. Each comparison restores the baseline first.
    public sealed class WebGlRenderProbe : MonoBehaviour
    {
        private Camera target;
        private UniversalAdditionalCameraData data;
        private bool postProcessing;
        private bool occlusion;
        private CameraOverrideOption colorOption;
        private CameraOverrideOption depthOption;
        private CameraClearFlags clearFlags;
        private GameObject cube;
        private Material probeMaterial;
        private readonly Dictionary<Renderer, Material[]> originalMaterials = new();
        private int mode;
        private readonly List<Behaviour> disabledBehaviours = new();
        private readonly List<ScriptableRendererFeature> disabledFeatures = new();
        private bool originalBatcher;
        private UniversalRenderPipelineAsset pipeline;
        private Material litProbe;
        private static readonly string[] Labels =
        {
            "Baseline", "Post processing ON", "Opaque texture OFF",
            "Depth texture OFF", "Occlusion culling OFF", "Skybox OFF",
            "Test cube", "Unlit scene", "Cube depth bypass",
            "SRP batcher OFF", "Renderer features OFF", "Other cameras OFF",
            "Lights OFF", "Reflection probes OFF", "Default Lit cube", "Default Lit scene"
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!Application.absoluteURL.Contains("renderprobe=1")) return;
            var host = new GameObject(nameof(WebGlRenderProbe));
            DontDestroyOnLoad(host);
            host.AddComponent<WebGlRenderProbe>();
#endif
        }

        private void Update()
        {
            if (target == null)
            {
                var candidate = Camera.main;
                if (candidate == null || candidate.gameObject.scene.name != "Playground") return;
                target = candidate;
                data = target.GetUniversalAdditionalCameraData();
                postProcessing = data.renderPostProcessing;
                colorOption = data.requiresColorOption;
                depthOption = data.requiresDepthOption;
                occlusion = target.useOcclusionCulling;
                clearFlags = target.clearFlags;
                var shader = Resources.Load<Shader>("WebGlProbeUnlit");
                if (shader == null) { Debug.LogError("[RenderProbe] diagnostic shader missing"); enabled = false; return; }
                probeMaterial = new Material(shader);
                cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "Render probe cube";
                Destroy(cube.GetComponent<Collider>());
                cube.transform.SetParent(target.transform, false);
                cube.transform.localPosition = new Vector3(0, 0, 3);
                cube.transform.localScale = Vector3.one * 0.5f;
                cube.GetComponent<Renderer>().sharedMaterial = probeMaterial;
                cube.SetActive(false);
                var shaders = new HashSet<string>();
                foreach (var renderer in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                {
                    if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;
                    foreach (var material in renderer.sharedMaterials)
                        if (material != null) shaders.Add(material.shader.name + ":supported=" + material.shader.isSupported);
                }
                Debug.Log("[RenderProbe] shaders=" + string.Join(";", shaders));
                pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                originalBatcher = pipeline != null && pipeline.useSRPBatcher;
                var litShader = Shader.Find("Universal Render Pipeline/Lit");
                if (litShader != null) litProbe = new Material(litShader);
                var seen = new HashSet<Material>();
                foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                    foreach (var m in r.sharedMaterials)
                        if (m != null && seen.Add(m) && seen.Count <= 12)
                            Debug.Log($"[RenderProbeMaterial] {m.name} shader={m.shader.name} queue={m.renderQueue} passes={m.passCount} keywords={string.Join(",",m.shaderKeywords)}");
                Apply(0);
            }
            if (Keyboard.current != null && Keyboard.current.f6Key.wasPressedThisFrame)
                Apply((mode + 1) % Labels.Length);
        }

        private void Apply(int next)
        {
            foreach (var b in disabledBehaviours) if (b != null) b.enabled = true;
            disabledBehaviours.Clear();
            foreach (var f in disabledFeatures) if (f != null) f.SetActive(true);
            disabledFeatures.Clear();
            if (pipeline != null) pipeline.useSRPBatcher = originalBatcher;
            data.renderPostProcessing = postProcessing;
            data.requiresColorOption = colorOption;
            data.requiresDepthOption = depthOption;
            target.useOcclusionCulling = occlusion;
            target.clearFlags = clearFlags;
            foreach (var pair in originalMaterials)
                if (pair.Key != null) pair.Key.sharedMaterials = pair.Value;
            originalMaterials.Clear();
            cube.SetActive(false);
            cube.GetComponent<Renderer>().sharedMaterial = probeMaterial;
            probeMaterial.renderQueue = 2000;
            probeMaterial.SetFloat("_ZTest", 4);
            mode = next;
            if (mode == 1) data.renderPostProcessing = true;
            if (mode == 2) data.requiresColorOption = CameraOverrideOption.Off;
            if (mode == 3) data.requiresDepthOption = CameraOverrideOption.Off;
            if (mode == 4) target.useOcclusionCulling = false;
            if (mode == 5) target.clearFlags = CameraClearFlags.SolidColor;
            if (mode == 6 || mode == 7 || mode == 8) cube.SetActive(true);
            if (mode == 7)
            {
                foreach (var renderer in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                {
                    if (renderer.gameObject == cube || (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))) continue;
                    var old = renderer.sharedMaterials;
                    originalMaterials[renderer] = old;
                    var replacements = new Material[old.Length];
                    for (var i = 0; i < replacements.Length; i++) replacements[i] = probeMaterial;
                    renderer.sharedMaterials = replacements;
                }
            }
            if (mode == 8) { probeMaterial.renderQueue = 3000; probeMaterial.SetFloat("_ZTest", 8); }
            if (mode == 9 && pipeline != null) pipeline.useSRPBatcher = false;
            if (mode == 10 && pipeline != null)
                foreach (var rd in pipeline.rendererDataList)
                    if (rd != null) foreach (var f in rd.rendererFeatures)
                        if (f != null && f.isActive) { disabledFeatures.Add(f); f.SetActive(false); }
            if (mode == 11) foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (c != target && c.enabled) { disabledBehaviours.Add(c); c.enabled = false; }
            if (mode == 12) foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.enabled) { disabledBehaviours.Add(l); l.enabled = false; }
            if (mode == 13) foreach (var r in FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None))
                if (r.enabled) { disabledBehaviours.Add(r); r.enabled = false; }
            if ((mode == 14 || mode == 15) && litProbe != null)
            {
                cube.SetActive(true);
                cube.GetComponent<Renderer>().sharedMaterial = litProbe;
                if (mode == 15) foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                {
                    if (r.gameObject == cube || (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer))) continue;
                    var old = r.sharedMaterials;
                    originalMaterials[r] = old;
                    var replacements = new Material[old.Length];
                    for (var i = 0; i < replacements.Length; i++) replacements[i] = litProbe;
                    r.sharedMaterials = replacements;
                }
            }
            Debug.Log($"[RenderProbe] {Labels[mode]} post={data.renderPostProcessing} color={data.requiresColorOption} depth={data.requiresDepthOption} occlusion={target.useOcclusionCulling}");
        }

        private void OnGUI()
        {
            if (target == null) return;
            GUI.Box(new Rect(12, 12, 380, 50), "Render probe: " + Labels[mode] + "\nF6: next comparison (baseline restored each time)");
        }

        private void OnDestroy()
        {
            if (target != null && data != null && cube != null) Apply(0);
            if (probeMaterial != null) Destroy(probeMaterial);
            if (litProbe != null) Destroy(litProbe);
        }
    }
}
