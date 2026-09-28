using System;
using Photon.Voice;
using Photon.Voice.Unity;
using UnityEngine;

namespace Game.Network.Voice
{
    // Runs after WebRTC processing and before encoding, on the local microphone only.
    public sealed class VoiceCaptureLimiter : MonoBehaviour
    {
        private Processor processor;
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
            processor = new Processor(parameters.Voice.Info.SamplingRate, parameters.Voice.Info.Channels) { Gain = amplificationFactor };
            if (parameters.Voice is LocalVoiceAudioFloat floats) floats.AddPostProcessor(processor);
            else if (parameters.Voice is LocalVoiceAudioShort shorts) shorts.AddPostProcessor(processor);
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
