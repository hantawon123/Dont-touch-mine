using System;

namespace Game.Core.Settings
{
    /// <summary>
    /// The rows of the 그래픽 tab, in the order they are drawn.
    /// </summary>
    public enum GraphicsOption
    {
        DisplayMode,
        Resolution,
        FpsLimit,
        TextureQuality
    }

    /// <summary>
    /// What the 그래픽 tab holds: one chosen code per row, with no opinion
    /// about where they are shown, stored, or what they do to the picture.
    /// </summary>
    /// <remarks>
    /// A value type, so the screen can hold two of them — what is applied and
    /// what is being edited — and tell them apart with a single comparison.
    /// <para>
    /// A named type over <see cref="OptionValues"/> rather than a bag of four
    /// fields: the row-keeping is shared with every other tab, and the name is
    /// what stops one tab's values being handed to another.
    /// </para>
    /// </remarks>
    public readonly struct GraphicsSettings : IEquatable<GraphicsSettings>
    {
        internal static readonly int RowCount = Enum.GetValues(typeof(GraphicsOption)).Length;

        private readonly OptionValues values;

        internal GraphicsSettings(OptionValues values)
        {
            this.values = values;
        }

        /// <summary>
        /// Nothing chosen. What a store hands back when it has never been
        /// written to; <see cref="GraphicsCatalog.Normalise"/> turns it into
        /// something drawable.
        /// </summary>
        public static GraphicsSettings Empty => default;

        internal OptionValues Values => values;

        public string Get(GraphicsOption option) => values.Get((int)option);

        public GraphicsSettings With(GraphicsOption option, string code)
        {
            if ((int)option < 0 || (int)option >= RowCount)
            {
                throw new ArgumentOutOfRangeException(nameof(option));
            }

            return new GraphicsSettings(values.With((int)option, code));
        }

        public bool Equals(GraphicsSettings other) => values == other.values;

        public override bool Equals(object obj) => obj is GraphicsSettings other && Equals(other);

        public override int GetHashCode() => values.GetHashCode();

        public static bool operator ==(GraphicsSettings left, GraphicsSettings right) => left.Equals(right);

        public static bool operator !=(GraphicsSettings left, GraphicsSettings right) => !left.Equals(right);

        public override string ToString()
        {
            var parts = new string[RowCount];
            for (var index = 0; index < RowCount; index++)
            {
                var option = (GraphicsOption)index;
                parts[index] = $"{option}={Get(option)}";
            }

            return "Graphics(" + string.Join(", ", parts) + ")";
        }
    }
}
