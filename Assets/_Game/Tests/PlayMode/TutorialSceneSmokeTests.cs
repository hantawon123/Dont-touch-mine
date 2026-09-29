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
            Assert.That(TutorialRadioPresenter.Instruction(TutorialStep.Crouch), Does.Contain("C를"));
            Assert.That(TutorialRadioPresenter.Instruction(TutorialStep.Prone), Does.Contain("Z를"));
            Assert.That(TutorialRadioPresenter.Instruction(TutorialStep.Prone), Does.Not.Contain("V로"));
            Assert.That(TutorialRadioPresenter.Instruction(TutorialStep.Place), Does.Contain("Q/E와 마우스 휠"));
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
            var combat = player.GetComponent<Game.Client.Combat.PlayerCombatant>();
            Assert.That(combat.enabled, Is.True);
            Assert.That(combat.HasCombatRules, Is.True);
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
            var attack = (UnityEngine.InputSystem.InputAction)typeof(Game.Client.Combat.PlayerCombatant)
                .GetField("attackAction", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(combat);
            Assert.That(attack.enabled, Is.True);
            Assert.That(attack.bindings.Count, Is.GreaterThan(0));
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
            Assert.That(Object.FindAnyObjectByType<TutorialChecklistView>().transform.Find("Step0").GetComponent<TMPro.TMP_Text>().text, Does.Contain("주변 살피기"));
            session.ObserveMovement(Observe(distance: 6f, look: 30f));
            Assert.That(keyGuide.transform.Find("Row4/Action").GetComponent<TMPro.TMP_Text>().fontStyle, Is.EqualTo(TMPro.FontStyles.Bold));
            Assert.That(radio.CurrentMessage, Does.Contain("벽을 보고 걷지는"));
            yield return new WaitForSecondsRealtime(2f);
            Assert.That(radio.CurrentMessage, Does.Contain("전력으로 달려"));
            var radioGroup = (CanvasGroup)typeof(TutorialRadioView)
                .GetField("radioGroup", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(radio);
            Assert.That(radioGroup.alpha, Is.EqualTo(1f).Within(.01f));

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
            trainingItem.transform.position = new Vector3(spawn.x, -5, spawn.z);
            yield return null;
            Assert.That(trainingItem.transform.position.y, Is.GreaterThan(0));
            Assert.That(trainingItem.GetComponent<Rigidbody>().isKinematic, Is.False);
            Assert.That(trainingItem.GetComponent<Rigidbody>().useGravity, Is.True);

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
            yield return new WaitForSeconds(.5f);
            Assert.That(session.CurrentStep, Is.EqualTo(TutorialStep.Drop), "Dropping away from the marked zone must not complete the lesson.");
            trainingItem.OnReleased(new Pose(itemCourse.DropTargetPosition, Quaternion.identity), Vector3.zero);
            yield return new WaitForSeconds(2f);
            Assert.That(session.CurrentStep, Is.EqualTo(TutorialStep.Throw));
            Assert.That(interactor.TryPickUp(trainingItem), Is.True);
            interactor.SendMessage("ThrowCarried");
            yield return null;
            Assert.That(session.CurrentStep, Is.EqualTo(TutorialStep.Throw), "A throw that misses the target must not complete the lesson.");
            var player = interactor.GetComponent<PlayerMovement>();
            player.enabled = false;
            SetControllerPose(player.GetComponent<CharacterController>(), new Vector3(-6, .2f, -8), player.MovementSettings.StandHeight);
            interactor.transform.rotation = Quaternion.identity;
            var throwAim = new GameObject("ThrowTestAim").transform;
            throwAim.position = new Vector3(-6, 1.8f, -8);
            throwAim.LookAt(new Vector3(-6, 1.75f, -5.4f));
            typeof(PlayerInteractor).GetField("cameraTransform", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(interactor, throwAim);
            Assert.That(interactor.TryPickUp(trainingItem), Is.True);
            Physics.SyncTransforms();
            interactor.SendMessage("ThrowCarried");
            for (var elapsed = 0f; elapsed < 2f && session.CurrentStep == TutorialStep.Throw; elapsed += Time.deltaTime) yield return null;
            Object.Destroy(throwAim.gameObject);
            Assert.That(session.CurrentStep, Is.EqualTo(TutorialStep.Place));
            Assert.That(interactor.TryPickUp(trainingItem), Is.True);
            Assert.That(GameObject.Find("PlacementTargetGhost"), Is.Not.Null);
            var targetColor = GameObject.Find("PlacementTargetGhost").GetComponentInChildren<Renderer>().sharedMaterial.GetColor("_BaseColor");
            Assert.That(targetColor.b, Is.GreaterThan(targetColor.g), "Fixed target must be blue, distinct from the green live preview.");
            Assert.That(interactor.TryPlaceCarried(spawn, Quaternion.identity), Is.True);
            Assert.That(session.CurrentStep, Is.EqualTo(TutorialStep.Place), "An incorrect pose completed placement.");
            Assert.That(Vector3.Distance(trainingItem.transform.position, spawn), Is.LessThan(.01f), "Invalid placement teleported the item.");
            Assert.That(interactor.TryPickUp(trainingItem), Is.True);
            Assert.That(interactor.TryPlaceCarried(
                itemCourse.PlacementTargetPose.position + Vector3.right * .25f,
                Quaternion.identity), Is.True);
            Assert.That(session.CurrentStep, Is.EqualTo(TutorialStep.UseShredder), "A nearby placement with a different heading must pass.");
            Assert.That(interactor.TryPickUp(trainingItem), Is.True);
            shredder.Interact(interactor);
            yield return new WaitForSeconds(0.7f);

            Assert.That(itemCourse.enabled, Is.True);
            Assert.That(session.IsComplete, Is.True);
        }

        [UnityTest]
        public IEnumerator TablePlacementUsesRealPreviewAndPauseRestoresInput()
        {
            var load = SceneManager.LoadSceneAsync("Tutorial", LoadSceneMode.Single);
            while (!load.isDone) yield return null;
            yield return null;
            var player = Object.FindAnyObjectByType<PlayerMovement>();
            var pause = Object.FindAnyObjectByType<TutorialPauseController>();
            pause.Toggle();
            Assert.That(pause.IsOpen, Is.True);
            Assert.That(player.IsMovementLocked, Is.True);
            pause.Resume();
            Assert.That(player.IsMovementLocked, Is.False);

            player.enabled = false;
            var session = Object.FindAnyObjectByType<TutorialSession>();
            AdvanceToItemLessons(session);
            session.ObserveInteraction(TutorialInteractionAction.PickUp);
            session.ObserveInteraction(TutorialInteractionAction.Drop);
            session.ObserveInteraction(TutorialInteractionAction.Throw);
            var interactor = player.GetComponent<PlayerInteractor>();
            var placement = player.GetComponent<ItemPlacementController>();
            var course = Object.FindAnyObjectByType<Game.Bootstrap.TutorialItemCourse>();
            var item = GameObject.Find("TrainingItem").GetComponent<CarryableItem>();
            var desk = GameObject.Find("Placement").transform.Find("Workbench/Bench/LobbyDesk").GetComponentInChildren<Renderer>().bounds;
            Assert.That(desk.center.x, Is.EqualTo(-18).Within(.02f));
            Assert.That(desk.center.z, Is.EqualTo(-10).Within(.02f));
            var tableTop = GameObject.Find("Placement").transform.Find("Workbench/Bench/Top").GetComponent<Collider>().bounds;
            Assert.That(desk.max.y, Is.EqualTo(tableTop.max.y).Within(.02f), "Visible table and placement surface disagree.");
            SetControllerPose(player.GetComponent<CharacterController>(), new Vector3(course.PlacementTargetPose.position.x, .2f, -11.6f), player.MovementSettings.StandHeight);
            yield return null;
            var guide = Object.FindAnyObjectByType<Game.Client.KeySettingGuideView>();
            Assert.That(guide.transform.Find("Row0/Action").GetComponent<TMPro.TMP_Text>().text, Does.Contain("들기"));
            Assert.That(interactor.TryPickUp(item), Is.True);
            yield return null;
            Assert.That(guide.transform.Find("Row0/Action").GetComponent<TMPro.TMP_Text>().text, Is.EqualTo("배치 모드"));
            var aim = new GameObject("PlacementTestAim").transform;
            aim.position = new Vector3(course.PlacementTargetPose.position.x, 2.7f, -12.3f);
            aim.LookAt(course.PlacementTargetPose.position);
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            placement.enabled = false;
            typeof(ItemPlacementController).GetField("cameraTransform", flags).SetValue(placement, aim);
            placement.SendMessage("EnterPlacementMode");
            typeof(ItemPlacementController).GetField("ghostRotation", flags).SetValue(placement, course.PlacementTargetPose.rotation);
            Physics.SyncTransforms();
            placement.SendMessage("UpdatePreviewPose");
            var position = (Vector3)typeof(ItemPlacementController).GetField("previewPosition", flags).GetValue(placement);
            Assert.That((bool)typeof(ItemPlacementController).GetField("isCurrentPoseValid", flags).GetValue(placement), Is.True, $"Preview rejected at {position}");
            Assert.That(Vector3.Distance(position, course.PlacementTargetPose.position), Is.LessThan(.8f), $"Preview missed table: {position}");
            Object.FindAnyObjectByType<TutorialRadioView>().SendMessage("Update");
            guide.SetMode(Game.Client.KeySettingGuideView.Mode.Placing);
            Assert.That(guide.transform.Find("Row1/Action").GetComponent<TMPro.TMP_Text>().fontStyle, Is.EqualTo(TMPro.FontStyles.Bold));
            CaptureTutorialUi(aim.position, course.PlacementTargetPose.position);
            placement.SendMessage("ConfirmPlacement");
            Assert.That(session.CurrentStep, Is.EqualTo(TutorialStep.UseShredder));
            yield return new WaitForSeconds(1f);
            Assert.That(item.transform.position.y, Is.GreaterThan(1f), "Box fell through tabletop.");
            Object.Destroy(aim.gameObject);
        }

        private static void CaptureTutorialUi(Vector3 position, Vector3 target)
        {
            var camera = Camera.main;
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position));
            var render = new RenderTexture(1920, 1080, 24);
            var previous = RenderTexture.active;
            camera.targetTexture = render;
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = .5f;
            }
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = render;
            var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            image.Apply();
            System.IO.Directory.CreateDirectory("Logs/TutorialPreview");
            System.IO.File.WriteAllBytes("Logs/TutorialPreview/PlacementUI.png", image.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = previous;
            Object.Destroy(image);
            Object.Destroy(render);
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

            SetControllerPose(controller, new Vector3(20, .2f, 5.2f), settings.StandHeight);
            MoveController(controller, Vector3.back, 4.35f);
            Assert.That(player.transform.position.z, Is.GreaterThan(4f), "Standing bypassed stage 05.");
            SetControllerPose(controller, new Vector3(20, .2f, 5.2f), settings.CrouchHeight);
            MoveController(controller, Vector3.back, 4.35f);
            Assert.That(player.transform.position.z, Is.LessThan(1f), "Crouching cannot clear stage 05.");

            SetControllerPose(controller, new Vector3(20, .2f, .85f), settings.CrouchHeight);
            MoveController(controller, Vector3.back, 3.9f);
            Assert.That(player.transform.position.z, Is.GreaterThan(.5f), "Crouching bypassed stage 06.");
            SetControllerPose(controller, new Vector3(20, .2f, .85f), settings.ProneHeight);
            MoveController(controller, Vector3.back, 3.9f);
            Assert.That(player.transform.position.z, Is.LessThan(-2.7f), "Prone cannot clear stage 06.");

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
        public IEnumerator PostureLessonsRequireTraversingTheirPassages()
        {
            var load = SceneManager.LoadSceneAsync("Tutorial", LoadSceneMode.Single);
            while (!load.isDone) yield return null;
            yield return null;
            var player = Object.FindAnyObjectByType<PlayerMovement>();
            var session = Object.FindAnyObjectByType<TutorialSession>();
            var controller = player.GetComponent<CharacterController>();
            player.enabled = false;
            session.ObserveMovement(Observe(distance: 6f, look: 30f));
            session.ObserveMovement(Observe(distance: 8f, speed: 6f));
            session.ObserveMovement(Observe(grounded: true));
            session.ObserveMovement(Observe(grounded: false));
            session.ObserveMovement(Observe(grounded: true));
            player.ApplyNetworkPosture(Game.Core.Players.PlayerPosture.Crouching);
            yield return null;
            Assert.That(session.CurrentStep, Is.EqualTo(TutorialStep.Crouch));
            SetControllerPose(controller, new Vector3(20, .2f, 5.15f), player.MovementSettings.CrouchHeight);
            yield return null;
            for (var i = 0; i < 75; i++)
            {
                // Standing up at the exit mouth must still count once the tunnel was crossed crouched.
                if (i == 50) player.ApplyNetworkPosture(Game.Core.Players.PlayerPosture.Standing);
                controller.Move(Vector3.back * (4.3f / 60) + Vector3.down * .015f);
                yield return null;
            }
            Assert.That(session.CurrentStep, Is.EqualTo(TutorialStep.Prone), $"position={player.transform.position}, posture={player.Posture}, entered={typeof(TutorialMovementCourse).GetField("enteredPassage", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Object.FindAnyObjectByType<TutorialMovementCourse>())}");
            player.ApplyNetworkPosture(Game.Core.Players.PlayerPosture.Prone);
            SetControllerPose(controller, controller.transform.position, player.MovementSettings.ProneHeight);
            yield return null;
            Assert.That(session.CurrentStep, Is.EqualTo(TutorialStep.Prone));
            for (var i = 0; i < 75; i++)
            {
                controller.Move(Vector3.back * (4.3f / 60) + Vector3.down * .015f);
                yield return null;
            }
            Assert.That(session.CurrentStep, Is.EqualTo(TutorialStep.PickUp));
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

            var loadingView = Object.FindAnyObjectByType<LoadingView>(FindObjectsInactive.Include) ?? LoadingView.Create(null);
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
            new(distance, look, speed, 5f, grounded, posture, passageCompleted: true);
    }
}
