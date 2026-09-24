using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fusion;
using Game.Network.Players;
using Unity.AI.Navigation;
using Unity.InferenceEngine;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Training.EditorTools
{
    /// <summary>
    /// Tools > AI > Build Mansion Bot Sandbox.
    ///
    /// Copies the mansion scene into a local-only sandbox (never committed), strips the match
    /// bootstrap that would demand network services, bakes a NavMesh from the static level geometry,
    /// and adds the bot runner and the pick agent running the v1.1 model. The original
    /// Mansion.unity is opened read-only through the copy and is never saved.
    /// Re-run it whenever the mansion changes on develop.
    /// </summary>
    public static class MansionBotSandboxBuilder
    {
        private const string SourceScene = "Assets/_Game/Content/Scenes/Mansion.unity";
        private const string OutputFolderParent = "Assets/_Game/Content/Training";
        private const string OutputFolderName = "Local";
        private const string OutputFolder = OutputFolderParent + "/" + OutputFolderName;
        private const string SandboxScene = OutputFolder + "/Mansion_BotSandbox.unity";
        private const string NavMeshAsset = OutputFolder + "/Mansion_BotSandbox_NavMesh.asset";
        private const string BotPrefabPath = "Assets/_Game/Content/Prefabs/NetworkedBot.prefab";
        private const string ModelPath = "Assets/_Game/Content/Training/Models/PickSelect_v1_1_Seed101.onnx";

        // Types stripped from the copy: they build the live match and need services the training
        // bootstrap intentionally does not register.
        private static readonly string[] StrippedComponentTypes =
        {
            "MatchSceneConfiguration",
        };

        [MenuItem("Tools/AI/Build Mansion Bot Sandbox")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var log = new StringBuilder("[Mansion Sandbox Builder]\n");

            if (!AssetDatabase.IsValidFolder(OutputFolder))
            {
                AssetDatabase.CreateFolder(OutputFolderParent, OutputFolderName);
            }

            AssetDatabase.DeleteAsset(SandboxScene);
            AssetDatabase.DeleteAsset(NavMeshAsset);
            if (!AssetDatabase.CopyAsset(SourceScene, SandboxScene))
            {
                Debug.LogError($"[Mansion Sandbox Builder] could not copy {SourceScene}.");
                return;
            }

            var scene = EditorSceneManager.OpenScene(SandboxScene, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();

            // 1) Read the first player spawn point before the configuration is stripped.
            var hasSpawn = TryReadFirstSpawnPoint(roots, out var spawnPose);
            log.AppendLine(hasSpawn
                ? $"spawn point from MatchSceneConfiguration: {spawnPose.position}"
                : "no MatchSceneConfiguration spawn point; using the scene origin");

            // 2) Strip the match bootstrap and its HUD.
            var removed = StripMatchBootstrap(roots, log);
            log.AppendLine($"removed {removed} components/objects");

            // 3) Bake a NavMesh from static level geometry only (props and characters move).
            var navObject = new GameObject("BotSandbox_NavMesh");
            var surface = navObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = LayerMask.GetMask("Default", "Water");
            surface.BuildNavMesh();
            if (surface.navMeshData == null)
            {
                Debug.LogError("[Mansion Sandbox Builder] NavMesh bake produced no data.");
                return;
            }

            AssetDatabase.CreateAsset(surface.navMeshData, NavMeshAsset);
            log.AppendLine($"NavMesh baked -> {NavMeshAsset}");

            // 4) Bot spawn point on the NavMesh.
            var spawnPosition = hasSpawn ? spawnPose.position : Vector3.zero;
            if (NavMesh.SamplePosition(spawnPosition, out var hit, 3f, NavMesh.AllAreas))
            {
                spawnPosition = hit.position + Vector3.up * 0.1f;
            }
            else
            {
                log.AppendLine("WARNING: spawn point is not near the baked NavMesh");
            }

            var spawn = new GameObject("BotSandbox_SpawnPoint").transform;
            spawn.SetPositionAndRotation(spawnPosition, hasSpawn ? spawnPose.rotation : Quaternion.identity);
            var target = new GameObject("BotSandbox_Target").transform;
            target.position = spawnPosition;

            // 5) Fusion single-player runner that spawns the bot.
            var runner = new GameObject("BotSandbox_Runner");
            var bootstrap = runner.AddComponent<BotKccTestBootstrap>();
            var botPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BotPrefabPath);
            var bootstrapSo = new SerializedObject(bootstrap);
            bootstrapSo.FindProperty("botPrefab").objectReferenceValue =
                botPrefab != null ? botPrefab.GetComponent<NetworkObject>() : null;
            bootstrapSo.FindProperty("spawnPoint").objectReferenceValue = spawn;
            bootstrapSo.FindProperty("target").objectReferenceValue = target;
            bootstrapSo.FindProperty("sessionName").stringValue = "MansionBotSandbox";
            bootstrapSo.ApplyModifiedPropertiesWithoutUndo();

            // 6) Agent + environment + executor, running the v1.1 model.
            var envObject = new GameObject("BotSandbox_Environment");
            var behavior = envObject.AddComponent<BehaviorParameters>();
            behavior.BehaviorName = "PickSelect";
            behavior.BrainParameters.VectorObservationSize = 44;
            behavior.BrainParameters.NumStackedVectorObservations = 1;
            behavior.BrainParameters.ActionSpec = ActionSpec.MakeDiscrete(5);
            behavior.Model = AssetDatabase.LoadAssetAtPath<ModelAsset>(ModelPath);
            behavior.BehaviorType = BehaviorType.InferenceOnly;
            behavior.DeterministicInference = true;
            if (behavior.Model == null)
            {
                log.AppendLine($"WARNING: model not found at {ModelPath}");
            }

            var executor = envObject.AddComponent<PickBotExecutor>();
            var environment = envObject.AddComponent<MansionSandboxEnvironment>();
            var agent = envObject.AddComponent<PickSelectAgent>();

            var agentSo = new SerializedObject(agent);
            agentSo.FindProperty("environmentComponent").objectReferenceValue = environment;
            agentSo.ApplyModifiedPropertiesWithoutUndo();

            var envSo = new SerializedObject(environment);
            envSo.FindProperty("agent").objectReferenceValue = agent;
            envSo.FindProperty("executor").objectReferenceValue = executor;
            envSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(behavior);

            // 7) Save.
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            var carryables = Object.FindObjectsByType<Game.Client.Interactions.CarryableItem>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            log.AppendLine($"carryable props in sandbox: {carryables}");
            log.AppendLine($"saved {SandboxScene}");
            log.AppendLine("next: add this scene to the ACTIVE training build profile (Build Profiles > Add Open Scenes), then Play.");
#if !GAME_TRAINING
            log.AppendLine("WARNING: GAME_TRAINING is not defined. Switch to the Training_Reach_Windows profile before Play.");
#endif
            Debug.Log(log.ToString());
        }

        private static bool TryReadFirstSpawnPoint(IEnumerable<GameObject> roots, out Pose pose)
        {
            pose = default;
            foreach (var root in roots)
            {
                foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour == null || behaviour.GetType().Name != "MatchSceneConfiguration")
                    {
                        continue;
                    }

                    var points = new SerializedObject(behaviour).FindProperty("spawnPoints");
                    if (points == null || !points.isArray || points.arraySize == 0)
                    {
                        continue;
                    }

                    if (points.GetArrayElementAtIndex(0).objectReferenceValue is Transform first)
                    {
                        pose = new Pose(first.position, first.rotation);
                        return true;
                    }
                }
            }

            return false;
        }

        private static int StripMatchBootstrap(GameObject[] roots, StringBuilder log)
        {
            var removed = 0;

            // HUD canvases: every root canvas that hosts a Game.Client view (match HUD, intros, timers).
            var hudCanvases = new HashSet<GameObject>();
            foreach (var root in roots)
            {
                foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour == null)
                    {
                        continue;
                    }

                    var type = behaviour.GetType();
                    var isHudView = type.Namespace != null &&
                                    type.Namespace.StartsWith("Game.Client") &&
                                    type.Name.EndsWith("View");
                    if (!isHudView)
                    {
                        continue;
                    }

                    var canvas = behaviour.GetComponentInParent<Canvas>(true);
                    if (canvas != null)
                    {
                        hudCanvases.Add(canvas.rootCanvas.gameObject);
                    }
                }
            }

            foreach (var canvas in hudCanvases)
            {
                if (canvas == null)
                {
                    continue;
                }

                log.AppendLine($"  removed HUD canvas '{canvas.name}'");
                DestroyGameObject(canvas);
                removed++;
            }

            // Lifetime scopes (they build the live match) and the match configuration.
            foreach (var root in roots)
            {
                if (root == null)
                {
                    continue;
                }

                var targets = root.GetComponentsInChildren<MonoBehaviour>(true)
                    .Where(b => b != null &&
                                (b is VContainer.Unity.LifetimeScope ||
                                 StrippedComponentTypes.Contains(b.GetType().Name)))
                    .ToList();
                foreach (var behaviour in targets)
                {
                    log.AppendLine($"  removed {behaviour.GetType().Name} on '{behaviour.gameObject.name}'");
                    UnpackIfNeeded(behaviour.gameObject);
                    Object.DestroyImmediate(behaviour);
                    removed++;
                }
            }

            return removed;
        }

        private static void DestroyGameObject(GameObject target)
        {
            UnpackIfNeeded(target);
            Object.DestroyImmediate(target);
        }

        /// <summary>Objects inside a prefab instance cannot be destroyed until the instance is unpacked.</summary>
        private static void UnpackIfNeeded(GameObject target)
        {
            if (!PrefabUtility.IsPartOfPrefabInstance(target))
            {
                return;
            }

            var outermost = PrefabUtility.GetOutermostPrefabInstanceRoot(target);
            if (outermost != null && outermost != target)
            {
                PrefabUtility.UnpackPrefabInstance(outermost, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }
        }
    }
}
