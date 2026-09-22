using Game.Core.Ports;

namespace Game.Core.Settings
{
    /// <summary>
    /// For containers built without a machine behind them — tests, and the
    /// dedicated server — so that resolving the preference never depends on
    /// player preferences existing.
    /// </summary>
    public sealed class InMemoryCameraViewStore : ICameraViewStore
    {
        private bool saved;
        private bool firstPerson;

        /// <summary>What was last saved, or null. For tests.</summary>
        public bool? Saved => saved ? firstPerson : null;

        public bool TryLoad(out bool firstPerson)
        {
            firstPerson = this.firstPerson;
            return saved;
        }

        public void Save(bool firstPerson)
        {
            this.firstPerson = firstPerson;
            saved = true;
        }
    }

    /// <summary>
    /// What the player decided about first person versus third person, kept
    /// across the screens they carry it through and the rooms they join later.
    /// </summary>
    /// <remarks>
    /// Separate from the camera rig because the two live for different lengths
    /// of time. The rig is built and destroyed with each scene. This outlives
    /// all of them: a player who switched view in the lobby meant it for the
    /// match as well, and for the lobby they return to.
    /// </remarks>
    public sealed class CameraViewPreference
    {
        private readonly ICameraViewStore store;
        private bool firstPerson;

        public CameraViewPreference() : this(null)
        {
        }

        public CameraViewPreference(ICameraViewStore store)
        {
            this.store = store;
            if (store != null && store.TryLoad(out var saved))
            {
                firstPerson = saved;
            }
        }

        /// <summary>
        /// True while the player is looking in first person. Survives the walk
        /// from the lobby into a match and back, and the next room after that.
        /// Starts false because the rigs ship in third person.
        /// </summary>
        public bool FirstPerson
        {
            get => firstPerson;
            set
            {
                if (firstPerson == value)
                {
                    return;
                }

                firstPerson = value;
                Persist();
            }
        }

        private void Persist() => store?.Save(firstPerson);
    }
}
