using System.Text;
using Game.Training.HideSeek;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Training.EditorTools
{
    /// <summary>
    /// Tools > AI > Build Hide-Seek Training: a mansion copy (same bot-sized NavMesh as the sandbox, so the hiding
    /// spot bank matches) with a HideSeekArena and N hider / seeker agent pairs for asymmetric self-play
    /// (docs/planning/hide-seek-v1.md). No Fusion bot: bodies are simulated, so the time scale works.
    /// </summary>
    public static class HideSeekTrainingBuilder
    {
        private const string ScenePath = "Assets/_Game/Content/Training/Local/Mansion_HideSeek.unity";
        private const string NavMeshPath = "Assets/_Game/Content/Training/Local/Mansion_HideSeek_NavMesh.asset";
        private const string BankPath = "Assets/_Game/Content/Training/Local/Mansion_HidingSpotBank.asset";
        private const int Matches = 32;

        [MenuItem("Tools/AI/Build Hide-Seek Training")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var bank = AssetDatabase.LoadAssetAtPath<HidingSpotBank>(BankPath);
            if (bank == null)
            {
                Debug.LogError($"[HideSeek Builder] {BankPath} not found. Open the bot sandbox and run Tools > AI > Build Hiding Spot Bank first.");
                return;
            }

            var log = new StringBuilder("[HideSeek Builder]\n");
            if (!MansionBotSandboxBuilder.TryPrepareMansionCopy(ScenePath, NavMeshPath, log, out var scene, out _, out _,
                    new Vector2(MansionBotSandboxBuilder.SandboxAgentRadius, MansionBotSandboxBuilder.SandboxAgentHeight)))
            {
                return;
            }

            // Load again after the scene switch: opening the copy unloads assets nothing referenced.
            bank = AssetDatabase.LoadAssetAtPath<HidingSpotBank>(BankPath);
            var root = new GameObject("HideSeek_Arena");
            var arena = root.AddComponent<HideSeekArena>();
            var hide = new HideSeekHideAgent[Matches];
            var seek = new HideSeekSeekAgent[Matches];
            for (var i = 0; i < Matches; i++)
            {
                hide[i] = AddAgent<HideSeekHideAgent>(root.transform, $"Hider_{i}", "HideSelect", 0,
                    HideSeekArena.HideObservationSize, HideSeekMatch.HideActionCount);
                seek[i] = AddAgent<HideSeekSeekAgent>(root.transform, $"Seeker_{i}", "SeekSelect", 1,
                    HideSeekArena.SeekObservationSize, HideSeekMatch.SeekActionCount);
            }

            var so = new SerializedObject(arena);
            so.FindProperty("bank").objectReferenceValue = bank;
            var hideProp = so.FindProperty("hideAgents");
            var seekProp = so.FindProperty("seekAgents");
            hideProp.arraySize = Matches;
            seekProp.arraySize = Matches;
            for (var i = 0; i < Matches; i++)
            {
                hideProp.GetArrayElementAtIndex(i).objectReferenceValue = hide[i];
                seekProp.GetArrayElementAtIndex(i).objectReferenceValue = seek[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            log.AppendLine($"arena with {Matches} hider/seeker pairs (HideSelect team 0 obs {HideSeekArena.HideObservationSize} act {HideSeekMatch.HideActionCount}; SeekSelect team 1 obs {HideSeekArena.SeekObservationSize} act {HideSeekMatch.SeekActionCount})");
            log.AppendLine($"bank {BankPath}: {bank.Spots.Count} spots");
            log.AppendLine($"saved {ScenePath}. Behavior Type Default: trains with mlagents-learn, runs the heuristic (rule) without it.");
            Debug.Log(log.ToString());
        }

        private static T AddAgent<T>(Transform parent, string name, string behaviorName, int team, int obs, int actions)
            where T : HideSeekAgentBase
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var behavior = go.AddComponent<BehaviorParameters>();
            behavior.BehaviorName = behaviorName;
            behavior.TeamId = team;
            behavior.BrainParameters.VectorObservationSize = obs;
            behavior.BrainParameters.NumStackedVectorObservations = 1;
            behavior.BrainParameters.ActionSpec = ActionSpec.MakeDiscrete(actions);
            behavior.BehaviorType = BehaviorType.Default;
            var agent = go.AddComponent<T>();
            agent.MaxStep = 0; // rounds end themselves (150 s)
            EditorUtility.SetDirty(behavior);
            return agent;
        }
    }
}
