using System;
using Cysharp.Threading.Tasks;
using Game.Client.Interactions;
using Game.Client.Players;
using Game.Core.Match;
using Game.Server.Match;
using UnityEngine;

namespace Game.Bootstrap
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class ShredderInteractable : MonoBehaviour, IInteractable
    {
        private const int EjectionDelayMilliseconds = 500;

        // A plain C# service (NetworkInteractionSceneBridge) confirms this machine's state for
        // remote players and has no Inspector, so these clips are loaded by path like
        // MatchUrgencyAudio's — not serialized fields on this component.
        public const string FeedResource = "Audio/Shredder/ShredderFeed";
        public const string RunResource = "Audio/Shredder/ShredderRun";
        public const string EjectResource = "Audio/Shredder/ShredderEject";
        public const string SuccessResource = "Audio/Shredder/ShredderSuccess";
        public const float MinDistance = 2f;
        public const float MaxDistance = 15f;

        private AudioSource audioSource;
        private AudioClip feedClip;
        private AudioClip runClip;
        private AudioClip ejectClip;
        private AudioClip successClip;

        [SerializeField]
        private Transform ejectionPoint;

        [SerializeField]
        private Transform ejectionTarget;

        [SerializeField, Min(0f)]
        private float ejectionSpeed = 4f;

        [SerializeField, Min(0f)]
        private float ejectionUpwardSpeed = 1.5f;

        private MatchSessionCoordinator session;
        private IMatchClock clock;
        private int playerIndex;

        public string InteractionPrompt => "파괴하기";

#if !UNITY_SERVER
        private void Awake()
        {
            feedClip = Resources.Load<AudioClip>(FeedResource);
            runClip = Resources.Load<AudioClip>(RunResource);
            ejectClip = Resources.Load<AudioClip>(EjectResource);
            successClip = Resources.Load<AudioClip>(SuccessResource);
            audioSource = gameObject.AddComponent<AudioSource>();
            ConfigureSpatial(audioSource);
        }
#endif

        /// <summary>
        /// Plays at a world point with linear falloff, the same 2–15 m range as
        /// footsteps and combat one-shots. <c>PlayClipAtPoint</c> would keep the
        /// clip audible across the whole map.
        /// </summary>
        public static void PlaySpatial(AudioClip clip, Vector3 position)
        {
            if (clip == null)
            {
                return;
            }

#if UNITY_SERVER
            return;
#else
            var audioObject = new GameObject("ShredderAudio");
            audioObject.transform.position = position;
            var source = audioObject.AddComponent<AudioSource>();
            ConfigureSpatial(source);
            source.volume = .8f * Mathf.Clamp01(PlayerFootstepAudio.EffectsVolume);
            source.clip = clip;
            source.Play();
            Object.Destroy(audioObject, clip.length + .05f);
#endif
        }

        /// <summary>
        /// HUD-style 2D playback so every peer hears the success chime at the
        /// same loudness, regardless of how far they are from the shredder.
        /// </summary>
        public static void PlayGlobal(AudioClip clip)
        {
            if (clip == null)
            {
                return;
            }

#if UNITY_SERVER
            return;
#else
            var audioObject = new GameObject("ShredderSuccessAudio");
            var source = audioObject.AddComponent<AudioSource>();
            ConfigureGlobal(source);
            source.volume = .8f * Mathf.Clamp01(PlayerFootstepAudio.EffectsVolume);
            source.clip = clip;
            source.Play();
            Object.Destroy(audioObject, clip.length + .05f);
#endif
        }

        public static void ConfigureSpatial(AudioSource source)
        {
            if (source == null)
            {
                return;
            }

            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = MinDistance;
            source.maxDistance = MaxDistance;
            source.dopplerLevel = 0f;
        }

        public static void ConfigureGlobal(AudioSource source)
        {
            if (source == null)
            {
                return;
            }

            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
        }

        private void PlayClip(AudioClip clip)
        {
            if (audioSource == null || clip == null) return;
            audioSource.volume = .8f * Mathf.Clamp01(PlayerFootstepAudio.EffectsVolume);
            audioSource.PlayOneShot(clip);
        }

        public void Bind(
            MatchSessionCoordinator matchSession,
            int localPlayerIndex,
            IMatchClock matchClock)
        {
            session = matchSession ?? throw new ArgumentNullException(nameof(matchSession));
            clock = matchClock ?? throw new ArgumentNullException(nameof(matchClock));
            playerIndex = localPlayerIndex;
        }

        public bool CanInteract(PlayerInteractor interactor)
        {
            return interactor != null &&
                   interactor.CarriedItem != null &&
                   ejectionPoint != null &&
                   ejectionTarget != null;
        }

        public void Interact(PlayerInteractor interactor)
        {
            if (!CanInteract(interactor))
            {
                return;
            }

            if (interactor.TryUseAuthoritativeShredder())
            {
                return;
            }

            var item = interactor.CarriedItem;
            if (session != null)
            {
                var now = clock.ServerTime;
                if (session.TryDestroyHeldPlayerItem(playerIndex, now))
                {
                    interactor.ReleaseCarriedItem();
                    PlayGlobal(successClip);
                    Destroy(item.gameObject);
                    return;
                }

                var ejectionPose = new Pose(ejectionPoint.position, ejectionPoint.rotation);
                if (!session.TryUseShredderOnHeldMapObject(playerIndex, ejectionPose, now))
                {
                    return;
                }
            }
            else if (item.IsPlayerItem)
            {
                interactor.ReleaseCarriedItem();
                PlayGlobal(successClip);
                Destroy(item.gameObject);
                return;
            }

            interactor.ReleaseCarriedItem();
            item.transform.SetParent(transform, true);
            item.gameObject.SetActive(false);
            PlayClip(feedClip);
            PlayClip(runClip);
            EjectAfterDelay(item, this.GetCancellationTokenOnDestroy()).Forget();
        }

        private async UniTask EjectAfterDelay(
            CarryableItem item,
            System.Threading.CancellationToken cancellationToken)
        {
            await UniTask.Delay(
                EjectionDelayMilliseconds,
                cancellationToken: cancellationToken);

            if (item == null || ejectionPoint == null)
            {
                return;
            }

            var ejectionDirection = Vector3.ProjectOnPlane(ejectionPoint.right, Vector3.up);
            if (ejectionDirection.sqrMagnitude <= 0.0001f)
            {
                ejectionDirection = ejectionPoint.right;
            }

            ejectionDirection.Normalize();
            item.transform.SetPositionAndRotation(
                ejectionPoint.position,
                ejectionPoint.rotation);
            item.gameObject.SetActive(true);
            PlayClip(ejectClip);
            item.OnThrown(
                (ejectionDirection * ejectionSpeed) +
                (Vector3.up * ejectionUpwardSpeed));
        }
    }
}
