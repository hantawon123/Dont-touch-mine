using System.Collections.Generic;
using Game.Client.Lobby;
using Game.Core.Lobby;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class KeyGuidePresenterTests
    {
        [Test]
        public void Start_HidesPanelAndAppliesBindings()
        {
            var view = new FakeKeyGuideView();
            var bindings = new[]
            {
                new ControlKeyBinding("이동", "W A S D"),
                new ControlKeyBinding("점프", "Space"),
            };
            using var presenter = new KeyGuidePresenter(view, bindings);

            presenter.Start();

            Assert.That(view.IsVisible, Is.False);
            Assert.That(view.Entries, Is.SameAs(bindings));
            Assert.That(presenter.IsVisible, Is.False);
        }

        [Test]
        public void OpenRequested_TogglesVisibility()
        {
            var view = new FakeKeyGuideView();
            using var presenter = new KeyGuidePresenter(view, ControlKeyGuide.Bindings);

            presenter.Start();
            view.RaiseOpen();
            Assert.That(view.IsVisible, Is.True);
            Assert.That(presenter.IsVisible, Is.True);

            view.RaiseOpen();
            Assert.That(view.IsVisible, Is.False);
            Assert.That(presenter.IsVisible, Is.False);
        }

        [Test]
        public void CloseRequested_HidesVisiblePanel()
        {
            var view = new FakeKeyGuideView();
            using var presenter = new KeyGuidePresenter(view, ControlKeyGuide.Bindings);

            presenter.Start();
            view.RaiseOpen();
            view.RaiseClose();

            Assert.That(view.IsVisible, Is.False);
            Assert.That(presenter.IsVisible, Is.False);
        }

        private sealed class FakeKeyGuideView : IKeyGuideView
        {
            public bool IsVisible { get; private set; }
            public IReadOnlyList<ControlKeyBinding> Entries { get; private set; }

            public event System.Action OpenRequested;
            public event System.Action CloseRequested;

            public void SetVisible(bool visible) => IsVisible = visible;

            public void SetEntries(IReadOnlyList<ControlKeyBinding> bindings) =>
                Entries = bindings;

            public void RequestClose() => CloseRequested?.Invoke();

            public void RaiseOpen() => OpenRequested?.Invoke();

            public void RaiseClose() => CloseRequested?.Invoke();
        }
    }
}
