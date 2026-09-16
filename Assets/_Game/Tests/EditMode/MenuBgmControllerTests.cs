using Game.Bootstrap;
using Game.Core.Flow;
using Game.Core.Settings;
using NUnit.Framework;
using UnityEngine;

namespace Game.Architecture.Tests
{
    public sealed class MenuBgmControllerTests
    {
        [Test]
        public void Lobby_FadesOut_AndReturningToBrowserReversesTheFade()
        {
            var host = new GameObject("BGM Fade Test");
            MenuBgmController controller = null;
            try
            {
                var source = host.AddComponent<AudioSource>();
                var flow = new AppFlowSystem();
                var sound = new SoundSettingsSystem(new InMemorySoundSettingsStore());
                controller = new MenuBgmController(flow, sound, source);
                controller.Start();
                var initial = source.volume;
                var advance = typeof(MenuBgmController).GetMethod("AdvanceFade",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                flow.TryTransitionTo(AppFlowState.Lobby);
                Assert.That(source.volume, Is.EqualTo(initial));
                advance.Invoke(controller, new object[] { 0.5f });
                Assert.That(source.volume, Is.EqualTo(initial * 0.5f).Within(0.001f));
                flow.TryTransitionTo(AppFlowState.RoomBrowser);
                advance.Invoke(controller, new object[] { 0.25f });
                Assert.That(source.volume, Is.EqualTo(initial * 0.75f).Within(0.001f));
                flow.TryTransitionTo(AppFlowState.Lobby);
                advance.Invoke(controller, new object[] { 1f });
                Assert.That(source.volume, Is.Zero);
                Assert.That(source.isPlaying, Is.False);
            }
            finally
            {
                controller?.Dispose();
                Object.DestroyImmediate(host);
            }
        }

        [TestCase(AppFlowState.Home, true)]
        [TestCase(AppFlowState.Settings, true)]
        [TestCase(AppFlowState.CharacterCloset, true)]
        [TestCase(AppFlowState.RoomBrowser, true)]
        [TestCase(AppFlowState.Lobby, false)]
        [TestCase(AppFlowState.InGame, false)]
        [TestCase(AppFlowState.Highlight, false)]
        [TestCase(AppFlowState.Result, false)]
        public void Playback_IsLimitedToFrontend(AppFlowState state, bool expected)
        {
            Assert.That(MenuBgmController.ShouldPlay(state), Is.EqualTo(expected));
        }

        [Test]
        public void MicrophoneTest_FadesOut_AndStoppingRestoresTheSavedVolume()
        {
            var host = new GameObject("BGM Mic Test");
            MenuBgmController controller = null;
            try
            {
                var source = host.AddComponent<AudioSource>();
                var flow = new AppFlowSystem();
                var sound = new SoundSettingsSystem(new InMemorySoundSettingsStore());
                var microphoneTest = new NullMicrophoneTest();
                controller = new MenuBgmController(flow, sound, source, microphoneTest);
                controller.Start();
                var initial = source.volume;
                var advance = typeof(MenuBgmController).GetMethod("AdvanceFade",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

                microphoneTest.Start(SoundCatalog.DefaultDevice);
                advance.Invoke(controller, new object[] { 0.5f });
                Assert.That(source.volume, Is.EqualTo(initial * 0.5f).Within(0.001f));
                advance.Invoke(controller, new object[] { 0.5f });
                Assert.That(source.volume, Is.Zero);

                microphoneTest.Stop();
                advance.Invoke(controller, new object[] { MenuBgmController.MicReleaseSeconds });
                Assert.That(source.volume, Is.Zero, "Stay muted until the capture device is released.");
                advance.Invoke(controller, new object[] { 0.5f });
                Assert.That(source.volume, Is.EqualTo(initial * 0.5f).Within(0.001f));
                advance.Invoke(controller, new object[] { 1f });
                Assert.That(source.volume, Is.EqualTo(initial).Within(0.001f));
            }
            finally
            {
                controller?.Dispose();
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Volume_UsesSavedMusicAndAppliedChangesWithoutMultiplyingMaster()
        {
            var host = new GameObject("BGM Test");
            MenuBgmController controller = null;
            try
            {
                var source = host.AddComponent<AudioSource>();
                var flow = new AppFlowSystem();
                flow.TryTransitionTo(AppFlowState.Lobby);
                var store = new InMemorySoundSettingsStore();
                store.Save(SoundCatalog.Defaults.With(SoundVolume.Music, 20).With(SoundVolume.Master, 40));
                var sound = new SoundSettingsSystem(store);
                controller = new MenuBgmController(flow, sound, source);
                controller.Start();
                Assert.That(source.volume, Is.EqualTo(0.2f).Within(0.001f));
                sound.Preview(sound.Current.With(SoundVolume.Music, 80));
                Assert.That(source.volume, Is.EqualTo(0.8f).Within(0.001f));
                Assert.That(sound.Current.Get(SoundVolume.Music), Is.EqualTo(20));
                Assert.That(store.Saved.Value.Get(SoundVolume.Music), Is.EqualTo(20));
                sound.ApplyToAudio();
                Assert.That(source.volume, Is.EqualTo(0.2f).Within(0.001f));
                sound.Apply(sound.Current.With(SoundVolume.Music, 0));
                Assert.That(source.volume, Is.Zero);
                controller.Dispose();
                sound.Apply(sound.Current.With(SoundVolume.Music, 100));
                Assert.That(source.volume, Is.Zero, "Disposed controllers must unsubscribe.");
            }
            finally
            {
                controller?.Dispose();
                Object.DestroyImmediate(host);
            }
        }
    }
}
