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
    /// This calls <c>BuildLayout</c> itself, so the real fonts and art are what
    /// gets drawn.
    /// </para>
    /// </remarks>
    public static class HomeSuspendedNoticePreviewMenu
    {
        private const string MenuRoot = "Game/Home/";
        private const string PreviewName = "HomeSuspendedNoticePreview";
        private const string CanvasName = "HomeCanvas";

        [MenuItem(MenuRoot + "Preview Suspended Notice")]
        public static void Preview()
        {
            // In Play mode the real screen is already standing, and the notice
            // is the one thing it is missing.
            if (EditorApplication.isPlaying)
            {
                var live = Object.FindFirstObjectByType<HomeMenuView>(FindObjectsInactive.Include);
                if (live == null)
                {
                    Debug.LogWarning("[Home] 실행 중인 씬에 HomeMenuView 가 없습니다. Home 씬에서 Play 하세요.");
                    return;
                }

                live.SetSuspendedNoticeVisible(true);
                return;
            }

            if (!TryFindTemplate(out var template))
            {
                return;
            }

            Clear();

            // A throwaway copy rather than the scene's own view. BuildLayout is
            // written to run once, from Awake, on a component whose fields are
            // all still null; building a second time onto one that has already
            // been built walks into its leftovers - the run that found this out
            // died on friendButtonBadge, a field CreateFriendListRoot switches
            // off before CreateFriendButton has made it again.
            var host = new GameObject(PreviewName);
            host.transform.SetParent(null, false);
            var preview = host.AddComponent<HomeMenuView>();

            // Carries over the sprites and font assets wired onto the scene's
            // view, so this is the real screen rather than a grey mock-up.
            EditorUtility.CopySerialized(template, preview);

            var build = typeof(HomeMenuView).GetMethod(
                "BuildLayout", BindingFlags.Instance | BindingFlags.NonPublic);
            if (build == null)
            {
                Object.DestroyImmediate(host);
                Debug.LogError("[Home] HomeMenuView.BuildLayout 을 찾지 못했습니다.");
                return;
            }

            build.Invoke(preview, null);
            preview.SetSuspendedNoticeVisible(true);

            // DontSave, so the preview cannot end up inside Home.unity however
            // the scene is saved. It goes on its own when the scene reloads.
            MarkDontSave(host);
            MarkDontSave(InviteRootOf(preview));

            var notice = host.transform.Find($"{CanvasName}/SuspendedNotice");
            Selection.activeGameObject = notice != null ? notice.gameObject : host;

            Debug.Log($"[Home] 정지 안내 미리보기를 띄웠습니다. {MenuRoot}Clear Suspended Notice Preview 로 지웁니다.");
        }

        /// <summary>
        /// Takes the preview down. In Play mode it only hides the notice, since
        /// the screen it is over is the real one.
        /// </summary>
        [MenuItem(MenuRoot + "Clear Suspended Notice Preview")]
        public static void Clear()
        {
            if (EditorApplication.isPlaying)
            {
                var live = Object.FindFirstObjectByType<HomeMenuView>(FindObjectsInactive.Include);
                if (live != null)
                {
                    live.SetSuspendedNoticeVisible(false);
                }

                return;
            }

            foreach (var view in Object.FindObjectsByType<HomeMenuView>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (view.gameObject.name != PreviewName)
                {
                    // An earlier version of this tool built onto the scene's own
                    // view. Nothing else puts a canvas under it in edit mode -
                    // the screen is built from Awake - so a leftover is cleaned
                    // up here rather than left for someone to save by accident.
                    var stray = view.transform.Find(CanvasName);
                    if (stray != null)
                    {
                        Object.DestroyImmediate(stray.gameObject);
                    }

                    var strayInvites = InviteRootOf(view);
                    if (strayInvites != null)
                    {
                        Object.DestroyImmediate(strayInvites);
                    }

                    continue;
                }

                // The invite cards live on a scene root of their own, so they
                // are not a child of the view and do not go with it.
                var invites = InviteRootOf(view);
                if (invites != null)
                {
                    Object.DestroyImmediate(invites);
                }

                Object.DestroyImmediate(view.gameObject);
            }
        }

        /// <summary>
        /// The scene's own home view, which this reads the art and fonts off.
        /// Never a preview left over from a previous run.
        /// </summary>
        private static bool TryFindTemplate(out HomeMenuView template)
        {
            template = null;
            foreach (var view in Object.FindObjectsByType<HomeMenuView>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (view.gameObject.name == PreviewName)
                {
                    continue;
                }

                template = view;
                return true;
            }

            Debug.LogWarning(
                "[Home] 열려 있는 씬에 HomeMenuView 가 없습니다. " +
                "Assets/_Game/Content/Scenes/Home.unity 를 열고 실행하세요.");
            return false;
        }

        private static GameObject InviteRootOf(HomeMenuView view)
        {
            var field = typeof(HomeMenuView).GetField(
                "inviteRoot", BindingFlags.Instance | BindingFlags.NonPublic);
            return field?.GetValue(view) as GameObject;
        }

        /// <remarks>
        /// Every object, not just the root: Unity saves a scene object by
        /// object, and a kept child under a dropped parent is worse than either.
        /// </remarks>
        private static void MarkDontSave(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.hideFlags = HideFlags.DontSave;
            }
        }
    }
}
