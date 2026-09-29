using Game.Core.Players;
using Game.Core.Tutorial;
using Game.Client.Tutorial;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

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
            progress.ObserveMovement(Observe(grounded: true, jumpCourseCompleted: false));
            Assert.That(progress.CurrentStep, Is.EqualTo(TutorialStep.Jump));

            progress.ObserveMovement(Observe(grounded: false));
            progress.ObserveMovement(Observe(grounded: true, jumpCourseCompleted: true));
            Assert.That(progress.CurrentStep, Is.EqualTo(TutorialStep.Crouch));
        }

        [Test]
        public void JumpLandingTargetUsesHorizontalConfigurableRadius()
        {
            var target = new GameObject("JumpLandingTarget");
            try
            {
                target.transform.position = new Vector3(10f, -5f, 4f);

                Assert.That(TutorialMovementCourse.ReachedHorizontalTarget(
                    target.transform,
                    new Vector3(11.5f, 20f, 4f),
                    2f), Is.True);
                Assert.That(TutorialMovementCourse.ReachedHorizontalTarget(
                    target.transform,
                    new Vector3(12.1f, -5f, 4f),
                    2f), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void TutorialSceneAssignsLandingTargetBeyondJumpEntrance()
        {
            const string scenePath = "Assets/_Game/Content/Scenes/Tutorial.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                TutorialMovementCourse course = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    course = root.GetComponentInChildren<TutorialMovementCourse>(true);
                    if (course != null)
                        break;
                }

                Assert.That(course, Is.Not.Null);
                var serializedCourse = new SerializedObject(course);
                var entrance = serializedCourse.FindProperty("sprintJumpThreshold").objectReferenceValue as Transform;
                var landing = serializedCourse.FindProperty("jumpLandingTarget").objectReferenceValue as Transform;
                var radius = serializedCourse.FindProperty("jumpLandingRadius").floatValue;

                Assert.That(entrance, Is.Not.Null);
                Assert.That(landing, Is.Not.Null);
                Assert.That(radius, Is.GreaterThan(0f));

                var offset = landing.position - entrance.position;
                offset.y = 0f;
                Assert.That(offset.magnitude, Is.GreaterThan(radius));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void PosturesAdvanceOnlyTheirCurrentLesson()
        {
            var progress = AtJump();
            progress.ObserveMovement(Observe(grounded: true));
            progress.ObserveMovement(Observe(grounded: false));
            progress.ObserveMovement(Observe(grounded: true, jumpCourseCompleted: true));

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
        public void LeavingPassageCompletesEvenAfterStandingUp()
        {
            var progress = AtJump();
            progress.ObserveMovement(Observe(grounded: true));
            progress.ObserveMovement(Observe(grounded: false));
            progress.ObserveMovement(Observe(grounded: true, jumpCourseCompleted: true));

            progress.ObserveMovement(Observe(posture: PlayerPosture.Standing, passageCompleted: true));
            Assert.That(progress.CurrentStep, Is.EqualTo(TutorialStep.Prone));
            progress.ObserveMovement(Observe(posture: PlayerPosture.Standing, passageCompleted: true));
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
            bool sprintCourseCompleted = false,
            bool jumpCourseCompleted = false) =>
            new(
                distance,
                look,
                speed,
                5f,
                grounded,
                posture,
                passageCompleted,
                sprintCourseCompleted,
                jumpCourseCompleted);
    }
}
