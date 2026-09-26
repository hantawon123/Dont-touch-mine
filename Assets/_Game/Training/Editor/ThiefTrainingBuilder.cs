using System.Text;
using Game.Training.Thief;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Training.EditorTools
{
    /// <summary>
    /// Tools > AI > Build Thief Training: mansion copy (bot-sized NavMesh, same as the sandbox, so the hiding spot bank
    /// matches) with a ThiefArena and N rounds, each with one ThiefSeek and one ThiefHide agent on the same thief
    /// (docs/planning/thief-npc-v2.md). Players are rule bots inside the arena.
    /// </summary>
    public static class ThiefTrainingBuilder
    {
        private const string ScenePath = "Assets/_Game/Content/Training/Local/Mansion_Thief.unity";
        private const string NavMeshPath = "Assets/_Game/Content/Training/Local/Mansion_Thief_NavMesh.asset";
        private const string BankPath = "Assets/_Game/Content/Training/Local/Mansion_HidingSpotBank.asset";
        private const int Matches = 16;

        [MenuItem("Tools/AI/Build Thief Training")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<HidingSpotBank>(BankPath) == null)
            {
                Debug.LogError($"[Thief Builder] {BankPath} not found. Build the hiding spot bank first.");
                return;
            }

            var log = new StringBuilder("[Thief Builder]\n");
            if (!MansionBotSandboxBuilder.TryPrepareMansionCopy(ScenePath, NavMeshPath, log, out var scene, out _, out _,
                    new Vector2(MansionBotSandboxBuilder.SandboxAgentRadius, MansionBotSandboxBuilder.SandboxAgentHeight)))
            {
                return;
            }

            var bank = AssetDatabase.LoadAssetAtPath<HidingSpotBank>(BankPath); // after the scene switch
            var root = new GameObject("Thief_Arena");
            var arena = root.AddComponent<ThiefArena>();
            var seek = new ThiefSeekAgent[Matches];
            var hide = new ThiefHideAgent[Matches];
            for (var i = 0; i < Matches; i++)
            {
                seek[i] = AddAgent<ThiefSeekAgent>(root.transform, $"ThiefSeek_{i}", "ThiefSeek", ThiefArena.SeekObservationSize, ThiefMatch.SeekActionCount);
                hide[i] = AddAgent<ThiefHideAgent>(root.transform, $"ThiefHide_{i}", "ThiefHide", ThiefArena.HideObservationSize, ThiefMatch.HideActionCount);
            }

            var so = new SerializedObject(arena);
            so.FindProperty("bank").objectReferenceValue = bank;
            var seekProp = so.FindProperty("seekAgents");
            var hideProp = so.FindProperty("hideAgents");
            seekProp.arraySize = Matches;
            hideProp.arraySize = Matches;
            for (var i = 0; i < Matches; i++)
            {
                seekProp.GetArrayElementAtIndex(i).objectReferenceValue = seek[i];
                hideProp.GetArrayElementAtIndex(i).objectReferenceValue = hide[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            log.AppendLine($"arena with {Matches} rounds x {ThiefMatch.Players} rule players + thief (ThiefSeek obs {ThiefArena.SeekObservationSize} act {ThiefMatch.SeekActionCount}; ThiefHide obs {ThiefArena.HideObservationSize} act {ThiefMatch.HideActionCount})");
            log.AppendLine($"saved {ScenePath}");
            Debug.Log(log.ToString());
        }

        private static T AddAgent<T>(Transform parent, string name, string behaviorName, int obs, int actions) where T : ThiefAgentBase
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var behavior = go.AddComponent<BehaviorParameters>();
            behavior.BehaviorName = behaviorName;
            behavior.TeamId = 0;
            behavior.BrainParameters.VectorObservationSize = obs;
            behavior.BrainParameters.NumStackedVectorObservations = 1;
            behavior.BrainParameters.ActionSpec = ActionSpec.MakeDiscrete(actions);
            behavior.BehaviorType = BehaviorType.Default;
            var agent = go.AddComponent<T>();
            agent.MaxStep = 0;
            EditorUtility.SetDirty(behavior);
            return agent;
        }
    }
}
