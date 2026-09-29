using Game.Client.Common;
using Game.Client.Match;
using Game.Client.Settings;
using Game.Core.Items;
using Game.Core.Settings;
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
        public void HudScale_Medium_UsesTheRaisedDefault()
        {
            Assert.That(InterfaceHudView.Scale(InterfaceCatalog.Medium), Is.EqualTo(1f));
            Assert.That(
                InterfaceHudView.HudScale(InterfaceCatalog.Medium),
                Is.EqualTo(HudScreenScale.DefaultScale));
            Assert.That(
                InterfaceHudView.HudScale(InterfaceCatalog.Large),
                Is.EqualTo(1.15f * HudScreenScale.DefaultScale).Within(0.0001f));
        }

        [Test]
        public void ChatListScale_FollowsUiAndFontSize()
        {
            var large = InterfaceHudView.Scale(InterfaceCatalog.Large);
            Assert.That(
                MatchChatView.ListScale(large, InterfaceHudView.Scale(InterfaceCatalog.Medium)),
                Is.EqualTo(large).Within(0.0001f));
            Assert.That(
                MatchChatView.ScaledItemSpacing(large, large),
                Is.EqualTo(MatchChatView.ItemSpacing * large * large).Within(0.0001f));
        }

        [Test]
        public void Category_SitsBelowDestroyedItemSlots()
        {
            Assert.That(DestroyedItemsHudView.CategoryFontSize, Is.EqualTo(36f));
            Assert.That(
                DestroyedItemsHudView.CategoryAnchoredPosition,
                Is.EqualTo(new UnityEngine.Vector2(
                    DestroyedItemsHudView.LeftPadding,
                    -(DestroyedItemsHudView.TopPadding
                        + DestroyedItemsHudView.SlotSize
                        + DestroyedItemsHudView.CategoryGap))));
            Assert.That(
                InterfaceHudView.CategoryFontSize,
                Is.EqualTo(DestroyedItemsHudView.CategoryFontSize));
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
