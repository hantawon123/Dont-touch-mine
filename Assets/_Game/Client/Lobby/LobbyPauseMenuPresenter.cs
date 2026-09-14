using System;
using Game.Client.Cameras;
using Game.Client.Interactions;
using Game.Client.Players;
using Game.Core.Lobby;
using R3;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using VContainer.Unity;

namespace Game.Client.Lobby
{
    /// <summary>
    /// Owns what the mouse and the 1 / 2 / Esc keys do in the lobby.
    /// </summary>
    /// <remarks>
    /// The lobby is a place the player walks around, so the cursor stays
    /// captured for looking: movement is camera-relative and the character
    /// faces where the camera faces, which leaves a freed cursor with no way to
    /// turn. Esc frees the pointer and opens environment settings; 1 and 2
    /// open the other two overlays the bottom-right guide names. A released
    /// cursor with nothing to press is just a stuck screen.
    /// <para>
    /// Cursor and movement are set here rather than once while the scene loads.
    /// The one-shot call this replaces ran before the avatar had replicated in,
    /// so nothing owned the state afterwards.
    /// </para>
    /// <para>
    /// The menu also leads to the play settings screen. That screen is wider
    /// than this panel and sits at the same centre, so the menu steps aside
    /// while it is up rather than showing its edges around it. The cursor and
    /// the movement lock stay as they are through that: the player is still in
    /// the menu, just on a different page of it.
    /// </para>
    /// </remarks>
    public sealed class LobbyPauseMenuPresenter : IStartable, ITickable, IDisposable, IPlaySettingsOpener
    {
        private readonly ILobbyPauseMenuView view;
        private readonly IPlaySettingsView playSettings;
        private readonly ILobbyHostSession hostSession;
        private readonly LobbyExitPresenter exit;
        private readonly ILobbyShortcutOverlay shortcuts;
        private readonly Action shortcutClose;
        private readonly Action playSettingsClose;
        private IDisposable hostSubscription;
        private PlayerCameraController cameraRig;
        private PlayerMovement lockedMovement;

        /// <summary>
        /// True while the play settings were opened from an object in the room
        /// rather than from this menu, so closing them goes back to the room.
        /// </summary>
        private bool openedFromWorld;

        /// <summary>
        /// Opens the lobby's environment-settings overlay from the room. The
        /// overlay listens and shows the same panel Home uses.
        /// </summary>
        public event Action SettingsOpenRequested;

        /// <summary>
        /// Opens the lobby's character-closet overlay from the 1 key. The
        /// overlay listens and shows the Home closet inside the settings frame.
        /// </summary>
        public event Action CharacterOpenRequested;

