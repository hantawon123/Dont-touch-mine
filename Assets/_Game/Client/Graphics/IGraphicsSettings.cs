using System;
using Game.Core.Settings;

namespace Game.Client.Graphics
{
    public interface IGraphicsSettings
    {
        GraphicsSettingsState Current { get; }

        event Action<GraphicsSettingsState> Changed;

        bool TrySetQuality(GraphicsQualityPreset quality, out GraphicsSettingsError error);

        bool TrySetResolution(int index, out GraphicsSettingsError error);

        bool TrySetDisplayMode(DisplayMode mode, out GraphicsSettingsError error);

        bool TrySetFrameCap(int index, out GraphicsSettingsError error);

        bool TrySetShadows(ShadowQualityLevel shadows, out GraphicsSettingsError error);

        bool TrySetEffects(EffectsQualityLevel effects, out GraphicsSettingsError error);

        bool TrySetAntiAliasing(AntiAliasingMode antiAliasing, out GraphicsSettingsError error);

        bool TrySetBrightness(int percent, out GraphicsSettingsError error);
    }
}
