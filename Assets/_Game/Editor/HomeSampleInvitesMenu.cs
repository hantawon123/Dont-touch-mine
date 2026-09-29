using System;
using Game.Client.Home;
using Game.Core.Home;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Puts the three invites the mock-up draws onto the home screen, so the
    /// cards can be looked at without a friend in a lobby to send them.
    /// </summary>
    /// <remarks>
    /// The real cards come from a server push, and nothing in the client can
    /// send an invite yet, so the one part of this screen worth judging by eye
    /// has no way to appear. This writes to the view directly, the way
    /// <c>RoomBrowserSampleRooms</c> does for rooms.
    /// <para>
    /// Play mode only: the layout is built in <c>Awake</c>. The cards' buttons
    /// raise ids the bridge has never seen, so pressing them does nothing; that
    /// is what the clear entry is for.
    /// </para>
    /// </remarks>
    public static class HomeSampleInvitesMenu
    {
        private const string MenuRoot = "Game/Home/";

        [MenuItem(MenuRoot + "Show Sample Invites")]
        public static void Show()
        {
            if (!TryFindView(out var view))
            {
                return;
            }

            view.SetRoomInvites(
                new[]
                {
                    new RoomInvite("sample-1", "sample-p1", "이건바로로열두글자라구", "SAMPL1"),
                    new RoomInvite("sample-2", "sample-p2", "짧은이름", "SAMPL2"),
                    new RoomInvite("sample-3", "sample-p3", "친구", "SAMPL3")
                });
        }

        [MenuItem(MenuRoot + "Clear Sample Invites")]
        public static void Clear()
        {
            if (TryFindView(out var view))
            {
                view.SetRoomInvites(Array.Empty<RoomInvite>());
            }
        }

        private static bool TryFindView(out HomeMenuView view)
        {
            view = null;
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("[Home] Home 씬을 Play 한 뒤 샘플 초대를 띄우세요. 카드는 Play 중에만 만들어집니다.");
                return false;
            }

            view = UnityEngine.Object.FindFirstObjectByType<HomeMenuView>();
            if (view == null)
            {
                Debug.LogWarning("[Home] 열려 있는 씬에 HomeMenuView 가 없습니다. Home 씬에서 실행하세요.");
                return false;
            }

            return true;
        }
    }
}
