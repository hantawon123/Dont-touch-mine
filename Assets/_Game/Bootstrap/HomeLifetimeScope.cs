using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Client.Home;
using Game.Core.Home;
using Game.Core.Lobby;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

namespace Game.Bootstrap
{
    public sealed class HomeLifetimeScope : LifetimeScope
    {
        [SerializeField]
        private HomeMenuView homeMenuView;

        protected override void Configure(IContainerBuilder builder)
        {
            if (homeMenuView == null)
            {
                Debug.LogError("HomeMenuView must be assigned on HomeLifetimeScope.", this);
                return;
            }

            // No PlayerProfile here on purpose. Registering one would shadow the
            // application-wide profile for this scene only, so renaming yourself
            // on this screen would change a copy that nothing else can see: not
            // the saved profile, and not the name the network sends. Resolution
            // falls through to the project scope instead.
            builder.Register<NetworkHomeApplicationHost>(Lifetime.Scoped)
                .As<IHomeApplicationHost>();
            builder.RegisterEntryPoint<RoomBrowserWarmup>();
            builder.RegisterComponent(homeMenuView).As<IHomeMenuView>();
            builder.RegisterEntryPoint<HomeMenuPresenter>();

            // Placeholder rows until a Steam adapter calls FriendListSystem.ReplaceFriends.
            builder.RegisterBuildCallback(container =>
            {
                var friendList = container.Resolve<FriendListSystem>();
                var friendSearch = container.Resolve<FriendSearchSystem>();
                if (friendList.OnlineFriends.Count > 0 || friendList.OfflineFriends.Count > 0)
                {
                    return;
                }

                var previewFriends = new[]
                {
                    new FriendSummary("preview-1", "친구1", FriendPresence.InGame),
                    new FriendSummary("preview-2", "친구2", FriendPresence.InGame),
                    new FriendSummary("preview-3", "친구3", FriendPresence.Online),
                    new FriendSummary("preview-4", "친구4", FriendPresence.Offline)
                };
                friendList.ReplaceFriends(previewFriends);
                friendSearch.ReplaceDirectory(new[]
                {
                    previewFriends[0],
                    previewFriends[1],
                    previewFriends[2],
                    previewFriends[3],
                    new FriendSummary("preview-search-1", "친구5", FriendPresence.Online),
                    new FriendSummary("preview-search-2", "친구6", FriendPresence.Offline),
                    new FriendSummary("preview-search-3", "플레이어A", FriendPresence.Online)
                });
            });
        }

        /// <summary>
        /// Pays the Photon lobby handshake while the player is still on Home,
        /// so opening the room browser can reuse an established connection.
        /// </summary>
        private sealed class RoomBrowserWarmup : IStartable
        {
            private readonly RoomUiCommands rooms;

            public RoomBrowserWarmup(RoomUiCommands rooms)
            {
                this.rooms = rooms;
            }

            public void Start()
            {
                rooms.RefreshAsync(CancellationToken.None)
                    .Forget(exception => Debug.LogException(exception));
            }
        }

        /// <summary>
        /// Starts matchmaking beside the Room scene load instead of waiting for
        /// that scene to finish before opening the Photon lobby.
        /// </summary>
        private sealed class NetworkHomeApplicationHost : IHomeApplicationHost
        {
            private readonly RoomUiCommands rooms;
            private readonly FrontendSceneCoordinator scenes;
            private readonly UnityHomeApplicationHost fallback = new();

            public NetworkHomeApplicationHost(
                RoomUiCommands rooms,
                FrontendSceneCoordinator scenes)
            {
                this.rooms = rooms;
                this.scenes = scenes;
            }

            public void Quit() => fallback.Quit();

            public void OpenHome() => scenes.OpenHome();

            public void OpenRoomBrowser()
            {
                rooms.RefreshAsync(CancellationToken.None)
                    .Forget(exception => Debug.LogException(exception));
                scenes.OpenRoomBrowser();
            }

            public void OpenLobby() => fallback.OpenLobby();
        }
    }

    /// <summary>
    /// Keeps the two frontend screens loaded and swaps their scene roots.
    /// Gameplay scenes remain owned by Fusion and unload this pair normally.
    /// </summary>
    internal sealed class FrontendSceneCoordinator : IStartable, IDisposable
    {
        private const string Home = UnityHomeApplicationHost.HomeSceneName;
        private const string Room = UnityHomeApplicationHost.RoomBrowserSceneName;

        private string desiredScene;
        private AsyncOperation homeLoad;
        private AsyncOperation roomLoad;
        private double switchStartedAt = -1d;
        private readonly EventSystem sharedEventSystem;

        public FrontendSceneCoordinator(EventSystem sharedEventSystem)
        {
            this.sharedEventSystem = sharedEventSystem;
        }

        public void Start()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;

            var active = SceneManager.GetActiveScene();
            if (!IsFrontend(active))
            {
                sharedEventSystem.gameObject.SetActive(false);
                return;
            }

