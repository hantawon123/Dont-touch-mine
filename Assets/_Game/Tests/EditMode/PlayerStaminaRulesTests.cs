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
        public void FinalPeriod_OfSearching_SprintsWithoutDraining()
        {
            const double window = 30d;

            Assert.That(
                PlayerStaminaRules.IsUnlimited(MatchPhase.Searching, 31d, window),
                Is.False,
                "A second before the window opens the search phase still drains.");
            Assert.That(
                PlayerStaminaRules.IsUnlimited(MatchPhase.Searching, 30d, window),
                Is.True,
                "The window opens exactly at the final warning mark.");
            Assert.That(
                PlayerStaminaRules.IsUnlimited(MatchPhase.Searching, 0.1d, window),
                Is.True);

            var drained = PlayerStaminaRules.Step(
                40f, false, true, 1f, Settings,
                PlayerStaminaRules.IsUnlimited(MatchPhase.Searching, 31d, window));
            var free = PlayerStaminaRules.Step(
                40f, false, true, 1f, Settings,
                PlayerStaminaRules.IsUnlimited(MatchPhase.Searching, 29d, window));

            Assert.That(drained.Value, Is.EqualTo(20f));
            Assert.That(free.Value, Is.EqualTo(Settings.MaxStamina));
            Assert.That(free.CanSprint, Is.True);
        }

        [Test]
        public void FinalPeriod_EndsWithThePhaseAndNeverAppliesToOtherPhases()
        {
            const double window = 30d;

            // 남은 시간이 0 이하면 이미 페이즈가 끝난 뒤라 마지막 구간이 아니다.
            Assert.That(PlayerStaminaRules.IsFinalSprintPeriod(MatchPhase.Searching, 0d, window), Is.False);
            Assert.That(PlayerStaminaRules.IsFinalSprintPeriod(MatchPhase.Searching, -5d, window), Is.False);

            // 구간 길이를 0으로 꺼 두면 마지막 구간 자체가 없다.
            Assert.That(PlayerStaminaRules.IsFinalSprintPeriod(MatchPhase.Searching, 5d, 0d), Is.False);
            Assert.That(PlayerStaminaRules.IsUnlimited(MatchPhase.Searching, 5d, 0d), Is.False);

            // 숨기기처럼 원래 무제한인 페이즈는 남은 시간과 무관하게 무제한이다.
            Assert.That(PlayerStaminaRules.IsFinalSprintPeriod(MatchPhase.Hiding, 5d, window), Is.False);
            Assert.That(PlayerStaminaRules.IsUnlimited(MatchPhase.Hiding, 500d, window), Is.True);
        }

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
