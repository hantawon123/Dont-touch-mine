using System.Collections.Generic;
using Game.Client.Graphics;
using Game.Core.Settings;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class GraphicsSettingsServiceTests
    {
        [Test]
        public void Construction_LoadsStoreAndAppliesImmediately()
        {
            var stored = new GraphicsSettingsState(
                GraphicsQualityPreset.Low,
                0,
                DisplayMode.Windowed,
                0,
                ShadowQualityLevel.Low,
                EffectsQualityLevel.Low,
                AntiAliasingMode.Fxaa,
                20);
            var store = new MemoryGraphicsSettingsStore { NextLoad = stored };
            var applier = new RecordingGraphicsSettingsApplier();

            var service = new GraphicsSettingsService(store, applier);

            Assert.That(service.Current.Quality, Is.EqualTo(GraphicsQualityPreset.Low));
            Assert.That(service.Current.DisplayMode, Is.EqualTo(DisplayMode.Windowed));
            Assert.That(service.Current.Brightness, Is.EqualTo(20));
            Assert.That(applier.Applied.Count, Is.EqualTo(1));
            Assert.That(applier.Applied[0].Quality, Is.EqualTo(GraphicsQualityPreset.Low));
            Assert.That(GraphicsSettingsOutput.Current, Is.SameAs(service.Current));
        }

        [Test]
        public void TrySetQuality_PersistsAndReappliesPresetDetails()
        {
            var store = new MemoryGraphicsSettingsStore();
            var applier = new RecordingGraphicsSettingsApplier();
            var service = new GraphicsSettingsService(store, applier);
            var changedCount = 0;
            service.Changed += _ => changedCount++;

            Assert.That(
                service.TrySetQuality(GraphicsQualityPreset.VeryLow, out var error),
                Is.True);
            Assert.That(error, Is.EqualTo(GraphicsSettingsError.None));
            Assert.That(service.Current.Quality, Is.EqualTo(GraphicsQualityPreset.VeryLow));
            Assert.That(service.Current.Shadows, Is.EqualTo(ShadowQualityLevel.Off));
            Assert.That(store.Saved.Quality, Is.EqualTo(GraphicsQualityPreset.VeryLow));
            Assert.That(applier.Applied.Count, Is.EqualTo(2));
            Assert.That(changedCount, Is.EqualTo(1));

            Assert.That(service.TrySetQuality(GraphicsQualityPreset.VeryLow, out error), Is.True);
            Assert.That(applier.Applied.Count, Is.EqualTo(2));
            Assert.That(changedCount, Is.EqualTo(1));
        }

        [Test]
        public void TrySetShadows_MarksQualityCustom()
        {
            var store = new MemoryGraphicsSettingsStore();
            var applier = new RecordingGraphicsSettingsApplier();
            var service = new GraphicsSettingsService(store, applier);

            Assert.That(service.TrySetShadows(ShadowQualityLevel.Off, out _), Is.True);
            Assert.That(service.Current.Quality, Is.EqualTo(GraphicsQualityPreset.Custom));
            Assert.That(service.Current.Shadows, Is.EqualTo(ShadowQualityLevel.Off));
            Assert.That(store.Saved.Quality, Is.EqualTo(GraphicsQualityPreset.Custom));
        }

        [Test]
        public void TrySetBrightness_PersistsAndReapplies()
        {
            var store = new MemoryGraphicsSettingsStore();
            var applier = new RecordingGraphicsSettingsApplier();
            var service = new GraphicsSettingsService(store, applier);

            Assert.That(service.TrySetBrightness(10, out var error), Is.True);
            Assert.That(error, Is.EqualTo(GraphicsSettingsError.None));
            Assert.That(service.Current.Brightness, Is.EqualTo(10));
            Assert.That(store.Saved.Brightness, Is.EqualTo(10));
            Assert.That(applier.Applied.Count, Is.EqualTo(2));
        }

        private sealed class MemoryGraphicsSettingsStore : IGraphicsSettingsStore
        {
            public GraphicsSettingsState NextLoad { get; set; }

            public GraphicsSettingsState Saved { get; private set; }

            public GraphicsSettingsState LoadOrDefault()
            {
                return NextLoad ?? new GraphicsSettingsState();
            }

            public void Save(GraphicsSettingsState settings)
            {
                Saved = settings;
            }
        }

        private sealed class RecordingGraphicsSettingsApplier : IGraphicsSettingsApplier
        {
            public List<GraphicsSettingsState> Applied { get; } = new List<GraphicsSettingsState>();

            public void Apply(GraphicsSettingsState settings)
            {
                Applied.Add(settings);
            }
        }
    }
}
