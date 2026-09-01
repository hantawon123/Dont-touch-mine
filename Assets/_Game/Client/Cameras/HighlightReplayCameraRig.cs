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
        private readonly CinemachineCamera replayCamera;
        private bool disposed;

        private HighlightReplayCameraRig(Transform output)
        {
            root = new GameObject("[Highlight Cinemachine Rig]");
            root.transform.SetPositionAndRotation(output.position, output.rotation);

            var cameraObject = new GameObject("Replay Camera");
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.SetPositionAndRotation(output.position, output.rotation);
            replayCamera = cameraObject.AddComponent<CinemachineCamera>();
            replayCamera.Priority = ReplayPriority;
        }

        public static HighlightReplayCameraRig TryCreate(Transform output)
        {
            if (output == null || output.GetComponent<CinemachineBrain>() == null)
            {
                return null;
            }

            return new HighlightReplayCameraRig(output);
        }

        public void SetPose(
            Vector3 desiredPosition,
            Quaternion desiredRotation,
            float interpolation,
            bool hardCut)
        {
            if (disposed) return;
            var cameraTransform = replayCamera.transform;
            var position = Vector3.Lerp(cameraTransform.position, desiredPosition, interpolation);
            var rotation = Quaternion.Slerp(cameraTransform.rotation, desiredRotation, interpolation);
            cameraTransform.SetPositionAndRotation(position, rotation);
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
