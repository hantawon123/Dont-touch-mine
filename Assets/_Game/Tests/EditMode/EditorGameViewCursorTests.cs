using Game.Bootstrap;
using NUnit.Framework;
using UnityEditor;

namespace Game.Architecture.Tests
{
    public sealed class EditorGameViewCursorTests
    {
        [Test]
        public void InstalledEditor_ExposesNativeGameViewCapturePermission()
        {
            // The bridge uses a non-public Editor API. Fail explicitly after an
            // incompatible Unity upgrade instead of silently losing ESC capture.
            Assert.That(EditorGameViewCursor.GameViewType, Is.Not.Null);
            Assert.That(typeof(EditorWindow).IsAssignableFrom(EditorGameViewCursor.GameViewType), Is.True);
            Assert.That(EditorGameViewCursor.AllowCursorLockAndHide, Is.Not.Null);
            Assert.That(EditorGameViewCursor.AllowCursorLockAndHide.ReturnType, Is.EqualTo(typeof(void)));
        }

        [Test]
        public void InstalledEditor_ExposesGameViewNoCameraWarningToggle()
        {
            Assert.That(EditorGameViewNoCameraWarning.Field, Is.Not.Null);
            Assert.That(EditorGameViewNoCameraWarning.Field.FieldType, Is.EqualTo(typeof(bool)));
            EditorGameViewNoCameraWarning.Suppress();
            var windows = UnityEngine.Resources.FindObjectsOfTypeAll(EditorGameViewCursor.GameViewType);
            for (var index = 0; index < windows.Length; index++)
            {
                if (windows[index] is not EditorWindow window)
                {
                    continue;
                }

                var serialized = new SerializedObject(window);
                var property = serialized.FindProperty(EditorGameViewNoCameraWarning.FieldName);
                if (property != null)
                {
                    Assert.That(property.boolValue, Is.False);
                    continue;
                }

                Assert.That(EditorGameViewNoCameraWarning.Field.GetValue(window), Is.False);
            }
        }
    }
}
