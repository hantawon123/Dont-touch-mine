using Game.Training.HideSeek;
using Unity.InferenceEngine;
using Unity.MLAgents.Policies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Training.EditorTools
{
    /// <summary>
    /// Tools > AI > Hide-Seek Viewer: choose the hider and seeker brains (rule, random or a trained model), a seed
    /// and a speed, then play one round at a time in the Mansion_HideSeek scene with HideSeekViewer. The choices
    /// are applied to the scene in memory only; the scene file is not changed (it is reopened when you stop).
    /// </summary>
    public sealed class HideSeekViewerWindow : EditorWindow
    {
        private const string ScenePath = "Assets/_Game/Content/Training/Local/Mansion_HideSeek.unity";
        private enum Brain { Rule, Random, Model }

        private Brain hiderBrain = Brain.Model;
        private Brain seekerBrain = Brain.Rule;
        private ModelAsset hiderModel;
        private ModelAsset seekerModel;
        private int seed = 777001;
        private float speed = 1f;

        [MenuItem("Tools/AI/Hide-Seek Viewer")]
        public static void Open() => GetWindow<HideSeekViewerWindow>("Hide-Seek Viewer");

        private void OnGUI()
        {
            EditorGUILayout.LabelField("One round at a time, same simulation as training and evaluation.", EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space();
            hiderBrain = (Brain)EditorGUILayout.EnumPopup("Hider brain", hiderBrain);
            if (hiderBrain == Brain.Model)
            {
                hiderModel = (ModelAsset)EditorGUILayout.ObjectField("  HideSelect model", hiderModel, typeof(ModelAsset), false);
            }

            seekerBrain = (Brain)EditorGUILayout.EnumPopup("Seeker brain", seekerBrain);
            if (seekerBrain == Brain.Model)
            {
                seekerModel = (ModelAsset)EditorGUILayout.ObjectField("  SeekSelect model", seekerModel, typeof(ModelAsset), false);
            }

            seed = EditorGUILayout.IntField("First seed", seed);
            speed = EditorGUILayout.Slider("Speed", speed, 0.25f, 8f);
            EditorGUILayout.HelpBox("Seeds 777001+ and 888001+ are the evaluation rounds, so a seed shows the same round the evaluation table counted.", MessageType.None);

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
            if ((hiderBrain == Brain.Model && hiderModel == null) || (seekerBrain == Brain.Model && seekerModel == null))
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

            var arena = FindFirstObjectByType<HideSeekArena>();
            if (arena == null)
            {
                ShowNotification(new GUIContent("Build the scene first: Tools > AI > Build Hide-Seek Training."));
                return;
            }

            Apply<HideSeekHideAgent>(hiderBrain, hiderModel);
            Apply<HideSeekSeekAgent>(seekerBrain, seekerModel);

            // The viewer object is created after entering Play (see ViewerLauncher), so the scene file never changes.
            SessionState.SetBool(ViewerLauncher.PendingKey, true);
            SessionState.SetInt(ViewerLauncher.SeedKey, seed);
            SessionState.SetFloat(ViewerLauncher.SpeedKey, speed);
            EditorApplication.isPlaying = true;
        }

        private static void Apply<T>(Brain brain, ModelAsset model) where T : HideSeekAgentBase
        {
            var field = typeof(HideSeekAgentBase).GetField("heuristicMode",
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
    internal static class ViewerLauncher
    {
        internal const string PendingKey = "HideSeekViewer.Pending";
        internal const string ActiveKey = "HideSeekViewer.Active";
        internal const string SeedKey = "HideSeekViewer.Seed";
        internal const string SpeedKey = "HideSeekViewer.Speed";
        private const string ScenePath = "Assets/_Game/Content/Training/Local/Mansion_HideSeek.unity";
        private const string CharacterPrefabPath = "Assets/_Game/Content/Prefabs/PlayerCharacter.prefab";

        static ViewerLauncher()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        private static void OnPlayMode(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PendingKey, false))
            {
                SessionState.SetBool(PendingKey, false);
                SessionState.SetBool(ActiveKey, true);
                var arena = Object.FindFirstObjectByType<HideSeekArena>();
                if (arena == null)
                {
                    return;
                }

                var go = new GameObject("HideSeek_Viewer");
                go.SetActive(false);
                var viewer = go.AddComponent<HideSeekViewer>();
                viewer.Configure(arena, AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefabPath),
                    SessionState.GetInt(SeedKey, 777001), SessionState.GetFloat(SpeedKey, 1f));
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
