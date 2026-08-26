using Game.Core.Settings;
using UnityEngine;

namespace Game.Client.Audio
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class AudioChannelSource : MonoBehaviour
    {
        [SerializeField]
        private AudioChannel channel = AudioChannel.Sfx;

        private AudioSource audioSource;
        private float baseVolume = 1f;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            baseVolume = audioSource.volume;
        }

        private void OnEnable()
        {
            AudioSettingsOutput.Changed += Apply;
            if (AudioSettingsOutput.Current != null)
            {
                Apply(AudioSettingsOutput.Current);
            }
        }

        private void OnDisable()
        {
            AudioSettingsOutput.Changed -= Apply;
        }

        private void Apply(AudioSettingsState settings)
        {
            if (audioSource == null)
            {
                return;
            }

            audioSource.volume = baseVolume * settings.GetSourceVolume(channel);
        }
    }
}
