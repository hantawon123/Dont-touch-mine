using System.Reflection;
using Game.Client.Home;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Puts the suspended-account notice on screen in the open Home scene, so
    /// the panel and its 게임 종료 버튼 can be looked at without a suspended
    /// account to sign in with.
    /// </summary>
    /// <remarks>
    /// The notice only ever appears when sign-in answers
    /// <c>BackendFailure.Suspended</c>, which needs the server to have stopped
    /// the account. Nothing in the client can put it up, so the one thing worth
    /// judging by eye has no way to appear.
    /// <para>
    /// Works without Play mode, which is the point: the whole home screen is
    /// built in code from <c>Awake</c>, and edit mode never runs <c>Awake</c>.
    /// This calls <c>BuildLayout</c> itself when the scene has not been played,
    /// so the real fonts and art wired onto the component are what gets drawn.
    /// </para>
    /// <para>
    /// What it builds is a preview, not scene content. Clear it — or undo —
    /// before saving, or the whole home canvas is saved into Home.unity.
    /// </para>
    /// </remarks>
    public static class HomeSuspendedNoticePreviewMenu
    {
        private const string MenuRoot = "Game/Home/";
        private const string CanvasName = "HomeCanvas";

        [MenuItem(MenuRoot + "Preview Suspended Notice")]
        public static void Preview()
        {
            if (!TryFindView(out var view))
            {
                return;
            }

            if (!EditorApplication.isPlaying && view.transform.Find(CanvasName) == null)
            {
                var build = typeof(HomeMenuView).GetMethod(
                    "BuildLayout", BindingFlags.Instance | BindingFlags.NonPublic);
                if (build == null)
                {
                    Debug.LogError("[Home] HomeMenuView.BuildLayout 을 찾지 못했습니다.");
                    return;
                }

                // Recorded before the build, so undo has the empty object to go
                // back to rather than the one with the canvas already on it.
                Undo.RegisterFullObjectHierarchyUndo(view.gameObject, "Preview Suspended Notice");
                build.Invoke(view, null);
                Debug.Log(
                    "[Home] 미리보기용으로 홈 화면을 그렸습니다. 씬을 저장하지 말고 " +
                    "Game/Home/Clear Suspended Notice Preview 로 지우세요.");
            }

            view.SetSuspendedNoticeVisible(true);

            var notice = view.transform.Find($"{CanvasName}/SuspendedNotice");
            if (notice != null)
            {
                Selection.activeGameObject = notice.gameObject;
            }
        }

        /// <summary>
        /// Takes the notice back down. In edit mode the whole preview canvas
        /// goes with it, because nothing else built it.
        /// </summary>
        [MenuItem(MenuRoot + "Clear Suspended Notice Preview")]
        public static void Clear()
        {
            if (!TryFindView(out var view))
            {
                return;
            }

            view.SetSuspendedNoticeVisible(false);

            if (EditorApplication.isPlaying)
            {
                return;
            }

            // The invite cards live on a scene root of their own, so they are
            // not a child of the view and do not go with the canvas.
            var inviteRoot = typeof(HomeMenuView).GetField(
                "inviteRoot", BindingFlags.Instance | BindingFlags.NonPublic);
            if (inviteRoot?.GetValue(view) is GameObject invites)
            {
                Object.DestroyImmediate(invites);
                inviteRoot.SetValue(view, null);
            }

            var canvas = view.transform.Find(CanvasName);
            if (canvas != null)
            {
                Object.DestroyImmediate(canvas.gameObject);
            }
        }

        private static bool TryFindView(out HomeMenuView view)
        {
            view = Object.FindFirstObjectByType<HomeMenuView>(FindObjectsInactive.Include);
            if (view == null)
            {
                Debug.LogWarning(
                    "[Home] 열려 있는 씬에 HomeMenuView 가 없습니다. " +
                    "Assets/_Game/Content/Scenes/Home.unity 를 열고 실행하세요.");
                return false;
            }

            return true;
        }
    }
}
