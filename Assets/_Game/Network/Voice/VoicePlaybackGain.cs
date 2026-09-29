using Game.Core.Voice;
using Game.Network.Players;
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
        private VoicePreferences preferences;
        private PlayerAvatar avatar;
        private string playerId, userId;
        private volatile float playerGain = 1f;

        public void BindPreferences(VoicePreferences value)
        {
            avatar = GetComponentInParent<PlayerAvatar>();
            BindPlayer(value, null, null);
            RefreshIdentity();
        }

        internal void BindPlayer(VoicePreferences value, string id, string accountId)
        {
            if (preferences != null) preferences.PlayerVolumesChanged -= RefreshPlayerGain;
            preferences = value;
            playerId = id;
            userId = accountId;
            if (preferences != null) preferences.PlayerVolumesChanged += RefreshPlayerGain;
            RefreshPlayerGain();
        }

        private void Update()
        {
            // Speaker linking may precede the avatar's first network snapshot.
            if (string.IsNullOrEmpty(userId)) RefreshIdentity();
        }

        private void RefreshIdentity()
        {
            if (avatar == null || !avatar.HasNetworkState) return;
            playerId = avatar.PlayerId;
            userId = avatar.UserId.ToString();
            RefreshPlayerGain();
        }

        private void RefreshPlayerGain() => playerGain =
            (preferences?.GetPlayerVolume(playerId, userId) ?? VoicePreferences.DefaultPlayerVolume) / (float)VoicePreferences.DefaultPlayerVolume;

        private void OnDestroy()
        {
            if (preferences != null) preferences.PlayerVolumesChanged -= RefreshPlayerGain;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetGain() => Gain = 1f;

        internal static VoicePlaybackGain Attach(Speaker speaker)
        {
            if (!speaker.TryGetComponent<VoicePlaybackGain>(out var filter))
                filter = speaker.gameObject.AddComponent<VoicePlaybackGain>();
            return filter;
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
            processor.Gain = Gain * playerGain;
            processor.Process(data);
        }
    }
}