        /// <summary>
        /// True while the character closet overlay owns Esc / the 1 key.
        /// </summary>
        private bool characterOverlayOpen;

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
            IPlaySettingsView playSettings,
            ILobbyHostSession hostSession,
            LobbyExitPresenter exit,
            ILobbyShortcutOverlay shortcuts)
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.playSettings = playSettings
                ?? throw new ArgumentNullException(nameof(playSettings));
            this.hostSession = hostSession
                ?? throw new ArgumentNullException(nameof(hostSession));
            this.exit = exit ?? throw new ArgumentNullException(nameof(exit));
            this.shortcuts = shortcuts
                ?? throw new ArgumentNullException(nameof(shortcuts));
            shortcutClose = this.shortcuts.RequestClose;
            playSettingsClose = this.playSettings.RequestClose;
        }

        public void Start()
        {
            view.StartClicked += OnStartClicked;
            view.LeaveClicked += OnLeaveClicked;
            view.ResumeClicked += Close;
            view.PlaySettingsClicked += OnPlaySettingsClicked;

            // Play settings is opened by its own presenter, which listens to
            // the same button. Coming back is what is left over, and it is the
            // menu's to do. The 1 / 2 / Esc overlays share that same return.
            playSettings.CloseRequested += OnScreenClosed;
            shortcuts.CloseRequested += OnScreenClosed;
            hostSession.StartRequested += DismissForMatchStart;

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
            playSettings.CloseRequested -= OnScreenClosed;
            shortcuts.CloseRequested -= OnScreenClosed;
            hostSession.StartRequested -= DismissForMatchStart;
            hostSubscription?.Dispose();

            // A frozen avatar and a rig that no longer answers Esc would both
            // outlive this screen otherwise. The rig is shared with the match,
            // which has no menu of its own to release the cursor.
            ReleaseMovement();
            ResolveCameraRig()?.SetEscapeReleasesCursor(true);
        }

        public void Tick()
        {
            var browserReleased = Game.Client.Common.WebPointerInput.ReleasedByBrowserThisFrame;
            // The avatar can arrive after the menu is already open, and an open
            // menu the player can walk away from is not open in any useful
            // sense.
            if ((view.IsOpen || closeOpenScreen != null) && lockedMovement == null)
            {
                SetCursorCaptured(false);
                LockMovement();
                RefreshObjectPrompts();
            }

            if (Keyboard.current == null)
            {
                return;
            }

            // Chat opens on Enter and takes the keyboard while it is focused.
            // Keys there belong to the field the player is typing in.
            if (PlayerMovement.IsTextInputFocused())
            {
                return;
            }

            var keyboard = Keyboard.current;
            var pressed = LobbyShortcutBindings.ReadPressed(
                WasPressed(keyboard.digit1Key) || WasPressed(keyboard.numpad1Key),
                WasPressed(keyboard.digit2Key) || WasPressed(keyboard.numpad2Key),
                false);

            // Esc always backs out of whatever is already up. From the room it
            // opens environment settings. 1 and 2 switch between their overlays,
            // and only open a new one from the room.
            var canOpenShortcut = LobbyShortcutBindings.CanHandle(
                false,
                view.IsOpen,
                HasForeignScreen);
            if (pressed == LobbyShortcutKind.Character && characterOverlayOpen)
            {
                closeOpenScreen?.Invoke();
                return;
            }

            if ((pressed == LobbyShortcutKind.Character ||
                 pressed == LobbyShortcutKind.Players) &&
                (canOpenShortcut || (shortcuts.IsOpen && !HasForeignScreen)))
            {
                ToggleShortcut(pressed);
                return;
            }

            if (WasPressed(keyboard.escapeKey) ||
                (browserReleased && !view.IsOpen && closeOpenScreen == null))
            {
                HandleEscape();
            }
        }

        /// <summary>
        /// Esc backs out of an open screen, or opens environment settings from
        /// the room. The same key closes that overlay. Leaving the room is
        /// 게임 나가기 on it, not this key.
        /// </summary>
        public void HandleEscape()
        {
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
                return;
            }

            SettingsOpenRequested?.Invoke();
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
            RefreshObjectPrompts();
        }

        /// <summary>
        /// Folds every lobby menu and lets the avatar walk. Play settings'
        /// start button hides that screen without a close request, so the
        /// movement lock from opening it would otherwise last the whole
        /// countdown.
        /// </summary>
        public void DismissForMatchStart() => Close();

        private void Close()
        {
            var pending = closeOpenScreen;
            closeOpenScreen = null;
            openedFromWorld = false;
            characterOverlayOpen = false;
            pending?.Invoke();
            view.SetVisible(false);
            SetCursorCaptured(true);
            ReleaseMovement();
            ClearUiSelection();
            RefreshObjectPrompts();
        }

        /// <remarks>
        /// The same hand-over the menu does for its own button, minus the menu:
        /// the cursor is freed and the avatar held still so the screen can be
        /// used, and the flag makes the eventual close return to the room. A
        /// screen that is already up wins; opening a second one on top of it
        /// would leave two things claiming Esc.
        /// </remarks>
        public void OpenSettingsScreen(Action close, bool fromWorld = false)
        {
            openedFromWorld = fromWorld;
            closeOpenScreen = close;
            view.SetVisible(false);
            SetCursorCaptured(false);
            LockMovement();
            RefreshObjectPrompts();
        }

        /// <summary>
        /// Same hand-over as environment settings, tagged so 1 can close the
        /// closet without treating it as a foreign screen that ignores the key.
        /// </summary>
        public void OpenCharacterScreen(Action close, bool fromWorld = false)
        {
            characterOverlayOpen = true;
            OpenSettingsScreen(close, fromWorld);
        }

        public void OpenPlaySettingsFromWorld()
        {
            if (view.IsOpen || closeOpenScreen != null)
            {
                return;
            }

            SetCursorCaptured(false);
            LockMovement();
            openedFromWorld = true;
            StepAsideFor(playSettingsClose);
            playSettings.RequestOpen();
            RefreshObjectPrompts();
        }

        /// <remarks>
        /// The cursor and the movement lock are deliberately left alone. The
        /// player is still in the menu, and re-capturing the cursor here would
        /// hand them a settings screen they cannot click.
        /// </remarks>
        private void OnPlaySettingsClicked() => StepAsideFor(playSettingsClose);

        /// <summary>
        /// Opens a 1 / 2 overlay from the room. Closing it returns to
        /// walking rather than to the pause menu, the same as opening play
        /// settings from the plan board.
        /// </summary>
        public void ToggleShortcut(LobbyShortcutKind kind)
        {
            if (kind == LobbyShortcutKind.None)
            {
                return;
            }

            if (kind == LobbyShortcutKind.Character && characterOverlayOpen)
            {
                closeOpenScreen?.Invoke();
                return;
            }

            if (HasForeignScreen)
            {
                return;
            }

            if (kind == LobbyShortcutKind.Character)
            {
                if (shortcuts.IsOpen)
                {
                    shortcutClose.Invoke();
                }

                CharacterOpenRequested?.Invoke();
                return;
            }

            if (shortcuts.IsOpen && shortcuts.OpenKind == kind)
            {
                shortcutClose.Invoke();
                return;
            }

            if (!shortcuts.IsOpen)
            {
                if (view.IsOpen)
                {
                    openedFromWorld = false;
                    StepAsideFor(shortcutClose);
                }
                else
                {
                    openedFromWorld = true;
                    SetCursorCaptured(false);
                    LockMovement();
                    StepAsideFor(shortcutClose);
                }
            }

            shortcuts.Show(kind);
            RefreshObjectPrompts();
        }

        private bool HasForeignScreen =>
            closeOpenScreen != null && closeOpenScreen != shortcutClose;

        private static bool WasPressed(UnityEngine.InputSystem.Controls.KeyControl key)
        {
            return key != null && key.wasPressedThisFrame;
        }

        private void StepAsideFor(Action close)
        {
            closeOpenScreen = close;
            view.SetVisible(false);
        }

        /// <remarks>
        /// Runs for the screen's own close button and for Esc alike, since both
        /// arrive as the same request. Returning here preserves the menu's cursor and movement lock.
        /// </remarks>
        public void OnScreenClosed()
        {
            // Play settings refuses to leave while a draft is still dirty. A
            // close request that still arrives must not recapture the cursor,
            // or 적용하기 becomes unreachable. Only that screen: a dirty draft
            // left in the background must not keep Esc from returning the
            // mouse to the room.
            if (closeOpenScreen == playSettingsClose && playSettings.HasUnappliedChanges)
            {
                SetCursorCaptured(false);
                LockMovement();
                RefreshObjectPrompts();
                return;
            }

            characterOverlayOpen = false;
            if (closeOpenScreen == null)
            {
                return;
            }

            // Opened from the room, so there is no menu to come back to.
            if (openedFromWorld)
            {
                Close();
                return;
            }

            closeOpenScreen = null;
            view.SetVisible(true);
            RefreshObjectPrompts();
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
        private void OnLeaveClicked() => Leave();

        /// <summary>
        /// Leaves the room from another screen that offers the same way out,
        /// such as the lobby settings overlay.
        /// </summary>
        public void LeaveRoom() => Leave();

        private void Leave()
        {
            closeOpenScreen = null;
            openedFromWorld = false;
            view.SetVisible(false);
            ReleaseMovement();
            SetCursorCaptured(false);
            RefreshObjectPrompts();
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

        private static void ClearUiSelection()
        {
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        private void RefreshObjectPrompts()
        {
            SetObjectPromptsVisible(ShowsObjectPrompts(view.IsOpen, closeOpenScreen != null));
        }

        /// <summary>
        /// World key chips on the plan board, carryables and placement stay
        /// off for as long as a lobby modal is up.
        /// </summary>
        public static bool ShowsObjectPrompts(bool pauseMenuOpen, bool overlayOpen) =>
            !pauseMenuOpen && !overlayOpen;

        private static void SetObjectPromptsVisible(bool visible)
        {
            var interactors = UnityEngine.Object.FindObjectsByType<PlayerInteractor>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (var index = 0; index < interactors.Length; index++)
            {
                interactors[index].SetInteractionPromptVisible(visible);
            }
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
