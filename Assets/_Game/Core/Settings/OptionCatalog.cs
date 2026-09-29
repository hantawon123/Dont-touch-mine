using System;
using System.Collections.Generic;

namespace Game.Core.Settings
{
    /// <summary>
    /// What every row of one tab offers, held by row number.
    /// </summary>
    /// <remarks>
    /// The other half of what every tab needs and none should write twice; a
    /// tab wraps one of these in a catalogue of its own, which is where the
    /// rows get their names and their types.
    /// </remarks>
    public sealed class OptionCatalog
    {
        private readonly OptionChoices[] byIndex;

        /// <param name="rowCount">
        /// How many rows the tab has. Every one of them must be listed: a row
        /// with no choices would draw an empty picker, which is a mistake worth
        /// hearing about while the catalogue is being built rather than when
        /// the screen opens.
        /// </param>
        public OptionCatalog(int rowCount, IReadOnlyDictionary<int, OptionChoices> choices)
        {
            if (rowCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(rowCount));
            }

            if (choices == null)
            {
                throw new ArgumentNullException(nameof(choices));
            }

            byIndex = new OptionChoices[rowCount];
            for (var index = 0; index < rowCount; index++)
            {
                if (!choices.TryGetValue(index, out var listed) || listed == null)
                {
                    throw new ArgumentException($"No choices given for row {index}.", nameof(choices));
                }

                byIndex[index] = listed;
            }
        }

        public int RowCount => byIndex.Length;

        public OptionChoices For(int index)
        {
            if (index < 0 || index >= byIndex.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return byIndex[index];
        }

        /// <summary>
        /// What a player who has never opened the tab gets, and what 초기화
        /// puts back.
        /// </summary>
        public OptionValues Defaults
        {
            get
            {
                var values = OptionValues.Empty;
                for (var index = 0; index < byIndex.Length; index++)
                {
                    values = values.With(index, byIndex[index].Default.Code);
                }

                return values;
            }
        }

        /// <summary>
        /// Brings saved or offered values back within what the game can
        /// actually do. A code no longer listed falls back to that row's
        /// default rather than being kept: the picker would otherwise show
        /// nothing while the game ran on something unnamed.
        /// </summary>
        public OptionValues Normalise(OptionValues values)
        {
            var result = values;
            for (var index = 0; index < byIndex.Length; index++)
            {
                var choices = byIndex[index];
                result = result.With(
                    index,
                    choices.TryFind(values.Get(index), out var listed)
                        ? listed.Code
                        : choices.Default.Code);
            }

            return result;
        }

        /// <summary>
        /// What the picker shows for a code, or the row's default label when
        /// the code is not one of ours.
        /// </summary>
        public string Label(int index, string code)
        {
            var choices = For(index);
            return choices.TryFind(code, out var listed) ? listed.Label : choices.Default.Label;
        }
    }
}
