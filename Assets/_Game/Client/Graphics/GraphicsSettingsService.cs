using System;
using Game.Core.Settings;

namespace Game.Client.Graphics
{
    public sealed class GraphicsSettingsService : IGraphicsSettings
    {
        private readonly IGraphicsSettingsStore store;
        private readonly IGraphicsSettingsApplier applier;

        public GraphicsSettingsService(
            IGraphicsSettingsStore store,
            IGraphicsSettingsApplier applier)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.applier = applier ?? throw new ArgumentNullException(nameof(applier));
            Current = store.LoadOrDefault() ?? new GraphicsSettingsState();
            ApplyCurrent();
        }

        public GraphicsSettingsState Current { get; }

        public event Action<GraphicsSettingsState> Changed;

        public bool TrySetQuality(GraphicsQualityPreset quality, out GraphicsSettingsError error)
        {
            if (Current.Quality == quality)
            {
                error = GraphicsSettingsError.None;
                return true;
            }

            if (!Current.TrySetQuality(quality, out error))
            {
                return false;
            }

            PersistAndApply();
            return true;
        }

        public bool TrySetResolution(int index, out GraphicsSettingsError error)
        {
            if (Current.ResolutionIndex == index)
            {
                error = GraphicsSettingsError.None;
                return true;
            }

            if (!Current.TrySetResolution(index, out error))
            {
                return false;
            }

            PersistAndApply();
            return true;
        }

        public bool TrySetDisplayMode(DisplayMode mode, out GraphicsSettingsError error)
        {
            if (Current.DisplayMode == mode)
            {
                error = GraphicsSettingsError.None;
                return true;
            }

            if (!Current.TrySetDisplayMode(mode, out error))
            {
                return false;
            }

            PersistAndApply();
            return true;
        }

        public bool TrySetFrameCap(int index, out GraphicsSettingsError error)
        {
            if (Current.FrameCapIndex == index)
            {
                error = GraphicsSettingsError.None;
                return true;
            }

            if (!Current.TrySetFrameCap(index, out error))
            {
                return false;
            }

            PersistAndApply();
            return true;
        }

        public bool TrySetShadows(ShadowQualityLevel shadows, out GraphicsSettingsError error)
        {
            if (Current.Shadows == shadows)
            {
                error = GraphicsSettingsError.None;
                return true;
            }

            if (!Current.TrySetShadows(shadows, out error))
            {
                return false;
            }

            PersistAndApply();
            return true;
        }

        public bool TrySetEffects(EffectsQualityLevel effects, out GraphicsSettingsError error)
        {
            if (Current.Effects == effects)
            {
                error = GraphicsSettingsError.None;
                return true;
            }

            if (!Current.TrySetEffects(effects, out error))
            {
                return false;
            }

            PersistAndApply();
            return true;
        }

        public bool TrySetAntiAliasing(AntiAliasingMode antiAliasing, out GraphicsSettingsError error)
        {
            if (Current.AntiAliasing == antiAliasing)
            {
                error = GraphicsSettingsError.None;
                return true;
            }

            if (!Current.TrySetAntiAliasing(antiAliasing, out error))
            {
                return false;
            }

            PersistAndApply();
            return true;
        }

        public bool TrySetBrightness(int percent, out GraphicsSettingsError error)
        {
            if (Current.Brightness == percent)
            {
                error = GraphicsSettingsError.None;
                return true;
            }

            if (!Current.TrySetBrightness(percent, out error))
            {
                return false;
            }

            PersistAndApply();
            return true;
        }

        private void PersistAndApply()
        {
            store.Save(Current);
            ApplyCurrent();
            Changed?.Invoke(Current);
        }

        private void ApplyCurrent()
        {
            applier.Apply(Current);
            GraphicsSettingsOutput.Publish(Current);
        }
    }
}
