using System;
using Game.Client.Character;
using Game.Client.Lobby;
using Game.Client.Match;
using Game.Network.Session;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>
    /// Reuses the Home closet presenter without leaving the network-owned
    /// lobby scene. The panel is the same 1600×876 frame as environment
    /// settings.
    /// </summary>
    public sealed class LobbyCharacterOverlay : IStartable, ITickable, IDisposable
    {
        private readonly LobbyPauseMenuPresenter pause;
        private readonly CharacterClosetView view;
        private readonly CharacterClosetPresenter presenter;
        private readonly MatchChatView chat;
        private readonly NetworkRunnerService network;
        private bool opened, chatWasEnabled;

        public LobbyCharacterOverlay(
            LobbyPauseMenuPresenter pause,
            CharacterClosetView view,
            CharacterClosetPresenter presenter,
            MatchChatView chat,
            NetworkRunnerService network)
        {
            this.pause = pause;
            this.view = view;
            this.presenter = presenter;
            this.chat = chat;
            this.network = network;
        }

        public void Start()
        {
            pause.CharacterOpenRequested += Open;
            view.Closed += OnClosed;
        }

        private void Open()
        {
            if (opened || (network.HasRoomSession && !network.IsWaitingForMatch))
            {
                return;
            }

            opened = true;
            chatWasEnabled = chat.enabled;
            chat.enabled = false;
            view.gameObject.SetActive(true);
            pause.OpenCharacterScreen(Hide, fromWorld: true);
        }

        private void Hide()
        {
            if (view != null)
            {
                view.gameObject.SetActive(false);
            }
        }

        private void OnClosed()
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

            pause.OnScreenClosed();
        }

        public void Tick()
        {
            if (opened && network.HasRoomSession && !network.IsWaitingForMatch)
            {
                view.gameObject.SetActive(false);
            }
        }

        public void Dispose()
        {
            pause.CharacterOpenRequested -= Open;
            view.Closed -= OnClosed;
            if (opened && chat != null)
            {
                chat.enabled = chatWasEnabled;
            }
        }
    }
}
