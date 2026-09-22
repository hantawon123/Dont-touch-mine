using UnityEngine;

namespace Game.Client.Match
{
    /// <summary>
    /// 경기가 끝나는 순간 한 번 울리는 종 효과음. 땡땡땡땡, 네 번의 고른 타격.
    /// </summary>
    /// <remarks>
    /// The HUD lives in the scene, not a prefab, so the clip comes from Resources
    /// instead of a serialized field, the same reasoning as <see cref="MatchUrgencyAudio"/>.
    /// This is a HUD cue (2D, no attenuation/panning/doppler): everyone hears the
    /// same end bell, not a world-positioned clang. <see cref="Play"/> is
    /// edge-triggered for the current match so a republished result never rings
    /// twice, and <see cref="Reset"/> (a new hiding/waiting phase) lets the next
    /// match ring again. Loading is lazy rather than in Awake so edit mode, which
    /// never runs lifecycle callbacks, still sees a usable component.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class MatchEndBellAudio : MonoBehaviour
    {
        public const string RootName = "MatchEndBellAudio";
        public const string BellResource = "Audio/MatchEndBell";

        /// <summary>효과음 볼륨. 종료 종에 적용된다.</summary>
        public static float EffectsVolume { get; set; } = 1f;

        private AudioSource bellSource;
        private AudioClip bellClip;
        private bool sourcesReady;
        private bool played;

        /// <summary>이번 경기에서 아직 울리지 않았을 때만 친다.</summary>
        public static bool ShouldPlay(bool alreadyPlayed) => !alreadyPlayed;

        public static MatchEndBellAudio Create(Transform parent)
        {
            var rootObject = new GameObject(RootName);
            rootObject.transform.SetParent(parent, false);
            return rootObject.AddComponent<MatchEndBellAudio>();
        }

        private void EnsureSource()
        {
            if (sourcesReady) return;
            sourcesReady = true;
            bellClip = Resources.Load<AudioClip>(BellResource);

            var audioObject = new GameObject("Bell");
            audioObject.transform.SetParent(transform, false);
            bellSource = audioObject.AddComponent<AudioSource>();
            bellSource.playOnAwake = false;
            bellSource.loop = false;
            // A HUD cue, so no attenuation, panning or doppler.
            bellSource.spatialBlend = 0f;
            bellSource.dopplerLevel = 0f;
        }

        public void Play()
        {
            if (!ShouldPlay(played)) return;
            played = true;
            EnsureSource();
            if (bellSource != null && bellClip != null)
            {
                bellSource.volume = Mathf.Clamp01(EffectsVolume);
                bellSource.PlayOneShot(bellClip);
            }
        }

        public void Reset()
        {
            played = false;
            if (bellSource != null)
            {
                bellSource.Stop();
            }
        }

        private void OnDisable()
        {
            played = false;
            if (bellSource != null)
            {
                bellSource.Stop();
            }
        }
    }
}
