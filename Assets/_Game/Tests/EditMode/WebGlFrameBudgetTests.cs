using Game.Bootstrap;
using NUnit.Framework;
using Game.Core.Settings;
using UnityEngine;
using RenderPipelineAsset = UnityEngine.Rendering.RenderPipelineAsset;
using UnityEditor;

namespace Game.Tests.EditMode
{
    public class WebGlFrameBudgetTests
    {
        [Test]
        public void WebGlBuild_ContainsOnlyItsDedicatedPipeline()
        {
            var web = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/WebGL_RPAsset.asset");
            QualitySettings.GetRenderPipelineAssetsForPlatform<RenderPipelineAsset>(
                "WebGL", out var pipelines, out var allOverridden);
            Assert.That(allOverridden, Is.True, "Desktop fallback must not enter WebGL builds.");
            Assert.That(pipelines, Is.EquivalentTo(new[] { web }));
            Assert.That(QualitySettings.GetActiveQualityLevelsForPlatformCount("WebGL"), Is.EqualTo(1));
            QualitySettings.GetRenderPipelineAssetsForPlatform<RenderPipelineAsset>(
                "Standalone", out var desktop, out _);
            var pc = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            Assert.That(desktop, Is.EquivalentTo(new[] { pc }));
            var renderer = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/Settings/WebGL_Renderer.asset"));
            var features = renderer.FindProperty("m_RendererFeatures");
            for (var i = 0; i < features.arraySize; i++)
            {
                var feature = features.GetArrayElementAtIndex(i).objectReferenceValue;
                if (feature != null) Assert.That(feature.GetType().Name, Is.Not.EqualTo("ScreenSpaceShadows"));
            }
        }
        [Test]
        public void WebGlProfile_PreservesLightingAndDesktopQuality()
        {
            var pc = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/Settings/PC_RPAsset.asset"));
            var web = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/Settings/WebGL_RPAsset.asset"));
            foreach (var field in new[] { "m_MainLightShadowmapResolution", "m_AdditionalLightsShadowmapResolution",
                "m_ShadowDistance", "m_ShadowCascadeCount",
                "m_AdditionalLightsRenderingMode", "m_AdditionalLightsPerObjectLimit", "m_RenderScale" })
                Assert.That(SerializedProperty.DataEquals(pc.FindProperty(field), web.FindProperty(field)), Is.True, field);

            var quality = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
            var entries = quality.FindProperty("m_QualitySettings");
            Assert.That(entries.GetArrayElementAtIndex(1).FindPropertyRelative("name").stringValue, Is.EqualTo("PC"));
            Assert.That(entries.GetArrayElementAtIndex(2).FindPropertyRelative("name").stringValue, Is.EqualTo("WebGL"));
            Assert.That(entries.GetArrayElementAtIndex(2).FindPropertyRelative("customRenderPipeline").objectReferenceValue,
                Is.EqualTo(web.targetObject));
        }

        [Test]
        public void ShadowPreference_DoesNotEnableWebGlRealtimeShadows()
        {
            var source = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/WebGL_RPAsset.asset");
            var pipeline = Object.Instantiate(source);
            var previous = QualitySettings.renderPipeline;
            var previousFps = Application.targetFrameRate;
            var previousVsync = QualitySettings.vSyncCount;
            var previousTexture = QualitySettings.globalTextureMipmapLimit;
            try
            {
                QualitySettings.renderPipeline = pipeline;
                new UnityGraphicsSettingsApplier().Apply(GraphicsCatalog.Shipped.Defaults);
                var actual = new SerializedObject(pipeline);
                foreach (var field in new[] { "m_MainLightShadowsSupported", "m_AdditionalLightShadowsSupported",
                    "m_AnyShadowsSupported", "m_SoftShadowsSupported" })
                    Assert.That(actual.FindProperty(field).boolValue, Is.False, field);
            }
            finally
            {
                QualitySettings.renderPipeline = previous;
                Application.targetFrameRate = previousFps;
                QualitySettings.vSyncCount = previousVsync;
                QualitySettings.globalTextureMipmapLimit = previousTexture;
                Object.DestroyImmediate(pipeline);
            }
        }

        [Test]
        public void SustainedFramesBelowTenFps_StillLowerScale()
        {
            var budget = new WebGlFrameBudget();
            for (var i = 0; i < 200; i++) budget.AddFrame(0.2f, 60);
            Assert.That(budget.Scale, Is.EqualTo(0.5f));
        }

        [Test]
        public void SustainedSlowFrames_LowerScaleButNeverBelowHalf()
        {
            var budget = new WebGlFrameBudget();
            for (var i = 0; i < 180; i++) budget.AddFrame(1f / 30f, 120);
            Assert.That(budget.Scale, Is.LessThan(1f));
            for (var i = 0; i < 1800; i++) budget.AddFrame(1f / 30f, 120);
            Assert.That(budget.Scale, Is.EqualTo(0.5f));
        }

        [Test]
        public void LoadingAndFocusGaps_DoNotBecomeSustainedSlowFrames()
        {
            var budget = new WebGlFrameBudget();
            for (var i = 0; i < 20; i++)
            {
                for (var frame = 0; frame < 60; frame++) budget.AddFrame(1f / 60f, 120);
                budget.AddFrame(0.5f, 120);
                budget.DiscardWindow();
            }
            Assert.That(budget.Scale, Is.EqualTo(1f));
        }

        [Test]
        public void SelectedThirtyFpsCap_DoesNotTriggerQualityReduction()
        {
            var budget = new WebGlFrameBudget();
            for (var i = 0; i < 1800; i++) budget.AddFrame(1f / 30f, 30);
            Assert.That(budget.Scale, Is.EqualTo(1f));
        }

        [Test]
        public void StableFrames_GraduallyRestoreOriginalScaleEvenAtFrameCap()
        {
            var budget = new WebGlFrameBudget();
            for (var i = 0; i < 180; i++) budget.AddFrame(1f / 30f, 60);
            var low = budget.Scale;
            for (var i = 0; i < 60; i++) budget.AddFrame(1f / 60f, 60);
            Assert.That(budget.Scale, Is.LessThanOrEqualTo(low), "Recovery must not immediately raise resolution.");
            for (var i = 0; i < 7200; i++) budget.AddFrame(1f / 60f, 60);
            Assert.That(budget.Scale, Is.EqualTo(1f));
        }
    }
}
