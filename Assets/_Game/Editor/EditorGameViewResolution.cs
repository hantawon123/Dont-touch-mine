using System;
using System.Reflection;
using Game.Bootstrap;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Puts the Game view on the same resolution the 그래픽 tab chose, so Play
    /// Mode is not a free-aspect size the overlay pass cannot match.
    /// </summary>
    [InitializeOnLoad]
    internal static class EditorGameViewResolution
    {
        private const BindingFlags Hidden =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        static EditorGameViewResolution()
        {
            UnityGraphicsSettingsApplier.ApplyEditorGameView = Apply;
        }

        internal static void Apply(int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                return;
            }

            var editor = typeof(UnityEditor.Editor).Assembly;
            var gameViewType = editor.GetType("UnityEditor.GameView");
            if (gameViewType == null)
            {
                return;
            }

            var gameView = FindGameView(gameViewType);
            if (gameView == null)
            {
                return;
            }

            var index = FindOrAddFixedSize(editor, width, height);
            if (index < 0)
            {
                return;
            }

            var selected = gameViewType.GetProperty("selectedSizeIndex", Hidden);
            if (selected == null)
            {
                return;
            }

            if (!Equals(selected.GetValue(gameView, null), index))
            {
                selected.SetValue(gameView, index, null);
            }

            var snapZoom = gameViewType.GetMethod("SnapZoom", Hidden, null, new[] { typeof(float) }, null);
            snapZoom?.Invoke(gameView, new object[] { 1f });
            gameView.Repaint();
        }

        private static EditorWindow FindGameView(Type gameViewType)
        {
            var open = Resources.FindObjectsOfTypeAll(gameViewType);
            for (var index = 0; index < open.Length; index++)
            {
                if (open[index] is EditorWindow window)
                {
                    return window;
                }
            }

            return null;
        }

        private static int FindOrAddFixedSize(Assembly editor, int width, int height)
        {
            var sizesType = editor.GetType("UnityEditor.GameViewSizes");
            if (sizesType == null)
            {
                return -1;
            }

            var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var instance = singleton.GetProperty("instance")?.GetValue(null, null);
            if (instance == null)
            {
                return -1;
            }

            var groupType = sizesType.GetProperty("currentGroupType")?.GetValue(instance, null);
            var group = sizesType.GetMethod("GetGroup")?.Invoke(instance, new[] { groupType });
            if (group == null)
            {
                return -1;
            }

            var groupClass = group.GetType();
            var total = groupClass.GetMethod("GetTotalCount");
            var getSize = groupClass.GetMethod("GetGameViewSize");
            if (total == null || getSize == null)
            {
                return -1;
            }

            var count = (int)total.Invoke(group, null);
            for (var index = 0; index < count; index++)
            {
                var size = getSize.Invoke(group, new object[] { index });
                if (IsFixedSize(size, width, height))
                {
                    return index;
                }
            }

            if (!TryAddFixedSize(editor, groupClass, group, width, height))
            {
                return -1;
            }

            count = (int)total.Invoke(group, null);
            return count - 1;
        }

        private static bool IsFixedSize(object size, int width, int height)
        {
            if (size == null)
            {
                return false;
            }

            var type = size.GetType();
            var sizeType = type.GetProperty("sizeType")?.GetValue(size, null);
            var w = type.GetProperty("width")?.GetValue(size, null);
            var h = type.GetProperty("height")?.GetValue(size, null);
            return sizeType != null
                   && string.Equals(sizeType.ToString(), "FixedResolution", StringComparison.Ordinal)
                   && Equals(w, width)
                   && Equals(h, height);
        }

        private static bool TryAddFixedSize(
            Assembly editor, Type groupClass, object group, int width, int height)
        {
            var sizeTypeEnum = editor.GetType("UnityEditor.GameViewSizeType");
            var sizeClass = editor.GetType("UnityEditor.GameViewSize");
            var add = groupClass.GetMethod("AddCustomSize");
            if (sizeTypeEnum == null || sizeClass == null || add == null)
            {
                return false;
            }

            var ctor = sizeClass.GetConstructor(
                new[] { sizeTypeEnum, typeof(int), typeof(int), typeof(string) });
            if (ctor == null)
            {
                return false;
            }

            var fixedResolution = Enum.Parse(sizeTypeEnum, "FixedResolution");
            var size = ctor.Invoke(new[] { fixedResolution, width, height, $"{width}x{height}" });
            add.Invoke(group, new[] { size });
            return true;
        }
    }
}
