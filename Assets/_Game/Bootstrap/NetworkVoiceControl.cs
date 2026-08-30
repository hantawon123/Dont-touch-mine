using System;
using Game.Core.Ports;
using Game.Network.Session;
using R3;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>
    /// Holds the player's microphone choices across the sessions that carry
    /// them.
    /// </summary>
    /// <remarks>
    /// The rig that actually opens the microphone is a component on the runner
    /// object, so it is built when a session starts and destroyed when one ends.
    /// A screen given that rig directly would be holding a destroyed component
    /// after the first room, and would forget that the player had muted
    /// themselves every time they changed rooms. This keeps the answer and hands
    /// it to whichever rig is current.
    /// </remarks>
    public sealed class NetworkVoiceControl : IVoiceControl, ITickable, IDisposable
    {
        private readonly NetworkRunnerService network;
        private readonly ReactiveProperty<bool> available = new(false);
        private readonly ReactiveProperty<bool> muted = new(false);
        private readonly ReactiveProperty<bool> transmitting = new(false);

        /// <summary>
        /// The rig these choices were last handed to, so a replacement can be
        /// told about them.
        /// </summary>
        private IVoiceControl current;

        private bool talking;

        public NetworkVoiceControl(NetworkRunnerService network)
        {
            this.network = network ?? throw new ArgumentNullException(nameof(network));
        }

        public ReadOnlyReactiveProperty<bool> IsAvailable => available;
        public ReadOnlyReactiveProperty<bool> IsMuted => muted;
        public ReadOnlyReactiveProperty<bool> IsTransmitting => transmitting;

        public void SetMuted(bool muted)
        {
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
