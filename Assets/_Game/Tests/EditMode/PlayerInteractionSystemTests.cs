using Game.Core.Players;
using Game.Server.Players;
using Game.SOAP.Config;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class PlayerInteractionSystemTests
    {
        private MatchRulesSO rules;
        private PlayerInteractionSystem system;

        [SetUp]
        public void SetUp()
        {
            rules = ScriptableObject.CreateInstance<MatchRulesSO>();
            system = new PlayerInteractionSystem(rules);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(rules);
        }

        [Test]
        public void TryUseDestruction_AllowsFiveUsesPerPlayer()
        {
            for (var use = 0; use < 5; use++)
            {
                Assert.That(system.TryUseDestruction(0), Is.True);
            }

            Assert.That(system.TryUseDestruction(0), Is.False);
            Assert.That(system.GetRemainingDestructionUses(0), Is.Zero);
            Assert.That(system.GetRemainingDestructionUses(1), Is.EqualTo(5));
        }

        [Test]
        public void CustomDestructionLimit_IsUsedForTheMatch()
        {
            var configured = new PlayerInteractionSystem(rules, 2, 2);

            Assert.That(configured.TryUseDestruction(0), Is.True);
            Assert.That(configured.TryUseDestruction(0), Is.True);
            Assert.That(configured.TryUseDestruction(0), Is.False);
        }

        [Test]
        public void RegisterHit_StunsOnThirdHitForTwoSeconds()
        {
            Assert.That(system.RegisterHit(0, 10d), Is.EqualTo(HitResult.Registered));
            Assert.That(system.RegisterHit(0, 11d), Is.EqualTo(HitResult.Registered));
            Assert.That(system.RegisterHit(0, 12d), Is.EqualTo(HitResult.Stunned));

            Assert.That(system.GetHitCount(0), Is.Zero);
            Assert.That(system.IsStunned(0, 13.999d), Is.True);
            Assert.That(system.RegisterHit(0, 13d), Is.EqualTo(HitResult.Ignored));
            Assert.That(system.IsStunned(0, 14d), Is.False);
        }

        [Test]
        public void RegisterHit_UsesConfiguredHitCount()
        {
            var configured = new PlayerInteractionSystem(rules, 2, 5, 1);

            Assert.That(configured.RegisterHit(0, 10d), Is.EqualTo(HitResult.Stunned));
        }

        [Test]
        public void DefaultInvulnerability_DoesNotContinueAfterStun()
        {
            system.RegisterHit(0, 0d);
            system.RegisterHit(0, 0d);
            system.RegisterHit(0, 0d);

            Assert.That(system.IsInvulnerable(0, 2d), Is.False);
            Assert.That(system.RegisterHit(0, 2d), Is.EqualTo(HitResult.Registered));
        }
    }
}
