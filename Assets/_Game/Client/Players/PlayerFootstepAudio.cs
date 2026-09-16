using Game.Core.Players;
using UnityEngine;

namespace Game.Client.Players
{
    /// <summary>Client-only one-shots; never reuse the network voice AudioSource.</summary>
    public sealed class PlayerFootstepAudio : MonoBehaviour
    {
        public static float EffectsVolume { get; set; } = 1f;

        private AudioSource source;
        private AudioClip[] clips;
        private Vector3 previousPosition;
        private int previousState;
        private int previousStep = -1;
        private int clipIndex;

        public void Initialize(AudioClip[] footstepClips)
        {
            clips = footstepClips;
            var audioObject = new GameObject("FootstepAudio");
            audioObject.transform.SetParent(transform, false);
            source = audioObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 2f;
            source.maxDistance = 15f;
            source.dopplerLevel = 0f;
            previousPosition = transform.position;
        }

        public void Tick(Animator animator, string state, bool grounded, PlayerPosture posture)
        {
            var displacement = transform.position - previousPosition;
            previousPosition = transform.position;
            displacement.y = 0f;
            if (source == null) return;
            source.volume = Mathf.Clamp01(EffectsVolume);
            var speed = Time.deltaTime > 0f ? displacement.magnitude / Time.deltaTime : 0f;
            if (!source.isActiveAndEnabled || animator == null || animator.runtimeAnimatorController == null ||
                !CanPlay(state, grounded, posture, speed) || displacement.magnitude > 2f)
            {
                Stop();
                return;
            }

            var info = animator.IsInTransition(0)
                ? animator.GetNextAnimatorStateInfo(0)
                : animator.GetCurrentAnimatorStateInfo(0);
            // Two alternating footfalls per authored locomotion cycle. Playback
            // rate already follows walk/run speed in PlayerAnimationDriver.
            if (!AdvanceStep(info.fullPathHash, info.normalizedTime)) return;
            if (clips == null || clips.Length == 0) return;
            var clip = clips[clipIndex++ % clips.Length];
            if (clip != null) source.PlayOneShot(clip);
        }

        internal bool AdvanceStep(int stateHash, float normalizedTime)
        {
            var step = Mathf.FloorToInt(normalizedTime * 2f);
            if (stateHash != previousState)
            {
                previousState = stateHash;
                previousStep = step;
                return false;
            }
            if (step == previousStep) return false;
            previousStep = step;
            return true;
        }

        internal static bool CanPlay(string state, bool grounded, PlayerPosture posture, float speed) =>
            grounded && posture != PlayerPosture.Prone && speed >= .15f &&
            !string.IsNullOrEmpty(state) &&
            (state.Contains("Walk") || state.Contains("Run"));

        public void Stop()
        {
            previousState = 0;
            previousStep = -1;
            if (source != null) source.Stop();
        }

        private void OnDisable() => Stop();
    }
}
