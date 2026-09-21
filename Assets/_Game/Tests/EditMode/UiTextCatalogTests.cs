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
        }
    }
}
