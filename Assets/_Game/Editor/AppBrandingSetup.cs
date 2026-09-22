using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Writes the shipped product name and application icon into player settings.
    /// </summary>
    /// <remarks>
    /// The icon is the same artwork the download page uses as its favicon, so the
    /// executable, its taskbar entry and the site agree. Run it from the menu or,
    /// on a machine without the Editor open, with
    /// <c>-executeMethod Game.Editor.AppBrandingSetup.Apply</c>.
    /// </remarks>
    public static class AppBrandingSetup
    {
        public const string ProductName = "Don't Touch Mine";
        private const string IconPath = "Assets/_Game/Content/UI/App/AppIcon.png";

        [MenuItem("Game/Build/Apply App Branding")]
        public static void Apply()
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon == null)
                throw new InvalidOperationException($"The application icon is missing: {IconPath}.");

            PlayerSettings.productName = ProductName;
            ApplyIcons(NamedBuildTarget.Unknown, icon);
            ApplyIcons(NamedBuildTarget.Standalone, icon);
            ApplyIcons(NamedBuildTarget.WebGL, icon);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Branding] productName='{PlayerSettings.productName}', icon='{IconPath}'.");
        }

        private static void ApplyIcons(NamedBuildTarget target, Texture2D icon)
        {
            // Every slot the platform asks for takes the same square source; Unity
            // downsizes each one while building.
            var sizes = PlayerSettings.GetIconSizes(target, IconKind.Any);
            if (sizes == null || sizes.Length == 0) return;
            var icons = new Texture2D[sizes.Length];
            for (var index = 0; index < icons.Length; index++) icons[index] = icon;
            PlayerSettings.SetIcons(target, icons, IconKind.Any);
        }
    }
}
