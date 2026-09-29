using Game.Core.Players;
using NUnit.Framework;

namespace Game.Architecture.Tests
{
    public sealed class AvatarAppearanceTests
    {
        [Test]
        public void With_ChangesOneCategory_AndLeavesTheRest()
        {
            var worn = new AvatarAppearance("body_a", "hood_a", "shoes_a", "face_a");

            var changed = worn.With(AvatarPartCategory.Hood, "hood_b");

            Assert.That(changed.HoodId, Is.EqualTo("hood_b"));
            Assert.That(changed.BodyColorId, Is.EqualTo("body_a"));
            Assert.That(changed.ShoesId, Is.EqualTo("shoes_a"));
            Assert.That(changed.FaceId, Is.EqualTo("face_a"));
        }

        [Test]
        public void With_LeavesTheOriginalAlone()
        {
            var worn = new AvatarAppearance("body_a", "hood_a", "shoes_a", "face_a");

            worn.With(AvatarPartCategory.Hood, "hood_b");

            Assert.That(worn.HoodId, Is.EqualTo("hood_a"));
        }

        [Test]
        public void Get_AnswersForEveryCategory()
        {
            var worn = new AvatarAppearance("body_a", "hood_a", "shoes_a", "face_a");

            Assert.That(worn.Get(AvatarPartCategory.BodyColor), Is.EqualTo("body_a"));
            Assert.That(worn.Get(AvatarPartCategory.Hood), Is.EqualTo("hood_a"));
            Assert.That(worn.Get(AvatarPartCategory.Shoes), Is.EqualTo("shoes_a"));
            Assert.That(worn.Get(AvatarPartCategory.Face), Is.EqualTo("face_a"));
        }

        /// <summary>
        /// The comparison the apply button will be decided by, so a null out of
        /// a store and an empty string out of the screen have to agree.
        /// </summary>
        [Test]
        public void NullAndEmpty_AreTheSameThing()
        {
            var fromStore = new AvatarAppearance("body_a", null, null, null);
            var fromScreen = new AvatarAppearance("body_a", string.Empty, string.Empty, string.Empty);

            Assert.That(fromStore, Is.EqualTo(fromScreen));
            Assert.That(fromStore == fromScreen, Is.True);
            Assert.That(fromStore.GetHashCode(), Is.EqualTo(fromScreen.GetHashCode()));
        }

        [Test]
        public void Default_WearsNothing()
        {
            Assert.That(AvatarAppearance.Default.BodyColorId, Is.Empty);
            Assert.That(AvatarAppearance.Default.HoodId, Is.Empty);
        }

        [Test]
        public void ApplyingTheSameAppearance_TellsNobody()
        {
            var worn = new AvatarAppearance("body_a", "hood_a", string.Empty, string.Empty);
            var state = new AvatarAppearanceState();
            state.Apply(worn);
            var changes = 0;
            state.Changed += _ => changes++;

            state.Apply(worn);

            Assert.That(changes, Is.Zero);
        }

        [Test]
        public void ApplyingSomethingElse_SettlesOnIt()
        {
            var state = new AvatarAppearanceState();
            AvatarAppearance? announced = null;
            state.Changed += appearance => announced = appearance;

            var worn = new AvatarAppearance("body_a", string.Empty, string.Empty, string.Empty);
            state.Apply(worn);

            Assert.That(state.Current, Is.EqualTo(worn));
            Assert.That(announced, Is.EqualTo(worn));
        }
    }
}
