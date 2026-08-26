using System;
using System.Collections.Generic;
using Game.Core.Lobby;
using VContainer.Unity;

namespace Game.Client.Lobby
{
    public sealed class KeyGuidePresenter : IStartable, IDisposable
    {
        private readonly IKeyGuideView view;
        private readonly IReadOnlyList<ControlKeyBinding> bindings;
        private bool isVisible;

        public KeyGuidePresenter(
            IKeyGuideView view,
            IReadOnlyList<ControlKeyBinding> bindings)
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
        }

        public bool IsVisible => isVisible;

        public void Start()
        {
            view.SetEntries(bindings);
            view.SetVisible(false);
            view.OpenRequested += Toggle;
            view.CloseRequested += Close;
        }

        public void Dispose()
        {
            view.OpenRequested -= Toggle;
            view.CloseRequested -= Close;
        }

        private void Toggle()
        {
            if (isVisible)
            {
                Close();
                return;
            }

            isVisible = true;
            view.SetVisible(true);
        }

        private void Close()
        {
            if (!isVisible)
            {
                return;
            }

            isVisible = false;
            view.SetVisible(false);
        }
    }
}
