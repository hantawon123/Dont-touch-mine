using Game.Client.Cameras;
using Game.Client.Combat;
using Game.Client.Emotes;
using Game.Client.Interactions;
using Game.Core.Players;
using UnityEngine;

namespace Game.Client.Players
{
    /// <summary>
    /// 이동·전투 상태를 읽어 애니메이터 상태를 직접 지시한다.
    /// 전환 조건을 애니메이터 그래프가 아니라 코드가 소유한다. (루트 모션 미사용)
    /// </summary>
    [RequireComponent(typeof(PlayerMovement))]
    public sealed class PlayerAnimationDriver : MonoBehaviour
    {
        private const string PunchState = "Punch";
        private const string HitState = "Hit";
        private const string StunnedState = "Stunned";
        internal const string StunStartState = "Stun_Start";
        internal const string StunIdleState = "Stun_Idle";
        internal const string StunEndState = "Stun_End";
        private const string JumpState = "Jump";
        private const string CarryJumpState = "Carry_TwoHands_Jump";
        private const string AirborneState = "Fall";
        private const string LandState = "Land";
        private const string CarryLandState = "Carry_TwoHands_Land";
        private const float SpeedDampTime = 0.1f;
        private const float CrossFadeSeconds = 0.15f;
        private const float DirectionDeadZone = 0.2f;
        // 대각선 입력의 두 축이 거의 같을 때 프레임마다 전후/좌우가 바뀌는 것을 막는다.
        private const float DirectionDominanceHysteresis = 0.12f;
        private const float JumpSeconds = 32f / 30f;
        private const float LandSeconds = 20f / 30f;
        // 80% of the other combat one-shots (.8 * Effects).
        internal const float LandAudioVolume = .64f;
        // 앉기·일어서기 스윽만 70% of the other combat one-shots (.8 * Effects).
        internal const float PostureSwooshAudioVolume = .56f;
        private const float HitSeconds = 30f / 30f;
        internal const float StunStartSeconds = 2.2f;
        internal const float StunEndSeconds = 1.2f;
        private const float MinLocomotionPlayback = 0.5f;
        private const float MaxLocomotionPlayback = 2f;

        [SerializeField, Min(0.1f), Tooltip("펀치 모션 유지 시간(초). CombatConfig가 있으면 그 값을 우선한다")]
        private float punchDurationSeconds = 0.5f;

        [SerializeField, Min(0.1f), Tooltip("이동 모션 재생 배율. 1이면 설정 걷기/달리기 속도에서 1배")]
        private float locomotionPlaybackScale = 1f;

        [SerializeField, Tooltip("한 걸음씩 분리한 발소리. 순서대로 번갈아 재생한다")]
        private AudioClip[] footstepClips;

        private PlayerFootstepAudio footstepAudio;

        [SerializeField, Tooltip("펀치를 휘두를 때 재생하는 효과음 (명중 여부와 무관)")]
        private AudioClip punchSwingClip;

        private AudioSource punchAudioSource;

        [SerializeField, Tooltip("펀치가 실제로 명중해 피격 알림을 받았을 때 재생하는 효과음")]
        private AudioClip punchHitClip;

        private AudioSource hitAudioSource;

        [SerializeField, Tooltip("지면에서 위로 뛰어오를 때 한 번 재생하는 효과음")]
        private AudioClip jumpClip;

        private AudioSource jumpAudioSource;

        [SerializeField, Tooltip("물건을 집을 때 재생하는 효과음")]
        private AudioClip pickupSoundClip;

        private AudioSource pickupAudioSource;

        [SerializeField, Tooltip("물건을 내려놓을 때 재생하는 효과음 (일반 드롭과 정밀 배치 확정 모두)")]
        private AudioClip putDownSoundClip;

        private AudioSource putDownAudioSource;

        [SerializeField, Tooltip("물건을 던질 때 재생하는 효과음")]
        private AudioClip throwSoundClip;

        private AudioSource throwAudioSource;

        [SerializeField, Tooltip("기절에 들어가는 순간 재생하는 효과음")]
        private AudioClip stunSoundClip;

        private AudioSource stunAudioSource;

        [SerializeField, Tooltip("점프 후 착지할 때 한 번 재생하는 효과음")]
        private AudioClip landClip;

        private AudioSource landAudioSource;

        [SerializeField, Tooltip("앉기·일어나기·눕기·일어나기처럼 자세가 바뀔 때 재생하는 효과음")]
        private AudioClip postureSwooshClip;

        private AudioSource postureSwooshAudioSource;
        private float previousJumpHeight;
        private bool jumpGroundedSeen;
        private bool jumpSoundPlayed;
        private bool jumpWasAirborne;
        private bool landSoundPending;

        private float PunchDuration =>
            combatant != null && combatant.Config != null
                ? combatant.Config.PunchMotionSeconds
                : punchDurationSeconds;

        private PlayerMovement movement;
        private PlayerCombatant combatant;
        private PlayerInteractor interactor;
        private Animator animator;
        private string currentState;

        /// <summary>지금 재생 중인 클립(상태) 이름. 1인칭 팔 뷰가 동작별 자세 프로필을 고를 때 읽는다.</summary>
        public string CurrentState => currentState;

        /// <summary>펀치 모션이 재생 중인가. 1인칭 팔이 때리는 팔을 조준점 쪽으로 보정할 때 쓴다.</summary>
        public bool IsPunching => punchUntilTime > 0f && Time.time < punchUntilTime;

