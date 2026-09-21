using System;
using Game.Core.Match;
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
        public void LobbySprint_RemainsFullAndResumesDrainWhenMatchStarts()
        {
            var stamina = PlayerStaminaRules.Step(0f, true, true, 1f, Settings, unlimited: true);
            for (var second = 0; second < 600; second++)
                stamina = PlayerStaminaRules.Step(stamina.Value, stamina.IsExhausted, true, 1f, Settings, unlimited: true);

            Assert.That(stamina.Value, Is.EqualTo(Settings.MaxStamina));
            Assert.That(stamina.CanSprint, Is.True);
            var inGame = PlayerStaminaRules.Step(stamina.Value, stamina.IsExhausted, true, 1f, Settings);
            Assert.That(inGame.Value, Is.EqualTo(80f));
        }

        [Test]
        public void Sprint_DrainsToZeroAndUnlocksAfterAnyRecovery()
        {
            var depleted = PlayerStaminaRules.Step(10f, false, true, 1f, Settings);

            Assert.That(depleted.Value, Is.Zero);
            Assert.That(depleted.IsExhausted, Is.True);
            Assert.That(depleted.CanSprint, Is.False);

            var partial = PlayerStaminaRules.Step(
                depleted.Value,
                depleted.IsExhausted,
                true,
                0.1f,
                Settings);
            Assert.That(partial.Value, Is.EqualTo(1f));
            Assert.That(partial.IsExhausted, Is.False);
            Assert.That(partial.CanSprint, Is.True);
        }

        [Test]
        public void Rest_RecoversWithoutExceedingMaximum()
        {
            var recovered = PlayerStaminaRules.Step(95f, false, false, 1f, Settings);

            Assert.That(recovered.Value, Is.EqualTo(100f));
            Assert.That(recovered.IsExhausted, Is.False);
        }

        [TestCase(MatchPhase.Waiting, true, TestName = "Unlimited_InLobbyAndWaitingRoom")]
        [TestCase(MatchPhase.Result, true, TestName = "Unlimited_OnEndingStage")]
        [TestCase(MatchPhase.Highlight, true, TestName = "Unlimited_DuringHighlight")]
        [TestCase(MatchPhase.Hiding, true, TestName = "Unlimited_WhileHiding")]
        [TestCase(MatchPhase.Searching, false, TestName = "Limited_WhileSearching")]
        public void StaminaOnlyDrainsDuringSearching(MatchPhase phase, bool unlimited)
        {
            Assert.That(PlayerStaminaRules.IsUnlimitedInPhase(phase), Is.EqualTo(unlimited));
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
