using Game.Training.HideSeek;
using Game.Training.Thief;
using Unity.InferenceEngine;
using Unity.MLAgents.Policies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Training.EditorTools
{
    /// <summary>
    /// Tools > AI > Thief Viewer: choose the thief brains (rule, random or the two trained models), how players hid
    /// their props, a seed and a speed, then play one round at a time in the Mansion_Thief scene with ThiefViewer. The choices
    /// are applied to the scene in memory only; the scene file is not changed (it is reopened when you stop).
    /// </summary>
    public sealed class ThiefViewerWindow : EditorWindow
    {
        private const string ScenePath = "Assets/_Game/Content/Training/Local/Mansion_Thief.unity";
        private enum Brain { Rule, Random, Model }

        private Brain thiefBrain = Brain.Model;
        private ModelAsset seekModel;
        private ModelAsset hideModel;
        private ThiefArena.PlayerHiding hiding = ThiefArena.PlayerHiding.Rule;
        private int seed = 777001;
        private float speed = 1f;

        [MenuItem("Tools/AI/Thief Viewer")]
        public static void Open() => GetWindow<ThiefViewerWindow>("Thief Viewer");

        /// <summary>All 16 rounds of the arena at once on the map (rule thief), for the presentation.</summary>
        [MenuItem("Tools/AI/Thief Overlay Viewer (16 rounds)")]
        public static void PlayOverlay()
        {
            if (EditorApplication.isPlaying) return;
            if (EditorSceneManager.GetActiveScene().path != ScenePath || EditorSceneManager.GetActiveScene().isDirty)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            Apply<ThiefSeekAgent>(Brain.Rule, null);
            Apply<ThiefHideAgent>(Brain.Rule, null);
            SessionState.SetBool(ThiefViewerLauncher.OverlayPendingKey, true);
            EditorApplication.isPlaying = true;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("One round at a time, same simulation as training and evaluation.", EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space();
            thiefBrain = (Brain)EditorGUILayout.EnumPopup("Thief brain", thiefBrain);
            if (thiefBrain == Brain.Model)
            {
                seekModel = (ModelAsset)EditorGUILayout.ObjectField("  ThiefSeek model", seekModel, typeof(ModelAsset), false);
                hideModel = (ModelAsset)EditorGUILayout.ObjectField("  ThiefHide model", hideModel, typeof(ModelAsset), false);
            }

            hiding = (ThiefArena.PlayerHiding)EditorGUILayout.EnumPopup("Players hid with", hiding);
            seed = EditorGUILayout.IntField("First seed", seed);
            speed = EditorGUILayout.Slider("Speed", speed, 0.25f, 8f);
            EditorGUILayout.HelpBox("Seeds 777001+ and 888001+ are the evaluation rounds of the thief tables.", MessageType.None);

            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                if (GUILayout.Button("Play", GUILayout.Height(30)))
                {
                    Play();
                }
            }

            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
            {
                if (GUILayout.Button("Stop"))
                {
                    EditorApplication.isPlaying = false;
                }
            }
        }

        private void Play()
        {
            if (thiefBrain == Brain.Model && (seekModel == null || hideModel == null))
            {
                ShowNotification(new GUIContent("Pick the model asset(s) first."));
                return;
            }

            if (EditorSceneManager.GetActiveScene().path != ScenePath || EditorSceneManager.GetActiveScene().isDirty)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    return;
                }

                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var arena = FindFirstObjectByType<ThiefArena>();
            if (arena == null)
            {
                ShowNotification(new GUIContent("Build the scene first: Tools > AI > Build Thief Training."));
                return;
            }

            Apply<ThiefSeekAgent>(thiefBrain, seekModel);
            Apply<ThiefHideAgent>(thiefBrain, hideModel);
            typeof(ThiefArena).GetField("playerHiding", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(arena, hiding);

            // The viewer object is created after entering Play (see ViewerLauncher), so the scene file never changes.
            SessionState.SetBool(ThiefViewerLauncher.PendingKey, true);
            SessionState.SetInt(ThiefViewerLauncher.SeedKey, seed);
            SessionState.SetFloat(ThiefViewerLauncher.SpeedKey, speed);
            EditorApplication.isPlaying = true;
        }

        private static void Apply<T>(Brain brain, ModelAsset model) where T : ThiefAgentBase
        {
            var field = typeof(ThiefAgentBase).GetField("heuristicMode",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            foreach (var agent in FindObjectsByType<T>(FindObjectsSortMode.None))
            {
                var behavior = agent.GetComponent<BehaviorParameters>();
                if (brain == Brain.Model)
                {
                    behavior.Model = model;
                    behavior.BehaviorType = BehaviorType.InferenceOnly;
                    behavior.DeterministicInference = true;
                }
                else
                {
                    field?.SetValue(agent, brain == Brain.Rule ? HideSeekHeuristic.Rule : HideSeekHeuristic.Random);
                    behavior.BehaviorType = BehaviorType.HeuristicOnly;
                }
            }
        }
    }

    /// <summary>Creates the HideSeekViewer once Play starts and reopens the clean scene after Stop.</summary>
    [InitializeOnLoad]
    internal static class ThiefViewerLauncher
    {
        internal const string PendingKey = "ThiefViewer.Pending";
        internal const string OverlayPendingKey = "ThiefViewer.OverlayPending";
        internal const string ActiveKey = "ThiefViewer.Active";
        internal const string SeedKey = "ThiefViewer.Seed";
        internal const string SpeedKey = "ThiefViewer.Speed";
        internal const string SlowFromKey = "ThiefViewer.SlowFrom";
        private const string ScenePath = "Assets/_Game/Content/Training/Local/Mansion_Thief.unity";
        private const string CharacterPrefabPath = "Assets/_Game/Content/Prefabs/PlayerCharacter.prefab";

        static ThiefViewerLauncher()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        private static void OnPlayMode(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(OverlayPendingKey, false))
            {
                SessionState.SetBool(OverlayPendingKey, false);
                SessionState.SetBool(ActiveKey, true);
                var overlayArena = Object.FindFirstObjectByType<ThiefArena>();
                if (overlayArena == null) return;
                var holder = new GameObject("Thief_OverlayViewer");
                holder.SetActive(false);
                holder.AddComponent<ThiefOverlayViewer>().Configure(overlayArena, 4f);
                holder.SetActive(true);
            }
            else if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PendingKey, false))
            {
                SessionState.SetBool(PendingKey, false);
                SessionState.SetBool(ActiveKey, true);
                var arena = Object.FindFirstObjectByType<ThiefArena>();
                if (arena == null)
                {
                    return;
                }

                var go = new GameObject("Thief_Viewer");
                go.SetActive(false);
                var viewer = go.AddComponent<ThiefViewer>();
                viewer.Configure(arena, AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefabPath),
                    SessionState.GetInt(SeedKey, 777001), SessionState.GetFloat(SpeedKey, 1f), SessionState.GetFloat(SlowFromKey, 0f));
                SessionState.SetFloat(SlowFromKey, 0f);
                go.SetActive(true);
            }
            else if (change == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(ActiveKey, false))
            {
                SessionState.SetBool(ActiveKey, false);
                // The brain choices were applied in memory; reopen the scene file to drop them.
                if (EditorSceneManager.GetActiveScene().path == ScenePath && !EditorSceneManager.GetActiveScene().isDirty)
                {
                    EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                }
            }
        }
    }
}
