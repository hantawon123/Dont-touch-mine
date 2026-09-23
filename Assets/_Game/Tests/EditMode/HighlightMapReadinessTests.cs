using Fusion;
using Assert = NUnit.Framework.Assert;
using Game.Network;
using Game.Network.Session;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests.EditMode
{
    public sealed class HighlightMapReadinessTests
    {
        [TestCase("supermarket")]
        [TestCase("mansion")]
        public void Readiness_AcceptsWhicheverPlayableMapIsLoaded(string mapId)
        {
            var scenes = Scenes();
            var info = new NetworkSceneInfo();
            info.AddSceneRef(scenes.LobbyScene);
            Assert.That(NetworkRunnerService.IsHighlightMapLoaded(info, scenes), Is.False);
            info.AddSceneRef(scenes.MatchSceneFor(mapId));
            Assert.That(NetworkRunnerService.IsHighlightMapLoaded(info, scenes), Is.True);
        }

        /// <summary>
        /// A random room resolves its map on the authority alone, so every other
        /// peer holds an empty map id. Readiness must not ask that id which scene
        /// to expect: it answered with the default map, and a peer playing any
        /// other map never acknowledged, costing everyone the highlight.
        /// </summary>
        [Test]
        public void Readiness_DoesNotDependOnTheRoomsMapId()
        {
            var scenes = Scenes();
            var info = new NetworkSceneInfo();
            info.AddSceneRef(scenes.LobbyScene);
            info.AddSceneRef(scenes.MatchSceneFor("mansion"));
            Assert.That(scenes.MatchSceneFor(string.Empty),
                Is.EqualTo(scenes.MatchSceneFor("supermarket")),
                "An unmapped id still falls back to the default match scene.");
            Assert.That(NetworkRunnerService.IsHighlightMapLoaded(info, scenes), Is.True);
        }

        [Test]
        public void Readiness_RefusesAResultOrLobbyOnlySceneSet()
        {
            var scenes = Scenes();
            var info = new NetworkSceneInfo();
            info.AddSceneRef(scenes.LobbyScene);
            info.AddSceneRef(scenes.ResultScene);
            Assert.That(NetworkRunnerService.IsHighlightMapLoaded(info, scenes), Is.False);
        }

        private static NetworkScenes Scenes() =>
            AssetDatabase.LoadAssetAtPath<NetworkScenes>(
                "Assets/_Game/Content/Settings/NetworkScenes.asset");
    }
}
