using System;
using System.Collections.Generic;
using Game.Client.Home;
using Game.Core.Home;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class HomeMenuPresenterTests
    {
        [Test]
        public void Presenter_BindsProfileAndForwardsEveryMenuAction()
        {
            var profile = new PlayerProfile("사용자닉네임", 1);
            var menu = new HomeMenuSystem();
            var view = new FakeHomeMenuView();
            var host = new FakeHomeApplicationHost();
            var requestedActions = new List<HomeMenuAction>();
            menu.ActionRequested += requestedActions.Add;

            using (var presenter = new HomeMenuPresenter(profile, menu, view, host))
            {
                presenter.Start();
                Assert.That(view.Nickname, Is.EqualTo("사용자닉네임"));
                Assert.That(view.Level, Is.EqualTo(1));

                Assert.That(profile.TryChangeNickname("새닉네임", out _), Is.True);
                Assert.That(profile.TryUpdateLevel(3, out _), Is.True);
                Assert.That(view.Nickname, Is.EqualTo("새닉네임"));
                Assert.That(view.Level, Is.EqualTo(3));

                foreach (HomeMenuAction action in Enum.GetValues(typeof(HomeMenuAction)))
                {
                    view.Raise(action);
                }
            }

            Assert.That(
                requestedActions,
                Is.EqualTo(Enum.GetValues(typeof(HomeMenuAction))));
            Assert.That(host.QuitCount, Is.EqualTo(1));

            view.Raise(HomeMenuAction.FindRoom);
            Assert.That(requestedActions.Count, Is.EqualTo(Enum.GetValues(typeof(HomeMenuAction)).Length));
            Assert.That(host.QuitCount, Is.EqualTo(1));
        }

        [Test]
        public void Presenter_RequiresDependencies()
        {
            var profile = new PlayerProfile("사용자닉네임", 1);
            var menu = new HomeMenuSystem();
            var view = new FakeHomeMenuView();
            var host = new FakeHomeApplicationHost();

            Assert.That(
                () => new HomeMenuPresenter(null, menu, view, host),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => new HomeMenuPresenter(profile, null, view, host),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => new HomeMenuPresenter(profile, menu, null, host),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => new HomeMenuPresenter(profile, menu, view, null),
                Throws.TypeOf<ArgumentNullException>());
        }

        private sealed class FakeHomeMenuView : IHomeMenuView
        {
            public string Nickname { get; private set; }

            public int Level { get; private set; }

            public event Action<HomeMenuAction> ActionClicked;

            public void SetNickname(string nickname)
            {
                Nickname = nickname;
            }

            public void SetLevel(int level)
            {
                Level = level;
            }

            public void Raise(HomeMenuAction action)
            {
                ActionClicked?.Invoke(action);
            }
        }

        private sealed class FakeHomeApplicationHost : IHomeApplicationHost
        {
            public int QuitCount { get; private set; }

            public void Quit()
            {
                QuitCount++;
            }
        }
    }
}
