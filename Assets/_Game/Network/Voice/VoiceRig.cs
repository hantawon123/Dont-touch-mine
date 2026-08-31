using Fusion;
using Game.Core.Ports;
using Photon.Realtime;
using Photon.Voice.Fusion;
using Photon.Voice.Unity;
using R3;
using UnityEngine;

namespace Game.Network.Voice
{
    /// <summary>
    /// Puts voice on the runner object and answers the mute button.
    /// </summary>
    /// <remarks>
    /// Sits on the same object as the <see cref="NetworkRunner"/> because
    /// <see cref="FusionVoiceClient"/> requires it there. That client reads the
    /// runner's session to name its voice room and follows players in and out
    /// of it, so none of that is wired here.
    /// <para>
    /// The microphone is the connection's primary recorder rather than a
    /// component on the avatar. <c>VoiceNetworkObject</c> looks for a recorder
    /// among its own children first and falls back to this one, and a single
    /// microphone that follows whichever avatar the player owns is the simpler
    /// of the two — the avatar is respawned by Fusion, and a recorder on it
    /// would be torn down and rebuilt with it.
    /// </para>
    /// </remarks>
    public sealed class VoiceRig : MonoBehaviour, IVoiceControl
    {
        private readonly ReactiveProperty<bool> available = new(false);
        private readonly ReactiveProperty<bool> muted = new(false);
        private readonly ReactiveProperty<bool> transmitting = new(false);

        private Recorder recorder;
        private FusionVoiceClient client;
        private bool talking;

        public ReadOnlyReactiveProperty<bool> IsAvailable => available;
        public ReadOnlyReactiveProperty<bool> IsMuted => muted;
        public ReadOnlyReactiveProperty<bool> IsTransmitting => transmitting;

        /// <summary>
        /// Builds the voice components onto a runner object and returns the rig
        /// that drives them.
        /// </summary>
        /// <remarks>
        /// Added from code rather than placed on a prefab because the runner
        /// object is itself built at runtime, once per session.
        /// </remarks>
        public static VoiceRig Attach(GameObject runnerObject)
        {
            var recorder = runnerObject.AddComponent<Recorder>();

            // Silent until the player asks to talk. An open microphone in a game
            // about hiding gives away more than the player meant to say.
            recorder.TransmitEnabled = false;

            var client = runnerObject.AddComponent<FusionVoiceClient>();
            client.PrimaryRecorder = recorder;

            var rig = runnerObject.AddComponent<VoiceRig>();
            rig.recorder = recorder;
            rig.client = client;
            return rig;
        }

        public void SetMuted(bool muted)
        {
            if (this.muted.Value == muted)
            {
                return;
            }

            this.muted.Value = muted;
            ApplyTransmitState();
        }

        public void SetTalking(bool talking)
        {
            if (this.talking == talking)
            {
                return;
            }

            this.talking = talking;
            ApplyTransmitState();
        }

        /// <remarks>
        /// Polled rather than subscribed: the SDK reports the room it is in
        /// through a state enum and whether audio is leaving through a flag on
        /// the recorder, and neither raises an event. Both are single reads.
        /// </remarks>
        private void Update()
        {
            if (client == null || recorder == null)
            {
                return;
            }

            available.Value = client.ClientState == ClientState.Joined;
            transmitting.Value = recorder.IsCurrentlyTransmitting;
        }

        private void ApplyTransmitState()
        {
            if (recorder == null)
            {
                return;
            }

            // Mute wins. The talk key is a request to be heard, and a muted
            // player has already answered that.
            recorder.TransmitEnabled = talking && !muted.Value;
        }

        private void OnDestroy()
        {
            available.Dispose();
            muted.Dispose();
            transmitting.Dispose();
        }
    }
}
