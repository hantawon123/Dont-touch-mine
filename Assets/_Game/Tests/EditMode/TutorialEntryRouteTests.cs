using Game.Client.Tutorial;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class TutorialEntryRouteTests
    {
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
