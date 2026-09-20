using Game.Bootstrap;
using Game.Core.Flow;
using Game.Core.Settings;
using Game.Network.Match;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Architecture.Tests
{
    public sealed class EndingBgmControllerTests
    {
        [TestCase(AppFlowState.Highlight, true)]
        [TestCase(AppFlowState.Result, false)]
        [TestCase(AppFlowState.InGame, false)]
        [TestCase(AppFlowState.Lobby, false)]
        [TestCase(AppFlowState.Home, false)]
        public void Playback_CoversHighlightOnly(AppFlowState state, bool expected)
        {
            Assert.That(EndingBgmController.ShouldPlay(state), Is.EqualTo(expected));
        }

        [Test]
        public void ShouldPlay_StopsOnceTheLocalPlayerIsInTheLobby()
        {
            Assert.That(EndingBgmController.ShouldPlay(AppFlowState.Highlight), Is.True);
            Assert.That(EndingBgmController.ShouldPlay(AppFlowState.Highlight, true), Is.False);
        }

        [Test]
        public void FadesOutWhenLocalPlayerReachesLobbyBeforeHighlightEnds()
        {
            var host = new GameObject("Ending BGM Skip Lobby Test");
            EndingBgmController controller = null;
            try
            {
                var source = host.AddComponent<AudioSource>();
                var flow = EnterHighlight();
                var sound = new SoundSettingsSystem(new InMemorySoundSettingsStore());
                var navigation = new FakeNavigation { HasLeftLocalHighlight = false };
                controller = new EndingBgmController(flow, sound, source, navigation);
                controller.Start();
                var full = sound.Current.Get(SoundVolume.Music) / 100f * EndingBgmController.PlaybackVolume;
                var advance = typeof(EndingBgmController).GetMethod("AdvanceFade",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

                advance.Invoke(controller, new object[] { EndingBgmController.FadeSeconds });
                Assert.That(source.isPlaying, Is.True);
                Assert.That(source.volume, Is.EqualTo(full).Within(.001f));

                navigation.HasLeftLocalHighlight = true;
                controller.Tick();
                advance.Invoke(controller, new object[] { EndingBgmController.FadeSeconds });
                Assert.That(source.volume, Is.Zero);
                Assert.That(source.isPlaying, Is.False);
                Assert.That(flow.CurrentState, Is.EqualTo(AppFlowState.Highlight));
            }
            finally
            {
                controller?.Dispose();
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ProjectUsesTheApprovedEndingTrack()
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(EndingBgmController.ClipAssetPath);
            Assert.That(clip, Is.Not.Null);
            Assert.That(clip.name, Is.EqualTo("ending_bgm"));

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Bootstrap/ProjectLifetimeScope.prefab");
            var assigned = new SerializedObject(prefab.GetComponent<ProjectLifetimeScope>())
                .FindProperty("_endingBgm").objectReferenceValue as AudioClip;
            Assert.That(assigned, Is.SameAs(clip));
        }

        [Test]
        public void FadesInOnHighlight_AndFadesOutOnResult()
        {
            var host = new GameObject("Ending BGM Fade Test");
            EndingBgmController controller = null;
            try
            {
                var source = host.AddComponent<AudioSource>();
                var flow = EnterHighlight();
                var sound = new SoundSettingsSystem(new InMemorySoundSettingsStore());
                controller = new EndingBgmController(flow, sound, source);
                controller.Start();
                var full = sound.Current.Get(SoundVolume.Music) / 100f * EndingBgmController.PlaybackVolume;
                var advance = typeof(EndingBgmController).GetMethod("AdvanceFade",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

                Assert.That(source.isPlaying, Is.True);
                Assert.That(source.volume, Is.Zero);
                advance.Invoke(controller, new object[] { .5f });
                Assert.That(source.volume, Is.EqualTo(full * .5f).Within(.001f));
                advance.Invoke(controller, new object[] { .5f });
                Assert.That(source.volume, Is.EqualTo(full).Within(.001f));

                flow.TryTransitionTo(AppFlowState.Result);
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
            var host = new GameObject("Ending BGM Volume Test");
            EndingBgmController controller = null;
            try
            {
                var source = host.AddComponent<AudioSource>();
                var store = new InMemorySoundSettingsStore();
                store.Save(SoundCatalog.Defaults.With(SoundVolume.Music, 20).With(SoundVolume.Master, 40));
                var sound = new SoundSettingsSystem(store);
                var flow = EnterHighlight();
                controller = new EndingBgmController(flow, sound, source);
                controller.Start();
                var advance = typeof(EndingBgmController).GetMethod("AdvanceFade",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                advance.Invoke(controller, new object[] { EndingBgmController.FadeSeconds });
                Assert.That(source.volume, Is.EqualTo(.2f * EndingBgmController.PlaybackVolume).Within(.001f));
                sound.Preview(sound.Current.With(SoundVolume.Music, 80));
                Assert.That(source.volume, Is.EqualTo(.8f * EndingBgmController.PlaybackVolume).Within(.001f));
            }
            finally
            {
                controller?.Dispose();
                Object.DestroyImmediate(host);
            }
        }

        private static AppFlowSystem EnterHighlight()
        {
            var flow = new AppFlowSystem();
            Assert.That(flow.TryTransitionTo(AppFlowState.RoomBrowser), Is.True);
            Assert.That(flow.TryTransitionTo(AppFlowState.Lobby), Is.True);
            Assert.That(flow.TryTransitionTo(AppFlowState.InGame), Is.True);
            Assert.That(flow.TryTransitionTo(AppFlowState.Highlight), Is.True);
            return flow;
        }

        private sealed class FakeNavigation : INetworkResultNavigation
        {
            public bool IsServer => false;
            public bool IsRuntimeReady => true;
            public bool IsResultSceneLoaded => false;
            public bool IsLocalHighlightComplete { get; set; }
            public bool HasLeftLocalHighlight { get; set; }
            public bool EnterResultScene() => false;
            public bool PrepareLobbyForHighlights() => false;
            public bool CompleteLocalHighlightViewing() => IsLocalHighlightComplete;
            public bool RequestReturnToLobby() => false;
        }
    }
}
