using System.Collections;
using Game.Client.Cameras;
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
        [UnityTest]
        public IEnumerator TutorialStartsWithLocalPlayerCameraAndCourse()
        {
            var load = SceneManager.LoadSceneAsync("Tutorial", LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            while (!load.isDone) yield return null;
            yield return null;

            var player = Object.FindAnyObjectByType<PlayerMovement>();
            var camera = Object.FindAnyObjectByType<PlayerCameraController>();
            var course = Object.FindAnyObjectByType<TutorialMovementCourse>();
            var session = Object.FindAnyObjectByType<TutorialSession>();

            Assert.That(player, Is.Not.Null);
            Assert.That(player.GetComponent<CharacterController>().enabled, Is.True);
            Assert.That(camera, Is.Not.Null);
            Assert.That(course, Is.Not.Null);
            Assert.That(course.enabled, Is.True);
            Assert.That(session, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator LocalItemCourseCompletesAndRecoversLostItem()
        {
            var load = SceneManager.LoadSceneAsync("Tutorial", LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            while (!load.isDone) yield return null;
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
            interactor.DropCarriedItem();
            Assert.That(interactor.TryPickUp(trainingItem), Is.True);
            interactor.SendMessage("ThrowCarried");
            Assert.That(interactor.TryPickUp(trainingItem), Is.True);
            Assert.That(interactor.TryPlaceCarried(spawn, Quaternion.identity), Is.True);
            Assert.That(interactor.TryPickUp(trainingItem), Is.True);
            shredder.Interact(interactor);
            yield return new WaitForSeconds(0.7f);

            Assert.That(itemCourse.enabled, Is.True);
            Assert.That(session.IsComplete, Is.True);
        }

        private static void AdvanceToItemLessons(TutorialSession session)
        {
            session.ObserveMovement(Observe(distance: 3f, look: 30f));
            session.ObserveMovement(Observe(distance: 3f, speed: 6f));
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
