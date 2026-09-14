using Game.Client.Players;
using Game.Client.Common;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using NUnit.Framework;
using UnityEngine;

namespace Game.Architecture.Tests
{
    public sealed class PlayerMovementInputTests : InputTestFixture
    {
        [Test]
        public void MenuHandoff_CancelsHeldInputWithoutKeyUp_AndAcceptsFreshPress()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.AddDevice<Mouse>();
            using var move = new InputAction(type: InputActionType.Value);
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            using var sprint = new InputAction(binding: "<Keyboard>/leftShift", type: InputActionType.Button);
            using var attack = new InputAction(binding: "<Mouse>/leftButton", type: InputActionType.Button);
            try
            {
                move.Enable();
                sprint.Enable();
                attack.Enable();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.LeftShift));
                InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(123, 456) }.WithButton(MouseButton.Left));
                InputSystem.Update();
                Assert.That(move.ReadValue<Vector2>(), Is.EqualTo(Vector2.up));
                Assert.That(sprint.IsPressed() && attack.IsPressed(), Is.True);

                // Open the menu. No key-up event reaches Unity while it owns focus.
                WebPointerInput.DiscardHeldButtons();
                Assert.That(move.ReadValue<Vector2>(), Is.EqualTo(Vector2.zero));
                Assert.That(sprint.IsPressed() || attack.IsPressed(), Is.False);
                Assert.That(mouse.position.ReadValue(), Is.EqualTo(new Vector2(123, 456)));

                // A repeat/press during the menu must not leak through closing it.
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                InputSystem.Update();
                WebPointerInput.DiscardHeldButtons();
                InputSystem.Update();
                Assert.That(move.ReadValue<Vector2>(), Is.EqualTo(Vector2.zero));

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                InputSystem.Update();
                Assert.That(move.ReadValue<Vector2>(), Is.EqualTo(Vector2.up));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                Assert.That(move.ReadValue<Vector2>(), Is.EqualTo(Vector2.zero));
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.RemoveDevice(mouse);
            }
        }

        [Test]
        public void ShouldIgnoreAttackInput_WhenCursorIsUnlocked()
        {
            var previous = Cursor.lockState;
            try
            {
                Cursor.lockState = CursorLockMode.None;
                Assert.That(PlayerMovement.ShouldIgnoreAttackInput(), Is.True);
            }
            finally
            {
                Cursor.lockState = previous;
            }
        }

        [Test]
        public void ShouldIgnoreAttackInput_WhenCursorIsLockedAndNoEventSystem()
        {
            var previous = Cursor.lockState;
            try
            {
                Cursor.lockState = CursorLockMode.Locked;
                if (Cursor.lockState != CursorLockMode.Locked)
                {
                    Assert.Ignore("This Editor session cannot capture the cursor (for example, -nographics).");
                }
                if (UnityEngine.EventSystems.EventSystem.current != null)
                {
                    Assert.Ignore("This test requires a scene without an EventSystem.");
                }

                Assert.That(PlayerMovement.ShouldIgnoreAttackInput(), Is.False);
            }
            finally
            {
                Cursor.lockState = previous;
            }
        }
    }
}
