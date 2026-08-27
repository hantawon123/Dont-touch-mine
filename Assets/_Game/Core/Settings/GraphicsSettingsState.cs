using System;

namespace Game.Core.Settings
{
    public sealed class GraphicsSettingsState
    {
        public const int MinBrightness = 0;
        public const int MaxBrightness = 100;
        public const int DefaultBrightness = 50;
        public const int DefaultResolutionIndex = 1;
        public const int DefaultFrameCapIndex = 1;

        public static readonly GraphicsResolution[] Resolutions =
        {
            new GraphicsResolution(1280, 720),
            new GraphicsResolution(1920, 1080),
            new GraphicsResolution(2560, 1440),
            new GraphicsResolution(3840, 2160)
        };

        public static readonly int[] FrameCaps =
        {
            30, 60, 90, 120, 144, 165, 240, 0
        };

        public GraphicsSettingsState()
            : this(
                GraphicsQualityPreset.High,
                DefaultResolutionIndex,
                DisplayMode.Fullscreen,
                DefaultFrameCapIndex,
                ShadowQualityLevel.High,
                EffectsQualityLevel.High,
                AntiAliasingMode.Taa,
                DefaultBrightness)
        {
        }

        public GraphicsSettingsState(
            GraphicsQualityPreset quality,
            int resolutionIndex,
            DisplayMode displayMode,
            int frameCapIndex,
            ShadowQualityLevel shadows,
            EffectsQualityLevel effects,
            AntiAliasingMode antiAliasing,
            int brightness)
        {
            Quality = ClampQuality(quality);
            ResolutionIndex = ClampIndex(resolutionIndex, Resolutions.Length);
            DisplayMode = ClampDisplayMode(displayMode);
            FrameCapIndex = ClampIndex(frameCapIndex, FrameCaps.Length);
            Shadows = ClampShadows(shadows);
            Effects = ClampEffects(effects);
            AntiAliasing = ClampAntiAliasing(antiAliasing);
            Brightness = ClampBrightness(brightness);
            SyncQualityPreset();
        }

        public GraphicsQualityPreset Quality { get; private set; }

        public int ResolutionIndex { get; private set; }

        public DisplayMode DisplayMode { get; private set; }

        public int FrameCapIndex { get; private set; }

        public ShadowQualityLevel Shadows { get; private set; }

        public EffectsQualityLevel Effects { get; private set; }

        public AntiAliasingMode AntiAliasing { get; private set; }

        public int Brightness { get; private set; }

        public GraphicsResolution Resolution
        {
            get { return Resolutions[ResolutionIndex]; }
        }

        public int GetTargetFrameRate()
        {
            var cap = FrameCaps[FrameCapIndex];
            return cap == 0 ? -1 : cap;
        }

        public float GetExposure()
        {
            return ((Brightness - DefaultBrightness) / (float)DefaultBrightness) * 1.5f;
        }

        public bool TrySetQuality(
            GraphicsQualityPreset quality,
            out GraphicsSettingsError error)
        {
            if (!Enum.IsDefined(typeof(GraphicsQualityPreset), quality))
            {
                error = GraphicsSettingsError.InvalidOption;
                return false;
            }

            Quality = quality;
            if (quality != GraphicsQualityPreset.Custom)
            {
                ApplyPresetDetails(quality);
            }

            error = GraphicsSettingsError.None;
            return true;
        }

        public bool TrySetResolution(int index, out GraphicsSettingsError error)
        {
            return TrySetIndex(index, Resolutions.Length, value => ResolutionIndex = value, out error);
        }

        public bool TrySetDisplayMode(DisplayMode mode, out GraphicsSettingsError error)
        {
            if (!Enum.IsDefined(typeof(DisplayMode), mode))
            {
                error = GraphicsSettingsError.InvalidOption;
                return false;
            }

            DisplayMode = mode;
            error = GraphicsSettingsError.None;
            return true;
        }

        public bool TrySetFrameCap(int index, out GraphicsSettingsError error)
        {
            return TrySetIndex(index, FrameCaps.Length, value => FrameCapIndex = value, out error);
        }

        public bool TrySetShadows(ShadowQualityLevel shadows, out GraphicsSettingsError error)
        {
            if (!Enum.IsDefined(typeof(ShadowQualityLevel), shadows))
            {
                error = GraphicsSettingsError.InvalidOption;
                return false;
            }

            Shadows = shadows;
            SyncQualityPreset();
            error = GraphicsSettingsError.None;
            return true;
        }

        public bool TrySetEffects(EffectsQualityLevel effects, out GraphicsSettingsError error)
        {
            if (!Enum.IsDefined(typeof(EffectsQualityLevel), effects))
            {
                error = GraphicsSettingsError.InvalidOption;
                return false;
            }

            Effects = effects;
            SyncQualityPreset();
            error = GraphicsSettingsError.None;
            return true;
        }

