using System.Reflection;
using Game.Client;
using Game.Client.Emotes;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// CharacterTest 프리뷰는 매 프레임 발 높이로 캐릭터를 바닥에 다시 붙인다.
    /// 감정 클립은 바닥 스핀처럼 발이 공중에 뜨는 구간이 있어 그 보정이
    /// 몸통을 땅속으로 밀어 넣으므로, 클립이 든 루트 높이를 그대로 써야 한다.
    /// </summary>
    public sealed class CharacterTestEmoteGroundingTests
    {
        private static bool TrustsClipHeight(string state)
        {
            var method = typeof(CharacterTestPreviewDriver).GetMethod(
                "TrustsClipHeight", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "CharacterTestPreviewDriver.TrustsClipHeight");
            return (bool)method.Invoke(null, new object[] { state });
        }

        [Test]
        public void EveryEmoteStateTrustsItsOwnHeight()
        {
            foreach (var emote in EmoteCatalog.All)
            {
                Assert.That(TrustsClipHeight(emote.StateName), Is.True, emote.StateName);
            }
        }

        [Test]
        public void FloorContactStatesStillTrustTheirHeight()
        {
            Assert.That(TrustsClipHeight("Prone_Idle"), Is.True);
            Assert.That(TrustsClipHeight("Crawl_Forward"), Is.True);
            Assert.That(TrustsClipHeight("Stun_Idle"), Is.True);
        }

        [Test]
        public void LocomotionStillGetsReplantedByFoot()
        {
            Assert.That(TrustsClipHeight("Idle"), Is.False);
            Assert.That(TrustsClipHeight("Walk_Forward"), Is.False);
            Assert.That(TrustsClipHeight("Punch"), Is.False);
        }
    }
}
