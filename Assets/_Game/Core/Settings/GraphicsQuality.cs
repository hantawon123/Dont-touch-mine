namespace Game.Core.Settings
{
    public enum GraphicsSettingsError
    {
        None,
        InvalidOption,
        InvalidBrightness
    }

    public enum GraphicsQualityPreset
    {
        VeryLow,
        Low,
        Medium,
        High,
        VeryHigh,
        Custom
    }

    public enum DisplayMode
    {
        Fullscreen,
        Windowed,
        Borderless
    }

    public enum ShadowQualityLevel
    {
        Off,
        Low,
        Medium,
        High,
        VeryHigh
    }

    public enum EffectsQualityLevel
    {
        Low,
        Medium,
        High,
        VeryHigh
    }

    public enum AntiAliasingMode
    {
        Off,
        Fxaa,
        Smaa,
        Taa
    }

    public enum GraphicsSetting
    {
        Quality,
        Resolution,
        DisplayMode,
        FrameCap,
        Shadows,
        Effects,
        AntiAliasing
    }

    public readonly struct GraphicsResolution
    {
        public GraphicsResolution(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public int Width { get; }

        public int Height { get; }
    }
}
