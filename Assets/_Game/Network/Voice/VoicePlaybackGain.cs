using Photon.Voice.Unity;
using UnityEngine;

namespace Game.Network.Voice
{
    // Only remote voice sources get this filter. Unity still applies source mute,
    // spatial attenuation and master volume; music/effects never enter this filter.
    [DisallowMultipleComponent]
    public sealed class VoicePlaybackGain : MonoBehaviour
    {
        public static float Gain
        {
            get => gain;
            set => gain = float.IsNaN(value) ? 1f : Mathf.Clamp(value, 0f, 2f);
        }
        private static volatile float gain = 1f;
        private VoiceCaptureLimiter.Processor processor;
        private int sampleRate;
        private int processorRate, processorChannels;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetGain() => Gain = 1f;

        internal static void Attach(Speaker speaker)
        {
            if (!speaker.TryGetComponent<VoicePlaybackGain>(out _))
                speaker.gameObject.AddComponent<VoicePlaybackGain>();
        }

        private void OnEnable()
        {
            sampleRate = AudioSettings.outputSampleRate;
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
        }
        private void OnDisable() => AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
        private void OnAudioConfigurationChanged(bool _) => System.Threading.Volatile.Write(ref sampleRate, AudioSettings.outputSampleRate);

        private void OnAudioFilterRead(float[] data, int channels)
        {
            var rate = System.Threading.Volatile.Read(ref sampleRate);
            if (processor == null || processorChannels != channels || processorRate != rate)
            {
                processor = new VoiceCaptureLimiter.Processor(rate, channels);
                processorChannels = channels;
                processorRate = rate;
            }
            processor.Gain = Gain;
            processor.Process(data);
        }
    }
}
