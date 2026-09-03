using UnityEngine;

namespace Game.SOAP.Config
{
    [CreateAssetMenu(fileName = "MovementConfig", menuName = "Game/Movement Config")]
    public sealed class MovementConfigSO : ScriptableObject
    {
        [SerializeField, Min(0f)]
        private float walkSpeed = 4f;

        [SerializeField, Min(0f)]
        private float sprintSpeed = 7f;

        [Header("스태미나")]
        [SerializeField, Min(0.1f)]
        private float maxStamina = 100f;

        [SerializeField, Min(0.1f)]
        private float staminaDrainPerSecond = 20f;

        [SerializeField, Min(0.1f)]
        private float staminaRecoveryPerSecond = 15f;

        [SerializeField, Min(0f)]
        private float rotationSpeedDegrees = 720f;

        [SerializeField, Min(0f)]
        private float jumpHeight = 1.1f;

        [SerializeField, Min(0.1f)]
        private float gravityMultiplier = 2f;

        [Header("자세")]
        [SerializeField, Min(0f)]
        private float crouchSpeed = 2f;

        [SerializeField, Min(0f)]
        private float proneSpeed = 0.8f;

        [SerializeField, Min(0.5f)]
        private float standHeight = 1.8f;

        [SerializeField, Min(0.5f)]
        private float crouchHeight = 1.2f;

        [SerializeField, Min(0.3f)]
        private float proneHeight = 0.6f;

        [SerializeField, Min(0f)]
        private float standEyeHeight = 1.6f;

        [SerializeField, Min(0f)]
        private float crouchEyeHeight = 1f;

        [SerializeField, Min(0f)]
        private float proneEyeHeight = 0.45f;

        public float WalkSpeed => walkSpeed;
        public float SprintSpeed => sprintSpeed;
        public float MaxStamina => maxStamina;
        public float StaminaDrainPerSecond => staminaDrainPerSecond;
        public float StaminaRecoveryPerSecond => staminaRecoveryPerSecond;
        public float RotationSpeedDegrees => rotationSpeedDegrees;
        public float JumpHeight => jumpHeight;
        public float GravityMultiplier => gravityMultiplier;
        public float CrouchSpeed => crouchSpeed;
        public float ProneSpeed => proneSpeed;
        public float StandHeight => standHeight;
        public float CrouchHeight => crouchHeight;
        public float ProneHeight => proneHeight;
        public float StandEyeHeight => standEyeHeight;
        public float CrouchEyeHeight => crouchEyeHeight;
        public float ProneEyeHeight => proneEyeHeight;
    }
}
