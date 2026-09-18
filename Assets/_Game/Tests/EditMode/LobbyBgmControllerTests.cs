using Game.Bootstrap;
using Game.Core.Flow;
using Game.Core.Settings;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Architecture.Tests
{
    public sealed class LobbyBgmControllerTests
    {
        [TestCase(AppFlowState.Lobby, true)]
        [TestCase(AppFlowState.Home, false)]
        [TestCase(AppFlowState.RoomBrowser, false)]
        [TestCase(AppFlowState.InGame, false)]
        [TestCase(AppFlowState.Highlight, false)]
        [TestCase(AppFlowState.Result, false)]
        public void Playback_IsLimitedToTheLobby(AppFlowState state, bool expected)
        {
            Assert.That(LobbyBgmController.ShouldPlay(state), Is.EqualTo(expected));
        }

        [Test]
        public void ProjectUsesTheApprovedLobbyTrack()
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(LobbyBgmController.ClipAssetPath);
            Assert.That(clip, Is.Not.Null);
            Assert.That(clip.name, Is.EqualTo("lobby_bgm"));

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Bootstrap/ProjectLifetimeScope.prefab");
            var assigned = new SerializedObject(prefab.GetComponent<ProjectLifetimeScope>())
                .FindProperty("_lobbyBgm").objectReferenceValue as AudioClip;
            Assert.That(assigned, Is.SameAs(clip));
        }

        [Test]
        public void FadesInOnEnter_AndFadesOutOnLeave()
        {
            var host = new GameObject("Lobby BGM Fade Test");
            LobbyBgmController controller = null;
            try
            {
                var source = host.AddComponent<AudioSource>();
                source.loop = true;
                var flow = EnterLobby();
                var sound = new SoundSettingsSystem(new InMemorySoundSettingsStore());
                controller = new LobbyBgmController(flow, sound, source);
                controller.Start();
                var full = sound.Current.Get(SoundVolume.Music) / 100f * LobbyBgmController.PlaybackVolume;
                var advance = typeof(LobbyBgmController).GetMethod("AdvanceFade",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

                Assert.That(source.isPlaying, Is.True);
                Assert.That(source.loop, Is.True);
                Assert.That(source.volume, Is.Zero);
                advance.Invoke(controller, new object[] { .5f });
                Assert.That(source.volume, Is.EqualTo(full * .5f).Within(.001f));
                advance.Invoke(controller, new object[] { .5f });
                Assert.That(source.volume, Is.EqualTo(full).Within(.001f));

                flow.TryTransitionTo(AppFlowState.InGame);
                advance.Invoke(controller, new object[] { .5f });
                Assert.That(source.volume, Is.EqualTo(full * .5f).Within(.001f));
                advance.Invoke(controller, new object[] { .5f });
                Assert.That(source.volume, Is.Zero);
                Assert.That(source.isPlaying, Is.False);
            }
            finally
            {
                controller?.Dispose();
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Volume_UsesHalfTheMusicSliderWithoutMultiplyingMaster()
        {
            var host = new GameObject("Lobby BGM Volume Test");
            LobbyBgmController controller = null;
            try
            {
                var source = host.AddComponent<AudioSource>();
                var store = new InMemorySoundSettingsStore();
                store.Save(SoundCatalog.Defaults.With(SoundVolume.Music, 20).With(SoundVolume.Master, 40));
                var sound = new SoundSettingsSystem(store);
                var flow = EnterLobby();
                controller = new LobbyBgmController(flow, sound, source);
                controller.Start();
                var advance = typeof(LobbyBgmController).GetMethod("AdvanceFade",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                advance.Invoke(controller, new object[] { LobbyBgmController.FadeSeconds });
                Assert.That(source.volume, Is.EqualTo(.2f * LobbyBgmController.PlaybackVolume).Within(.001f));
                sound.Preview(sound.Current.With(SoundVolume.Music, 80));
                Assert.That(source.volume, Is.EqualTo(.8f * LobbyBgmController.PlaybackVolume).Within(.001f));
            }
            finally
            {
                controller?.Dispose();
                Object.DestroyImmediate(host);
            }
        }

        private static AppFlowSystem EnterLobby()
        {
            var flow = new AppFlowSystem();
            Assert.That(flow.TryTransitionTo(AppFlowState.Lobby), Is.True);
            return flow;
        }
    }
}
