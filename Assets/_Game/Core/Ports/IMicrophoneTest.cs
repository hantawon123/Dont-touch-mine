namespace Game.Core.Ports
{
    /// <summary>
    /// Lets the player hear whether a microphone works before committing to
    /// it: 마이크 테스트 on the 사운드 tab.
    /// </summary>
    /// <remarks>
    /// The application plays the voice straight back through the speakers.
    /// Tests and the dedicated server keep a no-op, because those containers
    /// have no microphone to open.
    /// </remarks>
    public interface IMicrophoneTest
    {
        bool IsRunning { get; }

        /// <param name="deviceName">
        /// The device being considered, by the machine's name for it, or
        /// <see cref="Settings.SoundCatalog.DefaultDevice"/>. The one in the
        /// draft rather than the applied one: the point is to try before
        /// applying.
        /// </param>
        void Start(string deviceName);

        void Stop();
    }
}
