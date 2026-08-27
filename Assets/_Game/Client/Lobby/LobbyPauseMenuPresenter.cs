using System;
using Game.Client.Cameras;
using Game.Client.Players;
using Game.Core.Lobby;
using R3;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer.Unity;

namespace Game.Client.Lobby
{
    /// <summary>
    /// Owns what the mouse and the Esc key do in the lobby.
    /// </summary>
    /// <remarks>
    /// The lobby is a place the player walks around, so the cursor stays
    /// captured for looking: movement is camera-relative and the character
    /// faces where the camera faces, which leaves a freed cursor with no way to
    /// turn. Esc is the way back out to the pointer, and it opens the menu in
    /// the same breath because a released cursor with nothing to press is just
    /// a stuck screen.
    /// <para>
    /// Cursor and movement are set here rather than once while the scene loads.
    /// The one-shot call this replaces ran before the avatar had replicated in,
    /// so nothing owned the state afterwards.
    /// </para>
    /// </remarks>
    public sealed class LobbyPauseMenuPresenter : IStartable, ITickable, IDisposable
    {
        private readonly ILobbyPauseMenuView view;
        private readonly ILobbyHostSession hostSession;
        private readonly LobbyExitPresenter exit;
        private IDisposable hostSubscription;
        private PlayerCameraController cameraRig;
        private PlayerMovement lockedMovement;

        public LobbyPauseMenuPresenter(
            ILobbyPauseMenuView view,
            ILobbyHostSession hostSession,
            LobbyExitPresenter exit)
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.hostSession = hostSession
                ?? throw new ArgumentNullException(nameof(hostSession));
            this.exit = exit ?? throw new ArgumentNullException(nameof(exit));
        }

        public void Start()
        {
            view.StartClicked += OnStartClicked;
            view.LeaveClicked += OnLeaveClicked;
            view.ResumeClicked += Close;

            // Starting is the host's to ask for, so the button is not there for
            // anyone else. Same rule the play settings button follows.
            hostSubscription = hostSession.IsLocalHost.Subscribe(view.SetStartVisible);

            Close();
        }

        public void Dispose()
        {
            view.StartClicked -= OnStartClicked;
            view.LeaveClicked -= OnLeaveClicked;
            view.ResumeClicked -= Close;
            hostSubscription?.Dispose();

            // A frozen avatar and a rig that no longer answers Esc would both
            // outlive this screen otherwise. The rig is shared with the match,
            // which has no menu of its own to release the cursor.
            ReleaseMovement();
            ResolveCameraRig()?.SetEscapeReleasesCursor(true);
        }

        public void Tick()
        {
            // The avatar can arrive after the menu is already open, and an open
            // menu the player can walk away from is not open in any useful
            // sense.
            if (view.IsOpen && lockedMovement == null)
            {
                LockMovement();
            }

            if (Keyboard.current == null ||
                !Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                return;
            }

            // Chat opens on Enter and takes the keyboard while it is focused.
            // Esc there belongs to the field the player is typing in.
            if (PlayerMovement.IsTextInputFocused())
            {
                return;
            }

            if (view.IsOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        private void Open()
        {
            view.SetVisible(true);
            if (!view.IsOpen)
            {
                // The panel is not wired up. Taking the cursor and the controls
                // away for a menu that never appears would leave the player
                // standing there with nothing to press.
                return;
            }

            SetCursorCaptured(false);
            LockMovement();
        }

        private void Close()
        {
            view.SetVisible(false);
            SetCursorCaptured(true);
            ReleaseMovement();
        }

        /// <remarks>
        /// The button only asks. Taking everyone into the map is the authority's
        /// job and already happens once the line-up is confirmed, so loading a
        /// scene here would move whoever clicked on ahead of the others and race
        /// the networked load on the authority's own screen.
        /// </remarks>
        private void OnStartClicked()
        {
            Close();
            hostSession.RequestStart();
        }

        /// <remarks>
        /// The cursor is handed over free rather than re-captured. The room
        /// browser this leads to is a screen made of buttons, and arriving there
        /// with a captured cursor leaves nothing on it clickable.
        /// </remarks>
        private void OnLeaveClicked()
        {
            view.SetVisible(false);
            ReleaseMovement();
            SetCursorCaptured(false);
            exit.RequestLeave();
        }

        /// <remarks>
        /// Esc is taken off the rig on the way, not once at startup: the rig can
        /// be found again later, and a rig that answers the same Esc as this
        /// menu re-releases the cursor on the frame the menu closes.
        /// </remarks>
        private void SetCursorCaptured(bool captured)
        {
            var rig = ResolveCameraRig();
            if (rig == null)
            {
                return;
            }

            rig.SetEscapeReleasesCursor(false);
            rig.SetCursorCaptureEnabled(captured);
        }

        private void LockMovement()
        {
            var movement = ResolveCameraRig()?.FollowMovement;
            if (movement == null)
            {
                return;
            }

            movement.IsMovementLocked = true;
            lockedMovement = movement;
        }

        /// <summary>
        /// Releases the character this menu locked, not whichever one the camera
        /// follows now. The camera rebinds when the avatar replicates in, and
        /// releasing the new one would leave the old one walking on its own.
        /// </summary>
        private void ReleaseMovement()
        {
            if (lockedMovement == null)
            {
                return;
            }

            lockedMovement.IsMovementLocked = false;
            lockedMovement = null;
        }

        /// <remarks>
        /// Looked up rather than injected: the rig is a scene object the lobby
        /// scope creates while it builds, and on a client it can be re-created
        /// after that. The same reason <c>LobbyPlayerCameraBinder</c> looks.
        /// </remarks>
        private PlayerCameraController ResolveCameraRig()
        {
            if (cameraRig == null)
            {
                cameraRig = UnityEngine.Object.FindFirstObjectByType<PlayerCameraController>(
                    FindObjectsInactive.Include);
            }

            return cameraRig;
        }
    }
}
