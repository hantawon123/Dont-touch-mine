using Game.Bootstrap;
using Game.Core.Settings;
using NUnit.Framework;
using UnityEngine;

namespace Game.Architecture.Tests
{
    public sealed class SupportedSettingsRuntimeTests
    {
        private int fps, vsync, texture;
        private float volume;
        [SetUp]
        public void SaveRuntimeValues()
        {
            fps = Application.targetFrameRate;
            vsync = QualitySettings.vSyncCount;
            texture = QualitySettings.globalTextureMipmapLimit;
            volume = AudioListener.volume;
        }
        [TearDown]
        public void RestoreRuntimeValues()
        {
            Application.targetFrameRate = fps;
            QualitySettings.vSyncCount = vsync;
            QualitySettings.globalTextureMipmapLimit = texture;
            AudioListener.volume = volume;
        }
        [TestCase("120", 120)]
        [TestCase("60", 60)]
        [TestCase("40", 40)]
        [TestCase("30", 30)]
        public void FrameLimitReachesUnityAndDisablesVsync(string code, int expected)
        {
            QualitySettings.vSyncCount = 1;
            new UnityGraphicsSettingsApplier().Apply(GraphicsCatalog.Shipped.Defaults.With(GraphicsOption.FpsLimit, code));
            Assert.That(Application.targetFrameRate, Is.EqualTo(expected));
            Assert.That(QualitySettings.vSyncCount, Is.Zero);
        }
        [TestCase(GraphicsCatalog.High, 0)]
        [TestCase(GraphicsCatalog.Medium, 1)]
        [TestCase(GraphicsCatalog.Low, 2)]
        public void TextureQualityReachesUnity(string code, int expected)
        {
            new UnityGraphicsSettingsApplier().Apply(GraphicsCatalog.Shipped.Defaults.With(GraphicsOption.TextureQuality, code));
            Assert.That(QualitySettings.globalTextureMipmapLimit, Is.EqualTo(expected));
        }
        [TestCase(0, 0f)]
        [TestCase(50, 0.5f)]
        [TestCase(100, 1f)]
        public void MasterVolumeReachesAudioListener(int percent, float expected)
        {
            new UnitySoundSettingsApplier().Apply(SoundCatalog.Defaults.With(SoundVolume.Master, percent));
            Assert.That(AudioListener.volume, Is.EqualTo(expected).Within(0.001f));
        }
        [Test]
        public void Apply_DoesNotResizeTheEditorOutsidePlayMode()
        {
            var previous = UnityGraphicsSettingsApplier.ApplyEditorGameView;
            UnityGraphicsSettingsApplier.ApplyEditorGameView = (_, _) =>
                Assert.Fail("EditMode tests must not move the Game view.");
            try
            {
                Assert.DoesNotThrow(() =>
                    new UnityGraphicsSettingsApplier().Apply(GraphicsCatalog.Shipped.Defaults));
            }
            finally
            {
                UnityGraphicsSettingsApplier.ApplyEditorGameView = previous;
            }
        }

        [Test]
        public void SavedGraphicsReachUnityOnStartup()
        {
            var store = new InMemoryGraphicsSettingsStore();
            store.Save(GraphicsCatalog.Shipped.Defaults.With(GraphicsOption.FpsLimit, "40").With(GraphicsOption.TextureQuality, "low"));
            var system = new GraphicsSettingsSystem(store, new UnityGraphicsSettingsApplier());
            new GraphicsSettingsStartup(system).Start();
            Assert.That(Application.targetFrameRate, Is.EqualTo(40));
            Assert.That(QualitySettings.globalTextureMipmapLimit, Is.EqualTo(2));
        }
        [Test]
        public void SavedMasterVolumeReachesUnityOnStartup()
        {
            var store = new InMemorySoundSettingsStore();
            store.Save(SoundCatalog.Defaults.With(SoundVolume.Master, 25));
            var system = new SoundSettingsSystem(store, new UnitySoundSettingsApplier());
            new SoundSettingsStartup(system).Start();
            Assert.That(AudioListener.volume, Is.EqualTo(0.25f).Within(0.001f));
        }
    }
}
