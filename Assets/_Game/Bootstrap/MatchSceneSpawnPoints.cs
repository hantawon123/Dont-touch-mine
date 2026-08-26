using System;
using Game.Network.Players;
using Game.Network.Session;
using UnityEngine;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>
    /// Hands the loaded scene's spawn points to the spawner every time a
    /// networked scene finishes loading.
    /// </summary>
    /// <remarks>
    /// This lives in Bootstrap rather than in the network layer because reading
    /// scene components is Bootstrap's job, and because <c>Game.Network</c>
    /// cannot see <see cref="MatchSceneConfiguration"/> without depending on
    /// Bootstrap, which would be a cycle. The network layer only announces that
    /// a scene arrived.
    /// <para>
    /// Registered on the project scope, not a scene scope. A networked scene
    /// load replaces the current scene, so anything living in that scene would
    /// be destroyed by the very event it is waiting for.
    /// </para>
    /// </remarks>
    public sealed class MatchSceneSpawnPoints : IStartable, IDisposable
    {
        private readonly NetworkRunnerService _network;
        private readonly PlayerSpawner _spawner;

        public MatchSceneSpawnPoints(NetworkRunnerService network, PlayerSpawner spawner)
        {
            _network = network;
            _spawner = spawner;
        }

        public void Start()
        {
            if (_network != null)
            {
                _network.SceneLoaded += OnSceneLoaded;
            }
        }

        public void Dispose()
        {
            if (_network != null)
            {
                _network.SceneLoaded -= OnSceneLoaded;
            }
        }

        /// <summary>
        /// Reads the points the loaded scene marked out, or clears them.
        /// </summary>
        /// <remarks>
        /// The previous scene's points are dropped even when the new scene has
        /// none. Keeping them would place characters on positions belonging to a
        /// scene that no longer exists, which looks like a physics bug rather
        /// than a missing configuration.
        /// </remarks>
        private void OnSceneLoaded()
        {
            if (_spawner == null)
            {
                return;
            }

            var configuration =
                UnityEngine.Object.FindAnyObjectByType<MatchSceneConfiguration>();

            if (configuration == null)
            {
                // Not a warning. This also runs when the session opens in the
                // lobby scene, which has no spawn points and is not supposed to.
                // A map that reaches a match without them is caught below, where
                // the component exists but cannot be read.
                Debug.Log(
                    "[Bootstrap] The loaded scene marks no spawn points, so " +
                    "characters stay in a ring around the origin.");

                _spawner.UseSpawnPoses(Array.Empty<Pose>());
                return;
            }

            try
            {
                var poses = configuration.CaptureSpawnPoses();
                _spawner.UseSpawnPoses(poses);

                // Said out loud on the way through. Staying silent on success
                // makes "it worked and there was nobody to place" impossible to
                // tell apart from "this never ran", which is exactly the
                // question asked when characters end up in the wrong place.
                Debug.Log(
                    $"[Bootstrap] Took {poses.Length} spawn points from " +
                    $"'{configuration.gameObject.scene.name}'.",
                    configuration);
            }
            catch (InvalidOperationException exception)
            {
                // Reported rather than swallowed. A map that reaches a real match
                // with unassigned points is a mistake, unlike a half-built test
                // scene, and the fallback ring is not somewhere a match can be
                // played.
                Debug.LogError(
                    $"[Bootstrap] The loaded scene's spawn points are unusable: " +
                    $"{exception.Message}",
                    configuration);

                _spawner.UseSpawnPoses(Array.Empty<Pose>());
            }
        }
    }
}
