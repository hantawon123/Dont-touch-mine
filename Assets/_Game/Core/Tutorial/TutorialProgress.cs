using System;
using Game.Core.Players;

namespace Game.Core.Tutorial
{
    public enum TutorialStep : byte
    {
        MoveAndLook,
        Sprint,
        Jump,
        Crouch,
        Prone,
        PickUp,
        Drop,
        Throw,
        Place,
        UseShredder,
        Complete
    }

    public enum TutorialInteractionAction : byte
    {
        PickUp,
        Drop,
        Throw,
        Place,
        UseShredder
    }

    public readonly struct TutorialMovementObservation
    {
        public TutorialMovementObservation(
            float distance,
            float lookDegrees,
            float speed,
            float sprintThreshold,
            bool grounded,
            PlayerPosture posture)
        {
            Distance = Math.Max(0f, distance);
            LookDegrees = Math.Max(0f, lookDegrees);
            Speed = Math.Max(0f, speed);
            SprintThreshold = Math.Max(0f, sprintThreshold);
            Grounded = grounded;
            Posture = posture;
        }

        public float Distance { get; }
        public float LookDegrees { get; }
        public float Speed { get; }
        public float SprintThreshold { get; }
        public bool Grounded { get; }
        public PlayerPosture Posture { get; }
    }

    /// <summary>
    /// Ordered tutorial rules. The client feeds observed game state; raw input
    /// never completes a lesson by itself.
    /// </summary>
    public sealed class TutorialProgress
    {
        internal const float RequiredMoveDistance = 6f;
        private const float RequiredLookDegrees = 30f;
        internal const float RequiredSprintDistance = 8f;

        private float distance;
        private float lookDegrees;
        private bool jumpWasGrounded;
        private bool jumpWasAirborne;

        public TutorialStep CurrentStep { get; private set; } = TutorialStep.MoveAndLook;
        public bool IsComplete => CurrentStep == TutorialStep.Complete;

        public bool ObserveMovement(TutorialMovementObservation observation)
        {
            switch (CurrentStep)
            {
                case TutorialStep.MoveAndLook:
                    distance += observation.Distance;
                    lookDegrees += observation.LookDegrees;
                    if (distance >= RequiredMoveDistance && lookDegrees >= RequiredLookDegrees)
                    {
                        return Advance();
                    }
                    break;

                case TutorialStep.Sprint:
                    if (observation.Speed >= observation.SprintThreshold)
                    {
                        distance += observation.Distance;
                    }

                    if (distance >= RequiredSprintDistance)
                    {
                        return Advance();
                    }
                    break;

                case TutorialStep.Jump:
                    if (observation.Grounded && !jumpWasAirborne) jumpWasGrounded = true;
                    if (jumpWasGrounded && !observation.Grounded) jumpWasAirborne = true;
                    if (jumpWasAirborne && observation.Grounded) return Advance();
                    break;

                case TutorialStep.Crouch:
                    if (observation.Posture == PlayerPosture.Crouching) return Advance();
                    break;

                case TutorialStep.Prone:
                    if (observation.Posture == PlayerPosture.Prone) return Advance();
                    break;
            }

            return false;
        }

        public void RetryCurrentStep()
        {
            distance = 0f;
            lookDegrees = 0f;
            jumpWasGrounded = false;
            jumpWasAirborne = false;
        }

        public bool ObserveInteraction(TutorialInteractionAction action)
        {
            var expected = CurrentStep switch
            {
                TutorialStep.PickUp => TutorialInteractionAction.PickUp,
                TutorialStep.Drop => TutorialInteractionAction.Drop,
                TutorialStep.Throw => TutorialInteractionAction.Throw,
                TutorialStep.Place => TutorialInteractionAction.Place,
                TutorialStep.UseShredder => TutorialInteractionAction.UseShredder,
                _ => (TutorialInteractionAction?)null
            };

            return expected == action && Advance();
        }

        internal bool Advance()
        {
            if (IsComplete) return false;
            CurrentStep++;
            RetryCurrentStep();
            return true;
        }
    }
}
