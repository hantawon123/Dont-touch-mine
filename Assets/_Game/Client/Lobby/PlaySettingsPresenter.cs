using System;
using Game.Core.Lobby;
using R3;
using UnityEngine;
using VContainer.Unity;

namespace Game.Client.Lobby
{
    public sealed class PlaySettingsPresenter : IStartable, IDisposable
    {
        private readonly ILobbyHostSession hostSession;
        private readonly IPlaySettingsView view;
        private readonly LobbyHudView hudView;
        private IDisposable hostSubscription;
        private IDisposable settingsSubscription;
        private bool isOpen;

        public PlaySettingsPresenter(
            ILobbyHostSession hostSession,
            IPlaySettingsView view,
            LobbyHudView hudView)
        {
            this.hostSession = hostSession ?? throw new ArgumentNullException(nameof(hostSession));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.hudView = hudView ?? throw new ArgumentNullException(nameof(hudView));
        }

        public void Start()
        {
            view.SetVisible(false);
            view.OpenRequested += Open;
            view.CloseRequested += Close;
            view.CopyRoomCodeRequested += CopyRoomCode;
            view.InviteRequested += Invite;
            view.CopyPasswordRequested += CopyPassword;
            hudView.PlaySettingsClicked += Open;
            hostSubscription = hostSession.IsLocalHost.Subscribe(HandleHostChanged);
            settingsSubscription = hostSession.Settings.Subscribe(HandleSettingsChanged);
        }

        public void Dispose()
        {
            view.OpenRequested -= Open;
            view.CloseRequested -= Close;
            view.CopyRoomCodeRequested -= CopyRoomCode;
            view.InviteRequested -= Invite;
            view.CopyPasswordRequested -= CopyPassword;
            hudView.PlaySettingsClicked -= Open;
            hostSubscription?.Dispose();
            settingsSubscription?.Dispose();
        }

        private void HandleHostChanged(bool isHost)
        {
            if (!isHost)
            {
                Close();
            }
        }

        private void HandleSettingsChanged(PlaySettingsDraft draft)
        {
            if (!isOpen)
            {
                return;
            }

            view.SetDraft(draft);
        }

        private void Open()
        {
            if (!hostSession.IsLocalHost.CurrentValue)
            {
                return;
            }

            view.SetDraft(hostSession.Settings.CurrentValue);
            isOpen = true;
            view.SetVisible(true);
        }

        private void Close()
        {
            if (!isOpen)
            {
                return;
            }

            if (hostSession.IsLocalHost.CurrentValue)
            {
                hostSession.RequestApplySettings(view.ReadDraft());
            }

            isOpen = false;
            view.SetVisible(false);
        }

        private void CopyRoomCode()
        {
            var code = hostSession.Settings.CurrentValue.RoomCode;
            if (string.IsNullOrWhiteSpace(code))
            {
                return;
            }

            GUIUtility.systemCopyBuffer = code;
        }

        private void Invite()
        {
            var settings = hostSession.Settings.CurrentValue;
            if (string.IsNullOrWhiteSpace(settings.RoomCode))
            {
                return;
            }

            GUIUtility.systemCopyBuffer =
                $"방 초대\n방제목: {settings.Title}\n방코드: {settings.RoomCode}";
        }

        private void CopyPassword()
        {
            var settings = hostSession.Settings.CurrentValue;
            if (!settings.PasswordEnabled || string.IsNullOrEmpty(settings.Password))
            {
                return;
            }

            GUIUtility.systemCopyBuffer = settings.Password;
        }
    }
}
