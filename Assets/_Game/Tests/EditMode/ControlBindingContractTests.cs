using System;
using Game.Bootstrap;
using Game.Core.Settings;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Architecture.Tests
{
    /// <summary>
    /// That <c>InputSystem_Actions</c> keeps its side of what the 컨트롤 rows
    /// promise.
    /// </summary>
    /// <remarks>
    /// Against the real asset, because the failure this guards against is
    /// somebody renaming an action or dropping a binding there. Nothing else
    /// would notice: the applier logs and carries on, and the row simply stops
    /// moving its key.
    /// <para>
    /// Every test works on a copy. A binding override written to the project's
    /// own asset would outlive the test run and follow whoever opens the editor
    /// next.
    /// </para>
    /// </remarks>
    public sealed class ControlBindingContractTests
    {
        private const string AssetPath = "Assets/InputSystem_Actions.inputactions";

        private InputActionAsset asset;

        [SetUp]
        public void LoadACopy()
        {
            var original = AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetPath);
            Assert.That(original, Is.Not.Null, $"{AssetPath} is missing.");
            asset = UnityEngine.Object.Instantiate(original);
        }

        [TearDown]
        public void DropTheCopy()
        {
            if (asset != null)
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        /// <summary>
        /// Every row can find its binding, and what the asset binds today is
        /// what the screen says the row ships with.
        /// </summary>
        /// <remarks>
        /// The second half is what catches a default drifting away from the
        /// asset — the state 마이크 송출 was in, showing T while the game
        /// listened to G.
        /// </remarks>
        [Test]
        public void EveryRow_FindsItsBinding_AndTheShippedKeyIsWhatTheAssetBinds()
        {
            foreach (ControlAction action in Enum.GetValues(typeof(ControlAction)))
            {
                Assert.That(ControlBindingMap.TryTarget(action, out var target), Is.True);

                var bound = asset.FindAction($"{ControlBindingMap.PlayerMap}/{target.ActionName}");
                Assert.That(bound, Is.Not.Null, $"{action} wants {target}, which the asset has not got.");

                var index = InputSystemControlBindingApplier.FindBindingIndex(bound, target);
                Assert.That(
                    index,
                    Is.GreaterThanOrEqualTo(0),
                    $"{target} has no {ControlBindingMap.KeyboardMouseGroup} binding.");

                Assert.That(
                    bound.bindings[index].path,
                    Is.EqualTo(ControlBindingMap.PathOf(ControlCatalog.Defaults.Get(action))),
                    $"{action} ships on {ControlCatalog.Defaults.Get(action)} but the asset binds something else.");
            }
        }

        /// <summary>
        /// Applying moves the keyboard-and-mouse binding of each row and
        /// nothing else.
        /// </summary>
        [Test]
        public void Apply_PutsEveryRowsKeyOnItsOwnBinding()
        {
            // Keys nothing ships with, so a binding that did not move is
            // obvious rather than accidentally right, and all different, so a
            // binding that took its neighbour's key is obvious too.
            var spare = new[]
            {
                "h", "i", "j", "k", "m", "n", "o", "p", "r",
                "u", "x", "f1", "f2", "f3", "f4", "f5", "f6", "f7", "f8", "f9"
            };
            var rows = Enum.GetValues(typeof(ControlAction));
            Assert.That(
                spare.Length,
                Is.EqualTo(rows.Length),
                "A row was added or removed; this test needs one spare key for each.");

            var moved = ControlSettings.Empty;
            var at = 0;
            foreach (ControlAction action in rows)
            {
                moved = moved.With(action, spare[at++]);
            }

            new InputSystemControlBindingApplier(asset).Apply(moved);

            foreach (ControlAction action in Enum.GetValues(typeof(ControlAction)))
            {
                Assert.That(ControlBindingMap.TryTarget(action, out var target), Is.True);
                var bound = asset.FindAction($"{ControlBindingMap.PlayerMap}/{target.ActionName}");
                var index = InputSystemControlBindingApplier.FindBindingIndex(bound, target);

                Assert.That(
                    bound.bindings[index].effectivePath,
                    Is.EqualTo(ControlBindingMap.PathOf(moved.Get(action))),
                    $"{action} did not land on its binding.");
            }
        }

        /// <summary>
        /// The gamepad, touch, joystick and XR bindings are not the player's to
        /// move from this screen, and 공격 carries four of them.
        /// </summary>
        [Test]
        public void Apply_LeavesTheOtherDevicesAlone()
        {
            var attack = asset.FindAction($"{ControlBindingMap.PlayerMap}/Attack");
            Assert.That(attack, Is.Not.Null);

            var before = new string[attack.bindings.Count];
            for (var index = 0; index < before.Length; index++)
            {
                before[index] = attack.bindings[index].effectivePath;
            }

            ControlBindingMap.TryTarget(ControlAction.PrimaryAction, out var target);
            var moving = InputSystemControlBindingApplier.FindBindingIndex(attack, target);

            new InputSystemControlBindingApplier(asset)
                .Apply(ControlCatalog.Defaults.With(ControlAction.PrimaryAction, "h"));

            for (var index = 0; index < before.Length; index++)
            {
                if (index == moving)
                {
                    Assert.That(
                        attack.bindings[index].effectivePath, Is.EqualTo("<Keyboard>/h"));
                    continue;
                }

                Assert.That(
                    attack.bindings[index].effectivePath,
                    Is.EqualTo(before[index]),
                    $"Attack's binding {index} moved and should not have.");
            }
        }

        /// <summary>
        /// The two arrow keys beside WASD are a second way in that the screen
        /// does not show, so moving 앞으로 이동 must not take them with it.
        /// </summary>
        [Test]
        public void Apply_LeavesTheArrowKeysOnMove()
        {
            var move = asset.FindAction($"{ControlBindingMap.PlayerMap}/Move");

            new InputSystemControlBindingApplier(asset)
                .Apply(ControlCatalog.Defaults.With(ControlAction.MoveForward, "i"));

            var up = 0;
            foreach (var binding in move.bindings)
            {
                if (!binding.isPartOfComposite
                    || !string.Equals(binding.name, "up", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Assert.That(
                    binding.effectivePath,
                    Is.EqualTo(up == 0 ? "<Keyboard>/i" : "<Keyboard>/upArrow"),
                    up == 0 ? "The letter moved." : "The arrow key stayed.");
                up++;
            }

            Assert.That(up, Is.EqualTo(2), "Move's up part should have a letter and an arrow.");
        }

        /// <summary>
        /// A row with no key is a binding that is off, not one left where it
        /// was.
        /// </summary>
        [Test]
        public void Apply_TurnsOffARowWithNoKey()
        {
            new InputSystemControlBindingApplier(asset)
                .Apply(ControlCatalog.Defaults.With(ControlAction.Jump, ControlCatalog.Unbound));

            var jump = asset.FindAction($"{ControlBindingMap.PlayerMap}/Jump");
            ControlBindingMap.TryTarget(ControlAction.Jump, out var target);
            var index = InputSystemControlBindingApplier.FindBindingIndex(jump, target);

            Assert.That(jump.bindings[index].effectivePath, Is.Empty);
        }

        /// <summary>
        /// Applying twice leaves the asset saying what the second set says.
        /// An override outlives play mode in the editor, so a set carried over
        /// from before would be invisible until somebody could not rebind a
        /// key back.
        /// </summary>
        [Test]
        public void ApplyingAgain_LeavesNothingOfTheSetBefore()
        {
            var applier = new InputSystemControlBindingApplier(asset);
            applier.Apply(ControlCatalog.Defaults.With(ControlAction.Jump, "h"));
            applier.Apply(ControlCatalog.Defaults);

            var jump = asset.FindAction($"{ControlBindingMap.PlayerMap}/Jump");
            ControlBindingMap.TryTarget(ControlAction.Jump, out var target);
            var index = InputSystemControlBindingApplier.FindBindingIndex(jump, target);

            Assert.That(jump.bindings[index].effectivePath, Is.EqualTo("<Keyboard>/space"));
        }
    }
}
