#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Game.Bootstrap
{
    /// <summary>
    /// Turns off Game view's "Display 1 / No cameras rendering" overlay.
    /// Home and Room cameras drop before Lobby's come up, and that overlay is
    /// drawn on top of the loading cover. Loading already hides the gap.
    /// </summary>
    [InitializeOnLoad]
    internal static class EditorGameViewNoCameraWarning
    {
        internal const string FieldName = "m_NoCameraWarning";

        internal static readonly FieldInfo Field = typeof(EditorWindow).Assembly
            .GetType("UnityEditor.GameView")
            ?.GetField(FieldName, BindingFlags.Instance | BindingFlags.NonPublic);

        static EditorGameViewNoCameraWarning()
        {
            EditorApplication.delayCall += Suppress;
            EditorApplication.playModeStateChanged += _ => Suppress();
        }

        internal static void Suppress()
        {
            var gameViewType = EditorGameViewCursor.GameViewType;
            if (gameViewType == null)
            {
                return;
            }

            var windows = Resources.FindObjectsOfTypeAll(gameViewType);
            for (var index = 0; index < windows.Length; index++)
            {
                if (windows[index] is EditorWindow window)
                {
                    Suppress(window);
                }
            }
        }

        private static void Suppress(EditorWindow window)
        {
            var serialized = new SerializedObject(window);
            var property = serialized.FindProperty(FieldName);
            if (property != null)
            {
                if (property.boolValue)
                {
                    property.boolValue = false;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    window.Repaint();
                }

                return;
            }

            if (Field == null || Field.GetValue(window) is not bool on || !on)
            {
                return;
            }

            Field.SetValue(window, false);
            window.Repaint();
        }
    }
}
#endif
