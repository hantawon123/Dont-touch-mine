using System.Collections;
using UnityEngine.TestTools;
using Game.Bootstrap;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Tests.EditMode
{
    public sealed class WebGlLobbyEntryTests
    {
        [UnityTest]
        public IEnumerator AdditiveInputTakeover_DisablesPreviousSystem_AndSupportsReturn()
        {
            yield return new EnterPlayMode();
            var home = new GameObject("Home input");
            var lobby = new GameObject("Lobby input");
            home.SetActive(false);
            lobby.SetActive(false);
            try
            {
                var first = home.AddComponent<ExclusiveEventSystem>();
                var second = lobby.AddComponent<ExclusiveEventSystem>();
                home.SetActive(true);
                lobby.SetActive(true);
                Assert.That(first.enabled, Is.False);
                Assert.That(EventSystem.current, Is.SameAs(second));
                first.enabled = true;
                Assert.That(second.enabled, Is.False);
                Assert.That(EventSystem.current, Is.SameAs(first));
            }
            finally
            {
                Object.DestroyImmediate(lobby);
                Object.DestroyImmediate(home);
            }
            yield return new ExitPlayMode();
        }

        [Test]
        public void LobbyBoxes_HavePositiveWorldScale_WithoutChangingVisualTransforms()
        {
            var root = PrefabUtility.LoadPrefabContents("Assets/_Game/Content/Prefabs/LobbyBasementEnvironment.prefab");
            try
            {
                var corrected = 0;
                foreach (var box in root.GetComponentsInChildren<BoxCollider>(true))
                {
                    var scale = box.transform.lossyScale;
                    Assert.That(scale.x, Is.GreaterThanOrEqualTo(0), box.name);
                    Assert.That(scale.y, Is.GreaterThanOrEqualTo(0), box.name);
                    Assert.That(scale.z, Is.GreaterThanOrEqualTo(0), box.name);
                    if (box.name == "PositiveScaleCollision")
                    {
                        corrected++;
                        Assert.That(box.GetComponent<Renderer>(), Is.Null);
                        Assert.That(box.transform.localRotation, Is.EqualTo(Quaternion.identity));
                    }
                }
                Assert.That(corrected, Is.EqualTo(8));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
