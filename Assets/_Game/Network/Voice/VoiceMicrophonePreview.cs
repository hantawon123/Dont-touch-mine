using System;
using System.Collections.Generic;
using Game.Core.Voice;
using Photon.Voice;
using Photon.Voice.Unity;
using UnityEngine;

namespace Game.Network.Voice
{
    // Local only: the same input/DSP/detector/Opus path, with encoded packets
    // routed straight to a decoder. This transport never connects to a server.
    public sealed class VoiceMicrophonePreview : IDisposable
    {
        private static VoiceMicrophonePreview active;
        private readonly List<(Recorder recorder, bool recordWhenJoined)> suspended = new();
        private GameObject host;
        private IAudioDesc input;
        private Realtime5Transport transport;
        private LocalVoice voice;
        private IEncoder encoder;
        private OpusCodec.Decoder<float> decoder;
        private UnityAudioOut output;
        private VoiceCaptureLimiter limiter;
        private string requestedDevice;
        private readonly Photon.Voice.Unity.Logger logger = new(Photon.Voice.LogLevel.Warning);
        public bool IsRunning => host != null && voice != null;

        public void Start(string deviceName, float gain)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (IsRunning && requestedDevice == deviceName)
            {
                limiter.AmplificationFactor = gain;
                return;
            }
            StartInput(() =>
            {
                var name = VoiceCaptureDevice.ResolveAvailable(deviceName, Microphone.devices);
                IAudioDesc capture = new MicWrapper(name, VoiceCaptureLimiter.SampleRate, logger);
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                if (capture.Error != null)
                {
                    capture.Dispose();
                    // Same fallback as Recorder: backend IDs differ, use default.
                    capture = new Photon.Voice.Windows.WindowsAudioInPusher(-1, logger);
                }
#endif
                return capture;
            }, gain);
            requestedDevice = deviceName;
#endif
        }

        internal void StartInput(Func<IAudioDesc> openInput, float gain)
        {
            Stop();
            active?.Stop();
            active = this;
            try
            {
                foreach (var rig in UnityEngine.Object.FindObjectsByType<VoiceRig>(FindObjectsSortMode.None))
                    SuspendCapture(rig.CaptureRecorder);
                input = openInput();
                if (input == null || input.Error != null) throw new InvalidOperationException(input?.Error ?? "No microphone input.");

                host = new GameObject("Processed Microphone Test") { hideFlags = HideFlags.HideAndDontSave };
                if (Application.isPlaying) UnityEngine.Object.DontDestroyOnLoad(host);
                var recorder = host.AddComponent<Recorder>();
                recorder.RecordingEnabled = false; // Input belongs to this preview, not Recorder.
                VoiceCaptureLimiter.Configure(recorder);
                limiter = host.GetComponent<VoiceCaptureLimiter>();
                limiter.AmplificationFactor = gain;
                var source = host.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.volume = 1f;
                host.AddComponent<VoicePlaybackGain>();
                output = new UnityAudioOut(source,
                    new AudioOutDelayControl.PlayDelayConfig { Low = 80, High = 80, Max = 1000, SpeedUpPerc = 5 },
                    logger, "Microphone test", false);

                var info = VoiceCaptureLimiter.CreateVoiceInfo(input.Channels);
                var sampleType = AudioSampleType.Source;
                host.GetComponent<WebRtcAudioDsp>().AdjustVoiceInfo(ref info, ref sampleType);
                if (sampleType == AudioSampleType.Source)
                    sampleType = input is IAudioReader<float> || input is IAudioPusher<float> ? AudioSampleType.Float : AudioSampleType.Short;
                output.Start(info.SamplingRate, info.Channels, info.FrameDurationSamples);
                decoder = new OpusCodec.Decoder<float>(frame =>
                {
                    output.Push(frame.Buf);
                    if (frame.EndOfStream) output.Flush();
                }, logger);
                decoder.Open(info);
                if (decoder.Error != null) throw new InvalidOperationException(decoder.Error);
                encoder = sampleType == AudioSampleType.Short
                    ? OpusCodec.Factory.CreateEncoder<short[]>(info, logger)
                    : OpusCodec.Factory.CreateEncoder<float[]>(info, logger);
                if (encoder.Error != null) throw new InvalidOperationException(encoder.Error);
                transport = new Realtime5Transport();
                voice = transport.VoiceClient.CreateLocalVoiceAudioFromSource(info, input, sampleType, 0,
                    new VoiceCreateOptions { Encoder = encoder });
                var detector = ((ILocalVoiceAudio)voice).VoiceDetector;
                detector.On = true;
                detector.Threshold = VoiceCaptureLimiter.DetectionThreshold;
                detector.ActivityDelayMs = VoiceCaptureLimiter.DetectionDelayMs;
                host.SendMessage("PhotonVoiceCreated", new PhotonVoiceCreatedParams { Voice = voice, AudioDesc = input }, SendMessageOptions.DontRequireReceiver);
                // LocalVoice installs a network callback in its constructor; replace it
                // before servicing input. No room, targets or credentials are involved.
                encoder.Output = (bytes, flags) =>
                {
                    var frame = new FrameBuffer(bytes.Array, bytes.Offset, bytes.Count, flags, 0, null);
                    try { decoder.Input(ref frame); }
                    finally { frame.Release(); }
                };
                voice.TransmitEnabled = true;
            }
            catch (Exception error)
            {
                Debug.LogWarning($"[Voice] 마이크 테스트를 시작하지 못했습니다: {error.Message}");
                Stop();
            }
        }

        // A newly spawned avatar must also yield the device while the test owns it.
        internal static void SuspendCapture(Recorder recorder)
        {
            if (active == null || recorder == null || !recorder.RecordingEnabled) return;
            active.suspended.Add((recorder, recorder.RecordWhenJoined));
            recorder.RecordWhenJoined = false;
            recorder.RecordingEnabled = false;
        }

        public void Tick()
        {
            if (!IsRunning) return;
            try
            {
                transport.VoiceClient.Service();
                output.Service();
                if (input.Error != null) Stop();
            }
            catch (Exception error)
            {
                Debug.LogWarning($"[Voice] 마이크 테스트가 중단되었습니다: {error.Message}");
                Stop();
            }
        }

        public void Stop()
        {
            try
            {
                // Dispose the voice first: its processing lock drains active audio
                // callbacks before the decoder and playback buffer are released.
                transport?.Dispose();
                transport = null;
                voice = null;
                encoder?.Dispose();
                encoder = null;
                decoder?.Dispose();
                decoder = null;
                try { input?.Dispose(); }
                catch (Exception error) { Debug.LogWarning($"[Voice] 마이크 종료: {error.Message}"); }
                input = null;
                output?.Stop();
                output = null;
                if (host != null) UnityEngine.Object.DestroyImmediate(host);
                host = null;
                limiter = null;
                requestedDevice = null;
            }
            finally
            {
                if (ReferenceEquals(active, this)) active = null;
                foreach (var previous in suspended)
                {
                    if (previous.recorder == null) continue;
                    previous.recorder.RecordWhenJoined = previous.recordWhenJoined;
                    previous.recorder.RecordingEnabled = true;
                }
                suspended.Clear();
            }
        }

        public void Dispose() => Stop();
    }
}
