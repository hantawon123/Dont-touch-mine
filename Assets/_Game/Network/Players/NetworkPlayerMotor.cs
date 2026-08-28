using Fusion;
using Game.Core.Players;
using UnityEngine;

namespace Game.Network.Players
{
    /// <summary>
    /// Collects input on the owning peer and applies the shared movement model
    /// on Input Authority for prediction and State Authority for validation.
    /// NetworkTransform reconciles both simulations for every peer.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(NetworkTransform))]
    public sealed class NetworkPlayerMotor : NetworkBehaviour
    {
        private CharacterController _controller;
        private NetworkTransform _networkTransform;
        private IPlayerInputIntentSource _inputSource;
        private Pose _pendingTeleportPose;
        private bool _reapplyTeleportNextTick;

        [Networked]
        private float VerticalVelocity { get; set; }

        [Networked]
        private NetworkButtons PreviousButtons { get; set; }

        [Networked]
        public NetworkBool ControlsEnabled { get; private set; }

        [Networked]
        public float AnimationSpeed { get; private set; }

        [Networked]
        public NetworkBool AnimationGrounded { get; private set; }

        [Networked]
        public int AttackSequence { get; private set; }

        [Networked]
        private float NextAttackAllowedAt { get; set; }

        [SerializeField, Min(0.1f)]
        [Tooltip("공격 재입력 대기 시간. CombatConfig의 PunchMotionSeconds와 맞춘다. (모션 중 재입력 방지)")]
        private float attackCooldownSeconds = 0.9f;

        [Networked]
        public PlayerPosture Posture { get; private set; }

        private bool IsConfigured =>
            _controller != null && _networkTransform != null && _inputSource != null;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _networkTransform = GetComponent<NetworkTransform>();

            var behaviours = GetComponents<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IPlayerInputIntentSource source)
                {
                    _inputSource = source;
                    break;
                }
            }

            if (_inputSource == null)
            {
                Debug.LogError(
                    "[Movement] NetworkedPlayer has no IPlayerInputIntentSource.",
                    this);
            }
        }

        public override void Spawned()
        {
            if (Object.HasStateAuthority)
            {
                ControlsEnabled = true;
                if (IsConfigured)
                {
                    ApplyPosture(
                        PlayerPosture.Standing,
                        _inputSource.MovementSettings);
                }
            }
        }

        /// <summary>Called only for the local player's object from OnInput.</summary>
        public NetworkPlayerInput CaptureInput()
        {
            if (!IsConfigured || Object == null || !Object.HasInputAuthority)
            {
                return default;
            }

            return NetworkPlayerInput.FromIntent(_inputSource.CaptureInputIntent());
        }

        public override void FixedUpdateNetwork()
        {
            if (!IsConfigured || Object == null)
            {
                return;
            }

            if (_reapplyTeleportNextTick)
            {
                _reapplyTeleportNextTick = false;
                ApplyTeleport(_pendingTeleportPose);
            }

            if (!GetInput(out NetworkPlayerInput input))
            {
                return;
            }

            if (!ControlsEnabled)
            {
                input = default;
            }

            var deltaTime = Runner.DeltaTime;
            var settings = _inputSource.MovementSettings;
            var requestedPosture = ResolvePosture(
                Posture,
                _controller.isGrounded,
                input,
                PreviousButtons);
            TryApplyPosture(requestedPosture, settings);

            VerticalVelocity = PlayerMovementKinematics.StepVerticalVelocity(
                VerticalVelocity,
                _controller.isGrounded,
                input.WasPressed(NetworkPlayerButton.Jump, PreviousButtons),
                Physics.gravity.y,
                deltaTime,
                settings);

            var direction = ToWorldDirection(input.Move, input.LookYawDegrees);
            var speed = MoveSpeedForPosture(
                settings,
                Posture,
                input.IsPressed(NetworkPlayerButton.Sprint));

            var velocity = direction * speed;
            velocity.y = VerticalVelocity;
            _controller.Move(velocity * deltaTime);

            AnimationSpeed = direction.magnitude * speed;
            AnimationGrounded = _controller.isGrounded;

            // 펀치 모션이 끝나기 전에는 공격 재입력을 받지 않는다.
            if (input.WasPressed(NetworkPlayerButton.Attack, PreviousButtons) &&
                Runner.SimulationTime >= NextAttackAllowedAt)
            {
                AttackSequence++;
                NextAttackAllowedAt = Runner.SimulationTime + attackCooldownSeconds;
            }

            if (ControlsEnabled)
            {
                var targetRotation = Quaternion.Euler(0f, input.LookYawDegrees, 0f);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRotation,
                    settings.RotationSpeedDegrees * deltaTime);
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
            }

            return true;
        }

        internal void ResetMotion()
        {
            if (Object != null && Object.HasStateAuthority)
            {
                VerticalVelocity = 0f;
                PreviousButtons = default;
            }
        }

        internal bool TryTeleport(Pose pose)
        {
            if (Object == null || !Object.HasStateAuthority)
            {
                return false;
            }

            ApplyTeleport(pose);
            _pendingTeleportPose = pose;
            _reapplyTeleportNextTick = true;
            ResetMotion();
            return true;
        }

        private void ApplyTeleport(Pose pose)
        {
            // CharacterController and NetworkTransform both cache their previous
            // pose. Suspend collision resolution while updating both writers.
            _controller.enabled = false;
            transform.SetPositionAndRotation(pose.position, pose.rotation);
            _networkTransform.Teleport(pose.position, pose.rotation);
            _controller.enabled = true;
            Physics.SyncTransforms();
        }

        internal static Vector3 ToWorldDirection(Vector2 move, float lookYawDegrees)
        {
            var local = Vector3.ClampMagnitude(new Vector3(move.x, 0f, move.y), 1f);
            return Quaternion.Euler(0f, lookYawDegrees, 0f) * local;
        }

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
            if (height > _controller.height && !HasHeadroom(height))
            {
                return;
            }

            ApplyPosture(requested, settings);
        }

        private void ApplyPosture(
            PlayerPosture posture,
            PlayerMovementSettings settings)
        {
            var height = HeightForPosture(posture, settings);
            Posture = posture;
            _controller.height = height;
            _controller.center = new Vector3(0f, height * 0.5f, 0f);
        }

        private bool HasHeadroom(float targetHeight)
        {
            var radius = _controller.radius * 0.95f;
            var topSphereCenter = transform.position + _controller.center +
                Vector3.up * (_controller.height * 0.5f - _controller.radius);

            return !Physics.SphereCast(
                topSphereCenter,
                radius,
                Vector3.up,
                out _,
                targetHeight - _controller.height,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
        }

        private static float HeightForPosture(
            PlayerPosture posture,
            PlayerMovementSettings settings) => posture switch
        {
            PlayerPosture.Crouching => settings.CrouchHeight,
            PlayerPosture.Prone => settings.ProneHeight,
            _ => settings.StandHeight
        };
    }
}
