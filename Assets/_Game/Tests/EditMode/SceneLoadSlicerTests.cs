using Game.Client.Common;
using NUnit.Framework;
using UnityEngine;

namespace Game.Architecture.Tests
{
    public sealed class SceneLoadSlicerTests
    {
        [Test]
        public void IsReadyToActivate_WaitsUntilTheUnityGate()
        {
            Assert.That(SceneLoadSlicer.IsReadyToActivate(0.89f), Is.False);
            Assert.That(SceneLoadSlicer.IsReadyToActivate(SceneLoadSlicer.ActivationGate), Is.True);
            Assert.That(SceneLoadSlicer.IsReadyToActivate(1f), Is.True);
        }

        [Test]
        public void BackgroundIntegration_UsesTheSmallestFrameBudget()
        {
            Assert.That(SceneLoadSlicer.BackgroundPriority, Is.EqualTo(ThreadPriority.Low));
        }
    }
}
