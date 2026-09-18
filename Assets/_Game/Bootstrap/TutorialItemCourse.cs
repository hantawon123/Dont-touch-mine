using Game.Client.Interactions;
using Game.Client.Tutorial;
using Game.Core.Tutorial;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Bootstrap
{
    /// <summary>
    /// Connects the standalone tutorial to the real carry, placement and
    /// shredder components. It contains recovery only; item behavior remains
    /// owned by the existing gameplay components.
    /// </summary>
    public sealed class TutorialItemCourse : MonoBehaviour
    {
        private const float FallHeight = -2f;
        private const float MaxDistanceFromPlayer = 20f;

        [SerializeField]
        private TutorialSession session;

        [SerializeField]
        private PlayerInteractor interactor;

        [SerializeField]
        private CarryableItem trainingItem;

        [SerializeField]
        private CarryableItem trainingItemPrefab;

        [SerializeField]
        private ShredderInteractable shredder;

        [SerializeField]
        private Transform[] recoveryPoints;

        private Pose spawnPose;

        private void Awake()
        {
            if (session == null || interactor == null || trainingItem == null ||
                trainingItemPrefab == null || shredder == null)
            {
                Debug.LogError("TutorialItemCourse references are incomplete.", this);
                enabled = false;
                return;
            }

            spawnPose = new Pose(trainingItem.transform.position, trainingItem.transform.rotation);
        }

        private void OnEnable()
        {
            if (interactor != null)
                interactor.LocalItemActionPerformed += OnItemAction;
            if (shredder != null)
                shredder.ItemProcessed += OnShredderProcessed;
        }

        private void OnDisable()
        {
            if (interactor != null)
                interactor.LocalItemActionPerformed -= OnItemAction;
            if (shredder != null)
                shredder.ItemProcessed -= OnShredderProcessed;
        }

        private void Update()
        {
            if (session.CurrentStep < TutorialStep.PickUp || session.IsComplete)
                return;

            var retryRequested = Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
            var itemMissing = trainingItem == null;
            var itemOutOfBounds = !itemMissing &&
                (trainingItem.transform.position.y < FallHeight ||
                 Vector3.Distance(trainingItem.transform.position, interactor.transform.position) > MaxDistanceFromPlayer);
            if (retryRequested || itemMissing || itemOutOfBounds)
                RecoverItem();
        }

        public void RecoverItem()
        {
            session.RetryCurrentStep();
            int stage = (int)session.CurrentStep - (int)TutorialStep.PickUp;
            if (recoveryPoints != null && stage >= 0 && stage < recoveryPoints.Length && recoveryPoints[stage] != null)
                spawnPose = new Pose(recoveryPoints[stage].position, recoveryPoints[stage].rotation);
            if (interactor.CarriedItem == trainingItem)
                interactor.ReleaseCarriedItem();

            if (trainingItem == null)
            {
                trainingItem = Instantiate(
                    trainingItemPrefab,
                    spawnPose.position,
                    spawnPose.rotation);
                trainingItem.name = "TrainingItem";
            }

            trainingItem.gameObject.SetActive(true);
            trainingItem.OnSettled(spawnPose, keepDynamic: false);
            Debug.Log($"[Tutorial] Recovered item for step: {session.CurrentStep}", this);
        }

        private void OnItemAction(LocalItemAction action, CarryableItem item)
        {
            if (item != trainingItem)
                return;
            var tutorialAction = action switch
            {
                LocalItemAction.PickedUp => TutorialInteractionAction.PickUp,
                LocalItemAction.Dropped => TutorialInteractionAction.Drop,
                LocalItemAction.Thrown => TutorialInteractionAction.Throw,
                LocalItemAction.Placed => TutorialInteractionAction.Place,
                _ => throw new System.ArgumentOutOfRangeException(nameof(action), action, null)
            };
            session.ObserveInteraction(tutorialAction);
        }

        private void OnShredderProcessed(CarryableItem item)
        {
            if (item == trainingItem)
            {
                session.ObserveInteraction(TutorialInteractionAction.UseShredder);
            }
        }
    }
}
