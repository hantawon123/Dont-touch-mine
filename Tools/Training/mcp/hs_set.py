"""Write hs_set.cs: put the open Mansion_HideSeek scene into evaluation mode (in memory only).

usage: hs_set.py <hider> <seeker> <episodes> [seed]
  side = Rule | Random | Model:<asset path of an .onnx imported under Assets/>
A Model side runs Inference Only with deterministic inference; a heuristic side runs Heuristic Only.
"""
import os
import sys

hider, seeker, episodes = sys.argv[1], sys.argv[2], int(sys.argv[3])
seed = sys.argv[4] if len(sys.argv) > 4 else "777001"


def side(agent_type, spec):
    if spec.startswith("Model:"):
        path = spec[len("Model:"):]
        return f"""
foreach (var ag in UnityEngine.Object.FindObjectsByType<{agent_type}>(FindObjectsSortMode.None)) {{
  var bp = ag.GetComponent<Unity.MLAgents.Policies.BehaviorParameters>();
  bp.Model = UnityEditor.AssetDatabase.LoadAssetAtPath<Unity.InferenceEngine.ModelAsset>("{path}");
  if (bp.Model == null) return "MODEL NOT FOUND {path}";
  bp.BehaviorType = Unity.MLAgents.Policies.BehaviorType.InferenceOnly;
  bp.DeterministicInference = true;
}}"""
    return f"""
foreach (var ag in UnityEngine.Object.FindObjectsByType<{agent_type}>(FindObjectsSortMode.None)) {{
  hm.SetValue(ag, Game.Training.HideSeek.HideSeekHeuristic.{spec});
  ag.GetComponent<Unity.MLAgents.Policies.BehaviorParameters>().BehaviorType = Unity.MLAgents.Policies.BehaviorType.HeuristicOnly;
}}"""


code = f"""
var F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
var a = UnityEngine.Object.FindFirstObjectByType<Game.Training.HideSeek.HideSeekArena>();
var t = typeof(Game.Training.HideSeek.HideSeekArena);
t.GetField("evaluationMode", F).SetValue(a, true);
t.GetField("evaluationEpisodes", F).SetValue(a, {episodes});
t.GetField("evaluationSeed", F).SetValue(a, {seed});
t.GetField("logEveryEpisodes", F).SetValue(a, 100000);
t.GetField("editorTimeScale", F).SetValue(a, 100f);
var hm = typeof(Game.Training.HideSeek.HideSeekAgentBase).GetField("heuristicMode", F);
{side("Game.Training.HideSeek.HideSeekHideAgent", hider)}
{side("Game.Training.HideSeek.HideSeekSeekAgent", seeker)}
return "set dirty=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty;
"""
open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "hs_set.cs"), "w", encoding="utf-8").write(code)
