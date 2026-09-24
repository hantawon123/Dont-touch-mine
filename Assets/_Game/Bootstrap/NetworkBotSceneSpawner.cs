using System;
using Game.Network.Session;
using UnityEngine;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>
    /// Turns scene bot markers into replicated bots after Fusion finishes loading.
    /// </summary>
    public sealed class NetworkBotSceneSpawner : IStartable, IDisposable
    {
        private readonly NetworkRunnerService network;

        public NetworkBotSceneSpawner(NetworkRunnerService network)
        {
            this.network = network;
        }

        public void Start()
        {
            if (network != null)
            {
                network.SceneLoaded += SpawnMarkedBots;
            }
        }

        public void Dispose()
        {
            if (network != null)
            {
                network.SceneLoaded -= SpawnMarkedBots;
            }
        }

        private void SpawnMarkedBots()
        {
            if (network == null || !network.IsServer)
            {
                return;
            }

            var points = UnityEngine.Object.FindObjectsByType<NetworkBotSpawnPoint>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            foreach (var point in points)
            {
                if (point == null ||
                    !point.TryBeginSpawn(
                        out var profile,
                        out var pose,
                        out var target))
                {
                    continue;
                }

                if (!network.TrySpawnBot(profile, pose, target))
                {
                    point.CancelSpawn();
                    Debug.LogWarning(
                        $"[Bot] Could not spawn the bot marked by '{point.name}'.",
                        point);
                }
            }
        }
    }
}
