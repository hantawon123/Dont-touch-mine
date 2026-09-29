using System;
using Photon.Voice;
using Photon.Voice.Unity;
using UnityEngine;

namespace Game.Network.Voice
{
    // Runs after WebRTC processing, then gates the corrected signal before encoding.
    // Gating raw input first discards quiet microphones before AGC can amplify them.
    public sealed class VoiceCaptureLimiter : MonoBehaviour
    {
        private Processor processor;
        private LocalVoice voice;
        internal const int SampleRate = 48000;
        internal const int Bitrate = 32000;
        internal const float DetectionThreshold = .01f;
        internal const int DetectionDelayMs = 500;

        internal static VoiceInfo CreateVoiceInfo(int channels) => VoiceInfo.CreateAudioOpus(
            POpusCodec.Enums.SamplingRate.Sampling48000, channels,
            OpusCodec.FrameDuration.Frame20ms, Bitrate);

        // Shared by live capture and the settings test. Component order matters:
        // WebRTC must attach before the limiter and the one remaining detector.
        public static void Configure(Recorder recorder)
        {
            recorder.SamplingRate = POpusCodec.Enums.SamplingRate.Sampling48000;
            recorder.Bitrate = Bitrate;
            recorder.FrameDuration = OpusCodec.FrameDuration.Frame20ms;
            recorder.VoiceDetectionThreshold = DetectionThreshold;
            recorder.VoiceDetectionDelayMs = DetectionDelayMs;
            var host = recorder.gameObject;
            var dsp = host.GetComponent<WebRtcAudioDsp>();
            var attached = dsp == null;
            if (dsp == null) dsp = host.AddComponent<WebRtcAudioDsp>();
            dsp.AEC = false;
            dsp.AGC = true;
            dsp.AgcCompressionGain = 9;
            dsp.AgcTargetLevel = 3;
            dsp.NoiseSuppression = true;
            dsp.VAD = false; // Recorder detects after DSP and user gain, never twice.
            dsp.enabled = true;
            if (host.GetComponent<VoiceCaptureLimiter>() == null)
            {
                host.AddComponent<VoiceCaptureLimiter>();
                attached = true;
            }
            if (attached) recorder.RestartRecording();
        }

        private void OnEnable() => AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
        private void OnDisable() => AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
        private void OnAudioConfigurationChanged(bool _) => OrderProcessors();
        private void PhotonVoiceRemoved() { voice = null; processor = null; }

        private float amplificationFactor = 1f;

        public float AmplificationFactor
        {
            get => amplificationFactor;
            set
            {
                amplificationFactor = Mathf.Clamp(value, 0f, 2f);
                if (processor != null) processor.Gain = amplificationFactor;
            }
        }

        private void PhotonVoiceCreated(PhotonVoiceCreatedParams parameters)
        {
            voice = parameters.Voice;
            var dsp = GetComponent<WebRtcAudioDsp>();
            if (dsp != null)
            {
                // Windows' Photon fallback already applies native AGC/NS/AEC.
                // Keep fallback for unavailable Unity devices without processing twice.
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                dsp.Bypass = parameters.AudioDesc is Photon.Voice.Windows.WindowsAudioInPusher;
#else
                dsp.Bypass = false;
#endif
            }
            processor = new Processor(voice.Info.SamplingRate, voice.Info.Channels) { Gain = amplificationFactor };
            OrderProcessors();
        }

        private void OrderProcessors()
        {
            // The SDK reattaches its DSP after output-device changes. Move this
            // tail again so reconnecting headphones cannot move detection before AGC.
            if (processor == null) return;
            if (voice is LocalVoiceAudioFloat floats)
            {
                var detector = (IProcessor<float>)floats.VoiceDetector;
                floats.RemoveProcessor(processor, detector);
                floats.AddPostProcessor(processor, detector);
            }
            else if (voice is LocalVoiceAudioShort shorts)
            {
                var detector = (IProcessor<short>)shorts.VoiceDetector;
                shorts.RemoveProcessor(processor, detector);
                shorts.AddPostProcessor(processor, detector);
            }
        }

        internal sealed class Processor : IProcessor<float>, IProcessor<short>
        {
            // Use the encoder's existing block as lookahead; no extra buffering or allocation.
            private const float Ceiling = .98f;
            private readonly int samplesPerSecond;
            private float scale = 1f;
            public volatile float Gain = 1f;

            internal Processor(int sampleRate, int channels = 1) =>
                samplesPerSecond = Math.Max(1, sampleRate) * Math.Max(1, channels);

            private float Factor(float peak, int samples)
            {
                var gain = Gain;
                var target = peak * gain > Ceiling ? Ceiling / (peak * gain) : 1f;
                // Immediate attack, 100 ms release: avoid flat clipping and sudden gain recovery.
                var release = 1f - (1f - scale) * (float)Math.Exp(-samples / (samplesPerSecond * .1));
                scale = Math.Min(target, release);
                return gain * scale;
            }

            public float[] Process(float[] samples)
            {
                var peak = 0f;
                for (var i = 0; i < samples.Length; i++) peak = Math.Max(peak, Math.Abs(samples[i]));
                var factor = Factor(peak, samples.Length);
                for (var i = 0; i < samples.Length; i++) samples[i] *= factor;
                return samples;
            }

            public short[] Process(short[] samples)
            {
                var peak = 0f;
                for (var i = 0; i < samples.Length; i++) peak = Math.Max(peak, Math.Abs((float)samples[i]) / 32768f);
                var factor = Factor(peak, samples.Length);
                for (var i = 0; i < samples.Length; i++) samples[i] = (short)(samples[i] * factor);
                return samples;
            }

            public void Dispose() { }
        }
    }
}
