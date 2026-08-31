using Fusion;
using Fusion.Addons.KCC;
using Game.Core.Players;
using UnityEngine;

namespace Game.Network.Players
{
    /// <summary>
    /// Feeds owner input into Fusion KCC. KCC is the only component that writes
    /// the networked position; this component owns gameplay state only.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(KCC))]
    [RequireComponent(typeof(PlayerKCCMovementProcessor))]
    public sealed class NetworkPlayerMotor : NetworkBehaviour
    {
        private KCC kcc;
        private PlayerKCCMovementProcessor movementProcessor;
        private IPlayerInputIntentSource inputSource;
        private bool hasPendingTeleport;
        private Pose pendingTeleport;
        private PlayerPosture? pendingPosture;

        [Networked]
        private NetworkBool ScenePlacementReady { get; set; }

        [Networked]
        private NetworkButtons PreviousButtons { get; set; }

        [Networked]
        public NetworkBool ControlsEnabled { get; private set; }

        [Networked]
        public float DesiredMoveSpeed { get; private set; }

        [Networked]
        public float AnimationSpeed { get; private set; }

        [Networked]
        public NetworkBool AnimationGrounded { get; private set; }

        [Networked]
        public int AttackSequence { get; private set; }

        [Networked]
        private float NextAttackAllowedAt { get; set; }

        [SerializeField, Min(0.1f)]
        [Tooltip("공격 재입력 대기 시간. CombatConfig의 PunchMotionSeconds와 맞춘다.")]
        private float attackCooldownSeconds = 0.9f;

        [Networked]
        public PlayerPosture Posture { get; private set; }

        public bool IsScenePlacementReady => Object != null && Object.IsValid &&
                                             ScenePlacementReady && !hasPendingTeleport;

        private bool IsConfigured =>
            kcc != null && movementProcessor != null && inputSource != null;

        public bool TryGetSimulationPose(out Pose pose)
        {
            if (kcc == null || Object == null || !Object.IsValid)
            {
                pose = default;
                return false;
            }
            pose = new Pose(kcc.FixedData.TargetPosition, kcc.FixedData.TransformRotation);
            return true;
        }

        private void Awake()
        {
            kcc = GetComponent<KCC>();
            movementProcessor = GetComponent<PlayerKCCMovementProcessor>();

            var behaviours = GetComponents<MonoBehaviour>();
            for (var index = 0; index < behaviours.Length; index++)
            {
                if (behaviours[index] is IPlayerInputIntentSource source)
                {
                    inputSource = source;
                    break;
                }
            }

            if (inputSource == null)
            {
                Debug.LogError(
                    "[Movement] NetworkedPlayer has no IPlayerInputIntentSource.",
                    this);
            }
        }

        public override void Spawned()
        {
            if (!IsConfigured)
            {
                return;
            }

            var settings = inputSource.MovementSettings;
            movementProcessor.ConfigureGravity(settings.GravityMultiplier);

            if (Object.HasStateAuthority)
            {
                // The room creates its avatar before the networked lobby scene
                // has finished loading. At that point this object already lives
                // in DontDestroyOnLoad, where there is deliberately no floor.
                // Keep KCC dormant until PlayerSpawner receives a real scene
                // spawn point and activates it through TryTeleport().
                kcc.SetActive(false);
                ScenePlacementReady = false;
                if (!Runner.IsResume)
                {
                    ControlsEnabled = true;
                    DesiredMoveSpeed = settings.WalkSpeed;
                }
                // CopyStateFrom runs before Spawned. Preserve the saved posture instead of
                // standing up inside low geometry, and reapply its local KCC collider shape.
                ApplyPosture(ResolveSpawnPosture(Runner.IsResume, Posture), settings);
            }
        }

        /// <summary>Called only for the local player's object from OnInput.</summary>
        public NetworkPlayerInput CaptureInput()
        {
            if (!IsConfigured || Object == null || !Object.HasInputAuthority)
            {
                return default;
            }

            return NetworkPlayerInput.FromIntent(inputSource.CaptureInputIntent());
        }

