using Game.Core.Settings;

namespace Game.Core.Voice
{
    /// <summary>
    /// How much to scale the captured microphone signal by, read off the
    /// 마이크 볼륨 slider.
    /// </summary>
    /// <remarks>
    /// The slider reads 0 to 100 like the speaker sliders beside it, but it
    /// does not mean the same thing. A speaker slider only turns sound down —
    /// 100 is the sound as recorded. A microphone has to go up as well, because
    /// a quiet headset is the thing a player reaches for this slider to fix.
    /// <para>
    /// So the middle is where the signal is untouched, the way
    /// <see cref="Settings.CameraLookScale"/> treats the sensitivity sliders:
    /// <see cref="SoundCatalog.DefaultVolume"/> is 1x, 0 is silence, 100 is
    /// twice as loud. Mapping 0-100 onto 0-1 instead would have halved every
    /// existing player's microphone the day this started being read, since they
    /// are all sitting on the default.
    /// </para>
    /// <para>
    /// Boosting amplifies the room along with the voice, and past some point
    /// it clips. That is the player's to judge — the slider is beside a test
    /// button for exactly that.
    /// </para>
    /// </remarks>
    public static class VoiceCaptureGain
    {
        /// <summary>The gain at <see cref="SoundCatalog.DefaultVolume"/>.</summary>
        public const float Neutral = 1f;

        /// <summary>The gain at <see cref="SoundCatalog.MaxVolume"/>.</summary>
        public const float Max = 2f;

        public static float From(SoundSettings settings) =>
            From(settings.Get(SoundVolume.Microphone));

        /// <inheritdoc cref="From(SoundSettings)"/>
        public static float From(int percent)
        {
            // Unset is what a store that has never been written to hands back.
            // It is not 0: a player who has never touched the slider means the
            // default, and reading it as silence would mute them.
            var value = percent == SoundCatalog.Unset
                ? SoundCatalog.DefaultVolume
                : SoundCatalog.Clamp(percent);

            return value / (float)SoundCatalog.DefaultVolume;
        }
    }
}
