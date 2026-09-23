using System;
using System.Collections.Generic;
using Fusion;
using Game.Core.Ports;
using Game.Network.Players;
using Photon.Realtime;
using Photon.Voice;
using Photon.Voice.Fusion;
using Photon.Voice.Unity;
using Photon.Voice.Unity.UtilityScripts;
using R3;
using Unity.Profiling;
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
        private static readonly ProfilerMarker ResolveRecorderMarker = new("VoiceRig.ResolveRecorder");
        private static readonly ProfilerMarker ListenStateMarker = new("VoiceRig.ApplyListenState");
        private static readonly ProfilerMarker FindSpeakersMarker = new("VoiceRig.FindSpeakers");
        private static readonly ProfilerMarker SourceLookupMarker = new("VoiceRig.SourceLookup");
        private static readonly ProfilerMarker MuteWriteMarker = new("VoiceRig.MuteWrite");
        private static readonly ProfilerMarker RealtimeServiceMarker = new("VoiceRig.RealtimeService");
        private static readonly ProfilerMarker VoiceServiceMarker = new("VoiceRig.VoiceService");
        private readonly ReactiveProperty<bool> available = new(false);
        private readonly ReactiveProperty<bool> muted = new(false);
        private readonly ReactiveProperty<bool> transmitting = new(false);
        private readonly ReactiveProperty<bool> listening = new(true);

        private FusionVoiceClient client;
        private PlayerRoster roster;
        private readonly List<Speaker> speakerBuffer = new();

        /// <summary>
        /// The local avatar's microphone, which is replaced every time Fusion
        /// respawns that avatar.
        /// </summary>
        private Recorder boundRecorder;

        private VoiceNetworkObject localVoice;
        private bool talking;

        /// <summary>
        /// The microphone the player picked in 사운드, by name. Empty means
        /// whichever one the machine calls default.
        /// </summary>
        private string requestedDevice = string.Empty;

        /// <summary>
        /// The name <see cref="boundRecorder"/> was last set from, so a
        /// choice that has not moved does not restart the capture. Null
        /// while no recorder has been told, which is how a fresh avatar is
        /// made to hear the choice again.
        /// </summary>
        private string appliedDevice;

        /// <summary>
        /// The 마이크 볼륨 slider as a multiplier. 1 until the settings say
        /// otherwise, which is the microphone untouched.
        /// </summary>
        private float requestedGain = 1f;


        public ReadOnlyReactiveProperty<bool> IsAvailable => available;
        public ReadOnlyReactiveProperty<bool> IsMuted => muted;
        public ReadOnlyReactiveProperty<bool> IsTransmitting => transmitting;
        public ReadOnlyReactiveProperty<bool> IsListening => listening;

        internal static void AttachServer(NetworkRunner runner)
        {
            // VoiceNetworkObject registers remote speakers in Spawned even on
            // a server. Supply its local registry without connecting to Voice.
            var connection = runner.gameObject.AddComponent<VoiceConnection>();
            connection.enabled = false;
        }

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
            // Before the client, because the SDK picks its logger by walking up
            // from whatever object a component sits on and settles on the first
            // one it finds. At the default level it says nothing about which
            // stream reached which speaker, which is the half of the chain we
            // cannot otherwise see.
            runnerObject.AddComponent<VoiceLogger>().LogLevel = Photon.Voice.LogLevel.Info;
