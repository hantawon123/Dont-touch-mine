using Game.Network.Session;
using NUnit.Framework;

namespace Game.Architecture.Tests
{
    public sealed class ExpectedFusionEntryLogHandlerTests
    {
        private const string MissingRoom =
            "<color=#73ACE5>[Fusion]</color> StartGame Failed: [StartGameResult: Ok:False, " +
            "ShutdownReason: GameNotFound, ErrorMessage=Game does not exist (ErrorCode: 32758), " +
            "StackTrace=  at Fusion.NetworkRunner.StartGameModeCloud (Fusion.StartGameArgs args) [0x00576] in <766720df767f43c6b2e311e65a63a557>:0 ]";

        [Test]
        public void MissingRoom_WithFusionColourTag_IsExpected()
        {
            // Debug.LogError(object) reaches the handler as a "{0}" format.
            Assert.That(
                ExpectedFusionEntryLogHandler.IsExpectedEntryFailure("{0}", new object[] { MissingRoom }),
                Is.True,
                "A mistyped room code must not pause the editor.");
        }

        [Test]
        public void MissingRoom_WithoutTags_IsExpected()
        {
            var plain = MissingRoom.Replace("<color=#73ACE5>", "").Replace("</color>", "");
            Assert.That(ExpectedFusionEntryLogHandler.IsExpectedEntryFailure(plain, null), Is.True);
        }

        [TestCase("GameIsFull")]
        [TestCase("ConnectionRefused")]
        [TestCase("CustomAuthenticationFailed")]
        public void OtherRefusedEntries_AreExpected(string reason)
        {
            var message = MissingRoom.Replace("GameNotFound", reason);
            Assert.That(ExpectedFusionEntryLogHandler.IsExpectedEntryFailure(message, null), Is.True);
        }

        [Test]
        public void UnexpectedShutdown_StaysAnError()
        {
            var message = MissingRoom.Replace("GameNotFound", "ConnectionTimeout");
            Assert.That(ExpectedFusionEntryLogHandler.IsExpectedEntryFailure(message, null), Is.False);
        }

        [Test]
        public void UnrelatedError_StaysAnError()
        {
            Assert.That(
                ExpectedFusionEntryLogHandler.IsExpectedEntryFailure(
                    "<color=#73ACE5>[Fusion]</color> ShutdownReason: GameNotFound reported elsewhere", null),
                Is.False);
        }
    }
}
