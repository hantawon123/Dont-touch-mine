using System;
using Game.Client.Cameras;
using Game.Client.Emotes;
using Game.Client.Lobby;
using Game.Client.Match;
using Game.Client.Players;
using Game.Client.Settings;
using Game.Network.Session;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>
    /// Toggles the in-match participant list on 2. Esc closes it without
    /// opening environment settings on the same press.
    /// </summary>
    public sealed class MatchParticipantListOverlay : ITickable, IDisposable
    {
        private readonly LobbyPlayerListView list;
        private readonly MatchChatView chat;
        private readonly NetworkRunnerService network;
        private readonly SettingsView settings;
        private readonly KickConfirmView confirm;
        private PlayerCameraController camera;

        public MatchParticipantListOverlay(
            LobbyPlayerListView list,
            MatchChatView chat,
            NetworkRunnerService network,
            SettingsView settings,
            KickConfirmView confirm = null)
        {
            this.list = list ?? throw new ArgumentNullException(nameof(list));
            this.chat = chat;
            this.network = network ?? throw new ArgumentNullException(nameof(network));
            this.settings = settings;
            this.confirm = confirm;
            list.ConfigureForMatch();
            Hide();
        }

        public bool IsOpen { get; private set; }

        public bool ConsumedEscapeThisFrame { get; private set; }

        public static bool ShouldHandleToggle(
            bool textFocused,
            bool settingsOpen,
            bool presentationBlocks,
            bool confirmOpen = false)
        {
            return !textFocused && !settingsOpen && !presentationBlocks && !confirmOpen;
        }

        public void Tick()
        {
            ConsumedEscapeThisFrame = false;
            if (network.IsResultSceneLoaded ||
                network.IsHighlightInProgress ||
                network.IsWaitingForMatch)
            {
                if (IsOpen)
                {
                    Hide();
                }

                return;
            }

            if (Keyboard.current == null)
            {
                return;
            }

            var keyboard = Keyboard.current;
            var confirmOpen = confirm != null && confirm.IsShown;
            var escapePressed = WasPressed(keyboard.escapeKey);
            if (IsOpen && escapePressed && !confirmOpen)
            {
                Hide();
                ConsumedEscapeThisFrame = true;
                return;
            }

            if (confirmOpen)
            {
                return;
            }

            var playersPressed = !EmoteWheelController.BlocksLobbyShortcuts &&
                (WasPressed(keyboard.digit2Key) || WasPressed(keyboard.numpad2Key));
            if (!playersPressed)
            {
                return;
            }

            if (!ShouldHandleToggle(
                    PlayerMovement.IsTextInputFocused() ||
                    (chat != null && chat.IsInputFocused),
                    settings != null && settings.gameObject.activeSelf,
                    !network.IsRuntimeReady,
                    confirmOpen))
            {
                return;
            }

            if (IsOpen)
            {
                Hide();
                return;
            }

            Show();
        }

        public void Dispose()
        {
            Hide();
        }

        private void Show()
        {
            list.ConfigureForMatch();
            list.transform.SetAsLastSibling();
            list.gameObject.SetActive(true);
            IsOpen = true;
            camera = UnityEngine.Object.FindFirstObjectByType<PlayerCameraController>(
                FindObjectsInactive.Include);
            if (camera != null)
            {
                camera.SetEscapeReleasesCursor(false);
                camera.SetCursorCaptureEnabled(false);
            }
        }

        private void Hide()
        {
            confirm?.Hide();
            if (list != null)
            {
                list.gameObject.SetActive(false);
            }

            IsOpen = false;
            if (camera != null &&
                (settings == null || !settings.gameObject.activeSelf) &&
                !network.IsResultSceneLoaded &&
                !network.IsHighlightInProgress)
            {
                camera.SetCursorCaptureEnabled(true);
            }
        }

        private static bool WasPressed(UnityEngine.InputSystem.Controls.KeyControl key)
        {
            return key != null && key.wasPressedThisFrame;
        }
    }
}
