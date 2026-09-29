using System;

namespace Game.Backend
{
    /// <summary>
    /// How long to wait before trying the socket again.
    /// </summary>
    /// <remarks>
    /// Doubles from one second to thirty and stays there, with a little
    /// randomness on each wait. The randomness is not decoration: when the
    /// server restarts every client loses the socket in the same instant, and
    /// without it they would all come back in the same instant too, thirty
    /// seconds later, and again thirty seconds after that.
    /// <para>
    /// Pure, so a test can read the sequence without waiting for it. The
    /// randomness is injected for the same reason.
    /// </para>
    /// </remarks>
    public sealed class ReconnectBackoff
    {
        public static readonly TimeSpan DefaultInitial = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Matches what the client guide asks for. Longer and a player whose
        /// network came back waits noticeably for their friends to reappear.
        /// </summary>
        public static readonly TimeSpan DefaultMaximum = TimeSpan.FromSeconds(30);

        /// <summary>Each wait lands between 75% and 125% of its nominal length.</summary>
        private const double JitterSpread = 0.5;

        private readonly TimeSpan initial;
        private readonly TimeSpan maximum;
        private readonly Func<double> random;
        private int failures;

        /// <param name="random">Returns a value in [0, 1). <see cref="Random.NextDouble"/> does.</param>
        public ReconnectBackoff(TimeSpan initial, TimeSpan maximum, Func<double> random)
        {
            if (initial <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(initial));
            if (maximum < initial) throw new ArgumentOutOfRangeException(nameof(maximum));

            this.initial = initial;
            this.maximum = maximum;
            this.random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public static ReconnectBackoff Default()
        {
            var source = new Random();
            return new ReconnectBackoff(DefaultInitial, DefaultMaximum, source.NextDouble);
        }

        /// <summary>The next wait. Each call counts as one more failure.</summary>
        public TimeSpan Next()
        {
            var nominal = Math.Min(maximum.TotalSeconds, initial.TotalSeconds * Math.Pow(2, failures));

            // Stops counting once the cap is reached. Counting on would overflow
            // the power in a long outage, and there is nothing past the cap.
            if (nominal < maximum.TotalSeconds)
            {
                failures++;
            }

            var factor = 1 - JitterSpread / 2 + JitterSpread * random();
            return TimeSpan.FromSeconds(nominal * factor);
        }

        /// <summary>
        /// A connection worked. The next failure starts again from the shortest
        /// wait, because a link that held for a while is not the link that has
        /// been refusing for a minute.
        /// </summary>
        public void Reset()
        {
            failures = 0;
        }
    }
}
