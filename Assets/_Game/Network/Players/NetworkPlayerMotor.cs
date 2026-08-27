using Fusion;
using Game.Core.Players;
using UnityEngine;

namespace Game.Network.Players
{
    /// <summary>
    /// Collects input on the owning peer and applies the shared movement model
    /// once on State Authority. NetworkTransform distributes that single result
    /// to every peer, so clients never become competing transform writers.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(NetworkTransform))]
    public sealed class NetworkPlayerMotor : NetworkBehaviour
    {
        private CharacterController _controller;
        private NetworkTransform _networkTransform;
        private IPlayerInputIntentSource _inputSource;

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
            if (!IsConfigured || Object == null || !Object.HasStateAuthority)
            {
                return;
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
            VerticalVelocity = PlayerMovementKinematics.StepVerticalVelocity(
                VerticalVelocity,
                _controller.isGrounded,
                input.WasPressed(NetworkPlayerButton.Jump, PreviousButtons),
                Physics.gravity.y,
                deltaTime,
                settings);

            var direction = ToWorldDirection(input.Move, input.LookYawDegrees);
            var speed = PlayerMovementKinematics.MoveSpeed(
                settings,
                input.IsPressed(NetworkPlayerButton.Sprint));

            var velocity = direction * speed;
            velocity.y = VerticalVelocity;
            _controller.Move(velocity * deltaTime);

            AnimationSpeed = direction.magnitude * speed;
            AnimationGrounded = _controller.isGrounded;

            if (input.WasPressed(NetworkPlayerButton.Attack, PreviousButtons))
            {
                AttackSequence++;
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

            _networkTransform.Teleport(pose.position, pose.rotation);
            ResetMotion();
            return true;
        }

        internal static Vector3 ToWorldDirection(Vector2 move, float lookYawDegrees)
        {
            var local = Vector3.ClampMagnitude(new Vector3(move.x, 0f, move.y), 1f);
            return Quaternion.Euler(0f, lookYawDegrees, 0f) * local;
        }
    }
}
