using Game.Client.Lobby;
using Game.Core.Items;

namespace Game.Client.Settings
{
    /// <summary>
    /// Resolves the in-match category label. Assignment wins over the room
    /// setting so a random pick still shows the category that was chosen.
    /// </summary>
    public static class MatchCategoryHud
    {
        public static string LabelFor(string assignedItemId, string configuredCategoryId)
        {
            var categoryId = ItemCatalog.CategoryOf(assignedItemId);
            if (string.IsNullOrEmpty(categoryId))
            {
                categoryId = configuredCategoryId;
            }

            return PlaySettingsCategoryCatalog.LabelOf(categoryId);
        }
    }
}
