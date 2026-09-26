"""Write th_set.cs: put the open Mansion_Thief scene into evaluation mode (in memory only).

usage: th_set.py <thief> <episodes> [seed] [hiding]
  thief = None | Rule | Random | Model:<ThiefSeek onnx>,<ThiefHide onnx>
  hiding = Rule | Random (how players hid their props, V4)
"""
import os
import sys

thief, episodes = sys.argv[1], int(sys.argv[2])
seed = sys.argv[3] if len(sys.argv) > 3 else "777001"
hiding = sys.argv[4] if len(sys.argv) > 4 else "Rule"
mode = "None" if thief == "None" else "Agent"

if thief.startswith("Model:"):
    seek_path, hide_path = thief[len("Model:"):].split(",")
    brains = f"""
foreach (var ag in UnityEngine.Object.FindObjectsByType<Game.Training.Thief.ThiefSeekAgent>(FindObjectsSortMode.None)) {{
  var bp = ag.GetComponent<Unity.MLAgents.Policies.BehaviorParameters>();
  bp.Model = UnityEditor.AssetDatabase.LoadAssetAtPath<Unity.InferenceEngine.ModelAsset>("{seek_path}");
  if (bp.Model == null) return "MODEL NOT FOUND {seek_path}";
  bp.BehaviorType = Unity.MLAgents.Policies.BehaviorType.InferenceOnly; bp.DeterministicInference = true; }}
foreach (var ag in UnityEngine.Object.FindObjectsByType<Game.Training.Thief.ThiefHideAgent>(FindObjectsSortMode.None)) {{
  var bp = ag.GetComponent<Unity.MLAgents.Policies.BehaviorParameters>();
  bp.Model = UnityEditor.AssetDatabase.LoadAssetAtPath<Unity.InferenceEngine.ModelAsset>("{hide_path}");
  if (bp.Model == null) return "MODEL NOT FOUND {hide_path}";
  bp.BehaviorType = Unity.MLAgents.Policies.BehaviorType.InferenceOnly; bp.DeterministicInference = true; }}"""
else:
    h = "Rule" if thief in ("None", "Rule") else "Random"
    brains = f"""
var hm = typeof(Game.Training.Thief.ThiefAgentBase).GetField("heuristicMode", F);
foreach (var ag in UnityEngine.Object.FindObjectsByType<Game.Training.Thief.ThiefAgentBase>(FindObjectsSortMode.None)) {{
  hm.SetValue(ag, Game.Training.HideSeek.HideSeekHeuristic.{h});
  ag.GetComponent<Unity.MLAgents.Policies.BehaviorParameters>().BehaviorType = Unity.MLAgents.Policies.BehaviorType.HeuristicOnly; }}"""

code = f"""
var F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
var a = UnityEngine.Object.FindFirstObjectByType<Game.Training.Thief.ThiefArena>();
var t = typeof(Game.Training.Thief.ThiefArena);
t.GetField("evaluationMode", F).SetValue(a, true);
t.GetField("evaluationEpisodes", F).SetValue(a, {episodes});
t.GetField("evaluationSeed", F).SetValue(a, {seed});
t.GetField("logEveryEpisodes", F).SetValue(a, 100000);
t.GetField("editorTimeScale", F).SetValue(a, 100f);
t.GetField("thiefMode", F).SetValue(a, Game.Training.Thief.ThiefArena.ThiefMode.{mode});
t.GetField("playerHiding", F).SetValue(a, Game.Training.Thief.ThiefArena.PlayerHiding.{hiding});
{brains}
return "set dirty=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty;
"""
open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "th_set.cs"), "w", encoding="utf-8").write(code)
