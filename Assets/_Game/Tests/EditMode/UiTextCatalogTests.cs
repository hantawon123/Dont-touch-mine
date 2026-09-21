using Game.Core.Settings;
using NUnit.Framework;

namespace Game.Architecture.Tests
{
    /// <summary>
    /// What looking up a line returns when the language is known, missing,
    /// or not yet translated.
    /// </summary>
    public sealed class UiTextCatalogTests
    {
        private static readonly UiTextCatalog TwoLines = new UiTextCatalog(
            new UiTextLine("greeting", "안녕하세요", "Hello"),
            new UiTextLine("farewell", "안녕히 가세요"));

        [Test]
        public void Get_InKorean_ReturnsTheKoreanWords()
        {
            Assert.That(TwoLines.Get("greeting", "ko"), Is.EqualTo("안녕하세요"));
            Assert.That(TwoLines.Get("farewell", "ko"), Is.EqualTo("안녕히 가세요"));
        }

        [Test]
        public void Get_InEnglish_ReturnsTheEnglishWords()
        {
            Assert.That(TwoLines.Get("greeting", "en"), Is.EqualTo("Hello"));
        }

        [Test]
        public void Get_InEnglish_WithoutATranslation_FallsBackToKorean()
        {
            Assert.That(TwoLines.Get("farewell", "en"), Is.EqualTo("안녕히 가세요"));
        }

        [Test]
        public void Get_InAnUnknownLanguage_FallsBackToKorean()
        {
            Assert.That(TwoLines.Get("greeting", "fr"), Is.EqualTo("안녕하세요"));
            Assert.That(TwoLines.Get("greeting", null), Is.EqualTo("안녕하세요"));
        }

        [Test]
        public void Get_WithAnUnknownKey_ReturnsTheKey()
        {
            Assert.That(TwoLines.Get("missing", "ko"), Is.EqualTo("missing"));
            Assert.That(TwoLines.Get("missing", "en"), Is.EqualTo("missing"));
        }

        [Test]
        public void ShippedCatalogue_OffersTheSettingsChrome()
        {
            var shipped = UiTextCatalog.Shipped;

            Assert.That(shipped.Get(UiText.Settings.Language, "ko"), Is.EqualTo("언어"));
            Assert.That(shipped.Get(UiText.Settings.Language, "en"), Is.EqualTo("Language"));
            Assert.That(shipped.Get(UiText.Settings.Apply, "ko"), Is.EqualTo("적용하기"));
            Assert.That(shipped.Get(UiText.Settings.Apply, "en"), Is.EqualTo("Apply"));
            Assert.That(shipped.Get(UiText.Settings.Reset, "en"), Is.EqualTo("Discard Changes"));
            Assert.That(shipped.Get(UiText.Settings.LeaveGame, "en"), Is.EqualTo("Leave Game"));
            Assert.That(shipped.Get(UiText.Settings.TabGeneral, "en"), Is.EqualTo("General"));
            Assert.That(shipped.Get(UiText.Settings.TabNotifications, "en"), Is.EqualTo("Notifications"));
            Assert.That(shipped.Get(UiText.Settings.Fullscreen, "en"), Is.EqualTo("Fullscreen"));
            Assert.That(shipped.Get(UiText.Settings.DisplayMode, "en"), Is.EqualTo("Display Mode"));
            Assert.That(shipped.Get(UiText.Settings.On, "ko"), Is.EqualTo("켜기"));
            Assert.That(shipped.Get(UiText.Settings.ChatOff, "en"), Is.EqualTo("Off (Everyone)"));
            Assert.That(shipped.Get(UiText.Settings.ResetAllTitle, "en"), Is.EqualTo("Discard all settings changes?"));
            Assert.That(shipped.Get(UiText.Settings.FeedbackSend, "en"), Is.EqualTo("Send Feedback"));
            Assert.That(shipped.Get(UiText.Settings.MicTestUnavailable, "en"),
                Is.EqualTo("This computer cannot open the microphone."));
            Assert.That(
                string.Format(shipped.Get(UiText.Settings.KeyInUseMessage, "en"), "F", "Jump"),
                Is.EqualTo("F is already used by Jump"));
            Assert.That(shipped.Get(UiText.Home.CreateRoom, "en"), Is.EqualTo("Create Room"));
            Assert.That(shipped.Get(UiText.Home.FindRoom, "en"), Is.EqualTo("Find Game"));
            Assert.That(shipped.Get(UiText.Home.Quit, "en"), Is.EqualTo("Quit"));
            Assert.That(shipped.Get(UiText.Home.Empty, "en"), Is.EqualTo("No friends yet"));
            Assert.That(shipped.Get(UiText.Home.SuspendedTitle, "en"), Is.EqualTo("This account is suspended"));
            Assert.That(shipped.Get(UiText.Play.Title, "en"), Is.EqualTo("Game Settings"));
            Assert.That(shipped.Get(UiText.Play.Random, "en"), Is.EqualTo("Random"));
            Assert.That(shipped.Get(UiText.Lobby.KickAction, "en"), Is.EqualTo("Kick"));
            Assert.That(shipped.Get(UiText.Rooms.NoRooms, "en"), Is.EqualTo("No rooms are open"));
            Assert.That(
                string.Format(shipped.Get(UiText.Lobby.KickTitle, "en"), "Guest"),
                Is.EqualTo("Kick Guest?"));
            Assert.That(shipped.Get(UiText.Match.ChatPlaceholder, "en"), Is.EqualTo("Press [Enter] to chat"));
            Assert.That(shipped.Get(UiText.Match.TimerHint, "en"), Is.EqualTo("Secure your item now!"));
            Assert.That(shipped.Get(UiText.Match.HighlightSkip, "en"), Is.EqualTo("Skip"));
            Assert.That(shipped.Get(UiText.Match.PhaseWaiting, "en"), Is.EqualTo("Waiting"));
            Assert.That(
                string.Format(shipped.Get(UiText.Match.Destroyed, "en"), "Mina"),
                Is.EqualTo("Mina destroyed an item!"));
        }
    }
}
