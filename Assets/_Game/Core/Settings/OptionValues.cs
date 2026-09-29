using System;

namespace Game.Core.Settings
{
    /// <summary>
    /// One chosen code per row of a tab, held by row number.
    /// </summary>
    /// <remarks>
    /// The part every tab of the settings screen needs and none of them should
    /// write twice: copy-on-write, comparison and hashing over a set of chosen
    /// codes. A tab wraps one of these in a type of its own —
    /// <see cref="GraphicsSettings"/>, <see cref="InterfaceSettings"/> — so the
    /// compiler still keeps one tab's values out of another's.
    /// <para>
    /// A row that has never been written reads as empty rather than as a
    /// missing entry, which is what lets a save made before a row existed still
    /// load: <see cref="OptionCatalog.Normalise"/> fills the gap with that
    /// row's default.
    /// </para>
    /// </remarks>
    public readonly struct OptionValues : IEquatable<OptionValues>
    {
        private readonly string[] codes;

        private OptionValues(string[] codes)
        {
            this.codes = codes;
        }

        /// <summary>Nothing chosen.</summary>
        public static OptionValues Empty => default;

        /// <summary>How many rows this has room for. Not how many a tab has.</summary>
        public int Length => codes?.Length ?? 0;

        public string Get(int index)
        {
            if (codes == null || index < 0 || index >= codes.Length)
            {
                return string.Empty;
            }

            return codes[index] ?? string.Empty;
        }

        public OptionValues With(int index, string code)
        {
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            var length = Math.Max(codes?.Length ?? 0, index + 1);
            var next = new string[length];
            if (codes != null)
            {
                Array.Copy(codes, next, codes.Length);
            }

            next[index] = code ?? string.Empty;
            return new OptionValues(next);
        }

        public bool Equals(OptionValues other)
        {
            var length = Math.Max(Length, other.Length);
            for (var index = 0; index < length; index++)
            {
                if (!string.Equals(Get(index), other.Get(index), StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj) => obj is OptionValues other && Equals(other);

        public override int GetHashCode()
        {
            // Trailing empties are skipped so that a set grown by a row nobody
            // has chosen yet hashes the same as the one it grew from, which is
            // what Equals already says of them.
            var last = Length - 1;
            while (last >= 0 && Get(last).Length == 0)
            {
                last--;
            }

            var hash = 17;
            for (var index = 0; index <= last; index++)
            {
                hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(Get(index));
            }

            return hash;
        }

        public static bool operator ==(OptionValues left, OptionValues right) => left.Equals(right);

        public static bool operator !=(OptionValues left, OptionValues right) => !left.Equals(right);
    }
}
