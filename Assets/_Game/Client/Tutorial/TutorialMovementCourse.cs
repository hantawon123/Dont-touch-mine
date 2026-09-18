using Game.Client.Players;
using Game.Core.Tutorial;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Client.Tutorial
{
    public sealed class TutorialMovementCourse : MonoBehaviour
    {
        private const float FallHeight = -2f;
        private const float MaxObservedDistancePerFrame = 1f;

        [SerializeField]
        private TutorialSession session;

        [SerializeField]
        private PlayerMovement player;

        [SerializeField]
        private Transform fallCheckpoint;

        private CharacterController characterController;
        private Vector3 previousPosition;
        private float previousYaw;
        private Pose checkpoint;
        private TutorialStep checkpointStep;

        private void Awake()
        {
            if (session == null || player == null)
            {
                Debug.LogError("TutorialMovementCourse requires TutorialSession and PlayerMovement.", this);
                enabled = false;
                return;
            }

            characterController = player.GetComponent<CharacterController>();
            previousPosition = player.transform.position;
            previousYaw = CurrentCameraYaw();
            SaveCheckpoint();
        }

        private void Update()
        {
            // Falling must recover even if the player already finished the movement lessons.
            if (player.transform.position.y < FallHeight)
            {
                if (fallCheckpoint != null)
                    checkpoint = new Pose(fallCheckpoint.position, fallCheckpoint.rotation);
                Retry();
                return;
            }
            if (session.CurrentStep > TutorialStep.Prone)
                return;

            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                Retry();
                return;
            }

            var position = player.transform.position;
            var horizontalDelta = position - previousPosition;
            horizontalDelta.y = 0f;
            var distance = Mathf.Min(horizontalDelta.magnitude, MaxObservedDistancePerFrame);
            var yaw = CurrentCameraYaw();
            var lookDegrees = Mathf.Abs(Mathf.DeltaAngle(previousYaw, yaw));
            var settings = player.MovementSettings;
            var observation = new TutorialMovementObservation(
                distance,
                lookDegrees,
                player.PlanarSpeed,
                settings.SprintSpeed * 0.8f,
                player.IsGrounded,
                player.Posture);

            previousPosition = position;
            previousYaw = yaw;
            if (session.ObserveMovement(observation))
                SaveCheckpoint();
        }

        public void Retry()
        {
            session.RetryCurrentStep();
            if (characterController != null)
                characterController.enabled = false;
            player.transform.SetPositionAndRotation(checkpoint.position, checkpoint.rotation);
            if (characterController != null)
                characterController.enabled = true;
            previousPosition = checkpoint.position;
            previousYaw = CurrentCameraYaw();
            Debug.Log($"[Tutorial] Retrying step: {checkpointStep}", this);
        }

        private void SaveCheckpoint()
        {
            checkpoint = new Pose(player.transform.position, player.transform.rotation);
            checkpointStep = session.CurrentStep;
        }

        private static float CurrentCameraYaw()
        {
            var camera = Camera.main;
            return camera != null ? camera.transform.eulerAngles.y : 0f;
        }
    }
}
