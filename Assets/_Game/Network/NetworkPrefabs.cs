using Fusion;
using UnityEngine;

namespace Game.Network
{
    /// <summary>
    /// Every prefab this layer spawns over the network, in one asset.
    /// </summary>
    /// <remarks>
    /// The reference lives here rather than on a scene component because
    /// <c>Game.Bootstrap</c> does not reference Fusion and therefore cannot hold
    /// a <see cref="NetworkObject"/> field. Bootstrap serializes this asset
    /// instead and registers it, so the Fusion type stays inside this layer.
    /// </remarks>
    [CreateAssetMenu(
        fileName = "NetworkPrefabs",
        menuName = "Game/Network/Network Prefabs")]
    public sealed class NetworkPrefabs : ScriptableObject
    {
        [SerializeField]
        [Tooltip("Character spawned for each player. Needs NetworkObject, " +
                 "NetworkTransform and PlayerAvatar.")]
        private NetworkObject _player;

        public NetworkObject Player => _player;

#if UNITY_EDITOR
        /// <summary>
        /// Catches the two mistakes that fail silently at runtime: a missing
        /// prefab, and a prefab without <c>NetworkTransform</c>. Fusion only
        /// applies the spawn position locally, so without it every character
        /// stacks at the origin on every peer but the one that spawned it, and
        /// nothing in the console says why.
        /// </summary>
        private void OnValidate()
        {
            if (_player == null)
            {
                return;
            }

            if (_player.GetComponentInChildren<NetworkTransform>() == null)
            {
                Debug.LogWarning(
                    $"[Network] '{_player.name}' has no NetworkTransform. Spawn " +
                    "positions will not replicate and every character will appear " +
                    "at the origin on remote peers.",
                    this);
            }
        }
#endif
    }
}
