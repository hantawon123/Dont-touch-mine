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
    /// <para>
    /// The menu also leads to the play settings and key guide screens. Those
    /// are wider than this panel and sit at the same centre, so the menu steps
    /// aside while one is up rather than showing its edges around it. The
    /// cursor and the movement lock stay as they are through that: the player
    /// is still in the menu, just on a different page of it.
    /// </para>
    /// </remarks>
    public sealed class LobbyPauseMenuPresenter : IStartable, ITickable, IDisposable
    {
        private readonly ILobbyPauseMenuView view;
        private readonly IKeyGuideView keyGuide;
        private readonly IPlaySettingsView playSettings;
        private readonly ILobbyHostSession hostSession;
        private readonly LobbyExitPresenter exit;
        private IDisposable hostSubscription;
        private PlayerCameraController cameraRig;
        private PlayerMovement lockedMovement;

        /// <summary>
        /// Closes whichever screen the menu stepped aside for, or null while the
        /// menu itself is the thing on screen.
        /// </summary>
        /// <remarks>
        /// Held as the close call rather than as the view: the two screens share
        /// no type, and what this needs from either of them is the same one
        /// thing.
        /// </remarks>
        private Action closeOpenScreen;

        public LobbyPauseMenuPresenter(
            ILobbyPauseMenuView view,
            IKeyGuideView keyGuide,
            IPlaySettingsView playSettings,
            ILobbyHostSession hostSession,
            LobbyExitPresenter exit)
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.keyGuide = keyGuide ?? throw new ArgumentNullException(nameof(keyGuide));
            this.playSettings = playSettings
                ?? throw new ArgumentNullException(nameof(playSettings));
            this.hostSession = hostSession
                ?? throw new ArgumentNullException(nameof(hostSession));
            this.exit = exit ?? throw new ArgumentNullException(nameof(exit));
        }

        public void Start()
        {
            view.StartClicked += OnStartClicked;
            view.LeaveClicked += OnLeaveClicked;
            view.ResumeClicked += Close;
            view.PlaySettingsClicked += OnPlaySettingsClicked;
            view.KeyGuideClicked += OnKeyGuideClicked;

            // Both screens are opened by their own presenters, which listen to
            // the same buttons. Coming back is what is left over, and it is the
            // menu's to do.
            keyGuide.CloseRequested += OnScreenClosed;
            playSettings.CloseRequested += OnScreenClosed;

            // Starting and changing the room are the host's to ask for, so
            // neither entry is there for anyone else.
            hostSubscription = hostSession.IsLocalHost.Subscribe(ApplyHostControls);

            Close();
        }

        public void Dispose()
        {
            view.StartClicked -= OnStartClicked;
            view.LeaveClicked -= OnLeaveClicked;
            view.ResumeClicked -= Close;
            view.PlaySettingsClicked -= OnPlaySettingsClicked;
            view.KeyGuideClicked -= OnKeyGuideClicked;
            keyGuide.CloseRequested -= OnScreenClosed;
            playSettings.CloseRequested -= OnScreenClosed;
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

            // One page back rather than all the way out. Asking the screen to
            // close, instead of hiding it, keeps its presenter's idea of whether
            // it is open in step with what is on the glass.
            if (closeOpenScreen != null)
            {
                closeOpenScreen.Invoke();
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

        private void ApplyHostControls(bool isHost)
        {
            view.SetStartVisible(isHost);
            view.SetPlaySettingsVisible(true);
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
            closeOpenScreen = null;
            view.SetVisible(false);
            SetCursorCaptured(true);
            ReleaseMovement();
        }

        /// <remarks>
        /// The cursor and the movement lock are deliberately left alone. The
        /// player is still in the menu, and re-capturing the cursor here would
        /// hand them a settings screen they cannot click.
        /// </remarks>
        private void OnPlaySettingsClicked() => StepAsideFor(playSettings.RequestClose);

        private void OnKeyGuideClicked() => StepAsideFor(keyGuide.RequestClose);

        private void StepAsideFor(Action close)
        {
            closeOpenScreen = close;
            view.SetVisible(false);
        }

        /// <remarks>
        /// Runs for the screen's own close button and for Esc alike, since both
        /// arrive as the same request. Returning here preserves the menu's cursor and movement lock.
        /// </remarks>
        private void OnScreenClosed()
        {
            if (closeOpenScreen == null)
            {
                return;
            }

            closeOpenScreen = null;
            view.SetVisible(true);
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
            closeOpenScreen = null;
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
