using Game.Core.Players;
using UnityEngine;

namespace Game.Client.Players
{
    /// <summary>Client-only one-shots; never reuse the network voice AudioSource.</summary>
    public sealed class PlayerFootstepAudio : MonoBehaviour
    {
        public static float EffectsVolume { get; set; } = 1f;

        public const string ClipAssetPath =
            "Assets/Free UI Click Sound Effects Pack/AUDIO/Plastic/SFX_UI_Click_Organic_Plastic_Soft_Generic_1.wav";

        // Walk_Forward hip Y minima: frame 2 and 14 of a 24-frame, 0.8s cycle.
        internal const float WalkPlantA = 1f / 12f;
        internal const float WalkPlantB = 7f / 12f;
        // Run_Forward hip Y minima at 0.067s and 0.367s of a 0.633s cycle.
        internal const float RunPlantA = .105f;
        internal const float RunPlantB = .579f;
        // Crouch_Walk_Forward is lowest at the cycle ends and the midpoint.
        internal const float CrouchWalkPlantA = 0f;
        internal const float CrouchWalkPlantB = .5f;

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
            ResolvePlants(state, out var plantA, out var plantB);
            if (!AdvanceStep(info.fullPathHash, info.normalizedTime, plantA, plantB)) return;
            if (clips == null || clips.Length == 0) return;
            var clip = clips[clipIndex++ % clips.Length];
            if (clip != null) source.PlayOneShot(clip);
        }

        internal bool AdvanceStep(int stateHash, float normalizedTime) =>
            AdvanceStep(stateHash, normalizedTime, WalkPlantA, WalkPlantB);

        internal bool AdvanceStep(int stateHash, float normalizedTime, float plantA, float plantB)
        {
            var step = PlantStep(normalizedTime, plantA, plantB);
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

        /// <summary>
        /// Which foot-plant of the looping clip <paramref name="normalizedTime"/> is in.
        /// Plants are the authored hip-squash instants, not a naive half-cycle split.
        /// </summary>
        internal static int PlantStep(float normalizedTime, float plantA, float plantB)
        {
            var cycle = Mathf.Floor(normalizedTime);
            var phase = normalizedTime - cycle;
            int plant;
            if (phase >= plantB)
            {
                plant = 1;
            }
            else if (phase >= plantA)
            {
                plant = 0;
            }
            else
            {
                cycle -= 1f;
                plant = 1;
            }

            return Mathf.FloorToInt(cycle) * 2 + plant;
        }

        internal static void ResolvePlants(string state, out float plantA, out float plantB)
        {
            if (!string.IsNullOrEmpty(state) &&
                state.IndexOf("Crouch_Walk", System.StringComparison.Ordinal) >= 0)
            {
                plantA = CrouchWalkPlantA;
                plantB = CrouchWalkPlantB;
                return;
            }

            if (!string.IsNullOrEmpty(state) &&
                state.IndexOf("Run", System.StringComparison.Ordinal) >= 0)
            {
                plantA = RunPlantA;
                plantB = RunPlantB;
                return;
            }

            plantA = WalkPlantA;
            plantB = WalkPlantB;
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
