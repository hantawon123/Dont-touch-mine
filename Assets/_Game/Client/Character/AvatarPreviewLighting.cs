using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.Client.Character
{
    /// <summary>Uses the same portrait lighting in the frontend and the additive lobby.</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class AvatarPreviewLighting : MonoBehaviour
    {
        private Camera portrait;
        private Light key;
        private readonly List<Light> suppressed = new();
        private bool rendering;
        private bool previousFog;
        private AmbientMode previousAmbientMode;
        private Color previousAmbientLight;
        private SphericalHarmonicsL2 previousProbe;
        private float previousReflection;
        private Light previousSun;

        private void Awake()
        {
            if (portrait != null) return;
            portrait = GetComponent<Camera>();
            var data = portrait.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.volumeLayerMask = 0;

            var source = new GameObject("PortraitDirectionalLight");
            source.transform.SetParent(transform, false);
            // Match the Character scene's authored key light.
            source.transform.rotation = Quaternion.Euler(50, -30, 0);
            key = source.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(1f, .95686275f, .8392157f);
            key.intensity = 1f;
            key.cullingMask = portrait.cullingMask;
            key.shadows = LightShadows.None;
            key.enabled = false;
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += BeginCamera;
            RenderPipelineManager.endCameraRendering += EndCamera;
        }

        internal void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.endCameraRendering -= EndCamera;
            RestoreLighting();
        }

        private void BeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera == portrait) ApplyLighting();
        }

        private void EndCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera == portrait) RestoreLighting();
        }

        internal void ApplyLighting()
        {
            if (rendering) return;
            if (portrait == null) Awake();
            rendering = true;
            previousFog = RenderSettings.fog;
            previousAmbientMode = RenderSettings.ambientMode;
            previousAmbientLight = RenderSettings.ambientLight;
            previousProbe = RenderSettings.ambientProbe;
            previousReflection = RenderSettings.reflectionIntensity;
            previousSun = RenderSettings.sun;

            // Only suppress scene lights while this camera renders. Restore them
            // before the world camera runs; no lobby lighting assets are changed.
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (!light.enabled || light.transform.IsChildOf(transform)) continue;
                suppressed.Add(light);
                light.enabled = false;
            }
            key.enabled = true;
            RenderSettings.sun = key;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Flat;
            var ambient = new Color(.212f, .227f, .259f);
            RenderSettings.ambientLight = ambient;
            var probe = new SphericalHarmonicsL2();
            probe.AddAmbientLight(ambient.linear);
            RenderSettings.ambientProbe = probe;
            RenderSettings.reflectionIntensity = 0f;
            var stage = transform.parent != null ? transform.parent : transform;
            foreach (var renderer in stage.GetComponentsInChildren<Renderer>(true))
            {
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
        }

        internal void RestoreLighting()
        {
            if (!rendering) return;
            key.enabled = false;
            foreach (var light in suppressed)
                if (light != null) light.enabled = true;
            suppressed.Clear();
            RenderSettings.sun = previousSun;
            RenderSettings.fog = previousFog;
            RenderSettings.ambientMode = previousAmbientMode;
            RenderSettings.ambientLight = previousAmbientLight;
            RenderSettings.ambientProbe = previousProbe;
            RenderSettings.reflectionIntensity = previousReflection;
            rendering = false;
        }
    }
}
