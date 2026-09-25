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
        private const string PlaceScene = OutputFolder + "/Mansion_PlaceTraining.unity";
        private const string PlaceNavMeshAsset = OutputFolder + "/Mansion_PlaceTraining_NavMesh.asset";
        private const int PlaceAgentCount = 8;

        // Types stripped from the copy: they build the live match and need services the training
        // bootstrap intentionally does not register.
        private static readonly string[] StrippedComponentTypes =
        {
            "MatchSceneConfiguration",
        };

        /// <summary>Copies the mansion, strips the match bootstrap, bakes a NavMesh. Shared by both sandboxes.</summary>
        private static bool TryPrepareMansionCopy(
            string scenePath,
            string navMeshPath,
            StringBuilder log,
            out UnityEngine.SceneManagement.Scene scene,
            out bool hasSpawn,
            out Pose spawnPose)
        {
            scene = default;
            hasSpawn = false;
            spawnPose = default;

            if (!AssetDatabase.IsValidFolder(OutputFolder))
            {
                AssetDatabase.CreateFolder(OutputFolderParent, OutputFolderName);
            }

            AssetDatabase.DeleteAsset(scenePath);
            AssetDatabase.DeleteAsset(navMeshPath);
            if (!AssetDatabase.CopyAsset(SourceScene, scenePath))
            {
                Debug.LogError($"[Mansion Sandbox Builder] could not copy {SourceScene}.");
                return false;
            }

            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();

            hasSpawn = TryReadFirstSpawnPoint(roots, out spawnPose);
            log.AppendLine(hasSpawn
                ? $"spawn point from MatchSceneConfiguration: {spawnPose.position}"
                : "no MatchSceneConfiguration spawn point; using the scene origin");

            var removed = StripMatchBootstrap(roots, log);
            log.AppendLine($"removed {removed} components/objects");

            // Bake a NavMesh from static level geometry only (props and characters move).
            var navObject = new GameObject("BotSandbox_NavMesh");
            var surface = navObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = LayerMask.GetMask("Default", "Water");
            surface.BuildNavMesh();
            if (surface.navMeshData == null)
            {
                Debug.LogError("[Mansion Sandbox Builder] NavMesh bake produced no data.");
                return false;
            }

            AssetDatabase.CreateAsset(surface.navMeshData, navMeshPath);
            log.AppendLine($"NavMesh baked -> {navMeshPath}");
            return true;
        }

        [MenuItem("Tools/AI/Build Mansion Bot Sandbox")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var log = new StringBuilder("[Mansion Sandbox Builder]\n");
            if (!TryPrepareMansionCopy(SandboxScene, NavMeshAsset, log, out var scene, out var hasSpawn, out var spawnPose))
            {
                return;
            }

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

        /// <summary>
        /// Tools > AI > Build Mansion Place Training: same mansion copy and NavMesh, but no Fusion bot. Eight
        /// PlaceSelect agents share one PlaceTrainingEnvironment (decision D3: choose a spot and score it at once).
        /// </summary>
        [MenuItem("Tools/AI/Build Mansion Place Training")]
        public static void BuildPlaceTraining()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var log = new StringBuilder("[Mansion Place Training Builder]\n");
            if (!TryPrepareMansionCopy(PlaceScene, PlaceNavMeshAsset, log, out var scene, out _, out _))
            {
                return;
            }

            var envObject = new GameObject("PlaceTraining_Environment");
            var environment = envObject.AddComponent<PlaceTrainingEnvironment>();

            for (var i = 0; i < PlaceAgentCount; i++)
            {
                var agentObject = new GameObject($"PlaceTraining_Agent_{i}");
                agentObject.transform.SetParent(envObject.transform, false);
                var behavior = agentObject.AddComponent<BehaviorParameters>();
                behavior.BehaviorName = "PlaceSelect";
                behavior.BrainParameters.VectorObservationSize = Game.BotRuntime.Policy.PlaceObservationLayout.VectorSize;
                behavior.BrainParameters.NumStackedVectorObservations = 1;
                behavior.BrainParameters.ActionSpec = ActionSpec.MakeDiscrete(Game.BotRuntime.Policy.PlaceObservationLayout.ActionCount);
                behavior.BehaviorType = BehaviorType.Default;
                var agent = agentObject.AddComponent<PlaceSelectAgent>();
                var agentSo = new SerializedObject(agent);
                agentSo.FindProperty("environment").objectReferenceValue = environment;
                agentSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(behavior);
                if (i == 0)
                {
                    var envSo = new SerializedObject(environment);
                    envSo.FindProperty("evaluationAgent").objectReferenceValue = agent;
                    envSo.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            log.AppendLine($"{PlaceAgentCount} PlaceSelect agents added (Behavior Type Default, obs {Game.BotRuntime.Policy.PlaceObservationLayout.VectorSize}, branch {Game.BotRuntime.Policy.PlaceObservationLayout.ActionCount})");
            log.AppendLine($"saved {PlaceScene}");
            log.AppendLine("next: add this scene to the ACTIVE training build profile, then Play (baseline) or run mlagents-learn (training).");
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
