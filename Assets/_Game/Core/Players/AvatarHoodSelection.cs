using System;

namespace Game.Core.Players
{
    /// <summary>Independent closet selections stored in the existing hood part id.</summary>
    public static class AvatarHoodSelection
    {
        public const string DefaultShape = "hood_bear";
        public const string DefaultColor = "hood_ice";
        private static readonly string[] Shapes = { "hood_bear", "hood_cat", "hood_dog", "hood_rabbit" };
        private static readonly string[] LegacyColors = { "hood_cream", "hood_pink", "hood_sky", "hood_mint", "hood_grey", "hood_lavender" };

        public static bool IsShape(string id) => Array.IndexOf(Shapes, id) >= 0;

        public static string Shape(string stored)
        {
            stored ??= string.Empty;
            foreach (var shape in Shapes)
                if (stored == shape || stored.StartsWith(shape + "_", StringComparison.Ordinal)) return shape;
            return Array.IndexOf(LegacyColors, stored) >= 0 ? DefaultShape : stored;
        }

        public static string Color(string stored)
        {
            stored ??= string.Empty;
            foreach (var shape in Shapes)
                if (stored.StartsWith(shape + "_", StringComparison.Ordinal)) return "hood_" + stored.Substring(shape.Length + 1);
            return Array.IndexOf(LegacyColors, stored) >= 0 ? stored : string.Empty;
        }

        public static string Compose(string shape, string color)
        {
            if (!IsShape(shape)) shape = DefaultShape;
            if (string.IsNullOrEmpty(color)) return shape;
            return shape + "_" + (color.StartsWith("hood_", StringComparison.Ordinal) ? color.Substring(5) : color);
        }
    }
}