        /// <summary>
        /// 감정 클립이 애니메이터에 있으면 재생한다. 없는 상태 이름은 무시한다.
        /// </summary>
        public bool TryPlayEmote(EmoteId id)
        {
            if (animator == null || (combatant != null && combatant.IsStunned))
            {
                return false;
            }

            var def = EmoteCatalog.Of(id);
            if (!animator.HasState(0, Animator.StringToHash(def.StateName)))
            {
                return false;
            }

            punchUntilTime = 0f;
            // 앉기→서기 전환 클립이 이 표현을 덮어쓰지 않게, 지금 자세를 이미 본 것으로 친다.
            if (movement != null)
            {
                lastPosture = movement.Posture;
            }

            var seconds = def.Loop ? 600f : Mathf.Max(0.2f, ClipLength(def.StateName, def.DurationSeconds));
            var restart = !def.Loop && currentState == def.StateName;
            PlayOneShot(def.StateName, seconds);
            if (restart)
            {
                // Update는 상태 이름이 바뀔 때만 크로스페이드하므로, 같은 1회성 표현 연타는 여기서 처음부터 다시 튼다.
                animator.Play(def.StateName, 0, 0f);
            }

            return true;
        }

        /// <summary>네트워크 모터가 애니메이션 상태를 주는가. 그러면 감정 표현도 복제 상태로만 시작한다.</summary>
        public bool UsesNetworkState => usesNetworkState;

        /// <summary>표현 계층이 보는 접지 여부. 네트워크 플레이어는 CharacterController가 꺼져 있어 복제값을 쓴다.</summary>
        public bool IsGrounded => usesNetworkState ? networkGrounded : movement != null && movement.IsGrounded;

        private float ClipLength(string state, float fallback)
        {
            var clips = animator.runtimeAnimatorController != null
                ? animator.runtimeAnimatorController.animationClips
                : null;
            if (clips == null)
            {
                return fallback;
            }

            for (var i = 0; i < clips.Length; i++)
            {
                if (clips[i] != null && clips[i].name == state)
                {
                    return clips[i].length;
                }
            }

            return fallback;
        }

        /// <summary>이번 펀치가 왼손인가.</summary>
        public bool IsLeftPunch => leftPunch;

        /// <summary>
        /// 기절 넘어진 포즈가 재생 중인가. 1인칭 시선이 머리 본을 따라갈지 고를 때 쓴다.
        /// </summary>
        public bool IsStunView => IsStunState(currentState);

        /// <summary>펀치 진행도 0(시작)~1(끝). 펀치 중이 아니면 -1.</summary>
        public float PunchProgress =>
            IsPunching ? Mathf.Clamp01((Time.time - punchStartedTime) / Mathf.Max(0.01f, PunchDuration)) : -1f;
        private float punchUntilTime;
        private float punchStartedTime;
        private bool leftPunch;
        private float hitUntilTime;
        private float oneShotUntilTime;
        private string oneShotState;
        private bool wasGrounded = true;
        private PlayerPosture lastPosture = PlayerPosture.Standing;
        private bool usesNetworkState;
        private float networkSpeed;
        private bool networkGrounded;
        private int networkAttackSequence;
        private int networkEmoteSequence;
        private Vector2 networkMoveLocal;
        private bool networkCarrying;
        private bool carryOverride;
        private MoveDirection lastLocomotionDirection;
        private bool wasStunned;
        private float networkLookPitch;
        private Transform neckBone;
        private Transform headBone;
        private PlayerCameraController cameraRig;

        private void Awake()
        {
            movement = GetComponent<PlayerMovement>();
            combatant = GetComponent<PlayerCombatant>();
            interactor = GetComponent<PlayerInteractor>();
            animator = GetComponentInChildren<Animator>();

            if (animator == null)
            {
                Debug.LogWarning("PlayerAnimationDriver: 자식에서 Animator를 찾지 못해 비활성화합니다.", this);
                enabled = false;
                return;
            }

            animator.applyRootMotion = false;
            lastPosture = movement.Posture;
            previousJumpHeight = transform.position.y;
            CacheLookBones();
#if !UNITY_SERVER
            if (punchSwingClip != null)
            {
                punchAudioSource = CreateCombatAudioSource("PunchSwingAudio");
            }
            if (punchHitClip != null)
                hitAudioSource = CreateCombatAudioSource("PunchHitAudio");
            if (jumpClip != null)
                jumpAudioSource = CreateCombatAudioSource("JumpAudio");
            if (pickupSoundClip != null)
                pickupAudioSource = CreateCombatAudioSource("PickupAudio");
            if (putDownSoundClip != null)
                putDownAudioSource = CreateCombatAudioSource("PutDownAudio");
            if (throwSoundClip != null)
                throwAudioSource = CreateCombatAudioSource("ThrowAudio");
            if (stunSoundClip != null)
                stunAudioSource = CreateCombatAudioSource("StunAudio");
            if (landClip != null)
                landAudioSource = CreateCombatAudioSource("LandAudio");
            if (postureSwooshClip != null)
                postureSwooshAudioSource = CreateCombatAudioSource("PostureSwooshAudio");
            if (footstepClips != null && footstepClips.Length > 0)
            {
                footstepAudio = gameObject.AddComponent<PlayerFootstepAudio>();
                footstepAudio.Initialize(footstepClips);
            }
#endif
        }

