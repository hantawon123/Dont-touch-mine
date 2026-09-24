using Game.Core.Match;
using Game.Core.Players;
using Game.Core.Rooms;
using NUnit.Framework;
using System;

namespace Game.Tests.EditMode
{
    public sealed class BotParticipantTests
    {
        [Test]
        public void Profile_GivesBotANonHumanIdAndStableChoices()
        {
            var profile = new BotProfile(
                3,
                "  수색대장  ",
                BotDifficulty.Hard,
                8241);

            Assert.That(profile.PlayerId, Is.EqualTo("bot:3"));
            Assert.That(profile.Nickname, Is.EqualTo("수색대장"));
            Assert.That(profile.Difficulty, Is.EqualTo(BotDifficulty.Hard));
            Assert.That(profile.AppearanceSeed, Is.EqualTo(8241));
            Assert.That(BotProfile.IsBotPlayerId(profile.PlayerId), Is.True);
            Assert.That(BotProfile.IsBotPlayerId("P3"), Is.False);
        }

        [Test]
        public void Profile_UsesReadableFallbackName()
        {
            var profile = new BotProfile(2, "   ");

            Assert.That(profile.Nickname, Is.EqualTo("Bot 2"));
        }

        [Test]
        public void MatchNpcProfile_UsesASeparateIdentityNamespace()
        {
            var npc = new MatchNpcBotProfile(2, "  추적자  ", BotDifficulty.Hard);

            Assert.That(npc.NpcId, Is.EqualTo("npc:2"));
            Assert.That(npc.DisplayName, Is.EqualTo("추적자"));
            Assert.That(npc.Difficulty, Is.EqualTo(BotDifficulty.Hard));
            Assert.That(MatchNpcBotProfile.IsNpcId(npc.NpcId), Is.True);
            Assert.That(MatchNpcBotProfile.IsNpcId("bot:2"), Is.False,
                "Legacy participant bots and searching NPCs must not share an id namespace.");
            Assert.That(BotProfile.IsBotPlayerId(npc.NpcId), Is.False);
        }

        [Test]
        public void RoomLineUp_KeepsBotKindAndDropsBackendAccount()
        {
            var room = new[]
            {
                new RoomParticipant("P1", 0, true, "사람", "account-1"),
                new RoomParticipant(
                    "bot:1",
                    1,
                    false,
                    "봇",
                    "must-not-leak",
                    isBot: true),
            };

            var lineUp = MatchParticipant.FromRoomParticipants(room);

            Assert.That(lineUp[0].IsBot, Is.False);
            Assert.That(lineUp[0].UserId, Is.EqualTo("account-1"));
            Assert.That(lineUp[1].IsBot, Is.True);
            Assert.That(lineUp[1].UserId, Is.Null);
        }

        [Test]
        public void Shuffle_KeepsBotFlagWithItsParticipant()
        {
            var ids = new[] { "P1", "bot:1", "P2" };
            var users = new[] { "account-1", string.Empty, "account-2" };
            var bots = new[] { false, true, false };

            MatchParticipant.ShufflePlayOrder(ids, users, bots, new Random(42));

            var botIndex = Array.IndexOf(ids, "bot:1");
            Assert.That(botIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(bots[botIndex], Is.True);
            Assert.That(users[botIndex], Is.Empty);
        }

    }
}
