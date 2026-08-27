using Game.Core.Settings;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class GraphicsSettingsStateTests
    {
        [Test]
        public void Defaults_AreHighQualityAt1080p()
        {
            var settings = new GraphicsSettingsState();

            Assert.That(settings.Quality, Is.EqualTo(GraphicsQualityPreset.High));
            Assert.That(settings.ResolutionIndex, Is.EqualTo(GraphicsSettingsState.DefaultResolutionIndex));
            Assert.That(settings.Resolution.Width, Is.EqualTo(1920));
            Assert.That(settings.Resolution.Height, Is.EqualTo(1080));
            Assert.That(settings.DisplayMode, Is.EqualTo(DisplayMode.Fullscreen));
            Assert.That(settings.FrameCapIndex, Is.EqualTo(GraphicsSettingsState.DefaultFrameCapIndex));
            Assert.That(settings.GetTargetFrameRate(), Is.EqualTo(60));
            Assert.That(settings.Shadows, Is.EqualTo(ShadowQualityLevel.High));
            Assert.That(settings.Effects, Is.EqualTo(EffectsQualityLevel.High));
            Assert.That(settings.AntiAliasing, Is.EqualTo(AntiAliasingMode.Taa));
            Assert.That(settings.Brightness, Is.EqualTo(GraphicsSettingsState.DefaultBrightness));
            Assert.That(settings.GetExposure(), Is.EqualTo(0f));
        }

        [Test]
        public void Constructor_ClampsOutOfRangeValues()
        {
            var settings = new GraphicsSettingsState(
                (GraphicsQualityPreset)999,
                -1,
                (DisplayMode)999,
                99,
                (ShadowQualityLevel)999,
                (EffectsQualityLevel)999,
                (AntiAliasingMode)999,
                140);

            Assert.That(settings.ResolutionIndex, Is.EqualTo(0));
            Assert.That(settings.DisplayMode, Is.EqualTo(DisplayMode.Fullscreen));
            Assert.That(settings.FrameCapIndex, Is.EqualTo(GraphicsSettingsState.FrameCaps.Length - 1));
            Assert.That(settings.Brightness, Is.EqualTo(100));
        }

        [Test]
        public void TrySetQuality_AppliesPresetDetails()
        {
            var settings = new GraphicsSettingsState();

            Assert.That(settings.TrySetQuality(GraphicsQualityPreset.Low, out var error), Is.True);
            Assert.That(error, Is.EqualTo(GraphicsSettingsError.None));
            Assert.That(settings.Quality, Is.EqualTo(GraphicsQualityPreset.Low));
            Assert.That(settings.Shadows, Is.EqualTo(ShadowQualityLevel.Low));
            Assert.That(settings.Effects, Is.EqualTo(EffectsQualityLevel.Low));
            Assert.That(settings.AntiAliasing, Is.EqualTo(AntiAliasingMode.Fxaa));
        }

        [Test]
        public void TrySetShadows_SwitchesToCustomThenBackWhenMatchingPreset()
        {
            var settings = new GraphicsSettingsState();

            Assert.That(settings.TrySetShadows(ShadowQualityLevel.Off, out var error), Is.True);
            Assert.That(error, Is.EqualTo(GraphicsSettingsError.None));
            Assert.That(settings.Quality, Is.EqualTo(GraphicsQualityPreset.Custom));
            Assert.That(settings.Shadows, Is.EqualTo(ShadowQualityLevel.Off));
            Assert.That(settings.Effects, Is.EqualTo(EffectsQualityLevel.High));

            Assert.That(settings.TrySetShadows(ShadowQualityLevel.High, out error), Is.True);
            Assert.That(settings.Quality, Is.EqualTo(GraphicsQualityPreset.High));
        }

        [Test]
        public void TrySetBrightness_RejectsOutOfRange()
        {
            var settings = new GraphicsSettingsState();

            Assert.That(settings.TrySetBrightness(-1, out var error), Is.False);
            Assert.That(error, Is.EqualTo(GraphicsSettingsError.InvalidBrightness));
            Assert.That(settings.Brightness, Is.EqualTo(GraphicsSettingsState.DefaultBrightness));

            Assert.That(settings.TrySetBrightness(101, out error), Is.False);
            Assert.That(error, Is.EqualTo(GraphicsSettingsError.InvalidBrightness));
        }

        [Test]
        public void GetTargetFrameRate_AndExposure_MapEnds()
        {
            var settings = new GraphicsSettingsState();

            Assert.That(settings.TrySetFrameCap(GraphicsSettingsState.FrameCaps.Length - 1, out _), Is.True);
            Assert.That(settings.GetTargetFrameRate(), Is.EqualTo(-1));

            Assert.That(settings.TrySetBrightness(0, out _), Is.True);
            Assert.That(settings.GetExposure(), Is.EqualTo(-1.5f).Within(0.0001f));

            Assert.That(settings.TrySetBrightness(100, out _), Is.True);
            Assert.That(settings.GetExposure(), Is.EqualTo(1.5f).Within(0.0001f));
        }
    }
}