#endif

            var client = runnerObject.AddComponent<FusionVoiceClient>();

            // Voice does not carry Fusion's credentials (S15P21D205-934). The two
            // are separate Photon applications - AppIdFusion and AppIdVoice - and
            // only the first has a custom authentication provider registered.
            // Left at the SDK default of true, the client copies the runner's
            // AuthenticationValues and offers custom authentication to an
            // application that has none, which answers 32755 and takes voice down
            // for the whole session.
            //
            // Nothing is lost by connecting anonymously here. A suspended account
            // is refused by Fusion and never reaches a room, and voice only
            // connects once a room has been joined, so the provider would be a
            // second lock behind a door that is already shut.
            client.UseFusionAuthValues = false;

            // Voice is optional. The SDK otherwise logs errors on room join
            // when neither endpoint is configured, pausing the Editor before
            // the pending lobby transition can resume with Error Pause on.
            // Keep the client for avatar recorder/speaker registration.
            var settings = Fusion.Photon.Realtime.PhotonAppSettings.Global.AppSettings;
            client.AutoConnectAndJoin =
                !string.IsNullOrWhiteSpace(settings.AppIdVoice) ||
                !string.IsNullOrWhiteSpace(settings.Server);
            if (!client.AutoConnectAndJoin)
            {
                Debug.LogWarning(
                    "[Voice] Photon Voice App ID and server are not configured. " +
                    "Voice chat is unavailable; room entry will continue without voice.");
            }

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

        public void SetListening(bool listening)
        {
            if (this.listening.Value == listening)
            {
                return;
            }

            this.listening.Value = listening;
            ApplyListenState();
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

                // Null rather than the name, so the device is set again on
                // the new recorder even though the choice never moved.
                appliedDevice = null;
                EnsureCaptureChain();
                ApplyTransmitState();
                ApplyCaptureDevice();
                ApplyCaptureGain();
            }

            available.Value = client.ClientState == ClientState.Joined;
            transmitting.Value = recorder != null && recorder.IsCurrentlyTransmitting;
            ApplyListenState();

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

            using (RealtimeServiceMarker.Auto()) client.Client.LoadBalancingPeer.Service();
            using (VoiceServiceMarker.Auto()) client.VoiceClient.Service();
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
            using var profile = ResolveRecorderMarker.Auto();
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

        /// <summary>
        /// Hands the chosen microphone to the recorder.
        /// </summary>
        /// <remarks>
        /// Setting <c>MicrophoneDevice</c> restarts the capture, so it is
        /// only written when the name has actually changed.
        /// </remarks>
        public void SetCaptureDevice(string deviceName)
        {
            requestedDevice = deviceName ?? string.Empty;
            ApplyCaptureDevice();
        }

        /// <summary>
        /// Hands the 마이크 볼륨 slider to the recorder.
        /// </summary>
        /// <remarks>
        /// Through <see cref="MicAmplifier"/>, an SDK component that hangs a
        /// post-processor on the outgoing stream. It has to be on the recorder's
        /// own object before the voice is created, because the recorder tells it
        /// so with <c>SendMessage</c> — which is why it sits on the prefab
        /// rather than being added here.
        /// </remarks>
        public void SetCaptureGain(float gain)
        {
            requestedGain = gain;
            ApplyCaptureGain();
        }

        /// <summary>
        /// Puts the microphone processing this player asked for onto their own
        /// recorder: automatic gain, noise suppression, and the 마이크 볼륨
        /// slider's amplifier.
        /// </summary>
        /// <remarks>
        /// Added here rather than left on <c>NetworkedPlayer.prefab</c>, which is
        /// where they were until 2026-09-18. On the prefab they came up on every
        /// avatar Fusion spawned - remote ones and the server's - because
        /// <c>WebRtcAudioDsp.Awake</c> runs wherever the component sits, while
        /// only the local avatar ever records. That made every spawn heavier: the
        /// match scene took 13-18s instead of 8s to load, and both players sat
        /// behind the loading cover until they gave up.
        /// <para>
        /// This rig only ever resolves the local player's recorder, so attaching
        /// from here is the same as saying "on the microphone that is actually
        /// used". The recorder reads its DSP with <c>GetComponent</c> at voice
        /// creation and does not cache it, so restarting the recording is what
        /// makes a freshly added component take effect.
        /// </para>
        /// <para>
        /// AEC stays off. It is the half that costs latency - it hooks the
        /// output through <c>AudioOutCapture</c> - and 8cb59fc7 turned this
        /// whole component off to win that latency back. Automatic gain is the
        /// half that makes a quiet microphone audible, and it costs nothing.
        /// </para>
        /// </remarks>
        private void EnsureCaptureChain()
        {
            if (boundRecorder == null)
            {
                return;
            }

            var host = boundRecorder.gameObject;
            var attached = false;

            var dsp = host.GetComponent<WebRtcAudioDsp>();
            if (dsp == null)
            {
                dsp = host.AddComponent<WebRtcAudioDsp>();
                attached = true;
            }

            dsp.AEC = false;
            dsp.AGC = true;
            dsp.NoiseSuppression = true;
            dsp.enabled = true;

            if (host.GetComponent<MicAmplifier>() == null)
            {
                host.AddComponent<MicAmplifier>();
                attached = true;
            }

            if (attached)
            {
                // The voice was created before these existed. Remaking it is what
                // hands them the stream; without this the first match of a session
                // records with neither.
                boundRecorder.RestartRecording();
            }
        }

        private void ApplyCaptureGain()
        {
            if (boundRecorder == null)
            {
                return;
            }

            var amplifier = boundRecorder.GetComponent<MicAmplifier>();
            if (amplifier == null)
            {
                return;
            }

            // Its own setter ignores a value that has not moved, so there is
            // nothing to remember on this side.
            amplifier.AmplificationFactor = requestedGain;
        }

        private void ApplyCaptureDevice()
        {
            if (boundRecorder == null
                || string.Equals(appliedDevice, requestedDevice, StringComparison.Ordinal))
            {
                return;
            }

            boundRecorder.MicrophoneDevice = ResolveDevice(requestedDevice);
            appliedDevice = requestedDevice;
        }

        /// <summary>
        /// A name from the 사운드 tab as a device the recorder can open.
        /// </summary>
        /// <remarks>
        /// Which list the name is looked up in depends on the back end the
        /// recorder captures with, and the two do not share ids. Unity names
        /// its microphones by the same string it opens them with, so a name
        /// is a device there. The Photon back end enumerates the platform's
        /// own devices, whose ids are numeric on Windows, so the name has to
        /// be looked up to find the id beside it.
        /// <para>
        /// A name nothing answers to falls back to the default device. The
        /// machine may have had the microphone unplugged since it was
        /// chosen, and being heard on the wrong microphone beats not being
        /// heard at all.
        /// </para>
        /// </remarks>
        private DeviceInfo ResolveDevice(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return DeviceInfo.Default;
            }

            if (boundRecorder.MicrophoneType == Recorder.MicType.Unity)
            {
                return new DeviceInfo(name);
            }

            try
            {
                using var devices = Platform.CreateAudioInEnumerator(DeviceLogger);
                foreach (var device in devices)
                {
                    if (string.Equals(device.Name, name, StringComparison.Ordinal))
                    {
                        return device;
                    }
                }
            }
            catch (Exception error)
            {
                // Enumerating goes through a native library, and a platform
                // without one throws rather than answering empty.
                Debug.LogWarning($"[Voice] 마이크 목록을 읽지 못했습니다: {error.Message}");
            }

            return DeviceInfo.Default;
        }

        private static readonly Photon.Voice.Unity.Logger DeviceLogger =
            new Photon.Voice.Unity.Logger(Photon.Voice.LogLevel.Warning);

        private void ApplyTransmitState()
        {
            if (boundRecorder == null)
            {
                return;
            }

            // Mute wins. The talk key is a request to be heard, and a muted
            // player has already answered that. Voice detection keeps silence
            // from looking like a live send: the HUD turns green only while
            // audio is actually leaving.
            boundRecorder.VoiceDetection = true;
            boundRecorder.TransmitEnabled =
                talking && !muted.Value && listening.Value;
        }

        /// <summary>
        /// The voice room stays joined. This only mutes playback on this
        /// machine, so leaving the speaker off does not drop the session.
        /// </summary>
        private void ApplyListenState()
        {
            using var profile = ListenStateMarker.Auto();
            var hear = listening.Value;
            if (roster == null) roster = GetComponent<PlayerRoster>();
            if (roster != null && roster.Avatars.Count > 0)
            {
                // Fusion already tracks avatar spawn/despawn here. Search only their
                // hierarchies, not thousands of unrelated map objects, every frame.
                var avatars = roster.Avatars;
                for (var index = 0; index < avatars.Count; index++)
                {
                    var avatar = avatars[index];
                    if (avatar == null || !avatar.gameObject.activeInHierarchy) continue;
                    using (FindSpeakersMarker.Auto()) avatar.GetComponentsInChildren(false, speakerBuffer);
                    foreach (var speaker in speakerBuffer) ApplySpeakerState(speaker, hear);
                }
                speakerBuffer.Clear();
                return;
            }

            // Preserve standalone/pre-spawn behavior when no roster is available.
            Speaker[] speakers;
            using (FindSpeakersMarker.Auto()) speakers = FindObjectsByType<Speaker>(FindObjectsSortMode.None);
            foreach (var speaker in speakers) ApplySpeakerState(speaker, hear);
        }

        private static void ApplySpeakerState(Speaker speaker, bool hear)
        {
            AudioSource source;
            using (SourceLookupMarker.Auto()) source = speaker.GetComponent<AudioSource>();
            if (source != null)
            {
                using (MuteWriteMarker.Auto()) source.mute = !hear;
            }
        }

        private void OnDestroy()
        {
            available.Dispose();
            muted.Dispose();
            transmitting.Dispose();
            listening.Dispose();
        }
    }
}
