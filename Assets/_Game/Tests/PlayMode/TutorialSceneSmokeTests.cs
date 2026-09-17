using System.Collections;
using Game.Client.Cameras;
using Game.Client.Players;
using Game.Client.Tutorial;
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
    }
}