        public bool TrySetAntiAliasing(AntiAliasingMode antiAliasing, out GraphicsSettingsError error)
        {
            if (!Enum.IsDefined(typeof(AntiAliasingMode), antiAliasing))
            {
                error = GraphicsSettingsError.InvalidOption;
                return false;
            }

            AntiAliasing = antiAliasing;
            SyncQualityPreset();
            error = GraphicsSettingsError.None;
            return true;
        }

        public bool TrySetBrightness(int percent, out GraphicsSettingsError error)
        {
            if (percent < MinBrightness || percent > MaxBrightness)
            {
                error = GraphicsSettingsError.InvalidBrightness;
                return false;
            }

            Brightness = percent;
            error = GraphicsSettingsError.None;
            return true;
        }

        public static void GetPresetDetails(
            GraphicsQualityPreset preset,
            out ShadowQualityLevel shadows,
            out EffectsQualityLevel effects,
            out AntiAliasingMode antiAliasing)
        {
            switch (preset)
            {
                case GraphicsQualityPreset.VeryLow:
                    shadows = ShadowQualityLevel.Off;
                    effects = EffectsQualityLevel.Low;
                    antiAliasing = AntiAliasingMode.Off;
                    return;
                case GraphicsQualityPreset.Low:
                    shadows = ShadowQualityLevel.Low;
                    effects = EffectsQualityLevel.Low;
                    antiAliasing = AntiAliasingMode.Fxaa;
                    return;
                case GraphicsQualityPreset.Medium:
                    shadows = ShadowQualityLevel.Medium;
                    effects = EffectsQualityLevel.Medium;
                    antiAliasing = AntiAliasingMode.Smaa;
                    return;
                case GraphicsQualityPreset.High:
                    shadows = ShadowQualityLevel.High;
                    effects = EffectsQualityLevel.High;
                    antiAliasing = AntiAliasingMode.Taa;
                    return;
                case GraphicsQualityPreset.VeryHigh:
                    shadows = ShadowQualityLevel.VeryHigh;
                    effects = EffectsQualityLevel.VeryHigh;
                    antiAliasing = AntiAliasingMode.Taa;
                    return;
                default:
                    shadows = ShadowQualityLevel.High;
                    effects = EffectsQualityLevel.High;
                    antiAliasing = AntiAliasingMode.Taa;
                    return;
            }
        }

        private void ApplyPresetDetails(GraphicsQualityPreset preset)
        {
            GetPresetDetails(preset, out var shadows, out var effects, out var antiAliasing);
            Shadows = shadows;
            Effects = effects;
            AntiAliasing = antiAliasing;
        }

        private void SyncQualityPreset()
        {
            foreach (GraphicsQualityPreset preset in Enum.GetValues(typeof(GraphicsQualityPreset)))
            {
                if (preset == GraphicsQualityPreset.Custom)
                {
                    continue;
                }

                GetPresetDetails(preset, out var shadows, out var effects, out var antiAliasing);
                if (Shadows == shadows && Effects == effects && AntiAliasing == antiAliasing)
                {
                    Quality = preset;
                    return;
                }
            }

            Quality = GraphicsQualityPreset.Custom;
        }

        private static bool TrySetIndex(
            int index,
            int length,
            Action<int> assign,
            out GraphicsSettingsError error)
        {
            if (index < 0 || index >= length)
            {
                error = GraphicsSettingsError.InvalidOption;
                return false;
            }

            assign(index);
            error = GraphicsSettingsError.None;
            return true;
        }

        private static GraphicsQualityPreset ClampQuality(GraphicsQualityPreset quality)
        {
            return Enum.IsDefined(typeof(GraphicsQualityPreset), quality)
                ? quality
                : GraphicsQualityPreset.High;
        }

        private static DisplayMode ClampDisplayMode(DisplayMode mode)
        {
            return Enum.IsDefined(typeof(DisplayMode), mode) ? mode : DisplayMode.Fullscreen;
        }

        private static ShadowQualityLevel ClampShadows(ShadowQualityLevel shadows)
        {
            return Enum.IsDefined(typeof(ShadowQualityLevel), shadows)
                ? shadows
                : ShadowQualityLevel.High;
        }

        private static EffectsQualityLevel ClampEffects(EffectsQualityLevel effects)
        {
            return Enum.IsDefined(typeof(EffectsQualityLevel), effects)
                ? effects
                : EffectsQualityLevel.High;
        }

        private static AntiAliasingMode ClampAntiAliasing(AntiAliasingMode antiAliasing)
        {
            return Enum.IsDefined(typeof(AntiAliasingMode), antiAliasing)
                ? antiAliasing
                : AntiAliasingMode.Taa;
        }

        private static int ClampIndex(int index, int length)
        {
            if (index < 0)
            {
                return 0;
            }

            if (index >= length)
            {
                return length - 1;
            }

            return index;
        }

        private static int ClampBrightness(int percent)
        {
            if (percent < MinBrightness)
            {
                return MinBrightness;
            }

            if (percent > MaxBrightness)
            {
                return MaxBrightness;
            }

            return percent;
        }
    }
}
