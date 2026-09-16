using System;
using Game.Client.Lobby;
using Game.Core.Lobby;
using NUnit.Framework;
using R3;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class LobbyMatchInfoPresenterTests
    {
        [Test]
        public void Start_WritesCurrentSessionCategoryAndMap()
        {
            using var session = new HostSession();
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var hud = canvas.AddComponent<LobbyHudView>();
                using var presenter = new LobbyMatchInfoPresenter(session, hud);
                presenter.Start();

                var info = canvas.transform.Find(LobbyMatchInfoView.RootName)
                    .GetComponent<LobbyMatchInfoView>();
                Assert.That(info.CategoryLabel, Is.EqualTo(PlaySettingsCategoryCatalog.Default.Label));
                Assert.That(info.MapLabel, Is.EqualTo("playground"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void SettingsChange_WritesRandomAndPlaygroundLabels()
        {
            using var session = new HostSession();
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var hud = canvas.AddComponent<LobbyHudView>();
                using var presenter = new LobbyMatchInfoPresenter(session, hud);
                presenter.Start();

                Assert.That(
                    MatchRuleSettings.TryCreate(30, 5, 1f, 3, "fruit", out var fruit, out _),
                    Is.True);
                session.ReplaceSettings(new PlaySettingsDraft(
                    "방",
                    "CODE",
                    false,
                    null,
                    6,
                    3,
                    "playground",
                    fruit));

                var info = canvas.transform.Find(LobbyMatchInfoView.RootName)
                    .GetComponent<LobbyMatchInfoView>();
                Assert.That(info.CategoryLabel, Is.EqualTo(PlaySettingsCategoryCatalog.Default.Label));
                Assert.That(info.MapLabel, Is.EqualTo("playground"));

                session.ReplaceSettings(new PlaySettingsDraft(
                    "방",
                    "CODE",
                    false,
                    null,
                    6,
                    3,
                    string.Empty,
                    MatchRuleSettings.Default));
                Assert.That(info.CategoryLabel, Is.EqualTo("랜덤"));
                Assert.That(info.MapLabel, Is.EqualTo("랜덤"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvas);
            }
        }

        private sealed class HostSession : ILobbyHostSession, IDisposable
        {
            private readonly ReactiveProperty<bool> host = new(false);
            private readonly ReactiveProperty<PlaySettingsDraft> settings = new(
                new PlaySettingsDraft("방", "CODE", false, null, 6, 3, "playground"));

            public string LocalPlayerId => "local";
            public ReadOnlyReactiveProperty<bool> IsLocalHost => host;
            public ReadOnlyReactiveProperty<PlaySettingsDraft> Settings => settings;
            public event Action StartRequested { add { } remove { } }
            public event Action<string> KickRequested { add { } remove { } }
            public event Action<string> HostTransferRequested { add { } remove { } }
            public event Action<PlaySettingsDraft> SettingsApplyRequested { add { } remove { } }
            public void SetLocalHost(bool value) => host.Value = value;
            public void ReplaceSettings(PlaySettingsDraft value) => settings.Value = value;
            public void RequestStart() { }
            public void RequestKick(string id) { }
            public void RequestHostTransfer(string id) { }
            public void RequestApplySettings(PlaySettingsDraft value) => settings.Value = value;
            public void Dispose()
            {
                host.Dispose();
                settings.Dispose();
            }
        }
    }
}
