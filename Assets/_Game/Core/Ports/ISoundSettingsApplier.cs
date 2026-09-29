using Game.Core.Settings;

namespace Game.Core.Ports
{
    /// <summary>
    /// Makes the 사운드 settings heard: the volumes on the mixer, and the
    /// microphone and how it listens on the voice rig.
    /// </summary>
    /// <remarks>
    /// One port for both halves rather than one each, because they arrive
    /// together — 적용하기 settles the whole tab — and because the thing that
    /// sets a mixer group's level and the thing that picks a microphone both
    /// live behind Unity's audio, where <c>Core</c> cannot look.
    /// </remarks>
    public interface ISoundSettingsApplier
    {
        void Apply(SoundSettings settings);
    }
}
