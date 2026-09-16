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
        private readonly ILobbyPauseMenuView pauseMenu;
        private readonly ILobbyParticipantList participants;
        private IDisposable hostSubscription;
        private IDisposable settingsSubscription;
        private IDisposable participantSubscription;
        private bool isOpen;
        private PlaySettingsDraft displayedSettings;

        public PlaySettingsPresenter(
            ILobbyHostSession hostSession,
            IPlaySettingsView view,
            ILobbyPauseMenuView pauseMenu)
            : this(hostSession, view, pauseMenu, null)
        {
        }

        [VContainer.Inject]
        public PlaySettingsPresenter(
            ILobbyHostSession hostSession,
            IPlaySettingsView view,
            ILobbyPauseMenuView pauseMenu,
            ILobbyParticipantList participants)
        {
            this.hostSession = hostSession ?? throw new ArgumentNullException(nameof(hostSession));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.pauseMenu = pauseMenu ?? throw new ArgumentNullException(nameof(pauseMenu));
            this.participants = participants;
        }

        public void Start()
        {
            view.SetVisible(false);
            view.OpenRequested += Open;
            view.CloseRequested += Close;
            view.CopyRoomCodeRequested += CopyRoomCode;
            view.InviteRequested += Invite;
            view.CopyPasswordRequested += CopyPassword;
            view.StartRequested += StartMatch;
            view.ApplyRequested += Apply;
            pauseMenu.PlaySettingsClicked += Open;
            hostSubscription = hostSession.IsLocalHost.Subscribe(HandleHostChanged);
            settingsSubscription = hostSession.Settings.Subscribe(HandleSettingsChanged);
            if (participants != null)
            {
                participantSubscription = participants.Participants.Subscribe(list =>
                    view.SetParticipantCount(list == null ? 0 : list.Count));
            }
        }

        public void Dispose()
        {
            view.OpenRequested -= Open;
            view.CloseRequested -= Close;
            view.CopyRoomCodeRequested -= CopyRoomCode;
            view.InviteRequested -= Invite;
            view.CopyPasswordRequested -= CopyPassword;
            view.StartRequested -= StartMatch;
            view.ApplyRequested -= Apply;
            pauseMenu.PlaySettingsClicked -= Open;
            hostSubscription?.Dispose();
            settingsSubscription?.Dispose();
            participantSubscription?.Dispose();
            if (isOpen)
            {
                SetInteractionPromptVisible(true);
            }
        }

        private void HandleHostChanged(bool isHost)
        {
            view.SetEditable(isHost);
            if (!isHost && isOpen)
            {
                DisplaySettings(hostSession.Settings.CurrentValue);
            }
        }

        private void HandleSettingsChanged(PlaySettingsDraft draft)
        {
            if (!isOpen || hostSession.IsLocalHost.CurrentValue)
            {
                return;
            }

            DisplaySettings(draft);
        }

        private void Open()
        {
            if (isOpen) return;
            view.SetEditable(hostSession.IsLocalHost.CurrentValue);
            DisplaySettings(hostSession.Settings.CurrentValue);
            isOpen = true;
            SetInteractionPromptVisible(false);
            view.SetVisible(true);
        }

        private void Close()
        {
            if (!isOpen)
            {
                return;
            }

            if (hostSession.IsLocalHost.CurrentValue && view.HasUnappliedChanges)
            {
                view.SetUnappliedWarningVisible(true);
                return;
            }

            isOpen = false;
            view.SetVisible(false);
            view.SetUnappliedWarningVisible(false);
        }

        private void Apply()
        {
            if (!isOpen || !hostSession.IsLocalHost.CurrentValue)
            {
                return;
            }

            var draft = view.ReadDraft();
            if (!RoomSettings.IsValidTitle(draft.Title))
            {
                return;
            }

            if (!draft.Equals(displayedSettings) &&
                !draft.Equals(hostSession.Settings.CurrentValue))
            {
                hostSession.RequestApplySettings(draft);
            }

            DisplaySettings(draft);
        }

        private void StartMatch()
        {
            if (!isOpen)
            {
                return;
            }

            if (hostSession.IsLocalHost.CurrentValue && view.HasUnappliedChanges)
            {
                view.SetUnappliedWarningVisible(true);
                return;
            }

            Close();
            if (isOpen)
            {
                return;
            }

            hostSession.RequestStart();
        }

        private static void SetInteractionPromptVisible(bool visible)
        {
            var interactors = UnityEngine.Object.FindObjectsByType<Interactions.PlayerInteractor>(
                FindObjectsSortMode.None);
            for (var i = 0; i < interactors.Length; i++)
            {
                interactors[i].SetInteractionPromptVisible(visible);
            }
        }

        private void DisplaySettings(PlaySettingsDraft draft)
        {
            displayedSettings = draft;
            view.SetDraft(draft);
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
