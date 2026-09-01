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
    /// Puts the voice client on the runner object and answers the mute button.
    /// </summary>
    /// <remarks>
    /// The client sits on the same object as the <see cref="NetworkRunner"/>
    /// because it requires it there: it reads the runner's session to name its
    /// voice room and follows players in and out of it.
    /// <para>
    /// The microphone is not here. It belongs on the avatar, because the voice
    /// stream advertises the avatar's network id and the receiving side matches
    /// that id against a registered speaker. A recorder shared across avatars
    /// keeps advertising the id of whichever avatar it was first registered
    /// under, so after a respawn the other side stops finding a speaker for it
    /// and hears nothing — one way, since only the machine whose avatar respawned
    /// goes stale. Letting the recorder live and die with the avatar is what
    /// keeps that id honest, and it is what <c>VoiceNetworkObject</c> already
    /// does on both ends of its own accord.
    /// </para>
    /// </remarks>
    public sealed class VoiceRig : MonoBehaviour, IVoiceControl
    {
        private readonly ReactiveProperty<bool> available = new(false);
        private readonly ReactiveProperty<bool> muted = new(false);
        private readonly ReactiveProperty<bool> transmitting = new(false);

        private FusionVoiceClient client;

        /// <summary>
        /// The local avatar's microphone, which is replaced every time Fusion
        /// respawns that avatar.
        /// </summary>
        private Recorder boundRecorder;

        private VoiceNetworkObject localVoice;
        private bool talking;


        public ReadOnlyReactiveProperty<bool> IsAvailable => available;
        public ReadOnlyReactiveProperty<bool> IsMuted => muted;
        public ReadOnlyReactiveProperty<bool> IsTransmitting => transmitting;

        /// <summary>
        /// Builds the voice client onto a runner object and returns the rig that
        /// drives it.
        /// </summary>
        /// <remarks>
        /// Added from code rather than placed on a prefab because the runner
        /// object is itself built at runtime, once per session.
        /// </remarks>
        public static VoiceRig Attach(NetworkRunner runner)
        {
            var runnerObject = runner.gameObject;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // VoiceDiagnostics already records the useful chain state. Keep the
            // SDK logger for failures without printing every connection step and
            // its stack trace on the main thread while a scene is loading.
            runnerObject.AddComponent<VoiceLogger>().LogLevel = Photon.Voice.LogLevel.Warning;
#endif

            var client = runnerObject.AddComponent<FusionVoiceClient>();

            // No primary recorder. Leaving it unset is what makes each avatar bring
            // its own: VoiceNetworkObject looks among its children first and only
            // falls back to the connection's when it finds none.
            //
            // Registered by hand because Fusion collects the callbacks it finds
            // on the runner object when the runner comes up, and this client is
            // added after that. Without it the client never hears that the local
            // player joined, which is the only thing that makes it connect.
            runner.AddCallbacks(client);

            var rig = runnerObject.AddComponent<VoiceRig>();
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
        /// the recorder, and neither raises an event.
        /// </remarks>
        private void Update()
        {
            if (client == null)
            {
                return;
            }

            var recorder = ResolveRecorder();
            if (!ReferenceEquals(recorder, boundRecorder))
            {
                // A new avatar brought a new microphone. It comes up knowing
                // nothing, so it hears what the player already decided.
                boundRecorder = recorder;
                ApplyTransmitState();
            }

            available.Value = client.ClientState == ClientState.Joined;
            transmitting.Value = recorder != null && recorder.IsCurrentlyTransmitting;

            PumpTransport();
        }

        /// <summary>
        /// Sends and receives once more this frame.
        /// </summary>
        /// <remarks>
        /// The SDK services its own transport on a fixed 33 ms timer, which is
        /// slower than the 20 ms frames the encoder produces. Frames wait for a
        /// tick that has not come and then leave in bursts, which costs up to a
        /// frame of delay for nothing. These are the same two calls the SDK
        /// makes, and a Photon peer expects to be serviced as often as the host
        /// application cares to: the extra call finds nothing to do when nothing
        /// is waiting.
        /// </remarks>
        private void PumpTransport()
        {
            if (client.ClientState != ClientState.Joined)
            {
                return;
            }

            client.Client.LoadBalancingPeer.Service();
            client.VoiceClient.Service();
        }

        /// <summary>
        /// Finds the microphone on whichever avatar this machine controls.
        /// </summary>
        /// <remarks>
        /// Searched rather than injected because Fusion spawns the avatar, and
        /// respawns it on every scene the session moves through. The search only
        /// runs while there is no avatar to find, which is the gap between
        /// joining a room and being placed in it.
        /// </remarks>
        private Recorder ResolveRecorder()
        {
            if (localVoice != null && localVoice.RecorderInUse != null)
            {
                return localVoice.RecorderInUse;
            }

            localVoice = null;
            foreach (var candidate in
                     FindObjectsByType<VoiceNetworkObject>(FindObjectsSortMode.None))
            {
                if (candidate.Object == null || !candidate.IsLocal)
                {
                    continue;
                }

                localVoice = candidate;
                break;
            }

            return localVoice != null ? localVoice.RecorderInUse : null;
        }

        private void ApplyTransmitState()
        {
            if (boundRecorder == null)
            {
                return;
            }

            // Mute wins. The talk key is a request to be heard, and a muted
            // player has already answered that.
            boundRecorder.TransmitEnabled = talking && !muted.Value;
        }

        private void OnDestroy()
        {
            available.Dispose();
            muted.Dispose();
            transmitting.Dispose();
        }
    }
}
