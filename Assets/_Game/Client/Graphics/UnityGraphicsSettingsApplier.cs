using System;
using Game.Core.Settings;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Game.Client.Graphics
{
    public sealed class UnityGraphicsSettingsApplier : IGraphicsSettingsApplier
    {
        private Volume brightnessVolume;
        private ColorAdjustments colorAdjustments;
        private GraphicsSettingsState lastSettings;
        private UniversalRenderPipelineAsset runtimePipeline;
        private RenderPipelineAsset originalQualityPipeline;
        private GraphicsRuntimeHost runtimeHost;

        public UnityGraphicsSettingsApplier()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            GraphicsRuntimeHost.Destroyed += RestoreRuntimePipeline;
        }

        public void Apply(GraphicsSettingsState settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            lastSettings = settings;
            if (!Application.isPlaying)
            {
                return;
            }

            ApplyFrameRate(settings);
            ApplyUrpAsset(settings);
            ApplyBuiltInQuality(settings);
            ApplyScreen(settings);
            ApplyAntiAliasing(settings.AntiAliasing);
            ApplyBrightness(settings);
            BindCanvases();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!Application.isPlaying || lastSettings == null)
            {
                return;
            }

            ApplyAntiAliasing(lastSettings.AntiAliasing);
            ApplyBrightness(lastSettings);
            BindCanvases();
        }

        private static void ApplyFrameRate(GraphicsSettingsState settings)
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = settings.GetTargetFrameRate();
        }

        private void ApplyUrpAsset(GraphicsSettingsState settings)
        {
            EnsureRuntimePipeline();
            if (runtimePipeline == null)
            {
                return;
            }

            switch (settings.Shadows)
            {
                case ShadowQualityLevel.Off:
                    runtimePipeline.shadowDistance = 0f;
                    runtimePipeline.shadowCascadeCount = 1;
                    runtimePipeline.mainLightShadowmapResolution = 256;
                    break;
                case ShadowQualityLevel.Low:
                    runtimePipeline.shadowDistance = 20f;
                    runtimePipeline.shadowCascadeCount = 1;
                    runtimePipeline.mainLightShadowmapResolution = 512;
                    break;
                case ShadowQualityLevel.Medium:
                    runtimePipeline.shadowDistance = 40f;
                    runtimePipeline.shadowCascadeCount = 2;
                    runtimePipeline.mainLightShadowmapResolution = 1024;
                    break;
                case ShadowQualityLevel.High:
                    runtimePipeline.shadowDistance = 80f;
                    runtimePipeline.shadowCascadeCount = 2;
                    runtimePipeline.mainLightShadowmapResolution = 2048;
                    break;
                default:
                    runtimePipeline.shadowDistance = 120f;
                    runtimePipeline.shadowCascadeCount = 4;
                    runtimePipeline.mainLightShadowmapResolution = 4096;
                    break;
            }

            switch (settings.Effects)
            {
                case EffectsQualityLevel.Low:
                    runtimePipeline.renderScale = 0.75f;
                    runtimePipeline.msaaSampleCount = 1;
                    break;
                case EffectsQualityLevel.Medium:
                    runtimePipeline.renderScale = 0.9f;
                    runtimePipeline.msaaSampleCount = 1;
                    break;
                case EffectsQualityLevel.High:
                    runtimePipeline.renderScale = 1f;
                    runtimePipeline.msaaSampleCount = 2;
                    break;
                default:
                    runtimePipeline.renderScale = 1f;
                    runtimePipeline.msaaSampleCount = 4;
                    break;
            }

            if (settings.AntiAliasing == AntiAliasingMode.Off)
            {
                runtimePipeline.msaaSampleCount = 1;
            }
        }

        private void EnsureRuntimePipeline()
        {
            if (runtimePipeline != null)
            {
                return;
            }

            var source = QualitySettings.renderPipeline as UniversalRenderPipelineAsset
                ?? UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (source == null)
            {
                return;
            }

            originalQualityPipeline = QualitySettings.renderPipeline;
            runtimePipeline = UnityEngine.Object.Instantiate(source);
            runtimePipeline.name = source.name + " Runtime";
            QualitySettings.renderPipeline = runtimePipeline;
            EnsureRuntimeHost();
        }

        private void EnsureRuntimeHost()
        {
            if (runtimeHost != null)
            {
                return;
            }

            var hostObject = new GameObject("GraphicsRuntimeHost");
            UnityEngine.Object.DontDestroyOnLoad(hostObject);
            runtimeHost = hostObject.AddComponent<GraphicsRuntimeHost>();
        }

        private void RestoreRuntimePipeline()
        {
            if (runtimePipeline == null)
            {
                return;
            }

            QualitySettings.renderPipeline = originalQualityPipeline;
            UnityEngine.Object.Destroy(runtimePipeline);
            runtimePipeline = null;
            originalQualityPipeline = null;
            runtimeHost = null;
        }

        private static void ApplyBuiltInQuality(GraphicsSettingsState settings)
        {
            switch (settings.Shadows)
            {
                case ShadowQualityLevel.Off:
                    QualitySettings.shadows = UnityEngine.ShadowQuality.Disable;
                    QualitySettings.shadowDistance = 0f;
                    break;
                case ShadowQualityLevel.Low:
                    QualitySettings.shadows = UnityEngine.ShadowQuality.HardOnly;
                    QualitySettings.shadowResolution = UnityEngine.ShadowResolution.Low;
                    QualitySettings.shadowDistance = 20f;
                    QualitySettings.shadowCascades = 1;
                    break;
                case ShadowQualityLevel.Medium:
                    QualitySettings.shadows = UnityEngine.ShadowQuality.All;
                    QualitySettings.shadowResolution = UnityEngine.ShadowResolution.Medium;
                    QualitySettings.shadowDistance = 40f;
                    QualitySettings.shadowCascades = 2;
                    break;
                case ShadowQualityLevel.High:
                    QualitySettings.shadows = UnityEngine.ShadowQuality.All;
                    QualitySettings.shadowResolution = UnityEngine.ShadowResolution.High;
                    QualitySettings.shadowDistance = 80f;
                    QualitySettings.shadowCascades = 2;
                    break;
                default:
                    QualitySettings.shadows = UnityEngine.ShadowQuality.All;
                    QualitySettings.shadowResolution = UnityEngine.ShadowResolution.VeryHigh;
                    QualitySettings.shadowDistance = 120f;
                    QualitySettings.shadowCascades = 4;
                    break;
            }

            switch (settings.Effects)
            {
                case EffectsQualityLevel.Low:
                    QualitySettings.lodBias = 0.5f;
                    QualitySettings.particleRaycastBudget = 64;
                    QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
                    QualitySettings.globalTextureMipmapLimit = 1;
                    break;
                case EffectsQualityLevel.Medium:
                    QualitySettings.lodBias = 1f;
                    QualitySettings.particleRaycastBudget = 256;
                    QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
                    QualitySettings.globalTextureMipmapLimit = 0;
                    break;
                case EffectsQualityLevel.High:
                    QualitySettings.lodBias = 1.5f;
                    QualitySettings.particleRaycastBudget = 512;
                    QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
                    QualitySettings.globalTextureMipmapLimit = 0;
                    break;
                default:
                    QualitySettings.lodBias = 2f;
                    QualitySettings.particleRaycastBudget = 1024;
                    QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
                    QualitySettings.globalTextureMipmapLimit = 0;
                    break;
            }
        }

        private static void ApplyScreen(GraphicsSettingsState settings)
        {
            var resolution = settings.Resolution;
            var fullScreenMode = ToFullScreenMode(settings.DisplayMode);
            if (Screen.width == resolution.Width &&
                Screen.height == resolution.Height &&
                Screen.fullScreenMode == fullScreenMode)
            {
                return;
            }

            Screen.SetResolution(resolution.Width, resolution.Height, fullScreenMode);
        }

        private static void ApplyAntiAliasing(AntiAliasingMode mode)
        {
            var cameras = UnityEngine.Object.FindObjectsByType<Camera>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (var index = 0; index < cameras.Length; index++)
            {
                var camera = cameras[index];
                if (camera.clearFlags == CameraClearFlags.SolidColor)
                {
                    RestoreMenuCamera(camera);
                    continue;
                }

                var data = camera.GetUniversalAdditionalCameraData();
                if (data == null || data.renderType == CameraRenderType.Overlay)
                {
                    continue;
                }

                data.renderPostProcessing = true;
                switch (mode)
                {
                    case AntiAliasingMode.Off:
                        data.antialiasing = AntialiasingMode.None;
                        break;
                    case AntiAliasingMode.Fxaa:
                        data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
                        break;
                    case AntiAliasingMode.Smaa:
                        data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                        data.antialiasingQuality = AntialiasingQuality.High;
                        break;
                    default:
                        data.antialiasing = AntialiasingMode.TemporalAntiAliasing;
                        break;
                }
            }
        }

        private static void RestoreMenuCamera(Camera camera)
        {
            if (!camera.TryGetComponent(out UniversalAdditionalCameraData data))
            {
                return;
            }

            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;
        }

        private void ApplyBrightness(GraphicsSettingsState settings)
        {
            EnsureBrightnessVolume();
            colorAdjustments.postExposure.Override(settings.GetExposure());
        }

        private void EnsureBrightnessVolume()
        {
            if (brightnessVolume != null)
            {
                return;
            }

            var volumeObject = new GameObject("GraphicsBrightnessVolume");
            UnityEngine.Object.DontDestroyOnLoad(volumeObject);
            brightnessVolume = volumeObject.AddComponent<Volume>();
            brightnessVolume.isGlobal = true;
            brightnessVolume.priority = 50f;
            brightnessVolume.weight = 1f;
            brightnessVolume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
            colorAdjustments = brightnessVolume.profile.Add<ColorAdjustments>(false);
        }

        private static void BindCanvases()
        {
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (var index = 0; index < canvases.Length; index++)
            {
                GraphicsBindings.EnsureCanvas(canvases[index].gameObject);
            }
        }

        private static FullScreenMode ToFullScreenMode(DisplayMode mode)
        {
            switch (mode)
            {
                case DisplayMode.Windowed:
                    return FullScreenMode.Windowed;
                case DisplayMode.Borderless:
                    return FullScreenMode.FullScreenWindow;
                default:
#if UNITY_EDITOR
                    return FullScreenMode.FullScreenWindow;
#else
                    return FullScreenMode.ExclusiveFullScreen;
#endif
            }
        }
    }
}
