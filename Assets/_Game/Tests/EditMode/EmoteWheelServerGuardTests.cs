using Game.Client.Emotes;
using NUnit.Framework;
using UnityEngine;

namespace Game.Architecture.Tests
{
    public sealed class EmoteWheelServerGuardTests
    {
        [Test]
        public void EnablingWheel_UsesBuildRole_AndNeverOpensOrBlocksInput()
        {
            var host = new GameObject("Emote wheel guard test");
            try
            {
                var wheel = host.AddComponent<EmoteWheelController>();
                // A scene or camera may attempt to enable it again.
                wheel.enabled = false;
                wheel.enabled = true;
                // EditMode does not drive lifecycle messages for ordinary behaviours.
                typeof(EmoteWheelController).GetMethod("OnEnable",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(wheel, null);
#if UNITY_SERVER
                Assert.That(wheel.enabled, Is.False);
#else
                Assert.That(wheel.enabled, Is.True);
#endif
                Assert.That(EmoteWheelController.IsOpen, Is.False);
                Assert.That(EmoteWheelController.BlocksLobbyShortcuts, Is.False);
            }
            finally { Object.DestroyImmediate(host); }
        }
    }
}