        private void OnEnable()
        {
            cameraRig = FindFirstObjectByType<PlayerCameraController>();
            if (combatant != null)
            {
                combatant.AttackPerformed += OnAttackPerformed;
                combatant.HitReceived += OnHitReceived;
                combatant.Stunned += OnStunned;
            }
        }

        private void OnDisable()
        {
            footstepAudio?.Stop();
            if (punchAudioSource != null) punchAudioSource.Stop();
            if (hitAudioSource != null) hitAudioSource.Stop();
            if (jumpAudioSource != null) jumpAudioSource.Stop();
            if (pickupAudioSource != null) pickupAudioSource.Stop();
            if (putDownAudioSource != null) putDownAudioSource.Stop();
            if (throwAudioSource != null) throwAudioSource.Stop();
            if (stunAudioSource != null) stunAudioSource.Stop();
            if (landAudioSource != null) landAudioSource.Stop();
            if (postureSwooshAudioSource != null) postureSwooshAudioSource.Stop();
            jumpGroundedSeen = false;
            jumpSoundPlayed = false;
            jumpWasAirborne = false;
            landSoundPending = false;
            if (combatant != null)
            {
                combatant.AttackPerformed -= OnAttackPerformed;
                combatant.HitReceived -= OnHitReceived;
                combatant.Stunned -= OnStunned;
            }

            if (animator != null)
            {
                animator.speed = 1f;
            }
        }

        private void OnAttackPerformed()
        {
            if (usesNetworkState)
            {
                return;
            }

            PlayPunch();
        }

        private void OnHitReceived()
        {
            if (hitAudioSource != null && hitAudioSource.isActiveAndEnabled)
            {
                PlayActionClip(hitAudioSource, punchHitClip, .8f);
            }
            PlayHit();
        }

        private void OnStunned()
        {
            if (stunAudioSource != null && stunAudioSource.isActiveAndEnabled)
            {
                PlayActionClip(stunAudioSource, stunSoundClip, .8f);
            }

            punchUntilTime = 0f;
            hitUntilTime = 0f;
            if (movement != null && movement.Posture == PlayerPosture.Prone)
            {
                ClearOneShot();
                return;
            }

            PlayOneShot(StunStartState, StunStartSeconds);
        }

        private AudioSource CreateCombatAudioSource(string objectName)
        {
            var audioObject = new GameObject(objectName);
            audioObject.transform.SetParent(transform, false);
            // Never reuse footsteps or network voice sources.
            var source = audioObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 2f;
            source.maxDistance = 15f;
            source.dopplerLevel = 0f;
            return source;
        }

        private void PlayPunch()
        {
            if (punchAudioSource != null && punchAudioSource.isActiveAndEnabled)
            {
                PlayActionClip(punchAudioSource, punchSwingClip, .8f);
            }
            // Network peers choose the same hand, even if an attack update was skipped.
            leftPunch = usesNetworkState
                ? (networkAttackSequence & 1) == 0
                : Time.time <= punchUntilTime + 0.35f && punchUntilTime > 0f && !leftPunch;
            punchStartedTime = Time.time;
            punchUntilTime = Time.time + PunchDuration;
            hitUntilTime = 0f;
            oneShotUntilTime = 0f;
            var clip = ResolveCurrentCombatClip(isHit: false);
            currentState = clip;
            animator.CrossFadeInFixedTime(clip, 0.05f, 0, 0f);
        }

        private void PlayHit()
        {
            if (combatant != null && combatant.IsStunned)
            {
                // Knockout plays Stun_Start instead of a flinch. Earlier hits
                // already used the posture clip from ResolveHitClip.
                return;
            }

            hitUntilTime = Time.time + HitSeconds;
            punchUntilTime = 0f;
            oneShotUntilTime = 0f;
            var clip = ResolveCurrentCombatClip(isHit: true);
            currentState = clip;
            animator.CrossFadeInFixedTime(clip, 0.05f, 0, 0f);
        }

        private string ResolveCurrentCombatClip(bool isHit)
        {
            var settings = movement.MovementSettings;
            var speed = usesNetworkState ? networkSpeed : movement.PlanarSpeed;
            var carrying = ResolveCarrying();
            return isHit
                ? ResolveHitClip(
                    movement.Posture, speed, settings.WalkSpeed, settings.SprintSpeed, carrying)
                : ResolvePunchClip(
                    movement.Posture, speed, settings.WalkSpeed, settings.SprintSpeed, leftPunch);
        }

        public void PlayPickup()
        {
            if (pickupAudioSource != null && pickupAudioSource.isActiveAndEnabled)
            {
                PlayActionClip(pickupAudioSource, pickupSoundClip, .8f);
            }
            var clip = ResolvePickupClip(movement.Posture);
            PlayOneShot(clip, ClipSeconds(clip));
        }

        public void PlayPutDown()
        {
            if (putDownAudioSource != null && putDownAudioSource.isActiveAndEnabled)
            {
                PlayActionClip(putDownAudioSource, putDownSoundClip, .8f);
            }
            var clip = ResolvePutDownClip(movement.Posture);
            PlayOneShot(clip, ClipSeconds(clip));
        }

