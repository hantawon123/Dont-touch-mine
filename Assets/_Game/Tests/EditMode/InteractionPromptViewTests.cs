using Game.Bootstrap;
using Game.Client.Home;
using Game.Client.Interactions;
using Game.Client.Match;
using Game.Core.Settings;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Architecture.Tests
{
    public sealed class InteractionPromptViewTests
    {
        [SetUp]
        public void UnbindSettings() => PlayerInteractor.UseSettings(null);

        [TearDown]
        public void TearDownSettings() => PlayerInteractor.UseSettings(null);

        [Test]
        public void Carryable_UsesPickupPrompt()
        {
            var itemObject = new GameObject("Item", typeof(Rigidbody));
            try
            {
                var item = itemObject.AddComponent<CarryableItem>();
                Assert.That(item.InteractionPrompt, Is.EqualTo("물건 잡기"));
            }
            finally
            {
                Object.DestroyImmediate(itemObject);
            }
        }

        [Test]
        public void Shredder_UsesDestroyPrompt()
        {
            var shredderObject = new GameObject("Shredder", typeof(BoxCollider));
            try
            {
                var shredder = shredderObject.AddComponent<ShredderInteractable>();
                Assert.That(shredder.InteractionPrompt, Is.EqualTo("파괴하기"));
            }
            finally
            {
                Object.DestroyImmediate(shredderObject);
            }
        }

        [Test]
        public void Prompt_UsesOpaqueKeyBoxAndSemibold18Label()
        {
            InteractionPromptView view = null;
            try
            {
                view = InteractionPromptView.Create();
                var follow = new GameObject("Follow");
                view.Show("F", "물건 잡기", follow.transform);

                Assert.That(view.IsVisible, Is.True);
                Assert.That(view.GetComponent<Canvas>().pixelPerfect, Is.False);
                Assert.That(view.KeyBox.color, Is.EqualTo(InteractionPromptView.KeyBoxColor));
                Assert.That(view.KeyBox.color.a, Is.EqualTo(0.27f));
                Assert.That(view.KeyLabel.gameObject.activeSelf, Is.True);
                Assert.That(view.KeyIcon.gameObject.activeSelf, Is.False);
                Assert.That(view.KeyLabel.text, Is.EqualTo("F"));
                Assert.That(
                    view.KeyBox.GetComponent<LayoutElement>().preferredWidth,
                    Is.EqualTo(HidingActiveHudView.MeasureKeyChipWidth(
                        view.KeyLabel.text,
                        view.KeyLabel.preferredWidth)));
                Assert.That(view.ActionLabel.text, Is.EqualTo("물건 잡기"));
                Assert.That(view.ActionLabel.color, Is.EqualTo(Color.white));
                Assert.That(view.ActionLabel.fontSize, Is.EqualTo(InteractionPromptView.LabelFontSize));
                Assert.That(view.ActionLabel.fontSize, Is.EqualTo(18f));
                Assert.That(view.ActionLabel.font, Is.EqualTo(HomeUiFonts.Apply()));
                Assert.That(view.ActionLabel.font.name, Does.Contain("Paperlogy").IgnoreCase);
                Assert.That(view.ActionLabel.font.name, Does.Contain("SemiBold").IgnoreCase);

                view.Show("F", "파괴하기", follow.transform);
                Assert.That(view.ActionLabel.text, Is.EqualTo("파괴하기"));
                Assert.That(view.ActionLabel.color, Is.EqualTo(Color.white));

                view.Show("F", "방 설정", follow.transform, actionColor: Color.black);
                Assert.That(view.ActionLabel.text, Is.EqualTo("방 설정"));
                Assert.That(view.ActionLabel.color, Is.EqualTo(Color.black));

                view.Show("F", "파괴하기", follow.transform);
                Assert.That(view.ActionLabel.color, Is.EqualTo(Color.white));

                var clickIcon = InteractionPromptView.LoadLeftClickIcon();
                view.Show(string.Empty, ItemPlacementController.PlaceActionLabel, follow.transform, clickIcon);
                Assert.That(view.KeyLabel.gameObject.activeSelf, Is.False);
                Assert.That(view.KeyIcon.gameObject.activeSelf, Is.True);
                Assert.That(view.KeyIcon.sprite, Is.EqualTo(clickIcon));
                Assert.That(view.KeyIcon.sprite.name, Does.Contain("left_click").IgnoreCase);
                Assert.That(view.ActionLabel.text, Is.EqualTo("배치"));
                Assert.That(view.ActionLabel.fontSize, Is.EqualTo(18f));
                Assert.That(view.KeyBox.color.a, Is.EqualTo(0.27f));
                Assert.That(
                    view.KeyBox.GetComponent<LayoutElement>().preferredWidth,
                    Is.EqualTo(InteractionPromptView.KeyBoxSize));

                Object.DestroyImmediate(follow);
            }
            finally
            {
                if (view != null)
                {
                    Object.DestroyImmediate(view.gameObject);
                }
            }
        }

        [Test]
        public void Prompt_ScalesWithCameraDistance()
        {
            Assert.That(
                InteractionPromptView.ScaleFromDistance(InteractionPromptView.ScaleReferenceDistance),
                Is.EqualTo(1f).Within(0.001f));
            Assert.That(
                InteractionPromptView.ScaleFromDistance(InteractionPromptView.ScaleReferenceDistance * 0.5f),
                Is.GreaterThan(1f));
            Assert.That(
                InteractionPromptView.ScaleFromDistance(InteractionPromptView.ScaleReferenceDistance * 2f),
                Is.EqualTo(1f));
            Assert.That(
                InteractionPromptView.ScaleFromDistance(0.01f),
                Is.EqualTo(InteractionPromptView.MaxDistanceScale));
            Assert.That(
                InteractionPromptView.ScaleFromDistance(100f),
                Is.EqualTo(1f));
        }

        [Test]
        public void Prompt_GrowsKeyBoxWidthToFitKeyLabel()
        {
            InteractionPromptView view = null;
            try
            {
                view = InteractionPromptView.Create();
                var follow = new GameObject("Follow");
                view.Show("F", "물건 잡기", follow.transform);

                var box = view.KeyBox.GetComponent<LayoutElement>();
                Assert.That(box.preferredHeight, Is.EqualTo(InteractionPromptView.KeyBoxSize));
                Assert.That(box.preferredWidth, Is.EqualTo(HidingActiveHudView.KeyChipWidth));

                view.Show("SPACE", "물건 잡기", follow.transform);
                Assert.That(
                    box.preferredWidth,
                    Is.EqualTo(HidingActiveHudView.MeasureKeyChipWidth(
                        view.KeyLabel.text,
                        view.KeyLabel.preferredWidth)));
                Assert.That(box.preferredWidth, Is.GreaterThan(HidingActiveHudView.KeyChipWidth));
                Assert.That(box.preferredHeight, Is.EqualTo(InteractionPromptView.KeyBoxSize));

                Object.DestroyImmediate(follow);
            }
            finally
            {
                if (view != null)
                {
                    Object.DestroyImmediate(view.gameObject);
                }
            }
        }

        [Test]
        public void WorldPrompt_StaysOffWhenTheCursorIsFree()
        {
            Assert.That(PlayerInteractor.CanShowWorldPrompt(true, true, true), Is.True);
            Assert.That(PlayerInteractor.CanShowWorldPrompt(true, true, false), Is.False);
            Assert.That(PlayerInteractor.CanShowWorldPrompt(true, false, true), Is.False);
            Assert.That(PlayerInteractor.CanShowWorldPrompt(false, true, true), Is.False);
        }

        [Test]
        public void Crosshair_StaysOffWhenAModalOwnsTheCursor()
        {
            Assert.That(PlayerInteractor.CanShowCrosshair(true, true, false), Is.True);
            Assert.That(PlayerInteractor.CanShowCrosshair(true, false, true), Is.False);
            Assert.That(PlayerInteractor.CanShowCrosshair(true, true, true), Is.False);
            Assert.That(PlayerInteractor.CanShowCrosshair(false, true, false), Is.False);
        }

        [Test]
        public void InteractKeyLabel_UsesShippedKeyWhenSettingsAreUnbound()
        {
            Assert.That(PlayerInteractor.InteractKeyLabel(), Is.EqualTo("F"));
        }

        [Test]
        public void InteractKeyLabel_FollowsAppliedControlBinding()
        {
            var system = new ControlSettingsSystem(new InMemoryControlSettingsStore());
            PlayerInteractor.UseSettings(system);

            Assert.That(PlayerInteractor.InteractKeyLabel(), Is.EqualTo("F"));

            system.Apply(system.Current.With(ControlAction.Interact, "k"));
            Assert.That(PlayerInteractor.InteractKeyLabel(), Is.EqualTo("K"));

            system.Apply(system.Current
                .With(ControlAction.PrimaryAction, ControlCatalog.Unbound)
                .With(ControlAction.Interact, ControlCatalog.MouseLeft));
            Assert.That(PlayerInteractor.InteractKeyLabel(), Is.EqualTo("좌클릭"));

            system.Apply(system.Current.With(ControlAction.Interact, ControlCatalog.Unbound));
            Assert.That(PlayerInteractor.InteractKeyLabel(), Is.EqualTo(ControlCatalog.UnboundLabel));
        }
    }
}
