using System.Collections.Generic;
using Game.Bootstrap;
using Game.Client.Home;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Tests.EditMode
{
    public sealed class HomeSceneIntegrationTests
    {
        private const string HomeScenePath = "Assets/_Game/Content/Scenes/Home.unity";

        [Test]
        public void HomeScene_WiresMenuViewToGameSystems()
        {
            var buildScenes = EditorBuildSettings.scenes;
            Assert.That(buildScenes, Is.Not.Empty);
            Assert.That(buildScenes[0].enabled, Is.True);
            Assert.That(buildScenes[0].path, Is.EqualTo(HomeScenePath));

            var scene = SceneManager.GetSceneByPath(HomeScenePath);
            var openedForTest = !scene.isLoaded;

            if (openedForTest)
            {
                scene = EditorSceneManager.OpenScene(HomeScenePath, OpenSceneMode.Additive);
            }

            try
            {
                var scopes = CollectComponents<HomeLifetimeScope>(scene);
                var views = CollectComponents<HomeMenuView>(scene);

                Assert.That(scopes, Has.Count.EqualTo(1));
                Assert.That(views, Has.Count.EqualTo(1));

                var serializedScope = new SerializedObject(scopes[0]);
                Assert.That(
                    serializedScope.FindProperty("homeMenuView").objectReferenceValue,
                    Is.SameAs(views[0]));
                Assert.That(serializedScope.FindProperty("autoRun").boolValue, Is.True);
                Assert.That(
                    serializedScope.FindProperty("parentReference.TypeName").stringValue,
                    Is.EqualTo(typeof(ProjectLifetimeScope).FullName));
            }
            finally
            {
                if (openedForTest)
                {
                    EditorSceneManager.CloseScene(scene, removeScene: true);
                }
            }
        }

        private static List<T> CollectComponents<T>(Scene scene) where T : Component
        {
            var components = new List<T>();

            foreach (var rootObject in scene.GetRootGameObjects())
            {
                components.AddRange(rootObject.GetComponentsInChildren<T>(includeInactive: true));
            }

            return components;
        }
    }
}
