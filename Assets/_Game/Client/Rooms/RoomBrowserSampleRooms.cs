using System;
using System.Collections.Generic;
using Game.Core.Lobby;
using Game.Core.Rooms;
using R3;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Client.Rooms
{
    /// <summary>
    /// Fills the room list with made-up rooms so the screen can be looked at
    /// without a session behind it.
    /// </summary>
    /// <remarks>
    /// The list only has rows once matchmaking pushes some, which means the one
    /// part of this screen worth judging by eye — the rows — is invisible until
    /// a host, a client and a connection are all in place. That is a poor loop
    /// for laying out a row, so this pushes the rooms the mock-up draws straight
    /// into the view.
    /// <para>
    /// It prefers to write into <see cref="RoomBrowserSystem"/>, which is where
    /// the list actually comes from, so the presenter renders these the same way
    /// it renders real rooms and does not overwrite them a frame later with the
    /// empty list of a session that has not answered yet. A scene opened on its
    /// own has no session scope holding that system, and there it writes to the
    /// view directly.
    /// </para>
    /// <para>
    /// A real list replaces these the moment one arrives; the context menu puts
    /// them back.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RoomBrowserView))]
    public sealed class RoomBrowserSampleRooms : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Off for anything but looking at the screen. Leave it off in a "
            + "build: these rooms do not exist and cannot be entered.")]
        private bool showSampleRooms;

        private IDisposable refill;

        private void Start()
        {
            if (!showSampleRooms)
            {
                return;
            }

            Apply();

            // A session that cannot reach matchmaking still reports its result,
            // and that result is an empty list arriving a moment after this.
            // Refilling on empty is what keeps the rows on screen long enough to
            // look at, and it steps aside as soon as real rooms show up.
            if (TryFindRoomBrowser(out var system))
            {
                refill = system.Rooms.Subscribe(rooms =>
                {
                    if (rooms.Count == 0)
                    {
                        system.SetRooms(Build());
                    }
                });
            }
        }

        private void OnDestroy()
        {
            refill?.Dispose();
        }

        [ContextMenu("Show sample rooms")]
        public void Apply()
        {
            var rooms = Build();

            if (TryFindRoomBrowser(out var system))
            {
                system.SetRooms(rooms);
                return;
            }

            GetComponent<RoomBrowserView>().SetRooms(rooms);
        }

        private static bool TryFindRoomBrowser(out RoomBrowserSystem system)
        {
            system = null;

            foreach (var scope in FindObjectsByType<LifetimeScope>(
                FindObjectsSortMode.None))
            {
                // A scope that has not built its container yet, or one whose
                // parents never registered the browser, simply is not the one.
                if (scope.Container != null &&
                    scope.Container.TryResolve(out system))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The four rows of the wire-frame, including the two that exercise the
        /// hard cases: a title at the twenty character limit, and a host
        /// nickname long enough to push the count off the row.
        /// </summary>
        private static IReadOnlyList<RoomSummary> Build()
        {
            return new List<RoomSummary>
            {
                Room("sample-1", "방제목이지렁방제목이지렁방제목이지렁", 5, RoomStatus.Waiting, "엘리자베스3세"),
                Room("sample-2", "방제목이지렁방제목이지렁방제목이지렁", 6, RoomStatus.Playing, "엘리자베스3세"),
                Room("sample-3", "방제목이지렁방제목이지렁방제목이지렁", 6, RoomStatus.Waiting, "엘리자베스3세"),
                Room("sample-4", "방제목이스무글자까지가능해요이거이거이거", 1, RoomStatus.Waiting, "만약열두글자넘어가면이거"),
            };
        }

        private static RoomSummary Room(
            string id, string title, int players, RoomStatus status, string host)
        {
            return new RoomSummary(
                new RoomId(id),
                title,
                "맵 이름",
                players,
                RoomSettings.MaxPlayerCount,
                isLocked: false,
                isOpen: true,
                status,
                host);
        }
    }
}
