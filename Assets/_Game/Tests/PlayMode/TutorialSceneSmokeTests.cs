using System.Collections;
using System.Reflection;
using Game.Client.Cameras;
using Game.Client.Common;
using Game.Client.Interactions;
using Game.Client.Players;
using Game.Client.Tutorial;
using Game.Core.Tutorial;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    public sealed class TutorialSceneSmokeTests
    {
        [Test]
        public void LowPassageRadioRequestsFirstPersonView()
        {
            Assert.That(TutorialRadioPresenter.Instruction(TutorialStep.Crouch), Does.Contain("V로 1인칭"));
            Assert.That(TutorialRadioPresenter.Instruction(TutorialStep.Prone), Does.Contain("V로 1인칭"));
        }

        [UnityTest]
        public IEnumerator TutorialStartsWithLocalPlayerCameraAndCourse()
        {
            var load = SceneManager.LoadSceneAsync("Tutorial", LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            while (!load.isDone)
                yield return null;
            yield return null;

            var player = Object.FindAnyObjectByType<PlayerMovement>();
            var camera = Object.FindAnyObjectByType<PlayerCameraController>();
            var course = Object.FindAnyObjectByType<TutorialMovementCourse>();
            var session = Object.FindAnyObjectByType<TutorialSession>();
            var radio = Object.FindAnyObjectByType<TutorialRadioView>();

            Assert.That(player, Is.Not.Null);
            Assert.That(player.GetComponent<CharacterController>().enabled, Is.True);
            Assert.That(camera, Is.Not.Null);
            Assert.That(course, Is.Not.Null);
            Assert.That(course.enabled, Is.True);
            Assert.That(session, Is.Not.Null);
            Assert.That(radio, Is.Not.Null);
            Assert.That(radio.CurrentMessage, Does.Contain("신입"));
            Assert.That(RenderSettings.fog, Is.True);
            Assert.That(RenderSettings.skybox, Is.Not.Null);
            Assert.That(GameObject.Find("Lobby Post Volume"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator BossRadioFollowsTutorialProgressAndRetry()
        {
            var load = SceneManager.LoadSceneAsync("Tutorial", LoadSceneMode.Single);
            while (!load.isDone)
                yield return null;
            yield return null;

            var session = Object.FindAnyObjectByType<TutorialSession>();
            var radio = Object.FindAnyObjectByType<TutorialRadioView>();
            var keyGuide = Object.FindAnyObjectByType<Game.Client.KeySettingGuideView>();
            Assert.That(keyGuide, Is.Not.Null);
            Assert.That(keyGuide.AlwaysVisible, Is.True);
            session.ObserveMovement(Observe(distance: 6f, look: 30f));
            Assert.That(radio.CurrentMessage, Does.Contain("벽을 보고 걷지는"));
            yield return new WaitForSecondsRealtime(2f);
            Assert.That(radio.CurrentMessage, Does.Contain("전력으로 달려"));
            Assert.That(GameObject.Find("BossRadio").GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f).Within(.01f));

            session.RetryCurrentStep();
            Assert.That(radio.CurrentMessage, Does.Contain("같은 실수는 두 번"));
            yield return new WaitForSecondsRealtime(2f);
            Assert.That(radio.CurrentMessage, Does.Contain("전력으로 달려"));
        }

        [UnityTest]
        public IEnumerator LocalItemCourseCompletesAndRecoversLostItem()
        {
            var load = SceneManager.LoadSceneAsync("Tutorial", LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            while (!load.isDone)
                yield return null;
            yield return null;

            var session = Object.FindAnyObjectByType<TutorialSession>();
            var interactor = Object.FindAnyObjectByType<PlayerInteractor>();
            var itemCourse = Object.FindAnyObjectByType<Game.Bootstrap.TutorialItemCourse>();
            var shredder = Object.FindAnyObjectByType<Game.Bootstrap.ShredderInteractable>();
            var trainingItem = GameObject.Find("TrainingItem").GetComponent<CarryableItem>();
            AdvanceToItemLessons(session);

            var spawn = trainingItem.transform.position;
            trainingItem.transform.position = spawn + Vector3.right * 30f;
            yield return null;
            Assert.That(Vector3.Distance(trainingItem.transform.position, spawn), Is.LessThan(0.1f));

            Assert.That(interactor.TryPickUp(trainingItem), Is.True);
            Assert.That(trainingItem.transform.parent, Is.SameAs(interactor.HoldPoint));
            yield return new WaitForFixedUpdate();
            yield return null;
            foreach (var renderer in trainingItem.GetComponentsInChildren<Renderer>())
            {
                Assert.That(renderer.isPartOfStaticBatch, Is.False, renderer.name);
                Assert.That(Vector3.Distance(renderer.bounds.center, interactor.HoldPoint.position),
                    Is.LessThan(2f), "The rendered box stayed behind after pickup.");
            }
            interactor.DropCarriedItem();
            Assert.That(interactor.TryPickUp(trainingItem), Is.True);
            interactor.SendMessage("ThrowCarried");
            Assert.That(interactor.TryPickUp(trainingItem), Is.True);
            Assert.That(GameObject.Find("PlacementTargetGhost"), Is.Not.Null);
            Assert.That(interactor.TryPlaceCarried(spawn, Quaternion.identity), Is.True);
            Assert.That(session.CurrentStep, Is.EqualTo(TutorialStep.Place), "An incorrect pose completed placement.");
            Assert.That(interactor.TryPickUp(trainingItem), Is.True);
            Assert.That(interactor.TryPlaceCarried(
                itemCourse.PlacementTargetPose.position,
                itemCourse.PlacementTargetPose.rotation), Is.True);
            Assert.That(interactor.TryPickUp(trainingItem), Is.True);
            shredder.Interact(interactor);
            yield return new WaitForSeconds(0.7f);

            Assert.That(itemCourse.enabled, Is.True);
            Assert.That(session.IsComplete, Is.True);
        }

        [UnityTest]
        public IEnumerator CourseGeometryRequiresJumpCrouchAndProne()
        {
            var load = SceneManager.LoadSceneAsync("Tutorial", LoadSceneMode.Single);
            while (!load.isDone)
                yield return null;
            yield return null;
            var player = Object.FindAnyObjectByType<PlayerMovement>();
            var course = Object.FindAnyObjectByType<TutorialMovementCourse>();
            player.enabled = false;
            course.enabled = false;
            var controller = player.GetComponent<CharacterController>();
            var settings = player.MovementSettings;

            SetControllerPose(controller, new Vector3(20, .2f, 3.8f), settings.StandHeight);
            MoveController(controller, Vector3.back, 2.8f);
            Assert.That(player.transform.position.z, Is.GreaterThan(2.8f), "Standing bypassed stage 05.");
            SetControllerPose(controller, new Vector3(20, .2f, 3.8f), settings.CrouchHeight);
            MoveController(controller, Vector3.back, 2.8f);
            Assert.That(player.transform.position.z, Is.LessThan(1.4f), "Crouching cannot clear stage 05.");

            SetControllerPose(controller, new Vector3(20, .2f, .5f), settings.CrouchHeight);
            MoveController(controller, Vector3.back, 2.9f);
            Assert.That(player.transform.position.z, Is.GreaterThan(-.5f), "Crouching bypassed stage 06.");
            SetControllerPose(controller, new Vector3(20, .2f, .5f), settings.ProneHeight);
            MoveController(controller, Vector3.back, 2.9f);
            Assert.That(player.transform.position.z, Is.LessThan(-2f), "Prone cannot clear stage 06.");

            SetControllerPose(controller, new Vector3(14, .2f, 10), settings.StandHeight);
            SimulateJump(controller, settings, false);
            Assert.That(player.transform.position.y, Is.LessThan(-2f), "Walking over the pit did not fall.");
            course.enabled = true;
            yield return null;
            Assert.That(Vector3.Distance(player.transform.position, new Vector3(13.6f, .2f, 10)), Is.LessThan(.3f), "Fall did not recover at the jump checkpoint.");
            course.enabled = false;
            SetControllerPose(controller, new Vector3(14, .2f, 10), settings.StandHeight);
            SimulateJump(controller, settings, true);
            Assert.That(player.transform.position.x, Is.GreaterThan(17.5f), "Sprint jump did not reach the landing.");
            Assert.That(player.transform.position.y, Is.GreaterThan(-.1f), "Sprint jump fell through the landing.");
        }

        [UnityTest]
        public IEnumerator FinalDoorRequiresInteractionAndCarriedItemSurvivesLongRoute()
        {
            var load = SceneManager.LoadSceneAsync("Tutorial", LoadSceneMode.Single);
            while (!load.isDone)
                yield return null;
            yield return null;
            var player = Object.FindAnyObjectByType<PlayerMovement>();
            var session = Object.FindAnyObjectByType<TutorialSession>();
            var interactor = player.GetComponent<PlayerInteractor>();
            var door = Object.FindAnyObjectByType<TutorialExitDoor>();
            Assert.That(door, Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<TutorialCompletionTrigger>(), Is.Null, "Walking must not auto-exit.");
            Assert.That(door.CanInteract(interactor), Is.False);
            door.Interact(interactor);
            Assert.That(door.IsLoading, Is.False);
            AdvanceToItemLessons(session);
            var item = GameObject.Find("TrainingItem").GetComponent<CarryableItem>();
            var controller = player.GetComponent<CharacterController>();
            SetControllerPose(controller, new Vector3(18, .2f, -8), player.MovementSettings.StandHeight);
            Assert.That(interactor.TryPickUp(item), Is.True);
            SetControllerPose(controller, new Vector3(-18, .2f, -8), player.MovementSettings.StandHeight);
            item.transform.position = player.transform.position + Vector3.up;
            yield return null;
            Assert.That(interactor.CarriedItem, Is.SameAs(item), "Valid travel was mistaken for a lost item.");
            foreach (var action in new[] { TutorialInteractionAction.Drop, TutorialInteractionAction.Throw, TutorialInteractionAction.Place, TutorialInteractionAction.UseShredder })
                session.ObserveInteraction(action);
            Assert.That(door.CanInteract(interactor), Is.True);
            yield return null;
            Assert.That(door.IsLoading, Is.False, "Completion alone must not automatically leave.");

            var loadingView = LoadingView.Create(null);
            typeof(TutorialExitDoor).GetField("destinationScene", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(door, string.Empty);
            door.Interact(interactor);
            yield return null;
            Assert.That(loadingView.IsPresented, Is.True, "Exit did not present the shared loading view.");
            Object.Destroy(loadingView.gameObject);
        }

        private static void SetControllerPose(CharacterController controller, Vector3 position, float height)
        {
            controller.enabled = false;
            controller.transform.position = position;
            controller.height = height;
            controller.center = Vector3.up * height * .5f;
            controller.enabled = true;
            Physics.SyncTransforms();
            controller.Move(Vector3.down * .1f);
        }

        private static void MoveController(CharacterController controller, Vector3 direction, float distance)
        {
            for (int i = 0; i < 60; i++)
                controller.Move(direction * (distance / 60) + Vector3.down * .015f);
        }

        private static void SimulateJump(CharacterController controller, Game.Core.Players.PlayerMovementSettings settings, bool jump)
        {
            float gravity = Mathf.Abs(Physics.gravity.y) * settings.GravityMultiplier;
            float vertical = jump ? Mathf.Sqrt(2 * gravity * settings.JumpHeight) : 0;
            float speed = jump ? settings.SprintSpeed : settings.WalkSpeed;
            for (int i = 0; i < 60; i++)
            {
                vertical -= gravity / 60;
                controller.Move(new Vector3(speed, vertical, 0) / 60);
            }
        }

        private static void AdvanceToItemLessons(TutorialSession session)
        {
            session.ObserveMovement(Observe(distance: 6f, look: 30f));
            session.ObserveMovement(Observe(distance: 8f, speed: 6f));
            session.ObserveMovement(Observe(grounded: true));
            session.ObserveMovement(Observe(grounded: false));
            session.ObserveMovement(Observe(grounded: true));
            session.ObserveMovement(Observe(posture: Game.Core.Players.PlayerPosture.Crouching));
            session.ObserveMovement(Observe(posture: Game.Core.Players.PlayerPosture.Prone));
        }

        private static TutorialMovementObservation Observe(
            float distance = 0f,
            float look = 0f,
            float speed = 0f,
            bool grounded = true,
            Game.Core.Players.PlayerPosture posture = Game.Core.Players.PlayerPosture.Standing) =>
            new(distance, look, speed, 5f, grounded, posture);
    }
}