        public void PlayThrow()
        {
            if (throwAudioSource != null && throwAudioSource.isActiveAndEnabled)
            {
                PlayActionClip(throwAudioSource, throwSoundClip, .8f);
            }
            var settings = movement.MovementSettings;
            var speed = usesNetworkState ? networkSpeed : movement.PlanarSpeed;
            var clip = ResolveThrowClip(
                movement.Posture, speed, settings.WalkSpeed, settings.SprintSpeed);
            var offset = ThrowForwardStartSeconds(movement.Posture);
            PlayOneShot(clip, 24f / 30f - offset);
            // The item is released now. Skip the authored wind-up on every peer.
            currentState = clip;
            animator.speed = 1f;
            animator.CrossFadeInFixedTime(clip, 0.05f, 0, offset);
        }

        internal static float ThrowForwardStartSeconds(PlayerPosture posture) =>
            (posture == PlayerPosture.Prone ? 8f : 10f) / 30f;

        internal static string ResolvePickupClip(PlayerPosture posture) => posture switch
        {
            PlayerPosture.Crouching => "PutUp_TwoHands_Crouch",
            PlayerPosture.Prone => "PutUp_TwoHands_Prone",
            _ => "PutUp_TwoHands",
        };

        internal static string ResolvePutDownClip(PlayerPosture posture) => posture switch
        {
            PlayerPosture.Crouching => "PutDown_TwoHands_Crouch",
            PlayerPosture.Prone => "PutDown_TwoHands_Prone",
            _ => "PutDown_TwoHands",
        };

        private static float ClipSeconds(string clip)
        {
            if (clip.StartsWith("PutUp_TwoHands", System.StringComparison.Ordinal) ||
                clip.StartsWith("PutDown_TwoHands", System.StringComparison.Ordinal))
            {
                return 20f / 30f;
            }

            return clip switch
            {
                "Pickup_Low" or "PutDown_Low" => 60f / 30f,
                _ => 48f / 30f,
            };
        }

        public void ApplyNetworkState(
            float planarSpeed,
            bool grounded,
            int attackSequence)
        {
            ApplyNetworkState(planarSpeed, grounded, attackSequence, Vector2.zero, false, 0f);
        }

        private bool ResolveCarrying()
        {
            if (carryOverride) return true;
            return usesNetworkState
                ? networkCarrying
                : interactor != null && interactor.CarriedItem != null;
        }

        /// <summary>
        /// 손에 든 것이 없어도 들기 자세를 유지하게 한다 (S15P21D205-1087).
        /// </summary>
        /// <remarks>
        /// 엔딩 유치장은 진짜 물건 대신 표시용 복제본을 손에 붙인다. 그때 진짜 물건은 숨기고
        /// 잊게 만들며(로컬), 결과 씬에서는 들기 상태 자체를 끄고 보낸다(원격). 그대로 두면
        /// 물건은 손에 있는데 팔만 내려가므로, 무대에 세운 동안만 들기 자세를 못 박는다.
        /// </remarks>
        public void SetCarryOverride(bool force)
        {
            carryOverride = force;
        }

        public void ApplyNetworkState(
            float planarSpeed,
            bool grounded,
            int attackSequence,
            Vector2 planarDirectionLocal,
            bool carrying)
        {
            ApplyNetworkState(planarSpeed, grounded, attackSequence, planarDirectionLocal, carrying, 0f);
        }

        public void ApplyNetworkState(
            float planarSpeed,
            bool grounded,
            int attackSequence,
            Vector2 planarDirectionLocal,
            bool carrying,
            float lookPitchDegrees)
        {
            ApplyNetworkState(planarSpeed, grounded, attackSequence, planarDirectionLocal, carrying, lookPitchDegrees, 0, 0);
        }

        public void ApplyNetworkState(
            float planarSpeed,
            bool grounded,
            int attackSequence,
            Vector2 planarDirectionLocal,
            bool carrying,
            float lookPitchDegrees,
            int emoteSequence,
            int emoteId)
        {
            if (!usesNetworkState)
            {
                usesNetworkState = true;
                networkAttackSequence = attackSequence;
                networkEmoteSequence = emoteSequence;
            }
            else
            {
                if (networkAttackSequence != attackSequence)
                {
                    networkAttackSequence = attackSequence;
                    PlayPunch();
                }

                if (networkEmoteSequence != emoteSequence)
                {
                    networkEmoteSequence = emoteSequence;
                    TryPlayEmote((EmoteId)emoteId);
                }
            }

            networkSpeed = Mathf.Max(0f, planarSpeed);
            networkGrounded = grounded;
            networkMoveLocal = planarDirectionLocal;
            networkCarrying = carrying;
            networkLookPitch = lookPitchDegrees;
        }

        private void Update()
        {
            UpdateJumpAudio();
            if (animator.runtimeAnimatorController != null &&
                HasParameter(animator, "Speed"))
            {
                animator.SetFloat(
                    "Speed",
                    usesNetworkState ? networkSpeed : movement.PlanarSpeed,
                    SpeedDampTime,
                    Time.deltaTime);
            }

            var desiredState = ResolveDesiredState();
            if (desiredState != currentState)
            {
                // Changing posture/speed during the same punch must not restart its wind-up.
                var punchOffset = desiredState.StartsWith("Punch", System.StringComparison.Ordinal) &&
                                  currentState != null && currentState.StartsWith("Punch", System.StringComparison.Ordinal)
                    ? Mathf.Clamp(Time.time - punchStartedTime, 0f, 24f / 30f)
                    : 0f;
                currentState = desiredState;
                animator.CrossFadeInFixedTime(desiredState, CrossFadeSeconds, 0, punchOffset);
            }

            var planarSpeed = usesNetworkState ? networkSpeed : movement.PlanarSpeed;
            var settings = movement.MovementSettings;
            animator.speed = ResolvePlaybackSpeed(
                desiredState,
                planarSpeed,
                settings.WalkSpeed,
                settings.SprintSpeed,
                settings.CrouchSpeed,
                settings.ProneSpeed,
                locomotionPlaybackScale);
        }

