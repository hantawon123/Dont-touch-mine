using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.Bootstrap
{
    /// <summary>Adjusts only 3D pixel cost; overlay UI, simulation and effects retain their settings.</summary>
    public sealed class WebGlAdaptiveResolution : MonoBehaviour
    {
        private readonly WebGlFrameBudget budget = new();
        private RenderPipelineAsset previousQualityPipeline;
        private UniversalRenderPipelineAsset ownedPipeline;
        private float originalScale;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var host = new GameObject(nameof(WebGlAdaptiveResolution));
            DontDestroyOnLoad(host);
            host.AddComponent<WebGlAdaptiveResolution>();
#endif
        }

        private void OnEnable()
        {
            previousQualityPipeline = QualitySettings.renderPipeline;
            var source = previousQualityPipeline != null
                ? previousQualityPipeline : GraphicsSettings.defaultRenderPipeline;
            if (source is not UniversalRenderPipelineAsset urp) return;

            ownedPipeline = Instantiate(urp);
            ownedPipeline.name = urp.name + " (WebGL runtime)";
            originalScale = urp.renderScale;
            // Bilinear upscaling is supported by WebGL and avoids an additional sharpening pass.
            ownedPipeline.upscalingFilter = UpscalingFilterSelection.Linear;
            QualitySettings.renderPipeline = ownedPipeline;
            budget.Reset();
        }

        private void Update()
        {
            if (ownedPipeline == null || QualitySettings.renderPipeline != ownedPipeline) return;
            if (!Application.isFocused)
            {
                budget.DiscardWindow();
                return;
            }

            ownedPipeline.renderScale = originalScale * budget.AddFrame(
                Time.unscaledDeltaTime, Application.targetFrameRate);
        }

        private void OnDisable()
        {
            if (ownedPipeline == null) return;
            if (QualitySettings.renderPipeline == ownedPipeline)
                QualitySettings.renderPipeline = previousQualityPipeline;
            Destroy(ownedPipeline);
            ownedPipeline = null;
        }
    }

    // Constant storage, no per-frame allocations. Separate down/up windows prevent rapid oscillation.
    internal sealed class WebGlFrameBudget
    {
        internal float Scale { get; private set; } = 1f;
        private float elapsed;
        private float stableSeconds;
        private int frames;
        private int slowFrames;
        private int longFrames;

        internal void Reset()
        {
            Scale = 1f;
            DiscardWindow();
        }

        internal void DiscardWindow()
        {
            elapsed = stableSeconds = 0f;
            frames = slowFrames = 0;
            longFrames = 0;
        }

        internal float AddFrame(float seconds, int frameCap)
        {
            // Loading, tab restoration and isolated long stalls must not lower the image quality.
            if (seconds <= 0f)
            {
                DiscardWindow();
                return Scale;
            }
            if (seconds > 0.1f)
            {
                // Ignore isolated stalls, but still adapt when rendering stays below 10 FPS.
                if (++longFrames < 3)
                {
                    elapsed = stableSeconds = 0f;
                    frames = slowFrames = 0;
                    return Scale;
                }
                seconds = 0.1f;
            }
            else longFrames = 0;

            var target = frameCap > 0 ? Mathf.Min(60, frameCap) : 60;
            var frameBudget = 1f / target;
            elapsed += seconds;
            frames++;
            if (seconds > frameBudget * 1.3f) slowFrames++;
            if (elapsed < 2f) return Scale;

            if (elapsed / frames > frameBudget * 1.08f || slowFrames > frames * 0.08f)
            {
                Scale = Mathf.Max(0.5f, Scale - 0.1f);
                stableSeconds = 0f;
            }
            else
            {
                stableSeconds += elapsed;
                if (stableSeconds >= 10f)
                {
                    // Probe recovery even when a user-selected FPS cap hides spare GPU capacity.
                    Scale = Mathf.Min(1f, Scale + 0.05f);
                    stableSeconds = 0f;
                }
            }
            elapsed = 0f;
            frames = slowFrames = 0;
            return Scale;
        }
    }
}
