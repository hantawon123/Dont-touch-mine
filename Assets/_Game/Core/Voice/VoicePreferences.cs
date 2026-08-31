namespace Game.Core.Voice
{
    /// <summary>
    /// What the player decided about their own microphone, kept across the
    /// screens they carry it through.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="Ports.IVoiceControl"/> because the two live for
    /// different lengths of time. The control mirrors a voice rig that is built
    /// and destroyed with each session, and only exists on screens that offer a
    /// microphone button. This outlives all of them: a player who muted
    /// themselves in the lobby meant it for the match as well.
    /// <para>
    /// Only mute so far. A chosen input device or an output volume would belong
    /// here too.
    /// </para>
    /// </remarks>
    public sealed class VoicePreferences
    {
        /// <summary>
        /// True while the player has silenced themselves. Survives the walk from
        /// the lobby into a match and back.
        /// </summary>
        public bool Muted { get; set; }
    }
}
