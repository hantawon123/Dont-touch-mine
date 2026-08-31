using System;
using Game.Client.Cameras;
using Game.Network.Players;
using Game.Network.Session;
using UnityEngine;
using VContainer.Unity;

namespace Game.Bootstrap
{
    public sealed class HostMigrationPresentationController : IStartable, ITickable, IDisposable
    {
        private readonly NetworkRunnerService network;
        private readonly HostMigrationFrameView view;
        private PlayerCameraController cameraRig;
        private bool waiting;
        private double readyAt = -1d;
        private int revealFrame = -1;

        public HostMigrationPresentationController(NetworkRunnerService network, HostMigrationFrameView view)
        {
            this.network = network ?? throw new ArgumentNullException(nameof(network));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
        }

        public void Start() => network.HostMigrationStarting += OnMigrationStarting;

        public void Dispose()
        {
            network.HostMigrationStarting -= OnMigrationStarting;
            ReleaseCamera();
            if (view != null) view.Clear();
        }

        private void OnMigrationStarting()
        {
            try
            {
                ReleaseCamera();
                view.Capture();
                cameraRig = UnityEngine.Object.FindFirstObjectByType<PlayerCameraController>();
                if (cameraRig != null) cameraRig.SetMigrationSuspended(true);
                waiting = true;
                readyAt = -1d;
                revealFrame = -1;
            }
            catch (Exception exception)
            {
                // Presentation must never prevent Fusion from recovering the room.
                waiting = false;
                ReleaseCamera();
                view.Clear();
                Debug.LogException(exception);
            }
        }

        public void Tick()
        {
            if (!waiting || network.IsHostMigrationInProgress) return;
            if (!network.IsRuntimeReady || network.IsBrowsingLobby)
            {
                waiting = false;
                ReleaseCamera();
                view.Clear();
                return;
            }

            if (readyAt < 0d) readyAt = Time.unscaledTimeAsDouble;
            if (revealFrame < 0)
            {
                var cameraReady = IsLocalCameraReady();
                if (!CanReveal(network.IsRuntimeReady, cameraReady, network.IsResultSceneLoaded,
                        Time.unscaledTimeAsDouble - readyAt)) return;
                if (!network.IsResultSceneLoaded && !cameraReady)
                    Debug.LogWarning("[Network] Migration resumed, but its local camera was not ready within 5 seconds.");
                ReleaseCamera();
                // Let camera LateUpdate and Cinemachine render the restored target first.
                revealFrame = Time.frameCount + 2;
            }
            if (Time.frameCount < revealFrame) return;
            waiting = false;
            view.Reveal();
        }

        internal static bool CanReveal(bool runtimeReady, bool cameraReady, bool resultScene, double elapsed) =>
            runtimeReady && (cameraReady || resultScene || elapsed >= 5d);

        private bool IsLocalCameraReady()
        {
            var rig = UnityEngine.Object.FindFirstObjectByType<PlayerCameraController>();
            if (rig == null) return false;
            foreach (var avatar in network.PlayerAvatars)
            {
                if (avatar == null || avatar.PlayerId == null || !avatar.IsOwner) continue;
                var motor = avatar.GetComponent<NetworkPlayerMotor>();
                return motor != null && motor.IsScenePlacementReady && rig.FollowTarget == avatar.transform;
            }
            return false;
        }

        private void ReleaseCamera()
        {
            if (cameraRig != null) cameraRig.SetMigrationSuspended(false);
            cameraRig = null;
        }
    }
}
