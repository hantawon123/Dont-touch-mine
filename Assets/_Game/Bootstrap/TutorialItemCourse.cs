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
        private const float PlacementPositionTolerance = 0.8f;
        private const float PlacementRotationTolerance = 22f;
        private static readonly Quaternion TargetRotation = Quaternion.Euler(15f, 90f, 0f);

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
        private Pose placementTargetPose;
        private GameObject placementGuide;

        public Pose PlacementTargetPose => placementTargetPose;

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
            placementTargetPose = BuildPlacementTargetPose();
            placementGuide = CreatePlacementGuide();
            RefreshPlacementGuide(session.CurrentStep);
        }

        private void OnEnable()
        {
            if (interactor != null)
                interactor.LocalItemActionPerformed += OnItemAction;
            if (shredder != null)
                shredder.ItemProcessed += OnShredderProcessed;
            if (session != null)
                session.StepChanged += RefreshPlacementGuide;
        }

        private void OnDisable()
        {
            if (interactor != null)
                interactor.LocalItemActionPerformed -= OnItemAction;
            if (shredder != null)
                shredder.ItemProcessed -= OnShredderProcessed;
            if (session != null)
                session.StepChanged -= RefreshPlacementGuide;
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

            if (action == LocalItemAction.Placed && session.CurrentStep == TutorialStep.Place &&
                !IsAtPlacementTarget(item.transform))
            {
                RecoverItem();
                return;
            }

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

        private Pose BuildPlacementTargetPose()
        {
            var placementZone = recoveryPoints != null && recoveryPoints.Length > 3 && recoveryPoints[3] != null
                ? recoveryPoints[3].parent
                : transform;
            var surface = placementZone.position + Vector3.up * 1.29f;
            var verticalExtent = PlacementVolumeMath.RotatedVerticalExtent(
                TargetRotation, trainingItem.PlacementHalfExtents);
            var rootPosition = surface + Vector3.up * verticalExtent -
                TargetRotation * trainingItem.PlacementCenterOffset;
            return new Pose(rootPosition, TargetRotation);
        }

        private GameObject CreatePlacementGuide()
        {
            var guide = Instantiate(trainingItemPrefab.gameObject,
                placementTargetPose.position, placementTargetPose.rotation);
            guide.name = "PlacementTargetGhost";
            foreach (var collider in guide.GetComponentsInChildren<Collider>())
                collider.enabled = false;
            if (guide.TryGetComponent<Rigidbody>(out var body))
            {
                body.isKinematic = true;
                body.detectCollisions = false;
            }
            if (guide.TryGetComponent<CarryableItem>(out var carryable))
                carryable.enabled = false;

            var ghostMaterial = interactor.GetComponent<ItemPlacementController>()?.ValidGhostMaterial;
            if (ghostMaterial != null)
            {
                foreach (var renderer in guide.GetComponentsInChildren<Renderer>())
                {
                    var materials = renderer.sharedMaterials;
                    for (var i = 0; i < materials.Length; i++)
                        materials[i] = ghostMaterial;
                    renderer.sharedMaterials = materials;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }

            return guide;
        }

        private bool IsAtPlacementTarget(Transform itemTransform) =>
            Vector3.Distance(itemTransform.position, placementTargetPose.position) <= PlacementPositionTolerance &&
            Quaternion.Angle(itemTransform.rotation, placementTargetPose.rotation) <= PlacementRotationTolerance;

        private void RefreshPlacementGuide(TutorialStep step)
        {
            if (placementGuide != null)
                placementGuide.SetActive(step == TutorialStep.Place);
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
