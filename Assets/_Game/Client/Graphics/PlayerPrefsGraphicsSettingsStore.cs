using System;
using Game.Core.Settings;
using UnityEngine;

namespace Game.Client.Graphics
{
    public sealed class PlayerPrefsGraphicsSettingsStore : IGraphicsSettingsStore
    {
        private const string QualityKey = "Game.Graphics.Quality";
        private const string ResolutionKey = "Game.Graphics.Resolution";
        private const string DisplayModeKey = "Game.Graphics.DisplayMode";
        private const string FrameCapKey = "Game.Graphics.FrameCap";
        private const string ShadowsKey = "Game.Graphics.Shadows";
        private const string EffectsKey = "Game.Graphics.Effects";
        private const string AntiAliasingKey = "Game.Graphics.AntiAliasing";
        private const string BrightnessKey = "Game.Graphics.Brightness";

        public GraphicsSettingsState LoadOrDefault()
        {
            return new GraphicsSettingsState(
                (GraphicsQualityPreset)PlayerPrefs.GetInt(
                    QualityKey,
                    (int)GraphicsQualityPreset.High),
                PlayerPrefs.GetInt(ResolutionKey, GraphicsSettingsState.DefaultResolutionIndex),
                (DisplayMode)PlayerPrefs.GetInt(DisplayModeKey, (int)DisplayMode.Fullscreen),
                PlayerPrefs.GetInt(FrameCapKey, GraphicsSettingsState.DefaultFrameCapIndex),
                (ShadowQualityLevel)PlayerPrefs.GetInt(
                    ShadowsKey,
                    (int)ShadowQualityLevel.High),
                (EffectsQualityLevel)PlayerPrefs.GetInt(
                    EffectsKey,
                    (int)EffectsQualityLevel.High),
                (AntiAliasingMode)PlayerPrefs.GetInt(
                    AntiAliasingKey,
                    (int)AntiAliasingMode.Taa),
                PlayerPrefs.GetInt(BrightnessKey, GraphicsSettingsState.DefaultBrightness));
        }

        public void Save(GraphicsSettingsState settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            PlayerPrefs.SetInt(QualityKey, (int)settings.Quality);
            PlayerPrefs.SetInt(ResolutionKey, settings.ResolutionIndex);
            PlayerPrefs.SetInt(DisplayModeKey, (int)settings.DisplayMode);
            PlayerPrefs.SetInt(FrameCapKey, settings.FrameCapIndex);
            PlayerPrefs.SetInt(ShadowsKey, (int)settings.Shadows);
            PlayerPrefs.SetInt(EffectsKey, (int)settings.Effects);
            PlayerPrefs.SetInt(AntiAliasingKey, (int)settings.AntiAliasing);
            PlayerPrefs.SetInt(BrightnessKey, settings.Brightness);
            PlayerPrefs.Save();
        }
    }
}