        private void LateUpdate()
        {
            ApplyActionAudioPresentation();
            TickLandAudio();
            footstepAudio?.Tick(animator, currentState,
                usesNetworkState ? networkGrounded : movement.IsGrounded, movement.Posture);
            ApplyLookAim();
        }

        private void CacheLookBones()
        {
            foreach (var candidate in GetComponentsInChildren<Transform>(true))
            {
                if (neckBone == null && candidate.name == "Neck")
                {
                    neckBone = candidate;
                }
                else if (headBone == null && candidate.name == "Head")
                {
                    headBone = candidate;
                }
            }
        }

        private void ApplyLookAim()
        {
            if (IsStunView || neckBone == null && headBone == null)
            {
                return;
            }

            PlayerLookAim.Apply(
                transform,
                neckBone,
                headBone,
                ResolveLookPitch(),
                movement != null ? movement.Posture : PlayerPosture.Standing);
        }

        private float ResolveLookPitch()
        {
            if (cameraRig != null && cameraRig.FollowTarget == transform)
            {
                return cameraRig.PitchDegrees;
            }

            return networkLookPitch;
        }

        private bool IsLocalPresentation =>
            !usesNetworkState || (combatant != null && combatant.PresentsLocalScreen);

        internal static float ActionSpatialBlend(bool localPresentation) =>
            localPresentation ? 0f : 1f;

        private void ApplyActionAudioPresentation()
        {
            var blend = ActionSpatialBlend(IsLocalPresentation);
            SetSpatialBlend(punchAudioSource, blend);
            SetSpatialBlend(hitAudioSource, blend);
            SetSpatialBlend(jumpAudioSource, blend);
            SetSpatialBlend(pickupAudioSource, blend);
            SetSpatialBlend(putDownAudioSource, blend);
            SetSpatialBlend(throwAudioSource, blend);
            SetSpatialBlend(stunAudioSource, blend);
            SetSpatialBlend(landAudioSource, blend);
            SetSpatialBlend(postureSwooshAudioSource, blend);
            footstepAudio?.SetSpatialBlend(blend);
        }

        private static void SetSpatialBlend(AudioSource source, float blend)
        {
            if (source != null)
            {
                source.spatialBlend = blend;
            }
        }

        private void PlayActionClip(AudioSource source, AudioClip clip, float relativeVolume)
        {
            if (source == null)
            {
                return;
            }

            source.spatialBlend = ActionSpatialBlend(IsLocalPresentation);
            PlayerFootstepAudio.PlayEffects(source, clip, relativeVolume);
        }

        private void UpdateJumpAudio()
        {
            var height = transform.position.y;
            var rise = height - previousJumpHeight;
            previousJumpHeight = height;
            var grounded = usesNetworkState ? networkGrounded : movement.IsGrounded;
            var stunned = combatant != null && combatant.IsStunned;
            if (grounded)
            {
                if (ShouldPlayLandSound(jumpWasAirborne, jumpSoundPlayed, grounded, movement.Posture) &&
                    !stunned)
                {
                    landSoundPending = true;
                }

                jumpGroundedSeen = true;
                jumpSoundPlayed = false;
                jumpWasAirborne = false;
                return;
            }

            landSoundPending = false;
            jumpWasAirborne = true;
            // Observe actual upward movement, not input: avoids sounds for rejected
            // jump inputs, walking off a ledge, and spawning in mid-air.
            if (!ShouldPlayJumpSound(jumpGroundedSeen, jumpSoundPlayed, grounded, rise, movement.Posture) ||
                stunned) return;
            jumpSoundPlayed = true;
            if (jumpAudioSource != null && jumpAudioSource.isActiveAndEnabled)
            {
                PlayActionClip(jumpAudioSource, jumpClip, .8f);
            }
        }

        private void TickLandAudio()
        {
            if (!landSoundPending) return;
            if (combatant != null && combatant.IsStunned)
            {
                landSoundPending = false;
                return;
            }

            // Physics already grounded this frame; play once the land clip is the
            // current pose so the thud matches the body hitting the floor.
            if (!ShouldPlayPendingLandSound(landSoundPending, IsLandState(currentState)))
                return;
            landSoundPending = false;
            if (landAudioSource == null || !landAudioSource.isActiveAndEnabled || landClip == null)
                return;
            PlayActionClip(landAudioSource, landClip, LandAudioVolume);
        }

        internal static bool ShouldPlayPendingLandSound(bool pending, bool landClipActive) =>
            pending && landClipActive;

        internal static bool ShouldPlayJumpSound(bool groundedSeen, bool alreadyPlayed,
            bool grounded, float rise, PlayerPosture posture) =>
            groundedSeen && !alreadyPlayed && !grounded && rise > .001f && rise < 1f &&
            posture == PlayerPosture.Standing;

