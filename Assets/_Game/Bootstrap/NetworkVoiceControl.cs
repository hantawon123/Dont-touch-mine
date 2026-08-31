using System;
using Game.Core.Ports;
using Game.Core.Voice;
using Game.Network.Session;
using R3;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>
    /// Hands the player's microphone choices to whichever voice rig is current,
    /// and reports back what that rig is doing.
    /// </summary>
    /// <remarks>
    /// The rig that actually opens the microphone is a component on the runner
    /// object, so it is built when a session starts and destroyed when one ends.
    /// A screen given that rig directly would be holding a destroyed component
    /// after the first room. This stands between them.
    /// <para>
    /// Scoped to the screens that offer a microphone rather than to the app. On
    /// the home and room-list screens there is no rig to mirror and no button to
    /// paint, and something that ticks where it has no work is one more thing
    /// the next reader has to rule out. What has to outlive the screen is the
    /// mute choice alone, and that lives in <see cref="VoicePreferences"/>.
    /// </para>
    /// </remarks>
    public sealed class NetworkVoiceControl : IVoiceControl, ITickable, IDisposable
    {
        private readonly NetworkRunnerService network;
        private readonly VoicePreferences preferences;
        private readonly ReactiveProperty<bool> available = new(false);
        private readonly ReactiveProperty<bool> muted;
        private readonly ReactiveProperty<bool> transmitting = new(false);

        /// <summary>
        /// The rig these choices were last handed to, so a replacement can be
        /// told about them.
        /// </summary>
        private IVoiceControl current;

        private bool talking;

        public NetworkVoiceControl(
            NetworkRunnerService network,
            VoicePreferences preferences)
        {
            this.network = network ?? throw new ArgumentNullException(nameof(network));
            this.preferences = preferences
                ?? throw new ArgumentNullException(nameof(preferences));

            // Opens on whatever the player last decided, which is how a mute set
            // in the lobby survives the walk into the match.
            muted = new ReactiveProperty<bool>(preferences.Muted);
        }

        public ReadOnlyReactiveProperty<bool> IsAvailable => available;
        public ReadOnlyReactiveProperty<bool> IsMuted => muted;
        public ReadOnlyReactiveProperty<bool> IsTransmitting => transmitting;

        public void SetMuted(bool muted)
        {
            preferences.Muted = muted;
            this.muted.Value = muted;
            network.Voice?.SetMuted(muted);
        }

        public void SetTalking(bool talking)
        {
            this.talking = talking;
            network.Voice?.SetTalking(talking);
        }

        /// <remarks>
        /// Mirrors rather than forwards the rig's own properties: they belong to
        /// the rig and go away with it, and a screen that subscribed to them
        /// would have to resubscribe on every room change.
        /// </remarks>
        public void Tick()
        {
            var voice = network.Voice;
            if (voice == null)
            {
                current = null;
                available.Value = false;
                transmitting.Value = false;
                return;
            }

            if (!ReferenceEquals(voice, current))
            {
                // A session just started. The rig comes up silent and knowing
                // nothing, so it hears what the player already decided.
                current = voice;
                voice.SetMuted(muted.Value);
                voice.SetTalking(talking);
            }

            available.Value = voice.IsAvailable.CurrentValue;
            transmitting.Value = voice.IsTransmitting.CurrentValue;
        }

        public void Dispose()
        {
            available.Dispose();
            muted.Dispose();
            transmitting.Dispose();
        }
    }
}
