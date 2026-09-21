using UnityEngine;

namespace Game.Client.Match
{
    public interface IDestructionNoticeAudio
    {
        void Play();
    }

    /// <summary>
    /// 물건 파괴 안내가 뜰 때 나는 짧은 알림음. 같은 음을 세 번 잇달아 친다.
    /// </summary>
    /// <remarks>
    /// The HUD lives in the scene, not a prefab, so the clip comes from Resources
    /// instead of a serialized field, the same reasoning as <see cref="MatchEndBellAudio"/>.
    /// This is a HUD cue (2D, no attenuation/panning/doppler). <see cref="Play"/>
    /// starts the three-hit sequence from the beginning, so a second destruction
    /// while the first is still ringing replaces it rather than stacking.
    /// Loading is lazy rather than in Awake so edit mode, which never runs
    /// lifecycle callbacks, still sees a usable component.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class DestructionNoticeAudio : MonoBehaviour, IDestructionNoticeAudio
    {
        public const string RootName = "DestructionNoticeAudio";
        public const string ClipResource = "Audio/SFX_FastUiSoundMusical01";
        public const string ClipAssetPath = "Assets/FastUISounds/SFX_FastUiSoundMusical01.wav";
        public const int RepeatCount = 3;

        /// <summary>효과음 볼륨. 파괴 안내음에 적용된다.</summary>
        public static float EffectsVolume { get; set; } = 1f;

        private AudioSource noticeSource;
        private AudioClip noticeClip;
        private bool sourcesReady;
        private int remainingPlays;
        private float nextPlayTime;

        public static float NextPlayAt(float now, float clipLength) =>
            now + Mathf.Max(0f, clipLength);

        public static DestructionNoticeAudio Create(Transform parent)
        {
            var rootObject = new GameObject(RootName);
            rootObject.transform.SetParent(parent, false);
            return rootObject.AddComponent<DestructionNoticeAudio>();
        }

        public void Play()
        {
            EnsureSource();
            remainingPlays = RepeatCount;
            nextPlayTime = 0f;
            Advance(Time.unscaledTime);
        }

        internal void Advance(float now)
        {
            if (remainingPlays <= 0)
            {
                return;
            }

            EnsureSource();
            if (noticeSource == null || noticeClip == null || now < nextPlayTime)
            {
                return;
            }

            noticeSource.volume = Mathf.Clamp01(EffectsVolume);
            noticeSource.PlayOneShot(noticeClip);
            remainingPlays--;
            nextPlayTime = NextPlayAt(now, noticeClip.length);
        }

        private void Update() => Advance(Time.unscaledTime);

        private void EnsureSource()
        {
            if (sourcesReady)
            {
                return;
            }

            sourcesReady = true;
            noticeClip = LoadClip();

            var audioObject = new GameObject("Notice");
            audioObject.transform.SetParent(transform, false);
            noticeSource = audioObject.AddComponent<AudioSource>();
            noticeSource.playOnAwake = false;
            noticeSource.loop = false;
            noticeSource.spatialBlend = 0f;
            noticeSource.dopplerLevel = 0f;
        }

        private static AudioClip LoadClip()
        {
            var fromResources = Resources.Load<AudioClip>(ClipResource);
            if (fromResources != null)
            {
                return fromResources;
            }

#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(ClipAssetPath);
#else
            return null;
#endif
        }

        private void OnDisable()
        {
            remainingPlays = 0;
            if (noticeSource != null)
            {
                noticeSource.Stop();
            }
        }
    }
}
