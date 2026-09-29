using Game.Bootstrap;
using Game.Core.Settings;
using NUnit.Framework;

namespace Game.Architecture.Tests
{
    public sealed class UnityMicrophoneTestTests
    {
        [Test]
        public void ResolveDevice_UsesTheMachineDefaultWhenAskedOrMissing()
        {
            Assert.That(UnityMicrophoneTest.ResolveDevice(SoundCatalog.DefaultDevice, new[] { "Headset" }), Is.Null);
            Assert.That(UnityMicrophoneTest.ResolveDevice("", new[] { "Headset" }), Is.Null);
            Assert.That(UnityMicrophoneTest.ResolveDevice(null, new[] { "Headset" }), Is.Null);
            Assert.That(UnityMicrophoneTest.ResolveDevice("Gone", new[] { "Headset" }), Is.Null);
            Assert.That(UnityMicrophoneTest.ResolveDevice("Headset", null), Is.Null);
        }

        [Test]
        public void ResolveDevice_KeepsANameTheMachineHas()
        {
            Assert.That(
                UnityMicrophoneTest.ResolveDevice("Headset", new[] { "Webcam", "Headset" }),
                Is.EqualTo("Headset"));
        }

        [Test]
        public void Stop_LeavesTheTestIdleWhenNothingWasStarted()
        {
            var test = new UnityMicrophoneTest();

            Assert.That(test.IsRunning, Is.False);
            Assert.DoesNotThrow(test.Stop);
            Assert.That(test.IsRunning, Is.False);
        }
    }
}
