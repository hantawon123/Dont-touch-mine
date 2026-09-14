using System;
using System.Globalization;
using Game.Core.Ports;
using Game.Core.Settings;
using UnityEngine;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>
    /// Carries the 그래픽 settings into Unity.
    /// </summary>
    /// <remarks>
    /// Four of the nine rows reach the picture today: the window's mode and
    /// size, the frame cap, and texture detail. Those are properties of the
    /// player and of <see cref="QualitySettings"/>, which exist wherever the
    /// game runs.
    /// <para>
    /// The other five — anti-aliasing, HBAO, shadow quality, depth of field
    /// and volumetrics — are not wired up here, and this class is deliberately
    /// the only place that says so. Each needs something this project has not
    /// set up yet: anti-aliasing is a property of the camera that renders the
    /// match, not of any camera in the settings scene; HBAO and volumetrics
    /// are renderer features; shadow quality and depth of field live on the
    /// URP asset and on a Volume profile. They are saved and shown correctly,
    /// and turning one has no effect on the picture until those are added.
    /// </para>
    /// </remarks>
    public sealed class UnityGraphicsSettingsApplier : IGraphicsSettingsApplier
    {
        /// <summary>
        /// Editor-only. The Game view owns the picture there, so a hook in
        /// Game.Editor sets its size to the same code the player window uses.
        /// </summary>
        public static Action<int, int> ApplyEditorGameView;

        public void Apply(GraphicsSettings settings)
        {
            // ShadowQuality remains a saved/displayed preference only.
            // WebGL intentionally disables realtime shadows in its pipeline asset.
            ApplyWindow(settings);
            ApplyFrameCap(settings.Get(GraphicsOption.FpsLimit));
            ApplyTextureQuality(settings.Get(GraphicsOption.TextureQuality));
        }

        /// <summary>
        /// The window's size and mode, set together in one call: two would
        /// make the window jump twice.
        /// </summary>
        /// <remarks>
        /// Left alone on WebGL, where the page owns the canvas. In the editor
        /// the Game view is set to the same size, so overlay passes and the
        /// backbuffer stop disagreeing (1887×1153 vs 1920×1080).
        /// <para>
        /// This runs after <see cref="DesktopResolutionBootstrap"/>, which puts
        /// a fresh install at the display's own size before any scene loads.
        /// A player who has chosen a size gets theirs, because this comes
        /// later.
        /// </para>
        /// </remarks>
        private static void ApplyWindow(GraphicsSettings settings)
        {
            if (!TryReadResolution(settings.Get(GraphicsOption.Resolution), out var width, out var height))
            {
                return;
            }

#if UNITY_WEBGL
            return;
#elif UNITY_EDITOR
            if (!Application.isPlaying)
            {
                return;
            }

            ApplyEditorGameView?.Invoke(width, height);
#else
            var mode = string.Equals(
                settings.Get(GraphicsOption.DisplayMode),
                GraphicsCatalog.Windowed,
                StringComparison.Ordinal)
                ? FullScreenMode.Windowed
                : FullScreenMode.FullScreenWindow;

            if (Screen.width == width && Screen.height == height && Screen.fullScreenMode == mode)
            {
                return;
            }

            Screen.SetResolution(width, height, mode);
#endif
        }

        /// <summary>
        /// The frame cap.
        /// </summary>
        /// <remarks>
        /// Vertical sync is switched off with it, because Unity ignores
        /// <see cref="Application.targetFrameRate"/> while it is on: a screen
        /// that offers a choice of 30, 40, 60 and 120 cannot also leave the
        /// monitor deciding.
        /// </remarks>
        private static void ApplyFrameCap(string code)
        {
            if (!int.TryParse(code, NumberStyles.Integer, CultureInfo.InvariantCulture, out var fps)
                || fps <= 0)
            {
                return;
            }

            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = fps;
        }

        /// <summary>
        /// How much of each texture is loaded: all of it, half, or a quarter.
        /// </summary>
        private static void ApplyTextureQuality(string code)
        {
            switch (code)
            {
                case GraphicsCatalog.High:
                    QualitySettings.globalTextureMipmapLimit = 0;
                    break;
                case GraphicsCatalog.Medium:
                    QualitySettings.globalTextureMipmapLimit = 1;
                    break;
                case GraphicsCatalog.Low:
                    QualitySettings.globalTextureMipmapLimit = 2;
                    break;
            }
        }

        /// <summary>
        /// Reads a "1920x1080" code. False for anything else, which leaves the
        /// window as it is rather than resizing it to nonsense.
        /// </summary>
        public static bool TryReadResolution(string code, out int width, out int height)
        {
            width = 0;
            height = 0;
            if (string.IsNullOrWhiteSpace(code))
            {
                return false;
            }

            var parts = code.Split('x');
            return parts.Length == 2
                   && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out width)
                   && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out height)
                   && width > 0
                   && height > 0;
        }
    }

    /// <summary>
    /// Makes the saved graphics settings real when the game starts.
    /// </summary>
    /// <remarks>
    /// Applying is a step in the application's startup rather than a side
    /// effect of resolving the settings system, so that building a container
    /// — in a test, or in the editor — never resizes anybody's window.
    /// </remarks>
    public sealed class GraphicsSettingsStartup : IStartable
    {
        private readonly GraphicsSettingsSystem graphics;

        public GraphicsSettingsStartup(GraphicsSettingsSystem graphics)
        {
            this.graphics = graphics;
        }

        public void Start() => graphics.ApplyToRenderer();
    }
}
