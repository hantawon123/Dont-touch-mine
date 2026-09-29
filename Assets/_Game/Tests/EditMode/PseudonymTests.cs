using System.Collections.Generic;
using Game.Core.Settings;
using NUnit.Framework;

namespace Game.Architecture.Tests
{
    public sealed class PseudonymTests
    {
        [Test]
        public void ASeed_AlwaysGivesTheSameName()
        {
            Assert.That(Pseudonym.From(12345), Is.EqualTo(Pseudonym.From(12345)));
        }

        /// <summary>
        /// The whole point (S15P21D205-1018): a pseudonym must look exactly like
        /// the nickname a new account is given, so the room cannot pick the
        /// streamer out. Same shape, same length ceiling as a real nickname.
        /// </summary>
        [Test]
        public void EveryName_LooksLikeADefaultNickname()
        {
            for (var seed = -1000; seed < 1000; seed++)
            {
                var name = Pseudonym.From(seed);
                Assert.That(name, Does.Match(Pseudonym.Shape), $"seed {seed}");
                Assert.That(name.Length, Is.LessThanOrEqualTo(12), $"seed {seed}");
            }
        }

        /// <summary>
        /// A hash is as likely to be negative as not, and a name that threw for
        /// half of them would be a crash in the one place privacy was asked for.
        /// </summary>
        [Test]
        public void ANegativeSeed_IsAName_LikeAnyOther()
        {
            Assert.That(Pseudonym.From(int.MinValue), Does.Match(Pseudonym.Shape));
            Assert.That(Pseudonym.From(-1), Does.Match(Pseudonym.Shape));
        }

        [Test]
        public void TheNames_SpreadAcrossTheWholeList()
        {
            var seen = new HashSet<string>();
            for (var seed = 0; seed < 2000; seed++)
            {
                seen.Add(Pseudonym.From(seed));
            }

            Assert.That(
                seen.Count,
                Is.GreaterThan(Pseudonym.NounCount * Pseudonym.AdjectiveCount * 5),
                "A generator that keeps landing on the same few names is one people notice.");
        }
    }
}
