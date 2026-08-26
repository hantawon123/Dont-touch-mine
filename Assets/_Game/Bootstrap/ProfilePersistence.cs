using System;
using Game.Core.Home;
using Game.Core.Ports;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>
    /// Writes the profile back to the store whenever it changes.
    /// </summary>
    /// <remarks>
    /// Separate from the store so that neither the profile nor the store has to
    /// know about the other: the profile announces a change and this decides that
    /// a change is worth keeping.
    /// <para>
    /// Registered on the project scope, because the profile outlives every scene
    /// and a listener living in one would stop saving as soon as that scene went
    /// away.
    /// </para>
    /// </remarks>
    public sealed class ProfilePersistence : IStartable, IDisposable
    {
        private readonly PlayerProfile _profile;
        private readonly IProfileStore _store;

        public ProfilePersistence(PlayerProfile profile, IProfileStore store)
        {
            _profile = profile;
            _store = store;
        }

        public void Start()
        {
            if (_profile != null && _store != null)
            {
                _profile.Changed += OnProfileChanged;
            }
        }

        public void Dispose()
        {
            if (_profile != null)
            {
                _profile.Changed -= OnProfileChanged;
            }
        }

        private void OnProfileChanged(PlayerProfile changed)
        {
            _store.Save(changed.Nickname, changed.Level);
        }
    }
}
