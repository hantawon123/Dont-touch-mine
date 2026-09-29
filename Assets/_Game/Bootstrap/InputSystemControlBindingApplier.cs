using System;
using Game.Core.Ports;
using Game.Core.Settings;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>
    /// Carries the keys chosen in the 컨트롤 tab into
    /// <c>InputSystem_Actions</c>.
    /// </summary>
    /// <remarks>
    /// Every consumer — <c>PlayerMovement</c>, <c>PlayerInteractor</c>,
    /// <c>PlayerCombatant</c>, <c>ItemPlacementController</c>,
    /// <c>PlayerCameraController</c>, <c>VoicePresenter</c> — holds the same
    /// asset and asks it for an action by name, so an override written here
    /// reaches all of them at once. None of them has to know that keys can
    /// move.
    /// <para>
    /// Overriding an action that is enabled is allowed and takes effect at
    /// once, so a key changed from the pause menu during a match is the key
    /// the next press uses.
    /// </para>
    /// </remarks>
    public sealed class InputSystemControlBindingApplier : IControlBindingApplier
    {
        private readonly InputActionAsset asset;

        public InputSystemControlBindingApplier(InputActionAsset asset)
        {
            this.asset = asset ?? throw new ArgumentNullException(nameof(asset));
        }

        /// <inheritdoc cref="IControlBindingApplier"/>
        public void Apply(ControlSettings settings)
        {
            // Everything, every time. See IControlBindingApplier: an override
            // lives on the asset rather than on the settings, and in the editor
            // it outlives play mode.
            asset.RemoveAllBindingOverrides();

            foreach (ControlAction action in Enum.GetValues(typeof(ControlAction)))
            {
                if (!ControlBindingMap.TryTarget(action, out var target))
                {
                    continue;
                }

                var bound = asset.FindAction(
                    $"{ControlBindingMap.PlayerMap}/{target.ActionName}");
                if (bound == null)
                {
                    Debug.LogWarning(
                        $"[Controls] {action} has no {target} in the input asset.");
                    continue;
                }

                var index = FindBindingIndex(bound, target);
                if (index < 0)
                {
                    Debug.LogWarning(
                        $"[Controls] {target} has no {ControlBindingMap.KeyboardMouseGroup} binding.");
                    continue;
                }

                // An empty path is how the Input System is told a binding is
                // off, which is what an action with no key means.
                bound.ApplyBindingOverride(
                    index,
                    new InputBinding
                    {
                        overridePath = ControlBindingMap.PathOf(settings.Get(action))
                    });
            }
        }

        /// <summary>
        /// Which of an action's bindings the screen writes to, or -1.
        /// </summary>
        /// <remarks>
        /// Public because it is the contract between the 컨트롤 rows and
        /// <c>InputSystem_Actions</c>, and <c>ControlBindingContractTests</c>
        /// checks it against the real asset. Renaming an action there, or
        /// dropping its keyboard binding, should fail a test rather than
        /// quietly stop moving a key.
        /// <para>
        /// Found rather than counted, because a binding's position is the
        /// input asset's business and reordering one there must not quietly
        /// start overwriting a different device's key. The first match wins,
        /// which is what makes 앞으로 이동 the letter and not the arrow key
        /// beside it.
        /// </para>
        /// </remarks>
        public static int FindBindingIndex(InputAction action, ControlBindingTarget target)
        {
            var bindings = action.bindings;
            for (var index = 0; index < bindings.Count; index++)
            {
                var binding = bindings[index];
                if (binding.isComposite)
                {
                    continue;
                }

                if (target.IsCompositePart)
                {
                    if (!binding.isPartOfComposite
                        || !string.Equals(
                            binding.name, target.PartName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }
                else if (binding.isPartOfComposite)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(binding.groups)
                    || binding.groups.IndexOf(
                        ControlBindingMap.KeyboardMouseGroup, StringComparison.Ordinal) < 0)
                {
                    continue;
                }

                return index;
            }

            return -1;
        }
    }

    /// <summary>
    /// Keeps the input asset saying what the 컨트롤 tab says, for as long as
    /// there is a scene that plays.
    /// </summary>
    /// <remarks>
    /// Registered where the input asset is — the lobby and the playground —
    /// rather than in the project scope, which has no reference to it. That is
    /// also where it matters: the bindings describe what happens in a match,
    /// and a key chosen on the home screen is carried in by
    /// <see cref="Start"/> when the player arrives.
    /// </remarks>
    public sealed class ControlBindingBridge : IStartable, IDisposable
    {
        private readonly ControlSettingsSystem controls;
        private readonly IControlBindingApplier applier;

        public ControlBindingBridge(
            ControlSettingsSystem controls, IControlBindingApplier applier)
        {
            this.controls = controls ?? throw new ArgumentNullException(nameof(controls));
            this.applier = applier ?? throw new ArgumentNullException(nameof(applier));
        }

        public void Start()
        {
            controls.Changed += OnChanged;
            applier.Apply(controls.Current);
        }

        public void Dispose() => controls.Changed -= OnChanged;

        private void OnChanged(ControlSettings settings) => applier.Apply(settings);
    }
}
