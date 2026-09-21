using System;
using Game.Client.Home;
using Game.Client.Lobby;
using Game.Client.Settings;
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
        [TestCase(false)]
        [TestCase(true)]
        public void Host_UnchangedOrRevertedDraft_DoesNotOverwriteNewSessionSettings(bool revertEdit)
        {
            using var session = new HostSession();
            session.SetLocalHost(true);
            var view = new SettingsView();
            var menu = new PauseView();
            using var presenter = new PlaySettingsPresenter(session, view, menu);
            presenter.Start();
            menu.OpenSettings();
            if (revertEdit) { view.Draft = Draft(3); view.Draft = Draft(6); }
            session.ReplaceSettings(Draft(5));
            view.RequestClose();
            Assert.That(session.ApplyCount, Is.Zero);
            Assert.That(session.Settings.CurrentValue.MaxPlayers, Is.EqualTo(5));
            menu.OpenSettings();
            Assert.That(view.Draft.MaxPlayers, Is.EqualTo(5));
        }

        [Test]
        public void Host_DraftAlreadyAccepted_DoesNotApplyAgain()
        {
            using var session = new HostSession();
            session.SetLocalHost(true);
            var view = new SettingsView();
            var menu = new PauseView();
            using var presenter = new PlaySettingsPresenter(session, view, menu);
            presenter.Start();
            menu.OpenSettings();
            view.Draft = Draft(4);
            session.ReplaceSettings(Draft(4));
            view.RequestApply();
            Assert.That(session.ApplyCount, Is.Zero);
        }

        [Test]
        public void Host_StartRequested_ClosesThenRequestsStart()
        {
            using var session = new HostSession();
            session.SetLocalHost(true);
            var view = new SettingsView();
            var menu = new PauseView();
            using var presenter = new PlaySettingsPresenter(session, view, menu);
            presenter.Start();
            menu.OpenSettings();
            view.RequestStart();
            Assert.That(view.Visible, Is.False);
            Assert.That(session.ApplyCount, Is.Zero);
            Assert.That(session.StartCount, Is.EqualTo(1));
        }

        [Test]
        public void Host_PushesParticipantCountToView()
        {
            using var session = new HostSession();
            session.SetLocalHost(true);
            using var list = new LobbyParticipantList(new[]
            {
                new LobbyParticipant("1", "방장", true),
            });
            var view = new SettingsView();
            var menu = new PauseView();
            using var presenter = new PlaySettingsPresenter(session, view, menu, list);
            presenter.Start();
            Assert.That(view.ParticipantCount, Is.EqualTo(1));
            list.Replace(new[]
            {
                new LobbyParticipant("1", "방장", true),
                new LobbyParticipant("2", "게스트", false),
            });
            Assert.That(view.ParticipantCount, Is.EqualTo(2));
        }

        [Test]
        public void Host_StartOrCloseWithUnappliedChanges_WarnsAndDoesNotApply()
        {
            using var session = new HostSession();
            session.SetLocalHost(true);
            var view = new SettingsView();
            var menu = new PauseView();
            using var presenter = new PlaySettingsPresenter(session, view, menu);
            presenter.Start();
            menu.OpenSettings();
            view.Draft = Draft(4);
            view.RequestStart();
            Assert.That(view.Visible, Is.True);
            Assert.That(view.WarningVisible, Is.True);
            Assert.That(session.ApplyCount, Is.Zero);
            Assert.That(session.StartCount, Is.Zero);
            view.RequestClose();
            Assert.That(view.Visible, Is.True);
            Assert.That(view.WarningVisible, Is.True);
            Assert.That(session.ApplyCount, Is.Zero);
            view.RequestApply();
            Assert.That(view.WarningVisible, Is.False);
            Assert.That(session.ApplyCount, Is.EqualTo(1));
            Assert.That(session.Settings.CurrentValue.MaxPlayers, Is.EqualTo(4));
            view.RequestStart();
            Assert.That(view.Visible, Is.False);
            Assert.That(session.StartCount, Is.EqualTo(1));
        }

        [Test]
        public void Guest_StartRequested_ClosesWithoutStarting()
        {
            using var session = new HostSession();
            session.SetLocalHost(false);
            var view = new SettingsView();
            var menu = new PauseView();
            using var presenter = new PlaySettingsPresenter(session, view, menu);
            presenter.Start();
            menu.OpenSettings();
            view.RequestStart();
            Assert.That(view.Visible, Is.False);
            Assert.That(session.StartCount, Is.Zero);
        }

        [Test]
        public void Host_RepeatedOpenPreservesEdit_AndRepeatedCloseAppliesOnce()
        {
            using var session = new HostSession();
            session.SetLocalHost(true);
            var view = new SettingsView();
            var menu = new PauseView();
            using var presenter = new PlaySettingsPresenter(session, view, menu);
            presenter.Start();
            menu.OpenSettings();
            view.Draft = Draft(3);
            menu.OpenSettings();
            Assert.That(view.Draft.MaxPlayers, Is.EqualTo(3));
            view.RequestClose();
            Assert.That(view.Visible, Is.True);
            Assert.That(session.ApplyCount, Is.Zero);
            view.RequestApply();
            Assert.That(session.ApplyCount, Is.EqualTo(1));
            Assert.That(session.Settings.CurrentValue.MaxPlayers, Is.EqualTo(3));
            view.RequestClose();
            Assert.That(view.Visible, Is.False);
            Assert.That(session.ApplyCount, Is.EqualTo(1));
        }

        [Test]
        public void Host_RuleOnlyChange_IsApplied()
        {
            using var session = new HostSession();
            session.SetLocalHost(true);
            var view = new SettingsView();
            var menu = new PauseView();
            using var presenter = new PlaySettingsPresenter(session, view, menu);
            presenter.Start();
            menu.OpenSettings();
            Assert.That(MatchRuleSettings.TryCreate(60, 10, 1.5f, 5, "food", out var rules, out _), Is.True);
            view.Draft = new PlaySettingsDraft("방", "CODE", false, null, 6, 3, "playground", rules);
            view.RequestClose();
            Assert.That(session.ApplyCount, Is.Zero);
            view.RequestApply();
            Assert.That(session.ApplyCount, Is.EqualTo(1));
            Assert.That(session.Settings.CurrentValue.MatchRules, Is.EqualTo(rules));
        }

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
            Assert.That(view.Visible, Is.True);
            Assert.That(session.ApplyCount, Is.Zero);
            Assert.That(view.Draft.MaxPlayers, Is.EqualTo(3));
            menu.OpenSettings();
            view.Draft = Draft(6);
            session.SetLocalHost(false);
            Assert.That(view.Editable, Is.False);
            Assert.That(view.Draft.MaxPlayers, Is.EqualTo(5));
            view.RequestClose();
            Assert.That(view.Visible, Is.False);
            Assert.That(session.ApplyCount, Is.Zero);
        }

        [Test]
        public void RealView_ReadOnlyBlocksChanges_AndPreservesUnexposedRules()
        {
            var root = new GameObject("Settings view test");
            var panel = new GameObject("PlaySettingsPanel", typeof(RectTransform));
            panel.transform.SetParent(root.transform, false);
            root.SetActive(false);
            try
            {
                var view = root.AddComponent<PlaySettingsView>();
                var serialized = new SerializedObject(view);
                serialized.FindProperty("panel").objectReferenceValue = panel;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                root.SetActive(true);
                typeof(PlaySettingsView).GetMethod("OnEnable",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(view, null);
                var plusField = typeof(PlaySettingsView).GetField(
                    "maxPlayersPlusButton",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var button = (Button)plusField.GetValue(view);
                MatchRuleSettings.TryCreate(60, 10, 1.5f, 5, "food", out var rules, out _);
                view.SetDraft(new PlaySettingsDraft("방", "CODE", false, null, 4, 3, "playground", rules));
                view.SetEditable(false);
                Assert.That(button.interactable, Is.False);
                button.onClick.Invoke();
                Assert.That(view.ReadDraft().MaxPlayers, Is.EqualTo(4));
                view.SetEditable(true);
                Assert.That(button.interactable, Is.True);
                button.onClick.Invoke();
                Assert.That(view.ReadDraft().MaxPlayers, Is.EqualTo(5));
                Assert.That(view.ReadDraft().MatchRules, Is.EqualTo(rules));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void RealView_RuleEditingEnforcesBoundsAndAuthority_AndKeepsCategory()
        {
            var root = new GameObject("Rule editing test");
            var panel = new GameObject("PlaySettingsPanel", typeof(RectTransform));
            panel.transform.SetParent(root.transform, false);
            root.SetActive(false);
            try
            {
                var view = root.AddComponent<PlaySettingsView>();
                var serialized = new SerializedObject(view);
                serialized.FindProperty("panel").objectReferenceValue = panel;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                root.SetActive(true);
                typeof(PlaySettingsView).GetMethod("OnEnable",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(view, null);
                Assert.That(
                    MatchRuleSettings.TryCreate(10, 1, 1f, 1, "fruit", out var rules, out _),
                    Is.True);
                view.SetDraft(new PlaySettingsDraft("방", "CODE", false, null, 6, 3, "playground", rules));
                var change = typeof(PlaySettingsView).GetMethod("ChangeRule",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                void Step(int field, int direction) => change.Invoke(view, new object[] { field, direction });
                view.SetEditable(false);
                for (var field = 0; field < 4; field++) Step(field, 1);
                Assert.That(view.ReadDraft().MatchRules, Is.EqualTo(rules));
                view.SetEditable(true);
                Step(0, 1);
                Assert.That(view.ReadDraft().MatchRules.HidingDurationSeconds, Is.EqualTo(15));
                Step(1, 1);
                Assert.That(view.ReadDraft().MatchRules.SearchingDurationSeconds, Is.EqualTo(120));
                Step(0, -1);
                Step(1, -1);
                for (var field = 0; field < 4; field++) Step(field, -1);
                Assert.That(view.ReadDraft().MatchRules, Is.EqualTo(rules));
                foreach (var speed in new[] { 1.5f, 2f, 3f })
                {
                    Step(2, 1);
                    Assert.That(view.ReadDraft().MatchRules.SprintMultiplier, Is.EqualTo(speed));
                }
                for (var n = 0; n < 125; n++)
                    for (var field = 0; field < 4; field++) Step(field, 1);
                var actual = view.ReadDraft().MatchRules;
                Assert.That(actual.HidingDurationSeconds, Is.EqualTo(120));
                Assert.That(actual.SearchingDurationMinutes, Is.EqualTo(15));
                Assert.That(actual.SprintMultiplier, Is.EqualTo(3));
                Assert.That(actual.StunHitCount, Is.EqualTo(10));
                Assert.That(actual.CategoryId, Is.EqualTo("fruit"));
                view.SetDraft(new PlaySettingsDraft("방", "CODE", false, null, 6, 3, "playground", rules));
                Assert.That(view.ReadDraft().MatchRules, Is.EqualTo(rules));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        [Test]
        public void RealView_ApplyStaysDisabledUntilDraftChanges()
        {
            var root = new GameObject("Apply chrome test");
            var panel = new GameObject("PlaySettingsPanel", typeof(RectTransform));
            panel.transform.SetParent(root.transform, false);
            root.SetActive(false);
            try
            {
                var view = root.AddComponent<PlaySettingsView>();
                var serialized = new SerializedObject(view);
                serialized.FindProperty("panel").objectReferenceValue = panel;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                root.SetActive(true);
                typeof(PlaySettingsView).GetMethod("OnEnable",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(view, null);
                view.SetDraft(Draft(4));
                view.SetEditable(true);
                var applyField = typeof(PlaySettingsView).GetField(
                    "applyButton",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var applyFillField = typeof(PlaySettingsView).GetField(
                    "applyFill",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var applyWarningField = typeof(PlaySettingsView).GetField(
                    "applyWarning",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var apply = (Button)applyField.GetValue(view);
                var fill = (Image)applyFillField.GetValue(view);
                var warning = (Text)applyWarningField.GetValue(view);
                Assert.That(view.HasUnappliedChanges, Is.False);
                Assert.That(apply.interactable, Is.False);
                Assert.That(fill.color, Is.EqualTo(PlaySettingsStyle.Palette.ApplyOffFill));
                var plusField = typeof(PlaySettingsView).GetField(
                    "maxPlayersPlusButton",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                ((Button)plusField.GetValue(view)).onClick.Invoke();
                Assert.That(view.HasUnappliedChanges, Is.True);
                Assert.That(apply.interactable, Is.True);
                Assert.That(fill.color, Is.EqualTo(PlaySettingsStyle.Palette.ApplyFill));
                view.SetUnappliedWarningVisible(true);
                Assert.That(warning.gameObject.activeSelf, Is.True);
                apply.onClick.Invoke();
                view.SetDraft(view.ReadDraft());
                Assert.That(view.HasUnappliedChanges, Is.False);
                Assert.That(apply.interactable, Is.False);
                Assert.That(warning.gameObject.activeSelf, Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void RealView_GuestHidesApplyRevertAndGameStart()
        {
            var root = new GameObject("Guest chrome test", typeof(RectTransform));
            var panel = new GameObject("PlaySettingsPanel", typeof(RectTransform));
            panel.transform.SetParent(root.transform, false);
            root.SetActive(false);
            try
            {
                var view = root.AddComponent<PlaySettingsView>();
                var serialized = new SerializedObject(view);
                serialized.FindProperty("panel").objectReferenceValue = panel;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                root.SetActive(true);
                typeof(PlaySettingsView).GetMethod("OnEnable",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(view, null);
                view.SetDraft(Draft(4));
                view.SetEditable(false);
                view.SetVisible(true);
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var apply = (Button)typeof(PlaySettingsView).GetField("applyButton", flags).GetValue(view);
                var revert = (Button)typeof(PlaySettingsView).GetField("revertButton", flags).GetValue(view);
                var start = (Button)typeof(PlaySettingsView).GetField("gameStartButton", flags).GetValue(view);
                Assert.That(apply.gameObject.activeSelf, Is.False);
                Assert.That(revert.gameObject.activeSelf, Is.False);
                Assert.That(start, Is.Not.Null);
                Assert.That(start.gameObject.activeSelf, Is.False);
                view.SetEditable(true);
                Assert.That(apply.gameObject.activeSelf, Is.True);
                Assert.That(revert.gameObject.activeSelf, Is.True);
                Assert.That(start.gameObject.activeSelf, Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void RealView_RevertRestoresAppliedDraft()
        {
            var root = new GameObject("Revert test");
            var panel = new GameObject("PlaySettingsPanel", typeof(RectTransform));
            panel.transform.SetParent(root.transform, false);
            root.SetActive(false);
            try
            {
                var view = root.AddComponent<PlaySettingsView>();
                var serialized = new SerializedObject(view);
                serialized.FindProperty("panel").objectReferenceValue = panel;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                root.SetActive(true);
                typeof(PlaySettingsView).GetMethod("OnEnable",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(view, null);
                view.SetDraft(Draft(4));
                view.SetEditable(true);
                var plusField = typeof(PlaySettingsView).GetField(
                    "maxPlayersPlusButton",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                ((Button)plusField.GetValue(view)).onClick.Invoke();
                Assert.That(view.ReadDraft().MaxPlayers, Is.EqualTo(5));
                Assert.That(view.HasUnappliedChanges, Is.True);
                var revertField = typeof(PlaySettingsView).GetField(
                    "revertButton",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var revert = (Button)revertField.GetValue(view);
                Assert.That(revert.gameObject.activeSelf, Is.True);
                revert.onClick.Invoke();
                Assert.That(view.ReadDraft().MaxPlayers, Is.EqualTo(4));
                Assert.That(view.HasUnappliedChanges, Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void RealView_DirtyLeave_DoesNotEmitCloseOrStart()
        {
            var root = new GameObject("Dirty leave test");
            var panel = new GameObject("PlaySettingsPanel", typeof(RectTransform));
            panel.transform.SetParent(root.transform, false);
            root.SetActive(false);
            try
            {
                var view = root.AddComponent<PlaySettingsView>();
                var serialized = new SerializedObject(view);
                serialized.FindProperty("panel").objectReferenceValue = panel;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                root.SetActive(true);
                typeof(PlaySettingsView).GetMethod("OnEnable",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(view, null);
                view.SetDraft(Draft(4));
                view.SetEditable(true);
                var closed = 0;
                var started = 0;
                view.CloseRequested += () => closed++;
                view.StartRequested += () => started++;
                var plusField = typeof(PlaySettingsView).GetField(
                    "maxPlayersPlusButton",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                ((Button)plusField.GetValue(view)).onClick.Invoke();
                var warningField = typeof(PlaySettingsView).GetField(
                    "applyWarning",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var warning = (Text)warningField.GetValue(view);
                view.RequestClose();
                Assert.That(closed, Is.Zero);
                Assert.That(warning.gameObject.activeSelf, Is.True);
                typeof(PlaySettingsView).GetMethod("RequestStart",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(view, null);
                Assert.That(started, Is.Zero);
                Assert.That(warning.gameObject.activeSelf, Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void RealView_GameStartMatchesLeaveGamePlate()
        {
            var root = new GameObject("Game start chrome test", typeof(RectTransform));
            var panel = new GameObject("PlaySettingsPanel", typeof(RectTransform));
            panel.transform.SetParent(root.transform, false);
            root.SetActive(false);
            try
            {
                var view = root.AddComponent<PlaySettingsView>();
                var serialized = new SerializedObject(view);
                serialized.FindProperty("panel").objectReferenceValue = panel;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                root.SetActive(true);
                typeof(PlaySettingsView).GetMethod("OnEnable",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(view, null);
                view.SetVisible(true);
                Transform plate = null;
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    if (transform.name == "GameStartButton")
                    {
                        plate = transform;
                        break;
                    }
                }

                Assert.That(plate, Is.Not.Null);
                var rect = plate.GetComponent<RectTransform>();
                Assert.That(rect.sizeDelta, Is.EqualTo(PlaySettingsStyle.Overlay.GameStartSize));
                Assert.That(plate.GetComponent<UiLinearGradient>(), Is.Not.Null);
                Assert.That(plate.GetComponent<HomeLabelPop>(), Is.Not.Null);
                var label = plate.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
                Assert.That(label.text, Is.EqualTo("게임 시작"));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void RealView_GameStartDisabledWhenAlone()
        {
            var root = new GameObject("Game start disabled test", typeof(RectTransform));
            var panel = new GameObject("PlaySettingsPanel", typeof(RectTransform));
            panel.transform.SetParent(root.transform, false);
            root.SetActive(false);
            try
            {
                var view = root.AddComponent<PlaySettingsView>();
                var serialized = new SerializedObject(view);
                serialized.FindProperty("panel").objectReferenceValue = panel;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                root.SetActive(true);
                typeof(PlaySettingsView).GetMethod("OnEnable",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(view, null);
                view.SetDraft(Draft(4));
                view.SetEditable(true);
                view.SetVisible(true);
                view.SetParticipantCount(1);
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var start = (Button)typeof(PlaySettingsView).GetField("gameStartButton", flags).GetValue(view);
                Assert.That(start, Is.Not.Null);
                Assert.That(start.interactable, Is.False);
                Assert.That(
                    start.GetComponent<Image>().color,
                    Is.EqualTo(PlaySettingsStyle.Palette.GameStartOffFill));
                var label = start.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
                Assert.That(label.color, Is.EqualTo(PlaySettingsStyle.Palette.GameStartOffLabel));
                var started = 0;
                view.StartRequested += () => started++;
                start.onClick.Invoke();
                Assert.That(started, Is.Zero);

                view.SetParticipantCount(2);
                Assert.That(start.interactable, Is.True);
                Assert.That(start.GetComponent<Image>().color, Is.EqualTo(Color.white));
                start.onClick.Invoke();
                Assert.That(started, Is.EqualTo(1));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void DurationLabels_FollowSliderSteps()
        {
            Assert.That(PlaySettingsView.FormatHidingDuration(30), Is.EqualTo("30초"));
            Assert.That(PlaySettingsView.FormatSearchingDuration(300), Is.EqualTo("5분"));
            Assert.That(PlaySettingsView.FormatSearchingDuration(90), Is.EqualTo("1분 30초"));
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
            public int StartCount;
            public event Action StartRequested { add { } remove { } }
            public event Action<string> KickRequested { add { } remove { } }
            public event Action<string> HostTransferRequested { add { } remove { } }
            public event Action<PlaySettingsDraft> SettingsApplyRequested { add { } remove { } }
            public void SetLocalHost(bool value) => host.Value = value;
            public void ReplaceSettings(PlaySettingsDraft value) => settings.Value = value;
            public void RequestStart()
            {
                if (!host.CurrentValue)
                {
                    return;
                }

                StartCount++;
            }
            public void RequestKick(string id) { }
            public void RequestHostTransfer(string id) { }
            public void RequestApplySettings(PlaySettingsDraft value) { ApplyCount++; settings.Value = value; }
            public void Dispose() { host.Dispose(); settings.Dispose(); }
        }

        private sealed class SettingsView : IPlaySettingsView
        {
            public bool Visible;
            public bool Editable;
            public bool WarningVisible;
            public PlaySettingsDraft Draft;
            public PlaySettingsDraft Applied;
            public event Action OpenRequested;
            public event Action CloseRequested;
            public event Action ApplyRequested;
            public void RequestOpen() => OpenRequested?.Invoke();
            public event Action CopyRoomCodeRequested { add { } remove { } }
            public event Action InviteRequested { add { } remove { } }
            public event Action CopyPasswordRequested { add { } remove { } }
            public event Action StartRequested;
            public bool HasUnappliedChanges => Editable && !Draft.Equals(Applied);
            public void RequestStart() => StartRequested?.Invoke();
            public void RequestApply() => ApplyRequested?.Invoke();
            public void SetVisible(bool value) => Visible = value;
            public void SetEditable(bool value) => Editable = value;
            public void SetDraft(PlaySettingsDraft value)
            {
                Draft = value;
                Applied = value;
                WarningVisible = false;
            }
            public void SetParticipantCount(int count) => ParticipantCount = count;
            public int ParticipantCount;
            public void SetUnappliedWarningVisible(bool value) => WarningVisible = value;
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
            public bool IsOpen => true;
            public void SetVisible(bool value) { }
            public void SetStartVisible(bool value) { }
            public void SetStartEnabled(bool value) { }
            public void SetPlaySettingsVisible(bool value) { }
            public void OpenSettings() => PlaySettingsClicked?.Invoke();
        }
    }
}
