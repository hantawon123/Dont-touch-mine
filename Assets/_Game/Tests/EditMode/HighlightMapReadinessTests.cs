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
        [TestCase("playground")]
        [TestCase("supermarket")]
        public void Readiness_UsesTheSelectedMapInsteadOfDefaultScene(string mapId)
        {
            var scenes = AssetDatabase.LoadAssetAtPath<NetworkScenes>(
                "Assets/_Game/Content/Settings/NetworkScenes.asset");
            var info = new NetworkSceneInfo();
            info.AddSceneRef(scenes.LobbyScene);
            Assert.That(NetworkRunnerService.IsHighlightMapLoaded(info, scenes, mapId), Is.False);
            info.AddSceneRef(scenes.MatchSceneFor(mapId));
            Assert.That(NetworkRunnerService.IsHighlightMapLoaded(info, scenes, mapId), Is.True);
            if (mapId == "supermarket")
            {
                Assert.That(scenes.MatchSceneFor(mapId), Is.Not.EqualTo(scenes.MatchScene));
                var wrong = new NetworkSceneInfo();
                wrong.AddSceneRef(scenes.MatchScene);
                Assert.That(NetworkRunnerService.IsHighlightMapLoaded(wrong, scenes, mapId), Is.False);
            }
        }
    }
}
