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
        private const string PlaceModelPath = "Assets/_Game/Content/Training/Models/PlaceSelect_v21_Seed101.onnx";
        private const string PlaceScene = OutputFolder + "/Mansion_PlaceTraining.unity";
        private const string PlaceNavMeshAsset = OutputFolder + "/Mansion_PlaceTraining_NavMesh.asset";
        private const int PlaceAgentCount = 8;
        private const string PlaceSceneV21 = OutputFolder + "/Mansion_PlaceTraining_v21.unity";
        private const string PlaceNavMeshAssetV21 = OutputFolder + "/Mansion_PlaceTraining_v21_NavMesh.asset";

        // Types stripped from the copy: they build the live match and need services the training
        // bootstrap intentionally does not register.
        private static readonly string[] StrippedComponentTypes =
        {
            "MatchSceneConfiguration",
        };

        /// <summary>Copies the mansion, strips the match bootstrap, bakes a NavMesh. Shared by both sandboxes.</summary>
        internal static bool TryPrepareMansionCopy(
            string scenePath,
            string navMeshPath,
            StringBuilder log,
            out UnityEngine.SceneManagement.Scene scene,
            out bool hasSpawn,
            out Pose spawnPose,
            Vector2? agentRadiusHeight = null)
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
            if (agentRadiusHeight.HasValue)
            {
                // Same sources as NavMeshSurface.BuildNavMesh (checked: identical area at r 0.5), but the agent
                // size matches the bot body so ~1 m doorways stay open. Project agent settings are not touched.
                var settings = NavMesh.GetSettingsByID(surface.agentTypeID);
                settings.agentRadius = agentRadiusHeight.Value.x;
                settings.agentHeight = agentRadiusHeight.Value.y;
                var sources = new List<NavMeshBuildSource>();
                var world = new Bounds(Vector3.zero, Vector3.one * 2000f);
                UnityEngine.AI.NavMeshBuilder.CollectSources(world, surface.layerMask, NavMeshCollectGeometry.PhysicsColliders,
                    surface.defaultArea, new List<NavMeshBuildMarkup>(), sources);
                surface.navMeshData = UnityEngine.AI.NavMeshBuilder.BuildNavMeshData(settings, sources, world, Vector3.zero, Quaternion.identity);
                surface.RemoveData();
                surface.AddData(); // BuildNavMesh() does this too: the spawn snap below needs the NavMesh loaded
                log.AppendLine($"NavMesh agent: radius {settings.agentRadius:F2} m, height {settings.agentHeight:F2} m (bot body r 0.31, h 1.67); {sources.Count} sources");
            }
            else
            {
                surface.BuildNavMesh();
            }

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
            if (!TryPrepareMansionCopy(SandboxScene, NavMeshAsset, log, out var scene, out var hasSpawn, out var spawnPose,
                    new Vector2(SandboxAgentRadius, SandboxAgentHeight)))
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

            // 6b) Placement: v2.1 model chooses where to carry the prop. Driven by the sandbox, never self-driven.
            var placeEnvObject = new GameObject("BotSandbox_PlaceEnvironment");
            var placeEnvironment = placeEnvObject.AddComponent<PlaceTrainingEnvironment>();
            var placeAgentObject = new GameObject("BotSandbox_PlaceAgent");
            placeAgentObject.transform.SetParent(placeEnvObject.transform, false);
            var placeBehavior = placeAgentObject.AddComponent<BehaviorParameters>();
            placeBehavior.BehaviorName = "PlaceSelect";
            placeBehavior.BrainParameters.VectorObservationSize = Game.BotRuntime.Policy.PlaceObservationLayoutV21.VectorSize;
            placeBehavior.BrainParameters.NumStackedVectorObservations = 1;
            placeBehavior.BrainParameters.ActionSpec = ActionSpec.MakeDiscrete(Game.BotRuntime.Policy.PlaceObservationLayout.ActionCount);
            placeBehavior.Model = AssetDatabase.LoadAssetAtPath<ModelAsset>(PlaceModelPath);
            placeBehavior.BehaviorType = BehaviorType.InferenceOnly;
            placeBehavior.DeterministicInference = true;
            if (placeBehavior.Model == null)
            {
                log.AppendLine($"WARNING: placement model not found at {PlaceModelPath}");
            }

            var placeAgent = placeAgentObject.AddComponent<PlaceSelectAgent>();
            var placeAgentSo = new SerializedObject(placeAgent);
            placeAgentSo.FindProperty("environment").objectReferenceValue = placeEnvironment;
            placeAgentSo.FindProperty("observationVersion").enumValueIndex = (int)Game.BotRuntime.Policy.PlaceObservationVersion.V21;
            placeAgentSo.FindProperty("externallyDriven").boolValue = true;
            placeAgentSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(placeBehavior);

            var envSo = new SerializedObject(environment);
            envSo.FindProperty("agent").objectReferenceValue = agent;
            envSo.FindProperty("executor").objectReferenceValue = executor;
            envSo.FindProperty("placeAgent").objectReferenceValue = placeAgent;
            envSo.FindProperty("placeEnvironment").objectReferenceValue = placeEnvironment;
            envSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(behavior);
            log.AppendLine("placement: v2.1 model connected (carry the prop to the chosen floor spot)");

            // 6c) Large movable props block the bot's body but are not in the baked NavMesh (they move).
            //     A carving NavMeshObstacle cuts them out of the NavMesh where they currently stand.
            var obstacles = AddCarvingObstacles(log);
            log.AppendLine($"carving NavMeshObstacle added to {obstacles} large carryable props (largest side >= {LargePropSide} m)");

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
        public static void BuildPlaceTraining() =>
            BuildPlaceTraining(Game.BotRuntime.Policy.PlaceObservationVersion.V2, PlaceScene, PlaceNavMeshAsset);

        [MenuItem("Tools/AI/Build Mansion Place Training (v2.1 sightlines)")]
        public static void BuildPlaceTrainingV21() =>
            BuildPlaceTraining(Game.BotRuntime.Policy.PlaceObservationVersion.V21, PlaceSceneV21, PlaceNavMeshAssetV21);

        private static void BuildPlaceTraining(
            Game.BotRuntime.Policy.PlaceObservationVersion version,
            string scenePath,
            string navMeshPath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var log = new StringBuilder("[Mansion Place Training Builder]\n");
            var obsSize = Game.BotRuntime.Policy.PlaceObservationVersions.VectorSize(version);
            if (!TryPrepareMansionCopy(scenePath, navMeshPath, log, out var scene, out _, out _))
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
                behavior.BrainParameters.VectorObservationSize = obsSize;
                behavior.BrainParameters.NumStackedVectorObservations = 1;
                behavior.BrainParameters.ActionSpec = ActionSpec.MakeDiscrete(Game.BotRuntime.Policy.PlaceObservationLayout.ActionCount);
                behavior.BehaviorType = BehaviorType.Default;
                var agent = agentObject.AddComponent<PlaceSelectAgent>();
                var agentSo = new SerializedObject(agent);
                agentSo.FindProperty("environment").objectReferenceValue = environment;
                agentSo.FindProperty("observationVersion").enumValueIndex = (int)version;
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
            log.AppendLine($"{PlaceAgentCount} PlaceSelect agents added (Behavior Type Default, {Game.BotRuntime.Policy.PlaceObservationVersions.Name(version)}, obs {obsSize}, branch {Game.BotRuntime.Policy.PlaceObservationLayout.ActionCount})");
            log.AppendLine($"saved {scenePath}");
            log.AppendLine("next: add this scene to the ACTIVE training build profile, then Play (baseline) or run mlagents-learn (training).");
#if !GAME_TRAINING
            log.AppendLine("WARNING: GAME_TRAINING is not defined. Switch to the Training_Reach_Windows profile before Play.");
#endif
            Debug.Log(log.ToString());
        }

        private const float LargePropSide = 0.8f;

        // Sandbox NavMesh agent size (bot KCC body r 0.31 m, h 1.67 m, same as NetworkedPlayer since 9/23). The place-training scenes keep the default
        // Humanoid bake (r 0.5, h 2.0) so the v2 / v2.1 evaluations stay reproducible.
        internal const float SandboxAgentRadius = 0.35f;
        internal const float SandboxAgentHeight = 1.7f;

        private static int AddCarvingObstacles(StringBuilder log)
        {
            var added = 0;
            foreach (var item in Object.FindObjectsByType<Game.Client.Interactions.CarryableItem>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var colliders = item.GetComponentsInChildren<Collider>(true);
                if (colliders.Length == 0 || item.GetComponent<NavMeshObstacle>() != null)
                {
                    continue;
                }

                // Local-space box that contains every collider (corners of each world AABB mapped into local space).
                var root = item.transform;
                var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
                var max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
                var worldMax = 0f;
                foreach (var c in colliders)
                {
                    var b = c.bounds;
                    worldMax = Mathf.Max(worldMax, b.size.x, b.size.y, b.size.z);
                    for (var i = 0; i < 8; i++)
                    {
                        var corner = new Vector3(
                            (i & 1) == 0 ? b.min.x : b.max.x,
                            (i & 2) == 0 ? b.min.y : b.max.y,
                            (i & 4) == 0 ? b.min.z : b.max.z);
                        var local = root.InverseTransformPoint(corner);
                        min = Vector3.Min(min, local);
                        max = Vector3.Max(max, local);
                    }
                }

                if (worldMax < LargePropSide)
                {
                    continue;
                }

                var obstacle = item.gameObject.AddComponent<NavMeshObstacle>();
                obstacle.shape = NavMeshObstacleShape.Box;
                obstacle.center = (min + max) * 0.5f;
                obstacle.size = max - min;
                obstacle.carving = true;
                obstacle.carveOnlyStationary = true;
                obstacle.carvingMoveThreshold = 0.1f;
                obstacle.carvingTimeToStationary = 0.5f;
                added++;
            }

            return added;
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
