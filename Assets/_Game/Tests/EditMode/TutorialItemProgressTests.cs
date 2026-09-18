using Game.Core.Players;
using Game.Core.Tutorial;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class TutorialItemProgressTests
    {
        [Test]
        public void ItemLessonsAdvanceOnlyInExpectedOrder()
        {
            var progress = AtItemLessons();

            Assert.That(progress.ObserveInteraction(TutorialInteractionAction.Throw), Is.False);
            Assert.That(progress.CurrentStep, Is.EqualTo(TutorialStep.PickUp));

            Assert.That(progress.ObserveInteraction(TutorialInteractionAction.PickUp), Is.True);
            Assert.That(progress.ObserveInteraction(TutorialInteractionAction.Drop), Is.True);
            Assert.That(progress.ObserveInteraction(TutorialInteractionAction.Throw), Is.True);
            Assert.That(progress.ObserveInteraction(TutorialInteractionAction.Place), Is.True);
            Assert.That(progress.ObserveInteraction(TutorialInteractionAction.UseShredder), Is.True);
            Assert.That(progress.IsComplete, Is.True);
        }

        [Test]
        public void RepeatedPreviousActionDoesNotSkipNextLesson()
        {
            var progress = AtItemLessons();
            progress.ObserveInteraction(TutorialInteractionAction.PickUp);

            Assert.That(progress.ObserveInteraction(TutorialInteractionAction.PickUp), Is.False);
            Assert.That(progress.CurrentStep, Is.EqualTo(TutorialStep.Drop));
        }

        [Test]
        public void RetryKeepsCurrentItemLesson()
        {
            var progress = AtItemLessons();

            progress.RetryCurrentStep();

            Assert.That(progress.CurrentStep, Is.EqualTo(TutorialStep.PickUp));
        }

        private static TutorialProgress AtItemLessons()
        {
            var progress = new TutorialProgress();
            progress.ObserveMovement(Observe(distance: 6f, look: 30f));
            progress.ObserveMovement(Observe(distance: 8f, speed: 6f));
            progress.ObserveMovement(Observe(grounded: true));
            progress.ObserveMovement(Observe(grounded: false));
            progress.ObserveMovement(Observe(grounded: true));
            progress.ObserveMovement(Observe(posture: PlayerPosture.Crouching));
            progress.ObserveMovement(Observe(posture: PlayerPosture.Prone));
            return progress;
        }

        private static TutorialMovementObservation Observe(
            float distance = 0f,
            float look = 0f,
            float speed = 0f,
            bool grounded = true,
            PlayerPosture posture = PlayerPosture.Standing) =>
            new(distance, look, speed, 5f, grounded, posture, passageCompleted: true);
    }
}
