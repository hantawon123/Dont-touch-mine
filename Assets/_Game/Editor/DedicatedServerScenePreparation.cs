using System.Linq;
using Game.Bootstrap;
using Game.Client.Character;
using Game.Client.Home;
using Game.Client.Match;
using Game.Client.Settings;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace Game.Editor
{
    // DI does not prevent scene MonoBehaviours from running Awake/OnEnable.
    // Change only the build's scene copy, before stripped fonts reach UI code.
    public sealed class DedicatedServerScenePreparation : IProcessSceneWithReport
    {
        public int callbackOrder => 200;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            var developmentPlay = report == null && EditorApplication.isPlayingOrWillChangePlaymode &&
                Game.Network.Session.EditorDevelopmentSession.IsServer;
            var serverBuild = report != null && report.summary.platformGroup == BuildTargetGroup.Standalone &&
                EditorUserBuildSettings.standaloneBuildSubtarget == StandaloneBuildSubtarget.Server;
            if (!developmentPlay && !serverBuild) return;

            var roots = scene.GetRootGameObjects();
            var removed = 0;
            foreach (var component in roots.SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true)))
            {
                // These factories construct canvases in Awake and therefore are not
                // covered by Canvas deactivation. Preserve GameObjects and shared scopes. The three
                // frontend-only scopes are removed; gameplay scopes remain active.
                if (component is HomeMenuView || component is CharacterClosetView ||
                    component is ResultView || component is SettingsView ||
                    component is MatchChatBubbleView ||
                    component is RoomBrowserLifetimeScope || component is SettingsLifetimeScope ||
                    component is CharacterClosetLifetimeScope)
                {
                    Object.DestroyImmediate(component);
                    removed++;
                }
            }
            // Signage video is presentation-only. Strip the component before Awake
            // so headless/development servers never open a media decoder at all.
            var videos = roots.SelectMany(root =>
                root.GetComponentsInChildren<UnityEngine.Video.VideoPlayer>(true)).ToArray();
            foreach (var video in videos) Object.DestroyImmediate(video);
            var canvases = roots.SelectMany(root => root.GetComponentsInChildren<Canvas>(true)).ToArray();
            foreach (var canvas in canvases)
            {
                // A future mixed UI/gameplay hierarchy must be separated explicitly.
                if (canvas.GetComponentInChildren<Collider>(true) != null ||
                    canvas.GetComponentInChildren<Rigidbody>(true) != null ||
                    canvas.GetComponentInChildren<LifetimeScope>(true) != null)
                    throw new BuildFailedException($"Server UI contains gameplay components: {scene.name}/{canvas.name}");
                canvas.gameObject.SetActive(false);
            }

            Debug.Log($"[ServerBuild] {scene.name}: disabled {canvases.Length} canvases, removed {removed} UI components and {videos.Length} video players.");
        }
    }
}
