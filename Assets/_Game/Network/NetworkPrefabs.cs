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
                 "NetworkCharacterController, PlayerAvatar and NetworkPlayerMotor.")]
        private NetworkObject _player;

        [SerializeField]
        [Tooltip("One per room, holding whether the match started and who is " +
                 "playing. Needs NetworkObject and MatchSessionState. Nothing " +
                 "is drawn, so it has no visual parts.")]
        private NetworkObject _matchSession;

        public NetworkObject Player => _player;

        public NetworkObject MatchSession => _matchSession;

#if UNITY_EDITOR
        /// <summary>
        /// Catches the two mistakes that fail silently at runtime: a missing
        /// prefab, and a prefab without <c>NetworkCharacterController</c>.
        /// That component owns both predicted movement and replicated position;
        /// using a plain transform synchronizer beside a CharacterController
        /// makes prediction restore the character to its previous position.
        /// </summary>
        private void OnValidate()
        {
            if (_player == null)
            {
                return;
            }

            if (_player.GetComponentInChildren<NetworkCharacterController>() == null)
            {
                Debug.LogWarning(
                    $"[Network] '{_player.name}' has no NetworkCharacterController. " +
                    "Predicted movement and spawn positions will not replicate " +
                    "correctly.",
                    this);
            }

            if (_player.GetComponentInChildren<Players.NetworkPlayerMotor>() == null)
            {
                Debug.LogWarning(
                    $"[Network] '{_player.name}' has no NetworkPlayerMotor. " +
                    "It can spawn but cannot consume authoritative movement input.",
                    this);
            }

            if (_matchSession != null &&
                _matchSession.GetComponentInChildren<Match.MatchSessionState>() == null)
            {
                Debug.LogWarning(
                    $"[Network] '{_matchSession.name}' has no MatchSessionState, " +
                    "so starting a match would confirm nothing.",
                    this);
            }
        }
#endif
    }
}
