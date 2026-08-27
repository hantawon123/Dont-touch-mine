using System;
using System.Collections;
using Game.Core.Settings;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Client.Controls
{
    public sealed class UnityControlSettingsApplier : IControlSettingsApplier
    {
        private const string UnusedPath = "<Keyboard>/none";

        private readonly InputActionAsset inputActions;
        private ControlSettingsState lastSettings;
        private InputActionRebindingExtensions.RebindingOperation operation;
        private ControlRebindHost host;
        private Coroutine delayRoutine;
        private bool suppressCancelCallback;

        public UnityControlSettingsApplier(InputActionAsset inputActions = null)
        {
            this.inputActions = inputActions;
        }

        public void Apply(ControlSettingsState settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (inputActions == null)
            {
                return;
            }

            lastSettings = settings;
            ApplySharedPaths(
                "Interact",
                settings.GetPath(ControlAction.Pickup),
                settings.GetPath(ControlAction.Drop),
                settings.GetPath(ControlAction.InteractDevice));
            ApplySharedPaths(
                "Attack",
                settings.GetPath(ControlAction.Throw),
                settings.GetPath(ControlAction.Attack));

            var actions = (ControlAction[])Enum.GetValues(typeof(ControlAction));
            for (var index = 0; index < actions.Length; index++)
            {
                var action = actions[index];
                if (IsSharedBinding(action))
                {
                    continue;
                }

                ApplyPath(action, settings.GetPath(action));
            }
        }

        public void StartRebind(
            ControlAction action,
            Action<string> completed,
            Action cancelled)
        {
            CancelRebind();
            if (inputActions == null || !Application.isPlaying)
            {
                cancelled?.Invoke();
                return;
            }

            EnsureHost();
            delayRoutine = host.StartCoroutine(StartAfterClick(action, completed, cancelled));
        }

        public void CancelRebind()
        {
            if (host != null && delayRoutine != null)
            {
                host.StopCoroutine(delayRoutine);
                delayRoutine = null;
            }

            DisposeOperation(invokeCancelled: false);
        }

        private IEnumerator StartAfterClick(
            ControlAction action,
            Action<string> completed,
            Action cancelled)
        {
            yield return null;
            yield return null;
            delayRoutine = null;
            BeginRebind(action, completed, cancelled);
        }

        private void BeginRebind(
            ControlAction action,
            Action<string> completed,
            Action cancelled)
        {
            var inputAction = FindAction(action);
            var bindingIndex = FindBindingIndex(inputAction, action);
            if (inputAction == null || bindingIndex < 0)
            {
                cancelled?.Invoke();
                return;
            }

            var wasEnabled = inputAction.enabled;
            if (wasEnabled)
            {
                inputAction.Disable();
            }

            suppressCancelCallback = false;
            operation = inputAction.PerformInteractiveRebinding(bindingIndex)
                .WithCancelingThrough("<Keyboard>/escape")
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/delta")
                .WithControlsExcluding("<Pointer>/position")
                .WithControlsExcluding("<Pointer>/delta")
                .OnComplete(op =>
                {
                    var path = inputAction.bindings[bindingIndex].effectivePath;
                    RestoreEnabled(inputAction, wasEnabled);
                    DisposeOperation(invokeCancelled: false);
                    completed?.Invoke(path);
                })
                .OnCancel(op =>
                {
                    RestoreEnabled(inputAction, wasEnabled);
                    var invoke = !suppressCancelCallback;
                    DisposeOperation(invokeCancelled: false);
                    if (invoke)
                    {
                        cancelled?.Invoke();
                    }
                });

            if (action == ControlAction.RotatePitch)
            {
                operation.WithExpectedControlType("Axis");
            }

            operation.Start();
        }

        private static void RestoreEnabled(InputAction inputAction, bool wasEnabled)
        {
            if (wasEnabled && inputAction != null)
            {
                inputAction.Enable();
            }
        }

        private void DisposeOperation(bool invokeCancelled)
        {
            if (operation == null)
            {
                return;
            }

            suppressCancelCallback = !invokeCancelled;
            var current = operation;
            operation = null;
            current.Dispose();
        }

        private void ApplyPath(ControlAction action, string path)
        {
            var inputAction = FindAction(action);
            var bindingIndex = FindBindingIndex(inputAction, action);
            if (inputAction == null || bindingIndex < 0 || string.IsNullOrEmpty(path))
            {
                return;
            }

            inputAction.ApplyBindingOverride(bindingIndex, path);
        }

        private void ApplySharedPaths(string actionName, params string[] paths)
        {
            var map = inputActions.FindActionMap("Player");
            var inputAction = map?.FindAction(actionName);
            if (inputAction == null)
            {
                return;
            }

            var unique = new string[paths.Length];
            var uniqueCount = 0;
            for (var index = 0; index < paths.Length; index++)
            {
                var path = paths[index];
                if (string.IsNullOrEmpty(path) || AlreadyHasPath(unique, uniqueCount, path))
                {
                    continue;
                }

                unique[uniqueCount++] = path;
            }

            var slots = new int[8];
            var slotCount = 0;
            var bindings = inputAction.bindings;
            for (var index = 0; index < bindings.Count && slotCount < slots.Length; index++)
            {
                if (IsManagedBinding(bindings[index]))
                {
                    slots[slotCount++] = index;
                }
            }

            for (var index = 0; index < uniqueCount && index < slotCount; index++)
            {
                inputAction.ApplyBindingOverride(slots[index], unique[index]);
            }

            for (var index = uniqueCount; index < slotCount; index++)
            {
                inputAction.ApplyBindingOverride(slots[index], UnusedPath);
            }
        }

        private static bool AlreadyHasPath(string[] unique, int uniqueCount, string path)
        {
            var normalized = path.Trim().ToLowerInvariant();
            for (var index = 0; index < uniqueCount; index++)
            {
                if (string.Equals(unique[index].Trim().ToLowerInvariant(), normalized, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private InputAction FindAction(ControlAction action)
        {
            if (inputActions == null)
            {
                return null;
            }

            var map = inputActions.FindActionMap("Player");
            if (map == null)
            {
                return null;
            }

            switch (action)
            {
                case ControlAction.MoveForward:
                case ControlAction.MoveBack:
                case ControlAction.MoveLeft:
                case ControlAction.MoveRight:
                    return map.FindAction("Move");
                case ControlAction.Pickup:
                case ControlAction.Drop:
                case ControlAction.InteractDevice:
                    return map.FindAction("Interact");
                case ControlAction.Throw:
                case ControlAction.Attack:
                    return map.FindAction("Attack");
                case ControlAction.Place:
                    return map.FindAction("PlacementMode");
                case ControlAction.RotateYawLeft:
                case ControlAction.RotateYawRight:
                    return map.FindAction("RotateObject");
                case ControlAction.RotatePitch:
                    return map.FindAction("AdjustHeight");
                case ControlAction.Jump:
                    return map.FindAction("Jump");
                case ControlAction.Sprint:
                    return map.FindAction("Sprint");
                case ControlAction.ToggleView:
                    return map.FindAction("ToggleView");
                case ControlAction.Crouch:
                    return map.FindAction("Crouch");
                case ControlAction.Prone:
                    return map.FindAction("Prone");
                default:
                    return null;
            }
        }

        private int FindBindingIndex(InputAction inputAction, ControlAction action)
        {
            if (inputAction == null)
            {
                return -1;
            }

            var bindings = inputAction.bindings;
            for (var index = 0; index < bindings.Count; index++)
            {
                var binding = bindings[index];
                if (binding.isComposite)
                {
                    continue;
                }

                switch (action)
                {
                    case ControlAction.MoveForward:
                        if (IsCompositePart(binding, "up") && IsKeyboard(binding) && !IsArrow(binding))
                        {
                            return index;
                        }

                        break;
                    case ControlAction.MoveBack:
                        if (IsCompositePart(binding, "down") && IsKeyboard(binding) && !IsArrow(binding))
                        {
                            return index;
                        }

                        break;
                    case ControlAction.MoveLeft:
                        if (IsCompositePart(binding, "left") && IsKeyboard(binding) && !IsArrow(binding))
                        {
                            return index;
                        }

                        break;
                    case ControlAction.MoveRight:
                        if (IsCompositePart(binding, "right") && IsKeyboard(binding) && !IsArrow(binding))
                        {
                            return index;
                        }

                        break;
                    case ControlAction.RotateYawLeft:
                        if (IsCompositePart(binding, "negative"))
                        {
                            return index;
                        }

                        break;
                    case ControlAction.RotateYawRight:
                        if (IsCompositePart(binding, "positive"))
                        {
                            return index;
                        }

                        break;
                    case ControlAction.RotatePitch:
                        if (!binding.isPartOfComposite && Contains(binding.path, "scroll"))
                        {
                            return index;
                        }

                        break;
                    case ControlAction.Pickup:
                    case ControlAction.Drop:
                    case ControlAction.InteractDevice:
                    case ControlAction.Throw:
                    case ControlAction.Attack:
                        if (IsManagedBinding(binding) &&
                            MatchesPath(binding, lastSettings?.GetPath(action)))
                        {
                            return index;
                        }

                        break;
                    case ControlAction.Place:
                        if (!binding.isPartOfComposite && IsMouse(binding))
                        {
                            return index;
                        }

                        break;
                    default:
                        if (!binding.isPartOfComposite && IsKeyboard(binding))
                        {
                            return index;
                        }

                        break;
                }
            }

            if (IsSharedBinding(action))
            {
                for (var index = 0; index < bindings.Count; index++)
                {
                    if (IsManagedBinding(bindings[index]))
                    {
                        return index;
                    }
                }
            }

            return -1;
        }

        private static bool IsSharedBinding(ControlAction action)
        {
            var group = ControlSettingsState.ShareGroup(action);
            return group == ControlAction.Pickup || group == ControlAction.Throw;
        }

        private static bool IsManagedBinding(InputBinding binding)
        {
            if (binding.isComposite || binding.isPartOfComposite)
            {
                return false;
            }

            return IsKeyboard(binding) || IsMouse(binding);
        }

        private static bool MatchesPath(InputBinding binding, string expectedPath)
        {
            if (string.IsNullOrEmpty(expectedPath))
            {
                return false;
            }

            return PathsEqual(binding.effectivePath, expectedPath) ||
                PathsEqual(binding.overridePath, expectedPath) ||
                PathsEqual(binding.path, expectedPath);
        }

        private static bool PathsEqual(string left, string right)
        {
            return !string.IsNullOrEmpty(left) &&
                string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCompositePart(InputBinding binding, string partName)
        {
            return binding.isPartOfComposite &&
                string.Equals(binding.name, partName, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsKeyboard(InputBinding binding)
        {
            return Contains(binding.effectivePath, "<Keyboard>") || Contains(binding.path, "<Keyboard>");
        }

        private static bool IsMouse(InputBinding binding)
        {
            return Contains(binding.effectivePath, "<Mouse>") || Contains(binding.path, "<Mouse>");
        }

        private static bool IsArrow(InputBinding binding)
        {
            return Contains(binding.path, "Arrow");
        }

        private static bool Contains(string value, string token)
        {
            return !string.IsNullOrEmpty(value) &&
                value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void EnsureHost()
        {
            if (host != null)
            {
                return;
            }

            var hostObject = new GameObject("ControlRebindHost");
            UnityEngine.Object.DontDestroyOnLoad(hostObject);
            host = hostObject.AddComponent<ControlRebindHost>();
        }
    }
}
