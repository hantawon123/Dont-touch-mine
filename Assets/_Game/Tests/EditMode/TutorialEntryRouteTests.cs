using Game.Client.Tutorial;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class TutorialEntryRouteTests
    {
        [Test]
        public void TrainingBoxRenderersMustNotBeStaticBatched()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Content/Prefabs/Carryable/Basement_CardboardBox1 Carryable.prefab");
            foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                Assert.That(GameObjectUtility.GetStaticEditorFlags(renderer.gameObject) & StaticEditorFlags.BatchingStatic,
                    Is.EqualTo((StaticEditorFlags)0), renderer.name + " cannot move while statically batched.");
        }

        [Test]
        public void IncompleteVersionRoutesToTutorial()
        {
            var store = new FakeStore(completed: false);

            Assert.That(TutorialEntryRoute.Resolve("Home", store), Is.EqualTo("Tutorial"));
        }

        [Test]
        public void CompletedVersionKeepsConfiguredDestination()
        {
            var store = new FakeStore(completed: true);

            Assert.That(TutorialEntryRoute.Resolve("Home", store), Is.EqualTo("Home"));
        }

        private sealed class FakeStore : ITutorialCompletionStore
        {
            public FakeStore(bool completed) => IsCurrentVersionCompleted = completed;

            public bool IsCurrentVersionCompleted { get; }
            public void MarkCurrentVersionCompleted() { }
        }
    }
}