        internal static bool ShouldPlayLandSound(bool wasAirborne, bool jumped,
            bool grounded, PlayerPosture posture) =>
            wasAirborne && jumped && grounded && posture == PlayerPosture.Standing;

        private string ResolveDesiredState()
        {
            if (combatant != null && combatant.IsStunned)
            {
                punchUntilTime = 0f;
                hitUntilTime = 0f;
                lastPosture = movement.Posture;
                wasGrounded = usesNetworkState ? networkGrounded : movement.IsGrounded;
                wasStunned = true;
                if (Time.time < oneShotUntilTime && oneShotState == StunStartState)
                {
                    return StunStartState;
                }

                ClearOneShot();
                return StunIdleState;
            }

            if (wasStunned)
            {
                wasStunned = false;
                PlayOneShot(StunEndState, StunEndSeconds);
            }

            if (Time.time < hitUntilTime)
            {
                return ResolveCurrentCombatClip(isHit: true);
            }

            if (Time.time < punchUntilTime)
            {
                return ResolveCurrentCombatClip(isHit: false);
            }

            var posture = movement.Posture;
            var settings = movement.MovementSettings;
            var speed = usesNetworkState ? networkSpeed : movement.PlanarSpeed;
            var move = usesNetworkState ? networkMoveLocal : movement.PlanarVelocityLocal;
            var carrying = ResolveCarrying();
            var grounded = usesNetworkState ? networkGrounded : movement.IsGrounded;

            if (!grounded)
            {
                var leftGround = wasGrounded;
                wasGrounded = false;
                lastPosture = posture;
                // 웅크리기·엎드리기는 콜라이더 출렁임으로 잠깐 떠도 서서 Fall/Land를 쓰지 않는다.
                if (posture != PlayerPosture.Standing)
                {
                    ClearOneShot();
                    return ResolveLocomotionClip(
                        posture, carrying, 0f, Vector2.zero, settings.WalkSpeed, settings.SprintSpeed);
                }

                if (leftGround)
                {
                    PlayOneShot(ResolveJumpClip(carrying), JumpSeconds);
                }

                if (Time.time < oneShotUntilTime && IsJumpState(oneShotState))
                {
                    return oneShotState;
                }

                // 들고 점프 클립이 끝난 뒤에도 손 든 포즈를 유지한다(Carry_Fall 없음).
                return carrying ? CarryJumpState : AirborneState;
            }

            if (!wasGrounded)
            {
                wasGrounded = true;
                if (posture == PlayerPosture.Standing)
                {
                    PlayOneShot(ResolveLandClip(carrying), LandSeconds);
                }
            }

            if (posture != lastPosture)
            {
                var from = lastPosture;
                var transition = TransitionClip(from, posture, carrying);
                lastPosture = posture;
                if (transition != null)
                {
                    PlayOneShot(transition, TransitionSeconds(transition));
                    if (ShouldPlayPostureSwoosh(from, posture))
                    {
                        PlayPostureSwoosh();
                    }
                }
            }

            var direction = ResolveDirection(move, lastLocomotionDirection);
            lastLocomotionDirection = direction;
            var locomotion = ResolveLocomotionClip(
                posture,
                carrying,
                speed,
                direction,
                settings.WalkSpeed,
                settings.SprintSpeed);

            if (Time.time < oneShotUntilTime && !string.IsNullOrEmpty(oneShotState))
            {
                // 집기·내려놓기 중 이동하면 바로 들고 걷기/기어가기로 넘긴다.
                if (IsMovementInterruptible(oneShotState) && IsLocomotionMoving(locomotion))
                {
                    ClearOneShot();
                    return locomotion;
                }

                return oneShotState;
            }

            return locomotion;
        }

        private void PlayOneShot(string state, float seconds)
        {
            oneShotState = state;
            oneShotUntilTime = Time.time + seconds;
        }

        private void ClearOneShot()
        {
            oneShotState = null;
            oneShotUntilTime = 0f;
        }

        internal static string ResolveJumpClip(bool carrying) =>
            carrying ? CarryJumpState : JumpState;

        internal static string ResolveLandClip(bool carrying) =>
            carrying ? CarryLandState : LandState;

        internal static string ResolveThrowClip(
            PlayerPosture posture,
            float planarSpeed,
            float walkSpeed,
            float sprintSpeed)
        {
            if (posture == PlayerPosture.Prone)
            {
                return planarSpeed > 0.15f ? "Throw_TwoHands_Crawl" : "Throw_TwoHands_Prone";
            }

            return ResolveCombatLocomotionClip(
                "Throw_TwoHands", posture, planarSpeed, walkSpeed, sprintSpeed);
        }

        internal static string ResolvePunchClip(
            PlayerPosture posture,
            float planarSpeed,
            float walkSpeed,
            float sprintSpeed,
            bool leftHand = false) =>
            ResolveCombatLocomotionClip(leftHand ? "Punch_Left" : "Punch", posture, planarSpeed, walkSpeed, sprintSpeed);

        internal static string ResolveHitClip(
            PlayerPosture posture,
            float planarSpeed,
            float walkSpeed,
            float sprintSpeed,
            bool carrying = false)
        {
            if (posture == PlayerPosture.Prone)
            {
                var crawling = planarSpeed > 0.15f;
                if (carrying)
                {
                    return crawling ? "Carry_TwoHands_Hit_Crawl" : "Carry_TwoHands_Hit_Prone";
                }

                return crawling ? "Hit_Crawl" : "Hit_Prone";
            }

            if (carrying)
            {
                return ResolveCombatLocomotionClip(
                    "Carry_TwoHands_Hit", posture, planarSpeed, walkSpeed, sprintSpeed);
            }

            return ResolveCombatLocomotionClip("Hit", posture, planarSpeed, walkSpeed, sprintSpeed);
        }