            DisableDuplicateEventSystems(active);
            ActivateSharedEventSystem();
            desiredScene = active.name;
            SetRootsActive(active, true);
            EnsureCounterpartLoaded(active.name);
        }

        public void Dispose()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }

        public void OpenHome() => Open(Home);

        public void OpenRoomBrowser() => Open(Room);

        private void Open(string sceneName)
        {
            desiredScene = sceneName;
            switchStartedAt = Time.realtimeSinceStartupAsDouble;
            Debug.Log(
                $"[SceneTiming] Frontend switch requested: " +
                $"{SceneManager.GetActiveScene().name} -> {sceneName}.");

            if (!TryShow(sceneName))
            {
                EnsureLoaded(sceneName);
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!IsFrontend(scene))
            {
                SetRootsActive(GetLoadedScene(Home), false);
                SetRootsActive(GetLoadedScene(Room), false);
                sharedEventSystem.gameObject.SetActive(false);
                return;
            }

            DisableDuplicateEventSystems(scene);
            ActivateSharedEventSystem();
            ClearLoad(scene.name);

            var counterpart = GetLoadedScene(scene.name == Home ? Room : Home);
            if (!counterpart.IsValid())
            {
                // This frontend was entered from a gameplay/session scene.
                desiredScene = scene.name;
            }

            if (!TryShow(desiredScene))
            {
                SetRootsActive(scene, false);
            }

            EnsureCounterpartLoaded(desiredScene);
        }

        private void OnSceneUnloaded(Scene scene)
        {
            if (!IsFrontend(scene))
            {
                return;
            }

            ClearLoad(scene.name);
        }

        private bool TryShow(string sceneName)
        {
            var target = GetLoadedScene(sceneName);
            if (!target.IsValid())
            {
                return false;
            }

            var other = GetLoadedScene(sceneName == Home ? Room : Home);
            SetRootsActive(target, true);
            SceneManager.SetActiveScene(target);
            SetRootsActive(other, false);
            ActivateSharedEventSystem();

            if (switchStartedAt >= 0d)
            {
                Debug.Log(
                    $"[SceneTiming] Frontend switch completed: scene={sceneName}, " +
                    $"elapsed={Time.realtimeSinceStartupAsDouble - switchStartedAt:F3}s.");
                switchStartedAt = -1d;
            }

            EnsureCounterpartLoaded(sceneName);
            return true;
        }

        private void EnsureCounterpartLoaded(string visibleScene)
        {
            if (string.Equals(visibleScene, Home, StringComparison.Ordinal))
            {
                EnsureLoaded(Room);
            }
            else if (string.Equals(visibleScene, Room, StringComparison.Ordinal))
            {
                EnsureLoaded(Home);
            }
        }

        private void EnsureLoaded(string sceneName)
        {
            if (GetLoadedScene(sceneName).IsValid() || GetLoad(sceneName) != null)
            {
                return;
            }

            var operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (operation == null)
            {
                Debug.LogError($"[SceneTiming] Could not load frontend scene '{sceneName}'.");
                return;
            }

            operation.priority = 100;
            SetLoad(sceneName, operation);
            Debug.Log($"[SceneTiming] Frontend additive load requested: scene={sceneName}.");
        }

        private AsyncOperation GetLoad(string sceneName) =>
            string.Equals(sceneName, Home, StringComparison.Ordinal)
                ? homeLoad
                : roomLoad;

        private void SetLoad(string sceneName, AsyncOperation operation)
        {
            if (string.Equals(sceneName, Home, StringComparison.Ordinal))
            {
                homeLoad = operation;
            }
            else
            {
                roomLoad = operation;
            }
        }

        private void ClearLoad(string sceneName) => SetLoad(sceneName, null);

        private static Scene GetLoadedScene(string sceneName)
        {
            var scene = SceneManager.GetSceneByName(sceneName);
            return scene.IsValid() && scene.isLoaded ? scene : default;
        }

        private static bool IsFrontend(Scene scene) =>
            scene.IsValid() &&
            (string.Equals(scene.name, Home, StringComparison.Ordinal) ||
             string.Equals(scene.name, Room, StringComparison.Ordinal));

        private static void SetRootsActive(Scene scene, bool active)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            foreach (var root in scene.GetRootGameObjects())
            {
                root.SetActive(active);
            }
        }

        private void DisableDuplicateEventSystems(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var eventSystem in root.GetComponentsInChildren<EventSystem>(true))
                {
                    if (eventSystem == sharedEventSystem)
                    {
                        continue;
                    }

                    eventSystem.enabled = false;
                    var inputModule = eventSystem.GetComponent<BaseInputModule>();
                    if (inputModule != null)
                    {
                        inputModule.enabled = false;
                    }
                }
            }
        }

        private void ActivateSharedEventSystem()
        {
            sharedEventSystem.gameObject.SetActive(true);
            sharedEventSystem.enabled = true;
            EventSystem.current = sharedEventSystem;
        }
    }
}
