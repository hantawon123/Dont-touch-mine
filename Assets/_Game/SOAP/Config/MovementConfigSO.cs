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

        [SerializeField, Min(0f)]
        private float rotationSpeedDegrees = 720f;

        [SerializeField, Min(0f)]
        private float jumpHeight = 1.1f;

        [SerializeField, Min(0.1f)]
        private float gravityMultiplier = 2f;

        public float WalkSpeed => walkSpeed;
        public float SprintSpeed => sprintSpeed;
        public float RotationSpeedDegrees => rotationSpeedDegrees;
        public float JumpHeight => jumpHeight;
        public float GravityMultiplier => gravityMultiplier;
    }
}
