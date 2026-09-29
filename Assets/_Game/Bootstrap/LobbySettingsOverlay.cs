using System;
using Game.Client.Lobby;
using Game.Client.Match;
using Game.Client.Settings;
using Game.Network.Session;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>Reuses the settings presenter without leaving the network-owned lobby scene.</summary>
    public sealed class LobbySettingsOverlay : IStartable, ITickable, IDisposable
    {
        private readonly ILobbyPauseMenuView menu;
        private readonly LobbyPauseMenuPresenter pause;
        private readonly SettingsView view;
        private readonly SettingsPresenter presenter;
        private readonly MatchChatView chat;
        private readonly NetworkRunnerService network;
        private bool opened, chatWasEnabled;

        // A local skip exposes the lobby before the shared highlight phase ends.
        private bool CanUseSettings => !network.HasRoomSession || network.IsWaitingForMatch ||
            (network.IsHighlightInProgress && network.IsLocalHighlightComplete);

        public LobbySettingsOverlay(ILobbyPauseMenuView menu, LobbyPauseMenuPresenter pause,
            SettingsView view, SettingsPresenter presenter, MatchChatView chat, NetworkRunnerService network)
        {
            this.menu = menu; this.pause = pause; this.view = view;
            this.presenter = presenter; this.chat = chat; this.network = network;
        }

        public void Start()
        {
            menu.SettingsClicked += OpenFromMenu;
            pause.SettingsOpenRequested += OpenFromWorld;
            presenter.LeaveGameConfirmed += OnLeaveGame;
            view.Closed += OnClosed;
        }

        private void OpenFromMenu() => Open(fromWorld: false);

        private void OpenFromWorld() => Open(fromWorld: true);

        private void Open(bool fromWorld)
        {
            if (opened || !CanUseSettings) return;
            opened = true;
            chatWasEnabled = chat.enabled;
            chat.enabled = false;
            view.gameObject.SetActive(true);
            pause.OpenSettingsScreen(Hide, fromWorld);
        }

        private void Hide()
        {
            if (view != null)
            {
                view.gameObject.SetActive(false);
            }
        }

        private void OnLeaveGame()
        {
            if (!opened)
            {
                return;
            }

            opened = false;
            presenter.Open();
            if (chat != null)
            {
                chat.enabled = chatWasEnabled;
            }

            Hide();
            pause.LeaveRoom();
        }

        private void OnClosed()
        {
            if (!opened) return;
            opened = false;
            presenter.Open(); // Cancel microphone/key capture and any unapplied draft.
            if (chat != null) chat.enabled = chatWasEnabled;
            pause.OnScreenClosed();
        }

        public void Tick()
        {
            // Another participant can start the match while this local panel is open.
            if (opened && !CanUseSettings)
                view.gameObject.SetActive(false);
        }

        public void Dispose()
        {
            menu.SettingsClicked -= OpenFromMenu;
            pause.SettingsOpenRequested -= OpenFromWorld;
            presenter.LeaveGameConfirmed -= OnLeaveGame;
            view.Closed -= OnClosed;
            if (opened && chat != null) chat.enabled = chatWasEnabled;
        }
    }
}
