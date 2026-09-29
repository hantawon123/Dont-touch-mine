using Game.Client.Common;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Drops the loading cover into the open scene so the layout can be judged
    /// before it is wired to room entry, match start, or lobby return.
    /// </summary>
    public static class LoadingViewPreviewMenu
    {
        private const string MenuPath = "Game/UI/Preview Loading Screen";

        [MenuItem(MenuPath)]
        public static void Preview()
        {
            var existing = Object.FindFirstObjectByType<LoadingView>(FindObjectsInactive.Include);
            if (existing != null)
            {
                existing.Show();
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            var view = LoadingView.Create(null);
            view.Show();
            Undo.RegisterCreatedObjectUndo(view.gameObject, "Preview Loading Screen");
            Selection.activeGameObject = view.gameObject;
        }
    }
}
