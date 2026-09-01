using System;
using Game.Client.Home;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace Game.Bootstrap
{
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