        internal static string ResolveCombatLocomotionClip(
            string prefix,
            PlayerPosture posture,
            float planarSpeed,
            float walkSpeed,
            float sprintSpeed)
        {
            if (posture == PlayerPosture.Crouching)
            {
                return planarSpeed > 0.15f ? $"{prefix}_Crouch_Walk" : $"{prefix}_Crouch";
            }

            if (planarSpeed >= sprintSpeed * 0.85f)
            {
                return $"{prefix}_Run";
            }

            if (planarSpeed >= walkSpeed * 0.35f)
            {
                return $"{prefix}_Walk";
            }

            return prefix;
        }

        internal static bool IsJumpState(string state) =>
            state == JumpState || state == CarryJumpState;

        internal static bool IsStunState(string state) =>
            state == StunStartState ||
            state == StunIdleState ||
            state == StunEndState ||
            state == StunnedState;

        internal static bool IsMovementInterruptible(string state) =>
            !string.IsNullOrEmpty(state) &&
            (state.StartsWith("Pickup", System.StringComparison.Ordinal) ||
             state.StartsWith("PutUp", System.StringComparison.Ordinal) ||
             state.StartsWith("PutDown", System.StringComparison.Ordinal) ||
             EmoteCatalog.IsOneShotEmoteState(state) ||
             IsLandState(state));

        internal static bool IsLandState(string state) =>
            state == LandState || state == CarryLandState;

        internal static bool IsLocomotionMoving(string state) =>
            !string.IsNullOrEmpty(state) &&
            (state.IndexOf("Walk", System.StringComparison.Ordinal) >= 0 ||
             state.IndexOf("Run", System.StringComparison.Ordinal) >= 0 ||
             state.IndexOf("Crawl", System.StringComparison.Ordinal) >= 0);

        internal static string ResolveLocomotionClip(
            PlayerPosture posture,
            bool carrying,
            float speed,
            Vector2 localMove,
            float walkSpeed,
            float sprintSpeed) => ResolveLocomotionClip(
                posture,
                carrying,
                speed,
                ResolveDirection(localMove),
                walkSpeed,
                sprintSpeed);

        private static string ResolveLocomotionClip(
            PlayerPosture posture,
            bool carrying,
            float speed,
            MoveDirection direction,
            float walkSpeed,
            float sprintSpeed)
        {
            var moving = direction != MoveDirection.Neutral && speed >= 0.35f;

            if (posture == PlayerPosture.Prone)
            {
                if (!moving)
                {
                    return carrying ? "Carry_TwoHands_Prone_Idle" : "Prone_Idle";
                }

                return carrying
                    ? DirectionClip(
                        "Carry_TwoHands_Crawl_Forward",
                        "Carry_TwoHands_Crawl_Back",
                        "Carry_TwoHands_Crawl_Left",
                        "Carry_TwoHands_Crawl_Right",
                        direction)
                    : DirectionClip("Crawl_Forward", "Crawl_Back", "Crawl_Left", "Crawl_Right", direction);
            }

            if (posture == PlayerPosture.Crouching)
            {
                if (!moving)
                {
                    return carrying ? "Carry_TwoHands_Crouch_Idle" : "Crouch_Idle";
                }

                return carrying
                    ? DirectionClip(
                        "Carry_TwoHands_Crouch_Walk_Forward",
                        "Carry_TwoHands_Crouch_Walk_Back",
                        "Carry_TwoHands_Crouch_Walk_Left",
                        "Carry_TwoHands_Crouch_Walk_Right",
                        direction)
                    : DirectionClip(
                        "Crouch_Walk_Forward",
                        "Crouch_Walk_Back",
                        "Crouch_Walk_Left",
                        "Crouch_Walk_Right",
                        direction);
            }

            if (!moving)
            {
                return carrying ? "Carry_TwoHands" : "Idle";
            }

            if (speed >= (walkSpeed + sprintSpeed) * 0.5f)
            {
                return carrying
                    ? DirectionClip(
                        "Carry_TwoHands_Run_Forward",
                        "Carry_TwoHands_Run_Back",
                        "Carry_TwoHands_Run_Left",
                        "Carry_TwoHands_Run_Right",
                        direction)
                    : DirectionClip("Run_Forward", "Run_Back", "Run_Left", "Run_Right", direction);
            }

            return carrying
                ? DirectionClip(
                    "Carry_TwoHands_Walk_Forward",
                    "Carry_TwoHands_Walk_Back",
                    "Carry_TwoHands_Walk_Left",
                    "Carry_TwoHands_Walk_Right",
                    direction)
                : DirectionClip("Walk_Forward", "Walk_Back", "Walk_Left", "Walk_Right", direction);
        }

