using Fusion;
using Game.Core.Players;
using UnityEngine;

namespace Game.Network.Players
{
    /// <summary>
    /// Collects input on the owning peer and simulates the character on State Authority.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class NetworkPlayerMotor : NetworkBehaviour
    {
        private const float GroundedStickVelocity = -2f;

        [Header("Movement")]
        [SerializeField, Min(0f)]
        private float _walkSpeed = 4f;

        [SerializeField, Min(0f)]
        private float _sprintSpeed = 7f;

        [SerializeField, Min(0f)]
        private float _rotationSpeedDegrees = 720f;

        [SerializeField, Min(0f)]
        private float _jumpHeight = 1.1f;

        [SerializeField, Min(0.1f)]
        private float _gravityMultiplier = 2f;

        private CharacterController _controller;
        private IPlayerInputIntentSource _inputSource;

        [Networked]
        private float VerticalVelocity { get; set; }

        [Networked]
        private NetworkButtons PreviousButtons { get; set; }

        [Networked]
        public NetworkBool ControlsEnabled { get; private set; }

        private bool IsConfigured => _controller != null && _inputSource != null;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();

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
            if (!IsConfigured || Object == null || !Object.HasInputAuthority ||
                !ControlsEnabled)
            {
                return default;
            }

            return NetworkPlayerInput.FromIntent(_inputSource.CaptureInputIntent());
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority || !IsConfigured)
            {
                return;
            }

            var input = default(NetworkPlayerInput);
            if (ControlsEnabled)
            {
                GetInput(out input);
            }

            var deltaTime = Runner.DeltaTime;
            var gravity = Physics.gravity.y * _gravityMultiplier;

            if (_controller.isGrounded && VerticalVelocity < 0f)
            {
                VerticalVelocity = GroundedStickVelocity;
            }

            if (_controller.isGrounded &&
                input.WasPressed(NetworkPlayerButton.Jump, PreviousButtons))
            {
                VerticalVelocity = Mathf.Sqrt(
                    -2f * gravity * _jumpHeight);
            }

            VerticalVelocity += gravity * deltaTime;

            var direction = ToWorldDirection(input.Move, input.LookYawDegrees);
            var speed = input.IsPressed(NetworkPlayerButton.Sprint)
                ? _sprintSpeed
                : _walkSpeed;
            var velocity = direction * speed;
            velocity.y = VerticalVelocity;
            _controller.Move(velocity * deltaTime);

            if (ControlsEnabled)
            {
                var targetRotation = Quaternion.Euler(0f, input.LookYawDegrees, 0f);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRotation,
                    _rotationSpeedDegrees * deltaTime);
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

        internal static Vector3 ToWorldDirection(Vector2 move, float lookYawDegrees)
        {
            var local = Vector3.ClampMagnitude(new Vector3(move.x, 0f, move.y), 1f);
            return Quaternion.Euler(0f, lookYawDegrees, 0f) * local;
        }
    }
}
