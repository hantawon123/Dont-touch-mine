using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Editor
{
    public static class PlaygroundLobbyMapSetup
    {
        private const string MenuPath = "Game/Lobby/Copy Playground Environment";
        private const string LobbyScenePath = "Assets/_Game/Content/Scenes/Lobby.unity";
        private const string PlaygroundScenePath =
            "Assets/_Game/Content/Scenes/Playground.unity";
        private const string MapRootName = "LobbyPlaygroundEnvironment";

        private static readonly string[] EnvironmentRootNames =
        {
            "House4_С11",
            "Floor1",
            "Floor2",
            "Plane",
            "Shredder",
            "Directional Light",
            "Light1",
            "Light2",
            "Light3",
            "Light4",
            "FX",
            "StaticLightingSky",
        };

        [MenuItem(MenuPath)]
        public static void CopyPlaygroundEnvironment()
        {
            var lobby = EditorSceneManager.OpenScene(
                LobbyScenePath,
                OpenSceneMode.Single);
            var playground = EditorSceneManager.OpenScene(
                PlaygroundScenePath,
                OpenSceneMode.Additive);

            try
            {
                ReplaceGeneratedMap(lobby, playground);
                // Lobby points stay outdoors; Playground points are inside the match house.
                DisableLobbyPlaceholder(lobby, "Directional Light");

                EditorSceneManager.MarkSceneDirty(lobby);
                EditorSceneManager.SaveScene(lobby);
            }
            finally
            {
                EditorSceneManager.CloseScene(playground, true);
            }

            Selection.activeGameObject = FindRoot(lobby, MapRootName);
            SceneView.lastActiveSceneView?.FrameSelected();
            Debug.Log("[Lobby] Playground environment copied into Lobby.");
        }

        private static void ReplaceGeneratedMap(Scene lobby, Scene playground)
        {
            var previous = FindRoot(lobby, MapRootName);
            if (previous != null)
            {
                UnityEngine.Object.DestroyImmediate(previous);
            }

            var mapRoot = new GameObject(MapRootName);
            SceneManager.MoveGameObjectToScene(mapRoot, lobby);

            foreach (var rootName in EnvironmentRootNames)
            {
                var source = FindRoot(playground, rootName);
                if (source == null)
                {
                    throw new InvalidOperationException(
                        $"Playground is missing root object '{rootName}'.");
                }

                var clone = UnityEngine.Object.Instantiate(source);
                clone.name = rootName;
                SceneManager.MoveGameObjectToScene(clone, lobby);
                clone.transform.SetParent(mapRoot.transform, true);
            }

            if (mapRoot.GetComponentsInChildren<Renderer>(true).Length == 0 ||
                mapRoot.GetComponentsInChildren<Collider>(true).Length == 0)
            {
                throw new InvalidOperationException(
                    "Copied Playground environment has no renderers or colliders.");
            }
        }

        private static void DisableLobbyPlaceholder(Scene scene, string objectName)
        {
            var root = FindRoot(scene, objectName);
            if (root != null)
            {
                root.SetActive(false);
            }
        }

        private static GameObject FindRoot(Scene scene, string objectName)
        {
            return Array.Find(
                scene.GetRootGameObjects(),
                root => string.Equals(
                    root.name,
                    objectName,
                    StringComparison.Ordinal));
        }
    }
}
