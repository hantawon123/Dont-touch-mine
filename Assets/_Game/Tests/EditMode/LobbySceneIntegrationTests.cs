using System.Collections.Generic;
using Game.Bootstrap;
using Game.Client.Lobby;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Guards the wiring that <c>Game &gt; Lobby &gt; Build HUD Layout</c> writes
    /// into the lobby scene.
    /// </summary>
    /// <remarks>
    /// That menu is an authoring step: it edits the scene, and the scene is what
    /// ships. Changing the layout code without running it, or running it without
    /// saving, leaves the scene describing the old screen while the code expects
    /// the new one — and the first anyone hears of it is a teammate pulling a
    /// lobby whose buttons do nothing. These tests fail on the commit instead.
    /// </remarks>
    public sealed class LobbySceneIntegrationTests
    {
        private const string LobbyScenePath =
            "Assets/_Game/Content/Scenes/Lobby.unity";

        private const string PausePanelName = "PauseMenuPanel";

        /// <summary>
        /// Every entry the Esc menu offers, by the serialized field that reaches
        /// it.
        /// </summary>
        private static readonly string[] PauseMenuButtonFields =
        {
            "startButton",
            "playSettingsButton",
            "settingsButton",
            "keyGuideButton",
            "leaveButton",
            "resumeButton",
        };

        /// <summary>
        /// Buttons that used to sit in the corners of the always-on HUD, where a
        /// captured cursor could not reach them.
        /// </summary>
        private static readonly string[] RetiredCornerButtons =
        {
            "StartButton",
            "LeaveButton",
            "SettingsButton",
            "PlaySettingsButton",
            "KeyGuideButton",
        };

        [Test]
        public void PauseMenu_ReachesEveryEntryOnItsOwnPanel()
        {
            WithLobbyScene(scene =>
            {
                var view = SingleComponent<LobbyPauseMenuView>(scene);
                var serialized = new SerializedObject(view);

                var panel = serialized.FindProperty("panel").objectReferenceValue
                    as GameObject;
                Assert.That(
                    panel,
                    Is.Not.Null,
                    "PauseMenuPanel is not assigned on LobbyPauseMenuView.");
                Assert.That(panel.name, Is.EqualTo(PausePanelName));

                foreach (var field in PauseMenuButtonFields)
                {
                    var button = serialized.FindProperty(field).objectReferenceValue
                        as Component;

                    Assert.That(
                        button,
                        Is.Not.Null,
                        $"LobbyPauseMenuView.{field} is not assigned. Run " +
                        "Game > Lobby > Build HUD Layout and save the scene.");
                    Assert.That(
                        button.transform.parent,
                        Is.SameAs(panel.transform),
                        $"LobbyPauseMenuView.{field} points outside the pause " +
                        "panel.");
                }
            });
        }

        /// <remarks>
        /// Both screens are opened from the menu now, so their own open buttons
        /// have to be the ones sitting in it. A stale scene would leave them
        /// pointing at corner buttons that no longer exist, which reads as a
        /// menu entry that does nothing.
        /// </remarks>
        [Test]
        public void SubScreens_TakeTheirOpenButtonFromThePauseMenu()
        {
            WithLobbyScene(scene =>
            {
                var panel = FindPausePanel(scene);

                AssertOpensFromPauseMenu(
                    SingleComponent<KeyGuideView>(scene), panel);
                AssertOpensFromPauseMenu(
                    SingleComponent<PlaySettingsView>(scene), panel);
            });
        }

        [Test]
        public void Hud_KeepsNoButtonACapturedCursorCannotReach()
        {
            WithLobbyScene(scene =>
            {
                var hud = SingleComponent<LobbyHudView>(scene);

                foreach (var name in RetiredCornerButtons)
                {
                    Assert.That(
                        hud.transform.Find(name),
                        Is.Null,
                        $"'{name}' is still on the HUD root. It moved into the " +
                        "Esc menu, so the old slot should be gone.");
                }
            });
        }

        [Test]
        public void SceneFlow_UsesTheLobbySpawnConfiguration()
        {
            WithLobbyScene(scene =>
            {
                var scope = SingleComponent<LobbyLifetimeScope>(scene);
                var assigned = new SerializedObject(scope)
                    .FindProperty("sceneConfiguration")
                    .objectReferenceValue;

                Assert.That(assigned,
                    Is.SameAs(SingleComponent<MatchSceneConfiguration>(scene)),
                    "LobbyLifetimeScope must not discover spawn points across merged Fusion scenes.");
            });
        }

        private static void AssertOpensFromPauseMenu(
            Component view,
            Transform panel)
        {
            var openButton = new SerializedObject(view)
                .FindProperty("openButton")
                .objectReferenceValue as Component;

            Assert.That(
                openButton,
                Is.Not.Null,
                $"{view.GetType().Name}.openButton is not assigned.");
            Assert.That(
                openButton.transform.parent,
                Is.SameAs(panel),
                $"{view.GetType().Name} still opens from outside the Esc menu.");
        }

        private static Transform FindPausePanel(Scene scene)
        {
            var hud = SingleComponent<LobbyHudView>(scene);
            var panel = hud.transform.Find(PausePanelName);

            Assert.That(
                panel,
                Is.Not.Null,
                $"'{PausePanelName}' is missing from the lobby HUD.");

            return panel;
        }

        /// <summary>
        /// Runs against the lobby scene, opening it only if it is not already
        /// the one on screen and putting it back the way it was found.
        /// </summary>
        private static void WithLobbyScene(System.Action<Scene> assertions)
        {
            var scene = SceneManager.GetSceneByPath(LobbyScenePath);
            var openedForTest = !scene.isLoaded;

            if (openedForTest)
            {
                scene = EditorSceneManager.OpenScene(
                    LobbyScenePath, OpenSceneMode.Additive);
            }

            try
            {
                assertions(scene);
            }
            finally
            {
                if (openedForTest)
                {
                    EditorSceneManager.CloseScene(scene, removeScene: true);
                }
            }
        }

        private static T SingleComponent<T>(Scene scene) where T : Component
        {
            var found = new List<T>();

            foreach (var rootObject in scene.GetRootGameObjects())
            {
                found.AddRange(
                    rootObject.GetComponentsInChildren<T>(includeInactive: true));
            }

            Assert.That(
                found,
                Has.Count.EqualTo(1),
                $"Expected exactly one {typeof(T).Name} in the lobby scene.");

            return found[0];
        }
    }
}