        public override void FixedUpdateNetwork()
        {
            if (!ApplyPendingScenePlacement())
            {
                return;
            }

            if (!IsConfigured || Object == null ||
                !GetInput(out NetworkPlayerInput input))
            {
                return;
            }

            if (!ControlsEnabled)
            {
                input = default;
            }

            var settings = inputSource.MovementSettings;
            var grounded = kcc.FixedData.IsGrounded;
            var requestedPosture = ResolvePosture(
                Posture,
                grounded,
                input,
                PreviousButtons);
            TryApplyPosture(requestedPosture, settings);

            var direction = ToWorldDirection(input.Move, input.LookYawDegrees);
            DesiredMoveSpeed = MoveSpeedForPosture(
                settings,
                Posture,
                input.IsPressed(NetworkPlayerButton.Sprint));
            kcc.SetInputDirection(direction);

            if (grounded &&
                input.WasPressed(NetworkPlayerButton.Jump, PreviousButtons))
            {
                var gravity = -Physics.gravity.y * settings.GravityMultiplier;
                var jumpSpeed = Mathf.Sqrt(2f * gravity * settings.JumpHeight);
                kcc.Jump(Vector3.up * jumpSpeed);
            }

            var yaw = Mathf.MoveTowardsAngle(
                kcc.FixedData.LookYaw,
                input.LookYawDegrees,
                settings.RotationSpeedDegrees * Runner.DeltaTime);
            kcc.SetLookRotation(0f, yaw);

            AnimationSpeed = direction.magnitude * DesiredMoveSpeed;
            AnimationGrounded = grounded;

            if (input.WasPressed(NetworkPlayerButton.Attack, PreviousButtons) &&
                Runner.SimulationTime >= NextAttackAllowedAt)
            {
                AttackSequence++;
                NextAttackAllowedAt = Runner.SimulationTime + attackCooldownSeconds;
            }

            PreviousButtons = input.Buttons;
        }

        internal bool TrySetControlsEnabled(bool enabled)
        {
            if (Object == null || !Object.HasStateAuthority)
            {
                return false;
            }

            ControlsEnabled = enabled;
            if (!enabled)
            {
                PreviousButtons = default;
                kcc.SetInputDirection(Vector3.zero);
            }

            return true;
        }

        internal void ResetMotion()
        {
            if (Object == null || !Object.HasStateAuthority || kcc == null)
            {
                return;
            }

            PreviousButtons = default;
            kcc.SetInputDirection(Vector3.zero);
            kcc.SetDynamicVelocity(Vector3.zero);
            kcc.SetKinematicVelocity(Vector3.zero);
        }

        internal bool TryTeleport(Pose pose)
        {
            if (Object == null || !Object.HasStateAuthority || kcc == null)
            {
                return false;
            }

            // Scene callbacks run outside Fusion's fixed tick. KCC changes made
            // there affect render data only and are discarded by the next
            // simulation tick, which previously let the avatar resume falling
            // from its pre-scene position. Apply the complete placement from
            // FixedUpdateNetwork instead.
            pendingTeleport = pose;
            hasPendingTeleport = true;
            return true;
        }

        internal bool TryRestoreScenePose(Pose pose, PlayerPosture posture)
        {
            if (!TryTeleport(pose)) return false;
            pendingPosture = posture;
            return true;
        }

