using Game.Core.Settings;
using NUnit.Framework;

namespace Game.Architecture.Tests
{
    public sealed class CameraLookScaleTests
    {
        [TestCase(true)]
        [TestCase(false)]
        public void Defaults_PreserveExistingTurnSpeed(bool firstPerson)
        {
            Assert.That(CameraLookScale.From(ControlCatalog.Defaults, firstPerson), Is.EqualTo((1f, 1f)));
        }

        [Test]
        public void ViewsAndAxes_AreIndependent()
        {
            var settings = ControlCatalog.Defaults
                .With(ControlSensitivity.FirstPersonMouse, 25)
                .With(ControlSensitivity.ThirdPersonMouse, 100)
                .With(ControlSensitivity.ThirdPersonCamera, 25)
                .With(ControlToggle.FirstPersonInvertX, InterfaceCatalog.On)
                .With(ControlToggle.ThirdPersonInvertY, InterfaceCatalog.On);
            Assert.That(CameraLookScale.From(settings, true), Is.EqualTo((-.5f, .5f)));
            Assert.That(CameraLookScale.From(settings, false), Is.EqualTo((1f, -1f)));
        }

        [TestCase(0, 0f)]
        [TestCase(100, 2f)]
        public void SliderEndpoints_AffectTurnSpeed(int percent, float expected)
        {
            var settings = ControlCatalog.Defaults.With(ControlSensitivity.FirstPersonMouse, percent);
            Assert.That(CameraLookScale.From(settings, true).x, Is.EqualTo(expected));
        }
    }
}
