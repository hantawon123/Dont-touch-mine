using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Network
{
    /// <summary>
    /// Every scene this layer loads over the network, in one asset.
    /// </summary>
    /// <remarks>
    /// Here rather than on a scene component for the same reason
    /// <see cref="NetworkPrefabs"/> is: <c>Game.Bootstrap</c> does not reference
    /// Fusion and therefore cannot hold a <see cref="SceneRef"/>. Bootstrap
    /// serializes this asset and registers it, so the Fusion type stays inside
    /// this layer.
    /// <para>
    /// The scene is stored as a path rather than a build index. Indices shift
    /// whenever someone reorders the build list, and a silently shifted index
    /// would load the wrong map instead of failing.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(
        fileName = "NetworkScenes",
        menuName = "Game/Network/Network Scenes")]
    public sealed class NetworkScenes : ScriptableObject
    {
#if UNITY_EDITOR
        [SerializeField]
        [Tooltip("Map a match plays in. It must also be in the build scene " +
                 "list, or it cannot be loaded over the network.")]
        private UnityEditor.SceneAsset _matchScene;

        [SerializeField]
        [Tooltip("Waiting room the players return to after a match.")]
        private UnityEditor.SceneAsset _lobbyScene;
#endif

        /// <summary>
        /// Written from the editor field above. Serialized separately because
        /// <c>SceneAsset</c> does not exist in a player build.
        /// </summary>
        [SerializeField]
        [HideInInspector]
        private string _matchScenePath = string.Empty;

        [SerializeField]
        [HideInInspector]
        private string _lobbyScenePath = string.Empty;

        /// <summary>
        /// The map a match plays in. Invalid when nothing is assigned or the
        /// scene is missing from the build list; callers check
        /// <c>IsValid</c> rather than assuming.
        /// </summary>
        public SceneRef MatchScene => Resolve(_matchScenePath, "match scene");

        public SceneRef LobbyScene => Resolve(_lobbyScenePath, "lobby scene");

        /// <summary>
        /// Turns a project path into the reference Fusion replicates.
        /// </summary>
        /// <remarks>
        /// Fusion identifies a networked scene by its build index, so a scene
        /// that is not in the build list cannot be loaded no matter how it is
        /// referenced. That is reported here rather than left to fail inside the
        /// scene manager, where the message does not say which scene was meant.
        /// </remarks>
        private static SceneRef Resolve(string scenePath, string label)
        {
            if (string.IsNullOrEmpty(scenePath))
            {
                Debug.LogError(
                    $"[Network] No {label} is assigned on the NetworkScenes " +
                    "asset, so the room cannot move into the map.");

                return default;
            }

            var buildIndex = SceneUtility.GetBuildIndexByScenePath(scenePath);

            if (buildIndex < 0)
            {
                Debug.LogError(
                    $"[Network] '{scenePath}' is not in the build scene list, so " +
                    "Fusion cannot load it. Add it under File > Build Profiles.");

                return default;
            }

            return SceneRef.FromIndex(buildIndex);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Keeps the serialized path in step with the scene picked above, and
        /// says so while the project is open rather than at the moment a match
        /// starts.
        /// </summary>
        private void OnValidate()
        {
            var matchPath = _matchScene == null
                ? string.Empty
                : UnityEditor.AssetDatabase.GetAssetPath(_matchScene);
            var lobbyPath = _lobbyScene == null
                ? string.Empty
                : UnityEditor.AssetDatabase.GetAssetPath(_lobbyScene);

            if (_matchScenePath != matchPath || _lobbyScenePath != lobbyPath)
            {
                _matchScenePath = matchPath;
                _lobbyScenePath = lobbyPath;
                UnityEditor.EditorUtility.SetDirty(this);
            }

            WarnIfMissingFromBuild(matchPath, _matchScene);
            WarnIfMissingFromBuild(lobbyPath, _lobbyScene);
        }

        private void WarnIfMissingFromBuild(
            string path,
            UnityEditor.SceneAsset scene)
        {
            if (!string.IsNullOrEmpty(path) &&
                SceneUtility.GetBuildIndexByScenePath(path) < 0)
            {
                Debug.LogWarning(
                    $"[Network] '{scene.name}' is not in the build scene list. " +
                    "Add it under File > Build Profiles.",
                    this);
            }
        }
#endif
    }
}