        private bool ApplyPendingScenePlacement()
        {
            if (!IsConfigured || Object == null)
            {
                return false;
            }

            if (Object.HasStateAuthority && hasPendingTeleport)
            {
                hasPendingTeleport = false;
                if (pendingPosture.HasValue)
                {
                    ApplyPosture(pendingPosture.Value, inputSource.MovementSettings);
                    pendingPosture = null;
                }
                kcc.SetPosition(pendingTeleport.position);
                kcc.SetLookRotation(pendingTeleport.rotation);
                ResetMotion();
                kcc.SetActive(true);
                ScenePlacementReady = true;
            }
            else if (Object.HasStateAuthority && !ScenePlacementReady)
            {
                // Ensure fixed data is inactive as well as render data. Calling
                // SetActive only from Spawned is insufficient when Spawned runs
                // outside the simulation tick.
                kcc.SetActive(false);
            }

            // Input-authority peers used to start predicting from the prefab's
            // temporary position before the host had placed the avatar in the
            // loaded scene. The host saw the corrected body, while the owner
            // could remain pressed into the floor. Gate every peer on the same
            // replicated placement state.
            return ScenePlacementReady;
        }

        internal static Vector3 ToWorldDirection(Vector2 move, float lookYawDegrees)
        {
            var local = Vector3.ClampMagnitude(new Vector3(move.x, 0f, move.y), 1f);
            return Quaternion.Euler(0f, lookYawDegrees, 0f) * local;
        }

        internal static PlayerPosture ResolveSpawnPosture(bool isResuming, PlayerPosture savedPosture) =>
            isResuming ? savedPosture : PlayerPosture.Standing;

        internal static PlayerPosture ResolvePosture(
            PlayerPosture current,
            bool grounded,
            NetworkPlayerInput input,
            NetworkButtons previous)
        {
            if (!grounded)
            {
                return current;
            }

            if (input.WasPressed(NetworkPlayerButton.Crouch, previous))
            {
                current = current == PlayerPosture.Crouching
                    ? PlayerPosture.Standing
                    : PlayerPosture.Crouching;
            }

            if (input.WasPressed(NetworkPlayerButton.Prone, previous))
            {
                current = current == PlayerPosture.Prone
                    ? PlayerPosture.Standing
                    : PlayerPosture.Prone;
            }

            if (input.WasPressed(NetworkPlayerButton.Jump, previous) &&
                current != PlayerPosture.Standing)
            {
                current = PlayerPosture.Standing;
            }

            return current;
        }

        internal static float MoveSpeedForPosture(
            PlayerMovementSettings settings,
            PlayerPosture posture,
            bool sprinting) => posture switch
        {
            PlayerPosture.Crouching => settings.CrouchSpeed,
            PlayerPosture.Prone => settings.ProneSpeed,
            _ => PlayerMovementKinematics.MoveSpeed(settings, sprinting)
        };

        private void TryApplyPosture(
            PlayerPosture requested,
            PlayerMovementSettings settings)
        {
            if (requested == Posture)
            {
                return;
            }

            var height = HeightForPosture(requested, settings);
            if (height > kcc.Settings.Height && !HasHeadroom(height))
            {
                return;
            }

            ApplyPosture(requested, settings);
        }

        private void ApplyPosture(
            PlayerPosture posture,
            PlayerMovementSettings settings)
        {
            Posture = posture;
            kcc.SetShape(
                EKCCShape.Capsule,
                kcc.Settings.Radius,
                HeightForPosture(posture, settings));
        }

        private bool HasHeadroom(float targetHeight)
        {
            var radius = kcc.Settings.Radius * 0.95f;
            var currentHeight = kcc.Settings.Height;
            var origin = transform.position + Vector3.up * (currentHeight - radius);
            var hits = Physics.SphereCastAll(
                origin,
                radius,
                Vector3.up,
                targetHeight - currentHeight,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            for (var index = 0; index < hits.Length; index++)
            {
                if (!hits[index].collider.transform.IsChildOf(transform))
                {
                    return false;
                }
            }

            return true;
        }

        internal static float HeightForPosture(
            PlayerPosture posture,
            PlayerMovementSettings settings) => posture switch
        {
            PlayerPosture.Crouching => settings.CrouchHeight,
            PlayerPosture.Prone => settings.ProneHeight,
            _ => settings.StandHeight
        };
    }
}
