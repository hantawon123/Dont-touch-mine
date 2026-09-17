using System;
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
        [SerializeField]
        private UnityEditor.SceneAsset _resultScene;
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
        [SerializeField, HideInInspector]
        private string _resultScenePath = string.Empty;

        /// <summary>
        /// 맵 id 하나에 매치 씬 하나. 방장이 방 설정에서 고른 <c>MapCatalog</c>의 id가
        /// 매치 시작 때 이 목록으로 씬이 된다.
        /// </summary>
        [Serializable]
        private sealed class MapSceneEntry
        {
            [Tooltip("MapCatalog의 맵 id (예: playground, supermarket).")]
            public string mapId = string.Empty;

#if UNITY_EDITOR
            [Tooltip("이 맵이 플레이되는 씬. 빌드 씬 목록에도 있어야 한다.")]
            public UnityEditor.SceneAsset scene;
#endif

            [HideInInspector]
            public string scenePath = string.Empty;
        }

        [SerializeField]
        [Tooltip("맵 id별 매치 씬. 여기 없는 맵 id는 위의 Match Scene(기본 맵)으로 간다.")]
        private MapSceneEntry[] _mapScenes = Array.Empty<MapSceneEntry>();

        [SerializeField]
        [Tooltip("맵 id별 결과(엔딩) 씬. 여기 없는 맵은 위의 Result Scene(기본 엔딩)으로 간다. " +
                 "예: mansion → MansionResult(지하실 엔딩).")]
        private MapSceneEntry[] _mapResultScenes = Array.Empty<MapSceneEntry>();

        public string ResultScenePath => _resultScenePath;

        /// <summary>
        /// 방 설정의 맵 id에 해당하는 매치 씬. 목록에 없는 id는 기본 매치 씬으로
        /// 떨어지며, 그 사실을 경고로 남긴다(조용히 다른 맵을 여는 일이 없도록).
        /// </summary>
        public SceneRef MatchSceneFor(string mapId)
        {
            var entry = FindEntry(mapId);
            if (entry != null)
            {
                return Resolve(entry.scenePath, $"match scene for map '{entry.mapId}'");
            }

            if (_mapScenes.Length > 0)
            {
                Debug.LogWarning(
                    $"[Network] No match scene is mapped to map '{mapId}' on the " +
                    "NetworkScenes asset; using the default match scene.");
            }

            return MatchScene;
        }

        /// <summary>이 씬이 어떤 맵의 매치 씬인지(기본 매치 씬 포함). 로비 복귀 때 내릴 씬을 찾는 데 쓴다.</summary>
        public bool IsMatchScene(SceneRef scene)
        {
            if (!scene.IsValid)
            {
                return false;
            }

            if (TryResolve(_matchScenePath, out var defaultScene) && defaultScene == scene)
            {
                return true;
            }

            foreach (var entry in _mapScenes)
            {
                if (TryResolve(entry.scenePath, out var mapped) && mapped == scene)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>맵 id가 이 에셋에 씬으로 연결되어 있는지(기본 매치 씬 fallback은 세지 않는다).</summary>
        public bool HasMappedScene(string mapId) => FindEntry(mapId) != null;

        /// <summary>
        /// 이 맵의 결과(엔딩) 씬. 맵 전용 엔딩이 없으면 기본 결과 씬. 매치 씬과 달리 없는 것이 정상이라 경고하지 않는다.
        /// </summary>
        public SceneRef ResultSceneFor(string mapId)
        {
            var entry = FindEntry(_mapResultScenes, mapId);
            return entry != null
                ? Resolve(entry.scenePath, $"result scene for map '{entry.mapId}'")
                : ResultScene;
        }

        /// <summary>기본 결과 씬이든 맵 전용 결과 씬이든, 올라와 있는 씬이 결과 씬인지.</summary>
        public bool IsResultScene(SceneRef scene)
        {
            if (!scene.IsValid)
            {
                return false;
            }

            if (TryResolve(_resultScenePath, out var defaultScene) && defaultScene == scene)
            {
                return true;
            }

            foreach (var entry in _mapResultScenes)
            {
                if (entry != null && TryResolve(entry.scenePath, out var mapped) && mapped == scene)
                {
                    return true;
                }
            }

            return false;
        }

        public bool HasMappedResultScene(string mapId) => FindEntry(_mapResultScenes, mapId) != null;

        private MapSceneEntry FindEntry(string mapId) => FindEntry(_mapScenes, mapId);

        private static MapSceneEntry FindEntry(MapSceneEntry[] entries, string mapId)
        {
            if (string.IsNullOrWhiteSpace(mapId))
            {
                return null;
            }

            var candidate = mapId.Trim();
            foreach (var entry in entries)
            {
                if (entry != null && string.Equals(entry.mapId?.Trim(), candidate, StringComparison.Ordinal))
                {
                    return entry;
                }
            }

            return null;
        }
        public SceneRef ResultScene => Resolve(_resultScenePath, "result scene");

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

        /// <summary>같은 해석을 로그 없이. 여러 씬을 훑어 비교할 때 쓴다.</summary>
        private static bool TryResolve(string scenePath, out SceneRef scene)
        {
            scene = default;
            if (string.IsNullOrEmpty(scenePath))
            {
                return false;
            }

            var buildIndex = SceneUtility.GetBuildIndexByScenePath(scenePath);
            if (buildIndex < 0)
            {
                return false;
            }

            scene = SceneRef.FromIndex(buildIndex);
            return true;
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
            var resultPath = _resultScene == null
                ? string.Empty
                : UnityEditor.AssetDatabase.GetAssetPath(_resultScene);

            if (_matchScenePath != matchPath || _lobbyScenePath != lobbyPath || _resultScenePath != resultPath)
            {
                _matchScenePath = matchPath;
                _lobbyScenePath = lobbyPath;
                _resultScenePath = resultPath;
                UnityEditor.EditorUtility.SetDirty(this);
            }

            WarnIfMissingFromBuild(matchPath, _matchScene);
            WarnIfMissingFromBuild(lobbyPath, _lobbyScene);
            WarnIfMissingFromBuild(resultPath, _resultScene);

            SyncEntryPaths(_mapScenes);
            SyncEntryPaths(_mapResultScenes);
        }

        private void SyncEntryPaths(MapSceneEntry[] entries)
        {
            foreach (var entry in entries)
            {
                if (entry == null)
                {
                    continue;
                }

                var path = entry.scene == null
                    ? string.Empty
                    : UnityEditor.AssetDatabase.GetAssetPath(entry.scene);
                if (entry.scenePath != path)
                {
                    entry.scenePath = path;
                    UnityEditor.EditorUtility.SetDirty(this);
                }

                WarnIfMissingFromBuild(path, entry.scene);
            }
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
