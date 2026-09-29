using System;
using System.Collections.Generic;
using Game.Core.Ports;

namespace Game.Core.Voice
{
    /// <summary>
    /// For containers built without a machine behind them — tests, and the
    /// dedicated server — so that resolving the preferences never depends on
    /// player preferences existing.
    /// </summary>
    public sealed class InMemoryVoicePreferencesStore : IVoicePreferencesStore
    {
        private bool saved;
        private bool muted;
        private bool listening = true;

        /// <summary>What was last saved, or null. For tests.</summary>
        public (bool muted, bool listening)? Saved =>
            saved ? (muted, listening) : null;

        public bool TryLoad(out bool muted, out bool listening)
        {
            muted = this.muted;
            listening = this.listening;
            return saved;
        }

        public void Save(bool muted, bool listening)
        {
            this.muted = muted;
            this.listening = listening;
            saved = true;
        }
    }

    /// <summary>
    /// What the player decided about their own microphone and speaker, kept
    /// across the screens they carry it through and the rooms they join later.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="IVoiceControl"/> because the two live for
    /// different lengths of time. The control mirrors a voice rig that is built
    /// and destroyed with each session, and only exists on screens that offer a
    /// microphone button. This outlives all of them: a player who muted
    /// themselves in the lobby meant it for the match as well, and for the next
    /// room after that.
    /// <para>
    /// Mute and listen so far. A chosen input device or an output volume
    /// would belong here too.
    /// </para>
    /// </remarks>
    public sealed class VoicePreferences
    {
        private readonly IVoicePreferencesStore store;
        private bool muted;
        private bool listening = true;

        public VoicePreferences() : this(null)
        {
        }

        public VoicePreferences(IVoicePreferencesStore store)
        {
            this.store = store;
            if (store != null && store.TryLoad(out var savedMuted, out var savedListening))
            {
                muted = savedMuted;
                listening = savedListening;
            }
        }

        /// <summary>
        /// True while the player has silenced themselves. Survives the walk from
        /// the lobby into a match and back, and the next room after that.
        /// </summary>
        public bool Muted
        {
            get => muted;
            set
            {
                if (muted == value)
                {
                    return;
                }

                muted = value;
                Persist();
            }
        }

        /// <summary>
        /// True while other people's voice should play. Starts on because the
        /// room already joins voice; this only decides whether this machine
        /// hears it.
        /// </summary>
        public bool Listening
        {
            get => listening;
            set
            {
                if (listening == value)
                {
                    return;
                }

                listening = value;
                Persist();
            }
        }

        // Local receive preferences for this room, never sent to another player.
        // Account identity prevents a reused seat/player ID inheriting somebody's volume.
        private readonly Dictionary<string, int> playerVolumes = new(StringComparer.Ordinal);
        public event Action PlayerVolumesChanged;
        public const int DefaultPlayerVolume = 50;
        public const int MaxPlayerVolume = 100;

        private static string PlayerVolumeKey(string playerId, string userId) =>
            !string.IsNullOrWhiteSpace(userId) ? "user:" + userId
                : !string.IsNullOrWhiteSpace(playerId) ? "player:" + playerId : null;

        public int GetPlayerVolume(string playerId, string userId = null)
        {
            var key = PlayerVolumeKey(playerId, userId);
            return key != null && playerVolumes.TryGetValue(key, out var volume) ? volume : DefaultPlayerVolume;
        }

        public void SetPlayerVolume(string playerId, string userId, int percent)
        {
            var key = PlayerVolumeKey(playerId, userId);
            if (key == null) return;
            percent = Math.Clamp(percent, 0, MaxPlayerVolume);
            if (GetPlayerVolume(playerId, userId) == percent) return;
            playerVolumes[key] = percent;
            PlayerVolumesChanged?.Invoke();
        }

        public void ResetPlayerVolumes()
        {
            if (playerVolumes.Count == 0) return;
            playerVolumes.Clear();
            PlayerVolumesChanged?.Invoke();
        }

        private void Persist() => store?.Save(muted, listening);
    }
}