        internal static MoveDirection ResolveDirection(
            Vector2 localMove,
            MoveDirection previousDirection = MoveDirection.Neutral)
        {
            if (localMove.sqrMagnitude < DirectionDeadZone * DirectionDeadZone)
            {
                return MoveDirection.Neutral;
            }

            var horizontal = Mathf.Abs(localMove.x);
            var vertical = Mathf.Abs(localMove.y);
            if (horizontal > vertical + DirectionDominanceHysteresis)
            {
                return localMove.x > 0f ? MoveDirection.Left : MoveDirection.Right;
            }

            if (vertical > horizontal + DirectionDominanceHysteresis)
            {
                return localMove.y > 0f ? MoveDirection.Forward : MoveDirection.Back;
            }

            // 대각선은 별도 클립이 없으므로 직전 방향을 유지한다. 처음 누른 대각선은
            // 앞/뒤 축을 우선해 Walk/Run 계열의 전후 이동 모션을 일관되게 사용한다.
            if (previousDirection != MoveDirection.Neutral)
            {
                return previousDirection;
            }

            return localMove.y >= 0f ? MoveDirection.Forward : MoveDirection.Back;
        }

        private static string DirectionClip(
            string forward,
            string back,
            string left,
            string right,
            MoveDirection direction) => direction switch
        {
            MoveDirection.Back => back,
            MoveDirection.Left => left,
            MoveDirection.Right => right,
            _ => forward
        };

        /// <summary>
        /// 이동 클립만 실제 속도 / 기준 속도로 재생 배율을 맞춘다.
        /// Idle·원샷·기절 등은 1배를 유지한다.
        /// </summary>
        internal static float ResolvePlaybackSpeed(
            string state,
            float planarSpeed,
            float walkSpeed,
            float sprintSpeed,
            float crouchSpeed,
            float proneSpeed,
            float scale = 1f)
        {
            if (string.IsNullOrEmpty(state))
            {
                return 1f;
            }

            // Punch_Walk / Hit_Run 등은 하체가 섞여 있어도 임팩트 타이밍을 늘리지 않는다.
            if (state.StartsWith("Punch", System.StringComparison.Ordinal) ||
                state.StartsWith("Hit", System.StringComparison.Ordinal) ||
                state.StartsWith("Throw", System.StringComparison.Ordinal))
            {
                return 1f;
            }

            var reference = LocomotionReferenceSpeed(
                state, walkSpeed, sprintSpeed, crouchSpeed, proneSpeed);
            if (reference <= 0f)
            {
                return 1f;
            }

            var rate = (planarSpeed / reference) * Mathf.Max(0.1f, scale);
            return Mathf.Clamp(rate, MinLocomotionPlayback, MaxLocomotionPlayback);
        }

        private static float LocomotionReferenceSpeed(
            string state,
            float walkSpeed,
            float sprintSpeed,
            float crouchSpeed,
            float proneSpeed)
        {
            if (state.IndexOf("Crawl", System.StringComparison.Ordinal) >= 0)
            {
                return proneSpeed;
            }

            if (state.IndexOf("Crouch_Walk", System.StringComparison.Ordinal) >= 0)
            {
                return crouchSpeed;
            }

            if (state.IndexOf("Run", System.StringComparison.Ordinal) >= 0)
            {
                return sprintSpeed;
            }

            if (state.IndexOf("Walk", System.StringComparison.Ordinal) >= 0)
            {
                return walkSpeed;
            }

            return 0f;
        }

        internal static bool ShouldPlayPostureSwoosh(PlayerPosture from, PlayerPosture to) =>
            from != to && TransitionClip(from, to, false) != null;

        private void PlayPostureSwoosh()
        {
            if (postureSwooshAudioSource != null && postureSwooshAudioSource.isActiveAndEnabled)
            {
                PlayActionClip(postureSwooshAudioSource, postureSwooshClip, PostureSwooshAudioVolume);
            }
        }

        internal static string TransitionClip(PlayerPosture from, PlayerPosture to, bool carrying) => (from, to) switch
        {
            (PlayerPosture.Standing, PlayerPosture.Crouching) =>
                carrying ? "Carry_TwoHands_Crouch_Start" : "Crouch_Start",
            (PlayerPosture.Crouching, PlayerPosture.Standing) =>
                carrying ? "Carry_TwoHands_Crouch_End" : "Crouch_End",
            (PlayerPosture.Standing, PlayerPosture.Prone) =>
                carrying ? "Carry_TwoHands_Prone_Start" : "Prone_Start",
            (PlayerPosture.Crouching, PlayerPosture.Prone) =>
                carrying ? "Carry_TwoHands_Crouch_To_Prone" : "Crouch_To_Prone",
            (PlayerPosture.Prone, PlayerPosture.Standing) =>
                carrying ? "Carry_TwoHands_Prone_End" : "Prone_End",
            (PlayerPosture.Prone, PlayerPosture.Crouching) =>
                carrying ? "Carry_TwoHands_Prone_To_Crouch" : "Prone_To_Crouch",
            _ => null
        };

        private static float TransitionSeconds(string clip)
        {
            if (clip.IndexOf("Crouch_To_Prone", System.StringComparison.Ordinal) >= 0 ||
                clip.IndexOf("Prone_To_Crouch", System.StringComparison.Ordinal) >= 0)
            {
                return 36f / 30f;
            }

            return 24f / 30f;
        }

        private static bool HasParameter(Animator target, string name)
        {
            foreach (var parameter in target.parameters)
            {
                if (parameter.name == name)
                {
                    return true;
                }
            }

            return false;
        }

        internal enum MoveDirection
        {
            Neutral,
            Forward,
            Back,
            Left,
            Right
        }
    }
}
