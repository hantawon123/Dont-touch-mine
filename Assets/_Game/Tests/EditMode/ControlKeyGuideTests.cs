using Game.Core.Lobby;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class ControlKeyGuideTests
    {
        [Test]
        public void Bindings_ExposeLobbyControlKeys()
        {
            Assert.That(ControlKeyGuide.Bindings, Is.Not.Empty);
            Assert.That(ControlKeyGuide.Bindings[0].Action, Is.EqualTo("이동"));
            Assert.That(ControlKeyGuide.Bindings[0].KeyLabel, Is.EqualTo("W A S D"));
            Assert.That(ControlKeyGuide.Bindings, Has.Some.Matches<ControlKeyBinding>(
                binding => binding.Action == "배치 모드 ON/OFF" &&
                           binding.KeyLabel == "마우스 우클릭"));
        }

        [Test]
        public void Binding_RejectsEmptyValues()
        {
            Assert.That(
                () => new ControlKeyBinding(" ", "W"),
                Throws.ArgumentException);
            Assert.That(
                () => new ControlKeyBinding("이동", " "),
                Throws.ArgumentException);
        }
    }
}
