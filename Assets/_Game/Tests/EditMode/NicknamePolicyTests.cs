using Game.Core.Home;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// The nickname rule, which the server holds the other copy of.
    /// </summary>
    /// <remarks>
    /// These cases are written against the server's
    /// <c>NicknamePolicy.REGEX</c> — <c>^[가-힣a-zA-Z0-9]{2,12}$</c> — so a
    /// change on one side that the other has not followed shows up here rather
    /// than as a rejected request the player has to read.
    /// </remarks>
    public sealed class NicknamePolicyTests
    {
        [TestCase("금오산냥냥이")]
        [TestCase("ab")]
        [TestCase("Player01")]
        [TestCase("이건바로열두글자이지렁롱")]
        public void Valid_AcceptsHangulLatinAndDigits(string nickname)
        {
            Assert.That(NicknamePolicy.IsValid(nickname), Is.True);
        }

        [TestCase("가", Description = "한 글자")]
        [TestCase("", Description = "빈 문자열")]
        [TestCase(null, Description = "없음")]
        [TestCase("가 나", Description = "공백")]
        [TestCase("가!", Description = "특수문자")]
        [TestCase("ㅋㅋ", Description = "자모만")]
        [TestCase("가나ㅏ", Description = "자모가 섞임")]
        [TestCase("이건바로열두글자이지렁롱롱", Description = "열세 글자")]
        public void Valid_RejectsWhatTheServerRejects(string nickname)
        {
            Assert.That(NicknamePolicy.IsValid(nickname), Is.False);
        }

        [Test]
        public void Valid_RejectsThirteenCharacters()
        {
            Assert.That(NicknamePolicy.IsValid(new string('가', 12)), Is.True);
            Assert.That(NicknamePolicy.IsValid(new string('가', 13)), Is.False);
        }

        [Test]
        public void Typing_LetsJamoThroughSoAKoreanKeyboardCanCompose()
        {
            Assert.That(NicknamePolicy.IsAllowedWhileTyping('ㅋ'), Is.True);
            Assert.That(NicknamePolicy.IsAllowed('ㅋ'), Is.False);
        }

        [Test]
        public void Filter_DropsWhatMayNotBeTypedAndSaysSo()
        {
            var accepted = NicknamePolicy.Filter("가 나!", out var badCharacter, out var tooLong);

            Assert.That(accepted, Is.EqualTo("가나"));
            Assert.That(badCharacter, Is.True);
            Assert.That(tooLong, Is.False);
        }

        [Test]
        public void Filter_KeepsTheFirstTwelveAndSaysItRanOver()
        {
            var accepted = NicknamePolicy.Filter(
                new string('가', 15), out var badCharacter, out var tooLong);

            Assert.That(accepted.Length, Is.EqualTo(NicknamePolicy.MaxLength));
            Assert.That(tooLong, Is.True);
            Assert.That(badCharacter, Is.False);
        }

        [Test]
        public void Filter_LeavesAGoodNameAlone()
        {
            var accepted = NicknamePolicy.Filter(
                "금오산냥냥이", out var badCharacter, out var tooLong);

            Assert.That(accepted, Is.EqualTo("금오산냥냥이"));
            Assert.That(badCharacter, Is.False);
            Assert.That(tooLong, Is.False);
        }

        [Test]
        public void Filter_HandlesNothingTyped()
        {
            Assert.That(
                NicknamePolicy.Filter(null, out _, out _), Is.EqualTo(string.Empty));
            Assert.That(
                NicknamePolicy.Filter(string.Empty, out _, out _), Is.EqualTo(string.Empty));
        }
    }
}
