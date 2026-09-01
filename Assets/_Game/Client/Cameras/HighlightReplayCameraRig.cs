using System;
using Unity.Cinemachine;
using UnityEngine;

namespace Game.Client.Cameras
{
    /// <summary>
    /// Owns the temporary Cinemachine camera used only while a highlight is playing.
    /// </summary>
    public sealed class HighlightReplayCameraRig : IDisposable
    {
        private const int ReplayPriority = 100;

        private readonly GameObject root;
        private readonly CinemachineTargetGroup targetGroup;
        private readonly CinemachineCamera replayCamera;
        private readonly CinemachineGroupFraming groupFraming;
        private bool disposed;

        private HighlightReplayCameraRig(Transform output, int collisionLayerMask)
        {
            root = new GameObject("[Highlight Cinemachine Rig]");
            root.transform.SetPositionAndRotation(output.position, output.rotation);

            var targetGroupObject = new GameObject("Replay Targets");
            targetGroupObject.transform.SetParent(root.transform, false);
            targetGroup = targetGroupObject.AddComponent<CinemachineTargetGroup>();
            targetGroup.PositionMode = CinemachineTargetGroup.PositionModes.GroupCenter;
            targetGroup.RotationMode = CinemachineTargetGroup.RotationModes.Manual;
            targetGroup.UpdateMethod = CinemachineTargetGroup.UpdateMethods.LateUpdate;

            var cameraObject = new GameObject("Replay Camera");
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.SetPositionAndRotation(output.position, output.rotation);
            replayCamera = cameraObject.AddComponent<CinemachineCamera>();
            replayCamera.Priority = ReplayPriority;
            replayCamera.Target = new CameraTarget
            {
                TrackingTarget = targetGroup.transform,
                LookAtTarget = targetGroup.transform,
                CustomLookAtTarget = false,
            };

            var composer = cameraObject.AddComponent<CinemachineRotationComposer>();
            composer.Damping = new Vector2(0.25f, 0.25f);
            composer.CenterOnActivate = true;

            groupFraming = cameraObject.AddComponent<CinemachineGroupFraming>();
            groupFraming.FramingMode = CinemachineGroupFraming.FramingModes.HorizontalAndVertical;
            groupFraming.SizeAdjustment = CinemachineGroupFraming.SizeAdjustmentModes.DollyThenZoom;
            groupFraming.LateralAdjustment = CinemachineGroupFraming.LateralAdjustmentModes.ChangePosition;
            groupFraming.DollyRange = new Vector2(0f, 8f);
            groupFraming.FovRange = new Vector2(35f, 70f);
            groupFraming.Damping = 0.35f;

            var deoccluder = cameraObject.AddComponent<CinemachineDeoccluder>();
            deoccluder.CollideAgainst = collisionLayerMask;
            deoccluder.MinimumDistanceFromTarget = 0.5f;
            deoccluder.AvoidObstacles = new CinemachineDeoccluder.ObstacleAvoidance
            {
                Enabled = true,
                DistanceLimit = 0f,
                MinimumOcclusionTime = 0f,
                CameraRadius = 0.4f,
                Strategy = CinemachineDeoccluder.ObstacleAvoidance.ResolutionStrategy.PreserveCameraHeight,
                MaximumEffort = 4,
                SmoothingTime = 0.1f,
                Damping = 0.35f,
                DampingWhenOccluded = 0.05f,
            };
        }

        public static HighlightReplayCameraRig TryCreate(Transform output, int collisionLayerMask)
        {
            if (output == null || output.GetComponent<CinemachineBrain>() == null)
            {
                return null;
            }

            return new HighlightReplayCameraRig(output, collisionLayerMask);
        }

        public void SetTargets(Transform primary, Transform secondary)
        {
            targetGroup.Targets.Clear();
            if (primary != null)
            {
                targetGroup.AddMember(primary, 1f, 0.75f);
            }

            if (secondary != null && secondary != primary)
            {
                targetGroup.AddMember(secondary, 0.85f, 0.75f);
            }
        }

        public void SetPose(
            Vector3 desiredPosition,
            Quaternion desiredRotation,
            float interpolation,
            float framingSize,
            bool hardCut)
        {
            if (disposed) return;
            var cameraTransform = replayCamera.transform;
            var position = Vector3.Lerp(cameraTransform.position, desiredPosition, interpolation);
            var rotation = Quaternion.Slerp(cameraTransform.rotation, desiredRotation, interpolation);
            cameraTransform.SetPositionAndRotation(position, rotation);
            groupFraming.FramingSize = framingSize;
            if (hardCut)
            {
                replayCamera.PreviousStateIsValid = false;
                replayCamera.ForceCameraPosition(position, rotation);
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (root == null) return;
            root.SetActive(false);
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(root);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
