using Game.Core.Players;
using Game.Core.Tutorial;
using Game.Client.Tutorial;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class TutorialMovementProgressTests
    {
        [Test]
        public void MovementNeedsDistanceAndActualViewRotation()
        {
            var progress = new TutorialProgress();

            progress.ObserveMovement(Observe(distance: 3f));
            Assert.That(progress.CurrentStep, Is.EqualTo(TutorialStep.MoveAndLook));

            progress.ObserveMovement(Observe(look: 30f));
            Assert.That(progress.CurrentStep, Is.EqualTo(TutorialStep.MoveAndLook));

            progress.ObserveMovement(Observe(distance: 3f));
            Assert.That(progress.CurrentStep, Is.EqualTo(TutorialStep.Sprint));
        }

        [Test]
        public void SprintNeedsRunningAndJumpEntrance()
        {
            var progress = AtSprint();

            progress.ObserveMovement(Observe(distance: 20f, speed: 3f, sprintCourseCompleted: true));
            Assert.That(progress.CurrentStep, Is.EqualTo(TutorialStep.Sprint));

            progress.ObserveMovement(Observe(distance: 20f, speed: 6f));
            Assert.That(progress.CurrentStep, Is.EqualTo(TutorialStep.Sprint));

            progress.ObserveMovement(Observe(sprintCourseCompleted: true));
            Assert.That(progress.CurrentStep, Is.EqualTo(TutorialStep.Jump));
        }

        [Test]
        public void CourseBoundaryCanBeCrossedInEitherDirection()
        {
            var boundary = new GameObject("CourseBoundary");
            try
            {
                boundary.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

                Assert.That(TutorialMovementCourse.CrossedBoundary(
                    boundary.transform,
                    Vector3.back,
                    Vector3.forward), Is.True);
                Assert.That(TutorialMovementCourse.CrossedBoundary(
                    boundary.transform,
                    Vector3.forward,
                    Vector3.back), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(boundary);
            }
        }

        [Test]
        public void JumpNeedsGroundAirAndLandingInOrder()
        {
            var progress = AtJump();

            progress.ObserveMovement(Observe(grounded: false));
            progress.ObserveMovement(Observe(grounded: true));
            Assert.That(progress.CurrentStep, Is.EqualTo(TutorialStep.Jump));

            progress.ObserveMovement(Observe(grounded: false));
            progress.ObserveMovement(Observe(grounded: true));
            Assert.That(progress.CurrentStep, Is.EqualTo(TutorialStep.Crouch));
        }

        [Test]
        public void PosturesAdvanceOnlyTheirCurrentLesson()
        {
            var progress = AtJump();
            progress.ObserveMovement(Observe(grounded: true));
            progress.ObserveMovement(Observe(grounded: false));
            progress.ObserveMovement(Observe(grounded: true));

            progress.ObserveMovement(Observe(posture: PlayerPosture.Prone));
            Assert.That(progress.CurrentStep, Is.EqualTo(TutorialStep.Crouch));

            progress.ObserveMovement(Observe(posture: PlayerPosture.Crouching));
            Assert.That(progress.CurrentStep, Is.EqualTo(TutorialStep.Crouch), "Changing posture alone must not finish the passage.");
            progress.ObserveMovement(Observe(posture: PlayerPosture.Crouching, passageCompleted: true));
            progress.ObserveMovement(Observe(posture: PlayerPosture.Prone));
            Assert.That(progress.CurrentStep, Is.EqualTo(TutorialStep.Prone));
            progress.ObserveMovement(Observe(posture: PlayerPosture.Prone, passageCompleted: true));
            Assert.That(progress.CurrentStep, Is.EqualTo(TutorialStep.PickUp));
        }

        [Test]
        public void RetryClearsPartialProgressWithoutChangingStep()
        {
            var progress = new TutorialProgress();
            progress.ObserveMovement(Observe(distance: 2f, look: 20f));

            progress.RetryCurrentStep();
            progress.ObserveMovement(Observe(distance: 0.6f, look: 10f));

            Assert.That(progress.CurrentStep, Is.EqualTo(TutorialStep.MoveAndLook));
        }

        private static TutorialProgress AtSprint()
        {
            var progress = new TutorialProgress();
            progress.ObserveMovement(Observe(distance: 6f, look: 30f));
            return progress;
        }

        private static TutorialProgress AtJump()
        {
            var progress = AtSprint();
            progress.ObserveMovement(Observe(speed: 6f));
            progress.ObserveMovement(Observe(sprintCourseCompleted: true));
            return progress;
        }

        private static TutorialMovementObservation Observe(
            float distance = 0f,
            float look = 0f,
            float speed = 0f,
            bool grounded = true,
            PlayerPosture posture = PlayerPosture.Standing,
            bool passageCompleted = false,
            bool sprintCourseCompleted = false) =>
            new(distance, look, speed, 5f, grounded, posture, passageCompleted, sprintCourseCompleted);
    }
}
