using Game.Client.Interactions;
using Game.Client.Tutorial;
using Game.Core.Tutorial;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Bootstrap
{
    /// <summary>
    /// Connects the standalone tutorial to the real carry, placement and
    /// shredder components. Checks lesson targets and recovers lost items;
    /// item behavior remains owned by the existing gameplay components.
    /// </summary>
    public sealed class TutorialItemCourse : MonoBehaviour
    {
        private const float FallHeight = -2f;

        private const float PlacementPositionTolerance = 0.9f;
        private static readonly Quaternion TargetRotation = Quaternion.Euler(0f, 90f, 0f);

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
        [SerializeField] private Collider placementSurface;
        [SerializeField] private Collider throwTarget;

        private Pose spawnPose;
        private Pose placementTargetPose;
        private GameObject placementGuide;
        private GameObject dropGuide;
        private GameObject pickupGuide;
        private GameObject throwGuide;
        private GameObject placementLabel;
        private Material targetMaterial;
        private bool waitingForDrop;
        private bool waitingForThrow;
        private Vector3 previousThrownPosition;

        public Vector3 DropTargetPosition => recoveryPoints[1].position;

        public Pose PlacementTargetPose => placementTargetPose;

        private void Awake()
        {
            if (session == null || interactor == null || trainingItem == null ||
                trainingItemPrefab == null || shredder == null || placementSurface == null || throwTarget == null)
            {
                Debug.LogError("TutorialItemCourse references are incomplete.", this);
                enabled = false;
                return;
            }

            spawnPose = new Pose(trainingItem.transform.position, trainingItem.transform.rotation);
            placementTargetPose = BuildPlacementTargetPose();
            targetMaterial = new Material(interactor.GetComponent<ItemPlacementController>().ValidGhostMaterial);
            targetMaterial.SetColor("_BaseColor", new Color(.15f, .55f, 1f, .4f));
            targetMaterial.SetColor("_Color", new Color(.15f, .55f, 1f, .4f));
            placementGuide = CreatePlacementGuide();
            pickupGuide = CreateTargetOutline("PickupTarget", trainingItem.transform.position, Vector2.one, false,
                "상자를 보고 " + Game.Client.KeySettingGuideView.CurrentKeyLabel(Game.Core.Settings.ControlAction.Interact) + "로 들기");
            var surfaceBounds = placementSurface.bounds;
            placementLabel = CreateTargetOutline("PlacementTarget", new Vector3(surfaceBounds.center.x, surfaceBounds.max.y + .03f, surfaceBounds.center.z), new Vector2(1f, .85f), false, "파란 목표 근처에 배치하기");
            dropGuide = CreateTargetOutline("DropTarget", new Vector3(DropTargetPosition.x, .16f, DropTargetPosition.z), new Vector2(2.5f, 2.5f), false, "여기에 내려놓기");
            var target = throwTarget.bounds;
            throwGuide = CreateTargetOutline("ThrowTarget", new Vector3(target.center.x, target.center.y, target.min.z - .08f), new Vector2(target.size.x, target.size.y), true, "이 표적에 던지기");
            MakeDynamic(trainingItem);
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
                trainingItem.transform.position.y < FallHeight;
            if (retryRequested || itemMissing || itemOutOfBounds)
                RecoverItem();
            if (trainingItem == null || trainingItem.IsCarried) return;
            var body = trainingItem.GetComponent<Rigidbody>();
            if (waitingForDrop && session.CurrentStep == TutorialStep.Drop && body.linearVelocity.sqrMagnitude < .16f)
            {
                var offset = trainingItem.transform.position - DropTargetPosition;
                offset.y = 0;
                var bottom = trainingItem.transform.position.y + trainingItem.PlacementCenterOffset.y - trainingItem.PlacementHalfExtents.y;
                if (offset.magnitude <= 1.25f && bottom < .3f)
                {
                    waitingForDrop = false;
                    session.ObserveInteraction(TutorialInteractionAction.Drop);
                }
            }
            if (waitingForThrow && session.CurrentStep == TutorialStep.Throw)
            {
                var bounds = throwTarget.bounds;
                bounds.Expand(trainingItem.PlacementHalfExtents.magnitude * 2f);
                var position = trainingItem.transform.position;
                var delta = position - previousThrownPosition;
                if (bounds.Contains(position) || (delta.sqrMagnitude > .0001f &&
                    bounds.IntersectRay(new Ray(previousThrownPosition, delta.normalized), out var distance) && distance <= delta.magnitude))
                {
                    waitingForThrow = false;
                    session.ObserveInteraction(TutorialInteractionAction.Throw);
                }
                previousThrownPosition = position;
            }
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
            waitingForDrop = waitingForThrow = false;
            trainingItem.OnReleased(spawnPose, Vector3.zero);
            MakeDynamic(trainingItem);
            Debug.Log($"[Tutorial] Recovered item for step: {session.CurrentStep}", this);
        }

        private void OnItemAction(LocalItemAction action, CarryableItem item)
        {
            if (item != trainingItem)
                return;

            if (action == LocalItemAction.PickedUp) waitingForDrop = waitingForThrow = false;
            if (action == LocalItemAction.Dropped && session.CurrentStep == TutorialStep.Drop)
            {
                waitingForDrop = true;
                return;
            }
            if (action == LocalItemAction.Thrown && session.CurrentStep == TutorialStep.Throw)
            {
                waitingForThrow = true;
                previousThrownPosition = item.transform.position;
                return;
            }
            if (action == LocalItemAction.Placed && session.CurrentStep == TutorialStep.Place &&
                !IsAtPlacementTarget(item.transform)) return;

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
            var bounds = placementSurface.bounds;
            var surface = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
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

            var ghostMaterial = targetMaterial;
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

        private bool IsAtPlacementTarget(Transform itemTransform)
        {
            var center = itemTransform.position + itemTransform.rotation * trainingItem.PlacementCenterOffset;
            var surface = placementSurface.bounds;
            var bottom = center.y - PlacementVolumeMath.RotatedVerticalExtent(itemTransform.rotation, trainingItem.PlacementHalfExtents);
            var offset = new Vector2(center.x - surface.center.x, center.z - surface.center.z);
            // Teach placing on the desk, not matching the model's exact orientation.
            return offset.magnitude <= PlacementPositionTolerance &&
                center.x >= surface.min.x && center.x <= surface.max.x &&
                center.z >= surface.min.z && center.z <= surface.max.z &&
                Mathf.Abs(bottom - surface.max.y) <= .2f;
        }

        private void OnDestroy()
        {
            if (placementGuide != null) Destroy(placementGuide);
            if (targetMaterial != null) Destroy(targetMaterial);
        }

        private void RefreshPlacementGuide(TutorialStep step)
        {
            if (placementGuide != null)
                placementGuide.SetActive(step == TutorialStep.Place);
            if (dropGuide != null) dropGuide.SetActive(step == TutorialStep.Drop);
            if (pickupGuide != null) pickupGuide.SetActive(step == TutorialStep.PickUp);
            if (throwGuide != null) throwGuide.SetActive(step == TutorialStep.Throw);
            if (placementLabel != null) placementLabel.SetActive(step == TutorialStep.Place);
        }

        private static void MakeDynamic(CarryableItem item)
        {
            var body = item.GetComponent<Rigidbody>();
            body.useGravity = true;
            body.isKinematic = false;
            body.WakeUp();
        }

        private GameObject CreateTargetOutline(string name, Vector3 position, Vector2 size, bool vertical, string label)
        {
            return TutorialTargetView.Create(transform, name, position, size, vertical, label,
                targetMaterial);
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
