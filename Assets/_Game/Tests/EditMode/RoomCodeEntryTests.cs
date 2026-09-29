using Game.Core.Rooms;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// The rules the six-cell code entry is built on: which characters a code
    /// can hold, and when six of them count as one.
    /// </summary>
    /// <remarks>
    /// The panel raises a code only once it is complete, and the screen refuses
    /// anything that is not well formed before it reaches the session. Both
    /// decisions read these, so a change here changes what can be typed.
    /// </remarks>
    public sealed class RoomCodeEntryTests
    {
        [Test]
        public void Code_IsSixCharacters()
        {
            Assert.That(RoomCodeFormat.CodeLength, Is.EqualTo(6));
        }

        /// <summary>
        /// A code is read aloud as often as it is copied, so the characters that
        /// sound or look alike are not issued at all.
        /// </summary>
        [TestCase('I')]
        [TestCase('L')]
        [TestCase('O')]
        [TestCase('U')]
        public void MisreadLetters_AreNotPartOfACode(char letter)
        {
            Assert.That(RoomCodeFormat.IsAllowed(letter), Is.False);
        }

        [TestCase('A')]
        [TestCase('Z')]
        [TestCase('0')]
        [TestCase('9')]
        public void LettersAndDigits_ArePartOfACode(char character)
        {
            Assert.That(RoomCodeFormat.IsAllowed(character), Is.True);
        }

        /// <summary>
        /// Lower case is raised rather than refused. A code read out over voice
        /// chat and typed in lower case is the same code.
        /// </summary>
        [Test]
        public void TypedCode_IsRaisedToTheCaseCodesAreIssuedIn()
        {
            Assert.That(RoomCodeFormat.Normalize("ab12cd"), Is.EqualTo("AB12CD"));
        }

        [Test]
        public void PastedCode_LosesItsSurroundingSpaces()
        {
            Assert.That(RoomCodeFormat.Normalize("  AB12CD  "), Is.EqualTo("AB12CD"));
        }

        [Test]
        public void CompleteCode_IsWellFormed()
        {
            Assert.That(RoomCodeFormat.IsWellFormed("AB12CD"), Is.True);
        }

        [TestCase("AB12C", Description = "다섯 자리")]
        [TestCase("AB12CD7", Description = "일곱 자리")]
        [TestCase("", Description = "빈 값")]
        [TestCase(null, Description = "없음")]
        public void CodeOfTheWrongLength_IsNotWellFormed(string code)
        {
            Assert.That(RoomCodeFormat.IsWellFormed(code), Is.False);
        }

        /// <summary>
        /// Reported as a malformed code rather than as a room that does not
        /// exist, which are different things to tell a player.
        /// </summary>
        [Test]
        public void CodeHoldingAMisreadLetter_IsNotWellFormed()
        {
            Assert.That(RoomCodeFormat.IsWellFormed("ABI2CD"), Is.False);
        }

        [Test]
        public void LowerCaseCode_IsOnlyWellFormedOnceNormalized()
        {
            Assert.That(RoomCodeFormat.IsWellFormed("ab12cd"), Is.False);
            Assert.That(
                RoomCodeFormat.IsWellFormed(RoomCodeFormat.Normalize("ab12cd")),
                Is.True);
        }
    }
}
