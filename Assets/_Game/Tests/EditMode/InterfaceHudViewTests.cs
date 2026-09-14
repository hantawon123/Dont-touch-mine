using Game.Client.Settings;
using Game.Core.Items;
using Game.SOAP.Config;
using NUnit.Framework;

namespace Game.Architecture.Tests
{
    public sealed class InterfaceHudViewTests
    {
        [Test]
        public void FormatCounters_KeepsPingBelowFps()
        {
            Assert.That(
                InterfaceHudView.FormatCounters("60 FPS", "24 ms"),
                Is.EqualTo("60 FPS\n24 ms"));
            Assert.That(InterfaceHudView.FormatCounters("", "24 ms"), Is.EqualTo("24 ms"));
        }

        [Test]
        public void CategoryTopOffset_SitsBelowPingWhenCountersAreVisible()
        {
            Assert.That(InterfaceHudView.CategoryFontSize, Is.EqualTo(24f));
            Assert.That(
                InterfaceHudView.CategoryTopOffset(2, 1f),
                Is.GreaterThan(InterfaceHudView.CategoryTopOffset(1, 1f)));
            Assert.That(
                InterfaceHudView.PerformanceLineCount("60 FPS", "24 ms"),
                Is.EqualTo(2));
        }

        [Test]
        public void LabelFor_PrefersAssignedItemCategoryOverRoomSetting()
        {
            ItemCatalogSO.Load();
            var assigned = ItemCatalog.Definitions[0];
            Assert.That(
                MatchCategoryHud.LabelFor(assigned.ItemId, "missing_category"),
                Is.EqualTo(Game.Client.Lobby.PlaySettingsCategoryCatalog.LabelOf(assigned.Category)));
            Assert.That(
                MatchCategoryHud.LabelFor(null, assigned.Category),
                Is.EqualTo(Game.Client.Lobby.PlaySettingsCategoryCatalog.LabelOf(assigned.Category)));
        }
    }
}
