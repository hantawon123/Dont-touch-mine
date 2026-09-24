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
        // Entrance/Exit markers sit just outside the tunnel mouths (see TutorialHideoutArt.PosturePassage).
        private const float PassageEntranceMargin = .3f;
        private const float PassageExitMargin = .5f;
        private const float PassageHalfWidth = .9f;

        [SerializeField]
        private TutorialSession session;

        [SerializeField]
        private PlayerMovement player;

        [SerializeField]
        private Transform fallCheckpoint;

        [SerializeField] private Transform crouchEntrance;
        [SerializeField] private Transform crouchExit;
        [SerializeField] private Transform proneEntrance;
        [SerializeField] private Transform proneExit;
        [SerializeField] private Transform sprintJumpThreshold;
        [SerializeField] private Transform jumpLandingTarget;
        [SerializeField, Min(0f)] private float jumpLandingRadius = 2f;
        private bool enteredPassage;

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
                player.Posture,
                ObservePassage(position),
                ReachedSprintJumpThreshold(previousPosition, position),
                ReachedHorizontalTarget(jumpLandingTarget, position, jumpLandingRadius));

            previousPosition = position;
            previousYaw = yaw;
            if (session.ObserveMovement(observation))
                SaveCheckpoint();
        }

        public void Retry()
        {
            enteredPassage = false;
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
            enteredPassage = false;
            checkpoint = new Pose(player.transform.position, player.transform.rotation);
            checkpointStep = session.CurrentStep;
        }

        private bool ObservePassage(Vector3 position)
        {
            var crouch = session.CurrentStep == TutorialStep.Crouch;
            if (!crouch && session.CurrentStep != TutorialStep.Prone) return false;
            var entrance = crouch ? crouchEntrance : proneEntrance;
            var exit = crouch ? crouchExit : proneExit;
            if (entrance == null || exit == null) return false;
            var posture = crouch ? Game.Core.Players.PlayerPosture.Crouching : Game.Core.Players.PlayerPosture.Prone;
            var direction = exit.position - entrance.position;
            direction.y = 0f;
            var length = direction.magnitude;
            direction /= length;
            var delta = position - entrance.position;
            delta.y = 0f;
            var along = Vector3.Dot(delta, direction);
            var across = (delta - direction * along).magnitude;
            var tunnelEnd = length - PassageExitMargin;
            if ((position - previousPosition).magnitude > MaxObservedDistancePerFrame || along < PassageEntranceMargin)
            {
                // Teleports and backing out of the entrance restart the lesson.
                enteredPassage = false;
                return false;
            }

            if (along <= tunnelEnd)
            {
                // Only the posture inside the tunnel matters; the lintel keeps other postures out.
                if (across <= PassageHalfWidth && player.Posture == posture) enteredPassage = true;
                return false;
            }

            // Leaving the far end completes the passage, whatever posture the player takes on the way out.
            var completed = enteredPassage;
            enteredPassage = false;
            return completed;
        }

        private bool ReachedSprintJumpThreshold(Vector3 previous, Vector3 current)
        {
            return CrossedBoundary(sprintJumpThreshold, previous, current);
        }

        internal static bool CrossedBoundary(Transform boundary, Vector3 previous, Vector3 current)
        {
            if (boundary == null)
                return false;

            var previousSide = Vector3.Dot(
                previous - boundary.position,
                boundary.forward);

            var currentSide = Vector3.Dot(
                current - boundary.position,
                boundary.forward);

            return (previousSide < 0f && currentSide >= 0f) ||
                   (previousSide > 0f && currentSide <= 0f);
        }

        internal static bool ReachedHorizontalTarget(Transform target, Vector3 position, float radius)
        {
            if (target == null || radius < 0f)
                return false;

            var offset = position - target.position;
            offset.y = 0f;
            return offset.sqrMagnitude <= radius * radius;
        }

        private static float CurrentCameraYaw()
        {
            var camera = Camera.main;
            return camera != null ? camera.transform.eulerAngles.y : 0f;
        }
    }
}
