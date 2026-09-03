using System;
using Game.Core.Players;
using Game.Server.Players;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class PlayerStaminaRulesTests
    {
        private static readonly PlayerMovementSettings Settings = new(
            4f,
            7f,
            720f,
            1.1f,
            2f,
            maxStamina: 100f,
            staminaDrainPerSecond: 20f,
            staminaRecoveryPerSecond: 10f);

        [Test]
        public void Sprint_DrainsToZeroAndLocksSprintUntilFullRecovery()
        {
            var depleted = PlayerStaminaRules.Step(10f, false, true, 1f, Settings);

            Assert.That(depleted.Value, Is.Zero);
            Assert.That(depleted.IsExhausted, Is.True);
            Assert.That(depleted.CanSprint, Is.False);

            var partial = PlayerStaminaRules.Step(
                depleted.Value,
                depleted.IsExhausted,
                true,
                9.9f,
                Settings);
            Assert.That(partial.Value, Is.EqualTo(99f));
            Assert.That(partial.CanSprint, Is.False);

            var recovered = PlayerStaminaRules.Step(
                partial.Value,
                partial.IsExhausted,
                true,
                0.1f,
                Settings);
            Assert.That(recovered.Value, Is.EqualTo(100f));
            Assert.That(recovered.CanSprint, Is.True);
        }

        [Test]
        public void Rest_RecoversWithoutExceedingMaximum()
        {
            var recovered = PlayerStaminaRules.Step(95f, false, false, 1f, Settings);

            Assert.That(recovered.Value, Is.EqualTo(100f));
            Assert.That(recovered.IsExhausted, Is.False);
        }

        [TestCase(-1f, 0f)]
        [TestCase(101f, 0f)]
        [TestCase(50f, -0.1f)]
        public void InvalidStateOrDelta_IsRejected(float current, float deltaTime)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                PlayerStaminaRules.Step(current, false, false, deltaTime, Settings));
        }
    }
}
