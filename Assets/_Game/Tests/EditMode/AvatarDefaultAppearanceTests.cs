using Game.Client.Character;
using Game.Core.Players;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// The default outfit is a design decision that lives in an asset, where a
    /// reorder or a cleared field changes it without touching any code.
    /// </summary>
    public sealed class AvatarDefaultAppearanceTests
    {
        private const string CatalogPath =
            "Assets/_Game/Content/Config/AvatarPartCatalog.asset";

        [Test]
        public void Catalog_DressesANewPlayerInTheAgreedDefault()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AvatarPartCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null, $"No catalogue at {CatalogPath}.");

            var appearance = catalog.Default;
            Assert.That(appearance.BodyColorId, Is.EqualTo("body_black"));
            Assert.That(appearance.HoodColorId, Is.EqualTo("hood_purple"));
            Assert.That(appearance.ShoesId, Is.EqualTo("shoes_pink_vivid"));
        }

        [Test]
        public void Catalog_DefaultIsSomethingToWearRatherThanNothing()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AvatarPartCatalog>(CatalogPath);

            // An empty appearance means "whatever the model was authored with",
            // so a default that compares equal to it dresses nobody.
            Assert.That(catalog.Default, Is.Not.EqualTo(AvatarAppearance.Default));
        }
    }
}
