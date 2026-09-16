using System;
using System.Collections.Generic;
using Game.Client.Lobby;
using Game.Core.Lobby;
using NUnit.Framework;
using R3;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// The play settings screen can be reached from the Esc menu or from the
    /// plan board in the room. Where it goes back to on close depends on where
    /// it came from.
    /// </summary>
    public sealed class LobbyPauseMenuPresenterTests
    {
        [Test]
        public void SettingsOverlay_ReturnsToPauseMenuWithoutLeavingRoom()
        {
            using var fixture = new Fixture();
            fixture.Presenter.Start();
            fixture.Presenter.OpenSettingsScreen(() => { });
            Assert.That(fixture.Menu.IsOpen, Is.False);
            fixture.Presenter.OnScreenClosed();
            Assert.That(fixture.Menu.IsOpen, Is.True);
        }

        [Test]
        public void OpenFromWorld_RequestsSettingsOpen_WithoutShowingTheMenu()
        {
            using var fixture = new Fixture();
            fixture.Presenter.Start();
            fixture.Menu.VisibleCalls.Clear();

            fixture.Presenter.OpenPlaySettingsFromWorld();

            Assert.That(fixture.Settings.OpenRequests, Is.EqualTo(1));
            Assert.That(fixture.Menu.VisibleCalls, Has.No.Member(true));
        }

        [Test]
        public void OpenFromWorld_ThenClose_ReturnsToRoomNotMenu()
        {
            using var fixture = new Fixture();
            fixture.Presenter.Start();
            fixture.Presenter.OpenPlaySettingsFromWorld();
            fixture.Menu.VisibleCalls.Clear();

            fixture.Settings.RequestClose();

            Assert.That(fixture.Menu.VisibleCalls, Has.No.Member(true));
            Assert.That(fixture.Menu.IsOpen, Is.False);
        }

        [Test]
        public void OpenFromMenu_ThenClose_ReturnsToMenu()
        {
            using var fixture = new Fixture();
            fixture.Presenter.Start();
            fixture.Menu.ClickPlaySettings();
            fixture.Menu.VisibleCalls.Clear();

            fixture.Settings.RequestClose();

            Assert.That(fixture.Menu.VisibleCalls, Has.Member(true));
        }

        [Test]
        public void OpenFromWorld_UnappliedClose_KeepsScreenAndDoesNotReturnToRoom()
        {
            using var fixture = new Fixture();
            fixture.Presenter.Start();
            fixture.Presenter.OpenPlaySettingsFromWorld();
            fixture.Settings.UnappliedChanges = true;
            fixture.Menu.VisibleCalls.Clear();

            fixture.Settings.RequestClose();

            Assert.That(fixture.Menu.IsOpen, Is.False);
            Assert.That(fixture.Menu.VisibleCalls, Has.No.Member(true));

            fixture.Settings.UnappliedChanges = false;
            fixture.Settings.RequestClose();

            Assert.That(fixture.Menu.IsOpen, Is.False);
            Assert.That(fixture.Menu.VisibleCalls, Has.No.Member(true));
        }

        [Test]
        public void OpenFromMenu_UnappliedClose_DoesNotReturnToMenu()
        {
            using var fixture = new Fixture();
            fixture.Presenter.Start();
            fixture.Menu.ClickPlaySettings();
            fixture.Settings.UnappliedChanges = true;
            fixture.Menu.VisibleCalls.Clear();

            fixture.Settings.RequestClose();

            Assert.That(fixture.Menu.VisibleCalls, Has.No.Member(true));

            fixture.Settings.UnappliedChanges = false;
            fixture.Settings.RequestClose();

            Assert.That(fixture.Menu.VisibleCalls, Has.Member(true));
        }

        [Test]
        public void OpenFromWorld_WhileMenuIsUp_DoesNothing()
        {
            using var fixture = new Fixture();
            fixture.Presenter.Start();
            fixture.Menu.SetVisible(true); // as if Esc had opened it

            fixture.Presenter.OpenPlaySettingsFromWorld();

            Assert.That(fixture.Settings.OpenRequests, Is.Zero);
        }

        [Test]
        public void OpenFromWorld_WhileSettingsAlreadyOpen_DoesNotOpenTwice()
        {
            using var fixture = new Fixture();
            fixture.Presenter.Start();
            fixture.Presenter.OpenPlaySettingsFromWorld();

            fixture.Presenter.OpenPlaySettingsFromWorld();

            Assert.That(fixture.Settings.OpenRequests, Is.EqualTo(1));
        }

        [Test]
        public void OpenFromWorld_AfterAMenuRoundTrip_StillReturnsToRoom()
        {
            using var fixture = new Fixture();
            fixture.Presenter.Start();
            // Menu -> settings -> back to menu, then leave the menu via Resume.
            fixture.Menu.ClickPlaySettings();
            fixture.Settings.RequestClose();
            fixture.Menu.ClickResume();

            fixture.Presenter.OpenPlaySettingsFromWorld();
            fixture.Menu.VisibleCalls.Clear();
            fixture.Settings.RequestClose();

            Assert.That(fixture.Menu.VisibleCalls, Has.No.Member(true));
        }

        [Test]
        public void ToggleShortcut_OpensCharacterFromTheRoom()
        {
            using var fixture = new Fixture();
            var requested = 0;
            fixture.Presenter.CharacterOpenRequested += () => requested++;
            fixture.Presenter.Start();

            fixture.Presenter.ToggleShortcut(LobbyShortcutKind.Character);

            Assert.That(requested, Is.EqualTo(1));
            Assert.That(fixture.Shortcuts.IsOpen, Is.False);
            Assert.That(fixture.Menu.IsOpen, Is.False);
        }

        [Test]
        public void ToggleShortcut_Character_ClosesThePlayerOverlayFirst()
        {
            using var fixture = new Fixture();
            var requested = 0;
            fixture.Presenter.CharacterOpenRequested += () => requested++;
            fixture.Presenter.Start();
            fixture.Presenter.ToggleShortcut(LobbyShortcutKind.Players);

            fixture.Presenter.ToggleShortcut(LobbyShortcutKind.Character);

            Assert.That(requested, Is.EqualTo(1));
            Assert.That(fixture.Shortcuts.IsOpen, Is.False);
        }

        [Test]
        public void ToggleShortcut_SameKind_ClosesAndReturnsToRoom()
        {
            using var fixture = new Fixture();
            fixture.Presenter.Start();
            fixture.Presenter.ToggleShortcut(LobbyShortcutKind.Players);
            fixture.Menu.VisibleCalls.Clear();

            fixture.Presenter.ToggleShortcut(LobbyShortcutKind.Players);

            Assert.That(fixture.Shortcuts.IsOpen, Is.False);
            Assert.That(fixture.Menu.VisibleCalls, Has.No.Member(true));
        }

        [Test]
        public void ToggleShortcut_Character_WhileOverlayOpen_ClosesIt()
        {
            using var fixture = new Fixture();
            var closed = false;
            fixture.Presenter.Start();
            fixture.Presenter.OpenCharacterScreen(() => closed = true, fromWorld: true);
            fixture.Menu.VisibleCalls.Clear();

            fixture.Presenter.ToggleShortcut(LobbyShortcutKind.Character);

            Assert.That(closed, Is.True);
            Assert.That(fixture.Menu.IsOpen, Is.False);
            Assert.That(fixture.Menu.VisibleCalls, Has.No.Member(true));
        }

        [Test]
        public void ToggleShortcut_WhileCharacterOverlayOpen_DoesNotOpenPlayers()
        {
            using var fixture = new Fixture();
            fixture.Presenter.Start();
            fixture.Presenter.OpenCharacterScreen(() => { }, fromWorld: true);

            fixture.Presenter.ToggleShortcut(LobbyShortcutKind.Players);

            Assert.That(fixture.Shortcuts.IsOpen, Is.False);
        }

        [Test]
        public void Escape_WhileCharacterOpenFromWorld_ClosesWithoutLeaving()
        {
            using var fixture = new Fixture();
            var closed = false;
            fixture.Presenter.Start();
            fixture.Presenter.OpenCharacterScreen(() => closed = true, fromWorld: true);
            fixture.Menu.VisibleCalls.Clear();

            fixture.Presenter.HandleEscape();

            Assert.That(closed, Is.True);
            Assert.That(fixture.Left, Is.False);
        }

        [Test]
        public void ToggleShortcut_WhilePlaySettingsOpen_DoesNothing()
        {
            using var fixture = new Fixture();
            fixture.Presenter.Start();
            fixture.Presenter.OpenPlaySettingsFromWorld();

            fixture.Presenter.ToggleShortcut(LobbyShortcutKind.Settings);

            Assert.That(fixture.Shortcuts.IsOpen, Is.False);
        }

        [Test]
        public void Escape_FromTheRoom_OpensSettingsWithoutLeaving()
        {
            using var fixture = new Fixture();
            var requested = 0;
            fixture.Presenter.SettingsOpenRequested += () =>
            {
                requested++;
                fixture.Presenter.OpenSettingsScreen(() => { }, fromWorld: true);
            };
            fixture.Presenter.Start();

            fixture.Presenter.HandleEscape();

            Assert.That(requested, Is.EqualTo(1));
            Assert.That(fixture.Left, Is.False);
            Assert.That(fixture.Menu.IsOpen, Is.False);
            Assert.That(fixture.Shortcuts.IsOpen, Is.False);
        }

        [Test]
        public void Escape_WhileSettingsOpenFromWorld_ClosesWithoutLeaving()
        {
            using var fixture = new Fixture();
            var closed = false;
            fixture.Presenter.Start();
            fixture.Presenter.OpenSettingsScreen(() => closed = true, fromWorld: true);
            fixture.Menu.VisibleCalls.Clear();

            fixture.Presenter.HandleEscape();

            Assert.That(closed, Is.True);
            Assert.That(fixture.Left, Is.False);
        }

        [Test]
        public void SettingsFromWorld_ThenClose_ReturnsToRoomNotMenu()
        {
            using var fixture = new Fixture();
            fixture.Presenter.Start();
            fixture.Presenter.OpenSettingsScreen(() => { }, fromWorld: true);
            fixture.Menu.VisibleCalls.Clear();

            fixture.Presenter.OnScreenClosed();

            Assert.That(fixture.Menu.IsOpen, Is.False);
            Assert.That(fixture.Menu.VisibleCalls, Has.No.Member(true));
        }

        [Test]
        public void SettingsFromWorld_ThenClose_IgnoresUnappliedPlaySettings()
        {
            using var fixture = new Fixture();
            var requested = 0;
            fixture.Presenter.SettingsOpenRequested += () => requested++;
            fixture.Presenter.Start();
            fixture.Presenter.OpenSettingsScreen(() => { }, fromWorld: true);
            fixture.Settings.UnappliedChanges = true;

            fixture.Presenter.OnScreenClosed();
            fixture.Presenter.HandleEscape();

            Assert.That(requested, Is.EqualTo(1));
            Assert.That(fixture.Menu.IsOpen, Is.False);
        }

        [Test]
        public void ObjectPrompts_HideWhileAnyLobbyModalIsOpen()
        {
            Assert.That(LobbyPauseMenuPresenter.ShowsObjectPrompts(false, false), Is.True);
            Assert.That(LobbyPauseMenuPresenter.ShowsObjectPrompts(true, false), Is.False);
            Assert.That(LobbyPauseMenuPresenter.ShowsObjectPrompts(false, true), Is.False);
            Assert.That(LobbyPauseMenuPresenter.ShowsObjectPrompts(true, true), Is.False);
        }

        [Test]
        public void Escape_WhileShortcutOpen_ClosesItWithoutLeaving()
        {
            using var fixture = new Fixture();
            fixture.Presenter.Start();
            fixture.Presenter.ToggleShortcut(LobbyShortcutKind.Players);

            fixture.Presenter.HandleEscape();

            Assert.That(fixture.Shortcuts.IsOpen, Is.False);
            Assert.That(fixture.Left, Is.False);
        }

        [Test]
        public void LeaveRoom_LeavesTheGame()
        {
            using var fixture = new Fixture();
            fixture.Presenter.Start();

            fixture.Presenter.LeaveRoom();

            Assert.That(fixture.Left, Is.True);
            Assert.That(fixture.Menu.IsOpen, Is.False);
        }

        [Test]
        public void StartRequested_WhileSettingsOpenFromWorld_ReturnsToRoom()
        {
            using var fixture = new Fixture();
            fixture.Presenter.Start();
            fixture.Presenter.OpenPlaySettingsFromWorld();
            fixture.Menu.VisibleCalls.Clear();

            fixture.Session.RequestStart();

            Assert.That(fixture.Settings.CloseRequests, Is.EqualTo(1));
            Assert.That(fixture.Menu.IsOpen, Is.False);
            Assert.That(fixture.Menu.VisibleCalls, Has.No.Member(true));
        }

        [Test]
        public void StartRequested_WhileSettingsOpenFromMenu_DoesNotReturnToMenu()
        {
            using var fixture = new Fixture();
            fixture.Presenter.Start();
            fixture.Menu.ClickPlaySettings();
            fixture.Menu.VisibleCalls.Clear();

            fixture.Session.RequestStart();

            Assert.That(fixture.Settings.CloseRequests, Is.EqualTo(1));
            Assert.That(fixture.Menu.IsOpen, Is.False);
            Assert.That(fixture.Menu.VisibleCalls, Has.No.Member(true));
        }

        private sealed class Fixture : IDisposable
        {
            public readonly ShortcutOverlay Shortcuts = new();
            public readonly PauseView Menu = new();
            public readonly SettingsView Settings = new();
            public readonly HostSession Session = new();
            public readonly LobbyExitPresenter Exit = new();
            public readonly LobbyPauseMenuPresenter Presenter;
            public bool Left { get; private set; }

            public Fixture()
            {
                Exit.LeaveRequested += () => Left = true;
                Presenter = new LobbyPauseMenuPresenter(
                    Menu, Settings, Session, Exit, Shortcuts);
            }

            public void Dispose()
            {
                Presenter.Dispose();
                Session.Dispose();
            }
        }

        private sealed class PauseView : ILobbyPauseMenuView
        {
            public readonly List<bool> VisibleCalls = new();
            public event Action StartClicked { add { } remove { } }
            public event Action LeaveClicked { add { } remove { } }
            public event Action ResumeClicked;
            public event Action SettingsClicked;
            public event Action PlaySettingsClicked;
            public bool IsOpen { get; private set; }
            public void SetVisible(bool visible) { IsOpen = visible; VisibleCalls.Add(visible); }
            public void SetStartVisible(bool visible) { }
            public void SetStartEnabled(bool enabled) { }
            public void SetPlaySettingsVisible(bool visible) { }
            public void ClickPlaySettings() => PlaySettingsClicked?.Invoke();
            public void ClickSettings() => SettingsClicked?.Invoke();
            public void ClickResume() => ResumeClicked?.Invoke();
        }

        private sealed class ShortcutOverlay : ILobbyShortcutOverlay
        {
            public event Action CloseRequested;
            public LobbyShortcutKind OpenKind { get; private set; }
            public bool IsOpen => OpenKind != LobbyShortcutKind.None;
            public void Show(LobbyShortcutKind kind) => OpenKind = kind;
            public void Hide() => OpenKind = LobbyShortcutKind.None;
            public void RequestClose()
            {
                if (!IsOpen)
                {
                    return;
                }

                Hide();
                CloseRequested?.Invoke();
            }
        }

        private sealed class SettingsView : IPlaySettingsView
        {
            public int OpenRequests;
            public int CloseRequests;
            public event Action OpenRequested;
            public event Action CloseRequested;
            public event Action CopyRoomCodeRequested { add { } remove { } }
            public event Action InviteRequested { add { } remove { } }
            public event Action CopyPasswordRequested { add { } remove { } }
            public event Action StartRequested { add { } remove { } }
            public event Action ApplyRequested { add { } remove { } }
            public bool UnappliedChanges;
            public bool HasUnappliedChanges => UnappliedChanges;
            public void SetVisible(bool visible) { }
            public void SetEditable(bool editable) { }
            public void SetDraft(PlaySettingsDraft draft) { }
            public void SetParticipantCount(int count) { }
            public void SetUnappliedWarningVisible(bool visible) { }
            public PlaySettingsDraft ReadDraft() =>
                new("방", "CODE", false, null, 6, 3, "playground");
            public void RequestClose()
            {
                CloseRequests++;
                CloseRequested?.Invoke();
            }
            public void RequestOpen() { OpenRequests++; OpenRequested?.Invoke(); }
        }

        private sealed class HostSession : ILobbyHostSession, IDisposable
        {
            private readonly ReactiveProperty<bool> host = new(true);
            private readonly ReactiveProperty<PlaySettingsDraft> settings =
                new(new PlaySettingsDraft("방", "CODE", false, null, 6, 3, "playground"));

            public string LocalPlayerId => "me";
            public ReadOnlyReactiveProperty<bool> IsLocalHost => host;
            public ReadOnlyReactiveProperty<PlaySettingsDraft> Settings => settings;
            public event Action StartRequested;
            public event Action<string> KickRequested { add { } remove { } }
            public event Action<string> HostTransferRequested { add { } remove { } }
            public event Action<PlaySettingsDraft> SettingsApplyRequested { add { } remove { } }
            public void SetLocalHost(bool value) => host.Value = value;
            public void ReplaceSettings(PlaySettingsDraft value) => settings.Value = value;
            public void RequestStart() => StartRequested?.Invoke();
            public void RequestKick(string id) { }
            public void RequestHostTransfer(string id) { }
            public void RequestApplySettings(PlaySettingsDraft value) => settings.Value = value;
            public void Dispose() { host.Dispose(); settings.Dispose(); }
        }
    }
}
