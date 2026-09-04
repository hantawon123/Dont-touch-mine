using System;
using Game.Client.Lobby;
using Game.Core.Lobby;
using NUnit.Framework;
using R3;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Tests.EditMode
{
    public sealed class PlaySettingsPresenterTests
    {
        [Test]
        public void Guest_SeesLiveSettingsWithoutApplyingOnClose()
        {
            using var session = new HostSession();
            var view = new SettingsView();
            var menu = new PauseView();
            using var presenter = new PlaySettingsPresenter(session, view, menu);
            presenter.Start();
            menu.OpenSettings();
            Assert.That(view.Visible, Is.True);
            Assert.That(view.Editable, Is.False);
            session.ReplaceSettings(Draft(4));
            Assert.That(view.Draft.MaxPlayers, Is.EqualTo(4));
            view.RequestClose();
            Assert.That(view.Visible, Is.False);
            Assert.That(session.ApplyCount, Is.Zero);
        }

        [Test]
        public void Host_KeepsUnsavedEditsUntilClose_AndLosingAuthorityDiscardsThem()
        {
            using var session = new HostSession();
            session.SetLocalHost(true);
            var view = new SettingsView();
            var menu = new PauseView();
            using var presenter = new PlaySettingsPresenter(session, view, menu);
            presenter.Start();
            menu.OpenSettings();
            Assert.That(view.Editable, Is.True);
            view.Draft = Draft(3);
            session.ReplaceSettings(Draft(5));
            Assert.That(view.Draft.MaxPlayers, Is.EqualTo(3));
            view.RequestClose();
            Assert.That(session.ApplyCount, Is.EqualTo(1));
            Assert.That(session.Settings.CurrentValue.MaxPlayers, Is.EqualTo(3));
            menu.OpenSettings();
            view.Draft = Draft(6);
            session.SetLocalHost(false);
            Assert.That(view.Editable, Is.False);
            Assert.That(view.Draft.MaxPlayers, Is.EqualTo(3));
            view.RequestClose();
            Assert.That(session.ApplyCount, Is.EqualTo(1));
        }

        [Test]
        public void RealView_ReadOnlyBlocksChanges_AndPreservesUnexposedRules()
        {
            var root = new GameObject("Settings view test");
            root.SetActive(false);
            try
            {
                var view = root.AddComponent<PlaySettingsView>();
                var plus = new GameObject("Plus", typeof(RectTransform), typeof(Button));
                plus.transform.SetParent(root.transform);
                var button = plus.GetComponent<Button>();
                var serialized = new SerializedObject(view);
                serialized.FindProperty("maxPlayersPlusButton").objectReferenceValue = button;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                root.SetActive(true);
                // EditMode does not run lifecycle methods on this non-ExecuteAlways view.
                typeof(PlaySettingsView).GetMethod("OnEnable",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(view, null);
                MatchRuleSettings.TryCreate(60, 10, 1.5f, 5, "food", out var rules, out _);
                view.SetDraft(new PlaySettingsDraft("방", "CODE", false, null, 4, 3, "playground", rules));
                view.SetEditable(false);
                Assert.That(button.interactable, Is.False);
                button.onClick.Invoke(); // Even an invoked callback cannot mutate a read-only draft.
                Assert.That(view.ReadDraft().MaxPlayers, Is.EqualTo(4));
                view.SetEditable(true);
                Assert.That(button.interactable, Is.True);
                button.onClick.Invoke();
                Assert.That(view.ReadDraft().MaxPlayers, Is.EqualTo(5));
                Assert.That(view.ReadDraft().MatchRules, Is.EqualTo(rules));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static PlaySettingsDraft Draft(int capacity) =>
            new("방", "CODE", false, null, capacity, 3, "playground");

        private sealed class HostSession : ILobbyHostSession, IDisposable
        {
            private readonly ReactiveProperty<bool> host = new(false);
            private readonly ReactiveProperty<PlaySettingsDraft> settings = new(Draft(6));
            public string LocalPlayerId => "local";
            public ReadOnlyReactiveProperty<bool> IsLocalHost => host;
            public ReadOnlyReactiveProperty<PlaySettingsDraft> Settings => settings;
            public int ApplyCount;
            public event Action StartRequested { add { } remove { } }
            public event Action<string> KickRequested { add { } remove { } }
            public event Action<string> HostTransferRequested { add { } remove { } }
            public event Action<PlaySettingsDraft> SettingsApplyRequested { add { } remove { } }
            public void SetLocalHost(bool value) => host.Value = value;
            public void ReplaceSettings(PlaySettingsDraft value) => settings.Value = value;
            public void RequestStart() { }
            public void RequestKick(string id) { }
            public void RequestHostTransfer(string id) { }
            public void RequestApplySettings(PlaySettingsDraft value) { ApplyCount++; settings.Value = value; }
            public void Dispose() { host.Dispose(); settings.Dispose(); }
        }

        private sealed class SettingsView : IPlaySettingsView
        {
            public bool Visible;
            public bool Editable;
            public PlaySettingsDraft Draft;
            public event Action OpenRequested { add { } remove { } }
            public event Action CloseRequested;
            public event Action CopyRoomCodeRequested { add { } remove { } }
            public event Action InviteRequested { add { } remove { } }
            public event Action CopyPasswordRequested { add { } remove { } }
            public void SetVisible(bool value) => Visible = value;
            public void SetEditable(bool value) => Editable = value;
            public void SetDraft(PlaySettingsDraft value) => Draft = value;
            public PlaySettingsDraft ReadDraft() => Draft;
            public void RequestClose() => CloseRequested?.Invoke();
        }

        private sealed class PauseView : ILobbyPauseMenuView
        {
            public event Action StartClicked { add { } remove { } }
            public event Action LeaveClicked { add { } remove { } }
            public event Action ResumeClicked { add { } remove { } }
            public event Action SettingsClicked { add { } remove { } }
            public event Action PlaySettingsClicked;
            public event Action KeyGuideClicked { add { } remove { } }
            public bool IsOpen => true;
            public void SetVisible(bool value) { }
            public void SetStartVisible(bool value) { }
            public void SetPlaySettingsVisible(bool value) { }
            public void OpenSettings() => PlaySettingsClicked?.Invoke();
        }
    }
}
