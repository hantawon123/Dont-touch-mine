using Game.Client.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Architecture.Tests
{
    public sealed class AvatarPreviewLightingTests
    {
        [Test]
        public void PortraitIgnoresWorldLightAndRestoresItAfterRendering()
        {
            var camera = new GameObject("Portrait", typeof(Camera));
            var world = new GameObject("World light", typeof(Light));
            var disabled = new GameObject("Disabled light", typeof(Light));
            var originalFog = RenderSettings.fog;
            var originalMode = RenderSettings.ambientMode;
            var originalProbe = RenderSettings.ambientProbe;
            var originalReflection = RenderSettings.reflectionIntensity;
            var originalSun = RenderSettings.sun;
            disabled.GetComponent<Light>().enabled = false;
            var lighting = camera.AddComponent<AvatarPreviewLighting>();
            try
            {
                lighting.ApplyLighting();
                Assert.That(world.GetComponent<Light>().enabled, Is.False);
                Assert.That(RenderSettings.sun.transform.IsChildOf(camera.transform), Is.True);
                Assert.That(RenderSettings.ambientMode, Is.EqualTo(AmbientMode.Flat));
                Assert.That(RenderSettings.reflectionIntensity, Is.Zero);
                lighting.RestoreLighting();
                Assert.That(world.GetComponent<Light>().enabled, Is.True);
                Assert.That(disabled.GetComponent<Light>().enabled, Is.False);
                Assert.That(RenderSettings.sun, Is.EqualTo(originalSun));
                Assert.That(RenderSettings.fog, Is.EqualTo(originalFog));
                Assert.That(RenderSettings.ambientMode, Is.EqualTo(originalMode));
                Assert.That(RenderSettings.reflectionIntensity, Is.EqualTo(originalReflection));
                for (var channel = 0; channel < 3; channel++)
                    for (var coefficient = 0; coefficient < 9; coefficient++)
                        Assert.That(RenderSettings.ambientProbe[channel, coefficient],
                            Is.EqualTo(originalProbe[channel, coefficient]).Within(.00001f));

                // Closing the panel during rendering must also restore the world.
                // EditMode does not run SendMessage, so the same callback is
                // invoked directly.
                lighting.ApplyLighting();
                lighting.OnDisable();
                Assert.That(world.GetComponent<Light>().enabled, Is.True);
            }
            finally
            {
                lighting.RestoreLighting();
                Object.DestroyImmediate(camera);
                Object.DestroyImmediate(world);
                Object.DestroyImmediate(disabled);
            }
        }
    }
}
