using System;
using System.Collections.Generic;

namespace Game.Core.Settings
{
    /// <summary>
    /// One value a stepped setting can take: what gets saved, and what the
    /// picker shows for it.
    /// </summary>
    public readonly struct OptionChoice
    {
        /// <summary>A short, stable identifier that is what gets saved.</summary>
        public string Code { get; }

        /// <summary>The words shown between the two arrows.</summary>
        public string Label { get; }

        public OptionChoice(string code, string label)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ArgumentException("A choice needs a code.", nameof(code));
            }

            Code = code;
            Label = label ?? throw new ArgumentNullException(nameof(label));
        }
    }

    /// <summary>
    /// What one stepped setting offers, in the order its arrows walk through
    /// it.
    /// </summary>
    /// <remarks>
    /// Data rather than a fixed list, so a test can hand a screen two choices
    /// and watch the picker move. Stepping wraps at either end: the design
    /// draws both arrows on every row and never draws one as spent.
    /// </remarks>
    public sealed class OptionChoices
    {
        private readonly OptionChoice[] choices;
        private readonly int defaultIndex;

        /// <param name="defaultCode">
        /// What a player who has never touched this setting gets, and what
        /// 초기화 puts back. Null takes the first choice, which is what most
        /// of the lists are ordered for.
        /// </param>
        public OptionChoices(string defaultCode, params OptionChoice[] choices)
        {
            if (choices == null || choices.Length == 0)
            {
                throw new ArgumentException("A setting needs at least one choice.", nameof(choices));
            }

            this.choices = (OptionChoice[])choices.Clone();
            defaultIndex = defaultCode == null ? 0 : IndexOf(defaultCode);
            if (defaultIndex < 0)
            {
                throw new ArgumentException(
                    $"'{defaultCode}' is not one of the choices.", nameof(defaultCode));
            }
        }

        public OptionChoices(params OptionChoice[] choices)
            : this(null, choices)
        {
        }

        public IReadOnlyList<OptionChoice> All => choices;

        public OptionChoice Default => choices[defaultIndex];

        /// <summary>
        /// Whether the picker has anywhere to go. False with one choice, so the
        /// screen can draw its arrows as unavailable rather than as buttons
        /// that do nothing.
        /// </summary>
        public bool CanStep => choices.Length > 1;

        public bool TryFind(string code, out OptionChoice choice)
        {
            var index = IndexOf(code);
            choice = index >= 0 ? choices[index] : default;
            return index >= 0;
        }

        /// <summary>
        /// The choice <paramref name="steps"/> places along from
        /// <paramref name="code"/>, wrapping at either end. A code that is not
        /// listed steps from the default, so a stale saved value cannot leave
        /// the picker stuck.
        /// </summary>
        public OptionChoice Step(string code, int steps)
        {
            var from = IndexOf(code);
            if (from < 0)
            {
                from = defaultIndex;
            }

            var count = choices.Length;
            return choices[(((from + steps) % count) + count) % count];
        }

        private int IndexOf(string code)
        {
            for (var index = 0; index < choices.Length; index++)
            {
                if (string.Equals(choices[index].Code, code, StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
        }
    }
}
