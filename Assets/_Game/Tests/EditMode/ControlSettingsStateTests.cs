using Game.Core.Settings;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class ControlSettingsStateTests
    {
        [Test]
        public void Defaults_MatchListedKeyboardAndMouseBindings()
        {
            var settings = new ControlSettingsState();

            Assert.That(settings.GetPath(ControlAction.MoveForward), Is.EqualTo("<Keyboard>/w"));
            Assert.That(settings.GetPath(ControlAction.MoveBack), Is.EqualTo("<Keyboard>/s"));
            Assert.That(settings.GetPath(ControlAction.MoveLeft), Is.EqualTo("<Keyboard>/a"));
            Assert.That(settings.GetPath(ControlAction.MoveRight), Is.EqualTo("<Keyboard>/d"));
            Assert.That(settings.GetPath(ControlAction.Pickup), Is.EqualTo("<Keyboard>/f"));
            Assert.That(settings.GetPath(ControlAction.Drop), Is.EqualTo("<Keyboard>/f"));
            Assert.That(settings.GetPath(ControlAction.InteractDevice), Is.EqualTo("<Keyboard>/f"));
            Assert.That(settings.GetPath(ControlAction.Throw), Is.EqualTo("<Mouse>/leftButton"));
            Assert.That(settings.GetPath(ControlAction.Attack), Is.EqualTo("<Mouse>/leftButton"));
            Assert.That(settings.GetPath(ControlAction.Place), Is.EqualTo("<Mouse>/rightButton"));
            Assert.That(settings.GetPath(ControlAction.RotateYawLeft), Is.EqualTo("<Keyboard>/q"));
            Assert.That(settings.GetPath(ControlAction.RotateYawRight), Is.EqualTo("<Keyboard>/e"));
            Assert.That(settings.GetPath(ControlAction.RotatePitch), Is.EqualTo("<Mouse>/scroll/y"));
            Assert.That(settings.GetPath(ControlAction.Jump), Is.EqualTo("<Keyboard>/space"));
            Assert.That(settings.GetPath(ControlAction.Sprint), Is.EqualTo("<Keyboard>/leftShift"));
            Assert.That(settings.GetPath(ControlAction.ToggleView), Is.EqualTo("<Keyboard>/v"));
            Assert.That(settings.GetPath(ControlAction.Crouch), Is.EqualTo("<Keyboard>/c"));
            Assert.That(settings.GetPath(ControlAction.Prone), Is.EqualTo("<Keyboard>/z"));
        }

        [Test]
        public void TrySetPath_DoesNotSyncShareGroupWhenOneChanges()
        {
            var settings = new ControlSettingsState();

            Assert.That(settings.TrySetPath(ControlAction.Pickup, "<Keyboard>/g", out var error), Is.True);
            Assert.That(error, Is.EqualTo(ControlSettingsError.None));
            Assert.That(settings.GetPath(ControlAction.Drop), Is.EqualTo("<Keyboard>/f"));
            Assert.That(settings.GetPath(ControlAction.InteractDevice), Is.EqualTo("<Keyboard>/f"));

            Assert.That(settings.TrySetPath(ControlAction.Attack, "<Keyboard>/r", out error), Is.True);
            Assert.That(settings.GetPath(ControlAction.Throw), Is.EqualTo("<Mouse>/leftButton"));
        }

        [Test]
        public void TrySetPath_RejectsEmptyAndUnknown()
        {
            var settings = new ControlSettingsState();

            Assert.That(settings.TrySetPath(ControlAction.Jump, " ", out var error), Is.False);
            Assert.That(error, Is.EqualTo(ControlSettingsError.InvalidPath));
            Assert.That(settings.GetPath(ControlAction.Jump), Is.EqualTo("<Keyboard>/space"));

            Assert.That(settings.TrySetPath((ControlAction)999, "<Keyboard>/x", out error), Is.False);
            Assert.That(error, Is.EqualTo(ControlSettingsError.UnknownAction));
        }

        [Test]
        public void TrySetPath_RejectsPathOwnedByAnotherAction()
        {
            var settings = new ControlSettingsState();

            Assert.That(settings.TrySetPath(ControlAction.Jump, "<Keyboard>/w", out var error), Is.False);
            Assert.That(error, Is.EqualTo(ControlSettingsError.DuplicatePath));
            Assert.That(settings.GetPath(ControlAction.Jump), Is.EqualTo("<Keyboard>/space"));
            Assert.That(settings.TryFindConflict(ControlAction.Jump, "<Keyboard>/w", out var occupiedBy), Is.True);
            Assert.That(occupiedBy, Is.EqualTo(ControlAction.MoveForward));
        }

        [Test]
        public void TrySetPath_AllowsDuplicatesInsideShareGroupOnly()
        {
            var settings = new ControlSettingsState();

            Assert.That(
                settings.TrySetPath(ControlAction.Jump, "<Keyboard>/space", out var error),
                Is.True);
            Assert.That(error, Is.EqualTo(ControlSettingsError.None));

            Assert.That(settings.TrySetPath(ControlAction.Drop, "<Keyboard>/t", out error), Is.True);
            Assert.That(settings.GetPath(ControlAction.Pickup), Is.EqualTo("<Keyboard>/f"));
            Assert.That(settings.GetPath(ControlAction.Drop), Is.EqualTo("<Keyboard>/t"));
            Assert.That(settings.GetPath(ControlAction.InteractDevice), Is.EqualTo("<Keyboard>/f"));

            Assert.That(settings.TrySetPath(ControlAction.Drop, "<Keyboard>/f", out error), Is.True);
            Assert.That(settings.GetPath(ControlAction.Pickup), Is.EqualTo("<Keyboard>/f"));
            Assert.That(settings.GetPath(ControlAction.Drop), Is.EqualTo("<Keyboard>/f"));

            Assert.That(settings.TrySetPath(ControlAction.Throw, "<Keyboard>/f", out error), Is.False);
            Assert.That(error, Is.EqualTo(ControlSettingsError.DuplicatePath));
        }
    }
}
