using Game.Core.Players;
using Game.Core.Tutorial;
using NUnit.Framework;

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
        public void SprintCountsOnlyDistanceAtSprintSpeed()
        {
            var progress = AtSprint();

            progress.ObserveMovement(Observe(distance: 5f, speed: 3f));
            Assert.That(progress.CurrentStep, Is.EqualTo(TutorialStep.Sprint));

            progress.ObserveMovement(Observe(distance: 7f, speed: 6f));
            Assert.That(progress.CurrentStep, Is.EqualTo(TutorialStep.Sprint));

            progress.ObserveMovement(Observe(distance: 1f, speed: 6f));
            Assert.That(progress.CurrentStep, Is.EqualTo(TutorialStep.Jump));
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
            progress.ObserveMovement(Observe(posture: PlayerPosture.Prone));
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
            progress.ObserveMovement(Observe(distance: 8f, speed: 6f));
            return progress;
        }

        private static TutorialMovementObservation Observe(
            float distance = 0f,
            float look = 0f,
            float speed = 0f,
            bool grounded = true,
            PlayerPosture posture = PlayerPosture.Standing) =>
            new(distance, look, speed, 5f, grounded, posture);
    }
}
