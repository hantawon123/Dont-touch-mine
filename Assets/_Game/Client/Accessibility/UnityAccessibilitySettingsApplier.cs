using System;
using Game.Core.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Client.Accessibility
{
    public sealed class UnityAccessibilitySettingsApplier : IAccessibilitySettingsApplier
    {
        public UnityAccessibilitySettingsApplier()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        public void Apply(AccessibilitySettingsState settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (!Application.isPlaying)
            {
                return;
            }

            BindExisting();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            BindExisting();
        }

        private static void BindExisting()
        {
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (var index = 0; index < canvases.Length; index++)
            {
                AccessibilityBindings.EnsureCanvas(canvases[index].gameObject);
            }

            var texts = UnityEngine.Object.FindObjectsByType<TMP_Text>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (var index = 0; index < texts.Length; index++)
            {
                AccessibilityBindings.EnsureText(texts[index]);
            }
        }
    }
}
