using Cysharp.Threading.Tasks;

namespace Game.Client.Common
{
    public interface ILoadingOverlay
    {
        bool IsPresented { get; }
        void Show();
        void Hide();
        void HideImmediate();
        void Attach(ILoadingView view);
    }

    /// <summary>
    /// Application-wide handle for the loading cover. The live view is attached
    /// once the project scope builds it; tests and early calls no-op until then.
    /// </summary>
    public sealed class LoadingOverlay : ILoadingOverlay
    {
        private ILoadingView view;
        private bool shown;

        public bool IsPresented => view != null ? view.IsPresented : shown;

        public void Attach(ILoadingView view)
        {
            this.view = view;
            if (view == null)
            {
                return;
            }

            if (shown)
            {
                view.Show();
                return;
            }

            view.HideImmediate();
        }

        public void Show()
        {
            shown = true;
            view?.Show();
        }

        /// <remarks>
        /// Forgets the pending show as well as hiding the view. The flag is
        /// what <see cref="Attach"/> reads to catch up a cover that was asked
        /// for before there was anything to draw it on, and leaving it set
        /// would put the cover back up the next time a view arrives — over a
        /// screen nobody is loading. <see cref="HideImmediate"/> already
        /// cleared it; this did not.
        /// </remarks>
        public void Hide()
        {
            shown = false;
            view?.Hide();
        }

        public void HideImmediate()
        {
            shown = false;
            if (view != null)
            {
                view.HideImmediate();
            }
        }
    }

    public static class LoadingOverlayPaint
    {
        public static async UniTask ShowPainted(this ILoadingOverlay overlay)
        {
            overlay.Show();
            await SceneLoadSlicer.YieldFrame();
            await UniTask.WaitUntil(() => WebLoadingAnimation.IsReady);
        }
    }
}
