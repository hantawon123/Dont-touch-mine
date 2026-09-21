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
        public void Readiness_UsesTheSelectedPlayableMap(string mapId)
        {
            var scenes = AssetDatabase.LoadAssetAtPath<NetworkScenes>(
                "Assets/_Game/Content/Settings/NetworkScenes.asset");
            var info = new NetworkSceneInfo();
            info.AddSceneRef(scenes.LobbyScene);
            Assert.That(NetworkRunnerService.IsHighlightMapLoaded(info, scenes, mapId), Is.False);
            info.AddSceneRef(scenes.MatchSceneFor(mapId));
            Assert.That(NetworkRunnerService.IsHighlightMapLoaded(info, scenes, mapId), Is.True);
        }
    }
}
