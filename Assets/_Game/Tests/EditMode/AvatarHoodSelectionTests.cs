using Game.Core.Players;
using NUnit.Framework;

namespace Game.Architecture.Tests
{
    public class AvatarHoodSelectionTests
    {
        [Test]
        public void ShapeAndColor_CanChangeIndependently_AndRoundTripStoredId()
        {
            var value=new AvatarAppearance("body_lemon","hood_bear_ice","shoes_a","face_a");
            value=value.With(AvatarPartCategory.Hood,"hood_rabbit");
            Assert.That(value.HoodColorId,Is.EqualTo("hood_ice"));
            value=value.With(AvatarPartCategory.HoodColor,"hood_lavender");
            Assert.That(value.HoodShapeId,Is.EqualTo("hood_rabbit"));
            Assert.That(value.BodyColorId,Is.EqualTo("body_lemon"));
            Assert.That(value.ShoesId,Is.EqualTo("shoes_a"));
            Assert.That(value.FaceId,Is.EqualTo("face_a"));
            Assert.That(value.HoodId.Length,Is.LessThanOrEqualTo(32));
            var restored=new AvatarAppearance(value.BodyColorId,value.HoodId,value.ShoesId,value.FaceId);
            Assert.That(restored,Is.EqualTo(value));
            Assert.That(restored.Get(AvatarPartCategory.HoodColor),Is.EqualTo("hood_lavender"));
        }

        [TestCase("hood_pink")]
        [TestCase("hood_sky")]
        [TestCase("hood_cream")]
        public void LegacyColorOnlySave_UsesBearAndPreservesColor(string stored)
        {
            var old=new AvatarAppearance("body_coral",stored,"","");
            Assert.That(old.HoodShapeId,Is.EqualTo("hood_bear"));
            Assert.That(old.HoodColorId,Is.EqualTo(stored));
            var changed=old.With(AvatarPartCategory.Hood,"hood_cat");
            Assert.That(changed.HoodColorId,Is.EqualTo(stored));
        }

        [Test]
        public void ColorFirst_ChoosesDefaultHood_WithoutChangingBody()
        {
            var value=AvatarAppearance.Default.With(AvatarPartCategory.HoodColor,"hood_mint");
            Assert.That(value.HoodShapeId,Is.EqualTo("hood_bear"));
            Assert.That(value.HoodColorId,Is.EqualTo("hood_mint"));
            Assert.That(value.BodyColorId,Is.Empty);
        }
    }
}
