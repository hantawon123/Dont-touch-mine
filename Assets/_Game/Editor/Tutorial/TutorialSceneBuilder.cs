using System;
using System.IO;
using System.Linq;
using Game.Client.Combat;
using Game.Client.Interactions;
using Game.Client.Players;
using Game.Client.Tutorial;
using Game.Bootstrap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor.Tutorial
{
    public static class TutorialSceneBuilder
    {
        private const string ScenePath = "Assets/_Game/Content/Scenes/Tutorial.unity";
        private const string PlayerPrefabPath = "Assets/_Game/Content/Prefabs/PlayerCharacter.prefab";
        private const string CameraPrefabPath = "Assets/_Game/Content/Prefabs/PlayerCameraRig.prefab";
        private const string TrainingItemPrefabPath = "Assets/_Game/Content/Prefabs/Carryable/Basement_CardboardBox1 Carryable.prefab";

        [MenuItem("Game/Tutorial/Build Tutorial Hideout", priority = 10)]
        public static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Tutorial";
            var root = new GameObject("TutorialHideout").transform;
            new TutorialHideoutArt(root).Build();
            BuildTrainingObjects(root);
            BuildRuntime(root);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Could not save tutorial scene.");
            AssetDatabase.SaveAssets();
            EnsureSceneInBuildSettings();
            ValidateScene(true);
            Debug.Log("[Tutorial] Detailed hideout saved.");
        }

        public static void BuildAndRender()
        {
            BuildScene();
            RenderPreview();
        }

        private static void BuildTrainingObjects(Transform root)
        {
            var pickup = Require(root, "Zones/03_Items/PickupDrop");
            InstantiateRuntimePrefab(TrainingItemPrefabPath, pickup, "TrainingItem", pickup.Find("TrainingItemSpawn").position);
            var zone = Require(root, "Zones/04_Shredder");
            var shredder = InstantiateRuntimePrefab("Assets/PurpleBearShredder/Prefabs/PurpleBear_Shredder.prefab",
                zone, "PurpleBear_Shredder", zone.Find("ShredderOrigin").position);
            shredder.transform.rotation = Quaternion.Euler(0, -90, 0);
            shredder.transform.localScale = Vector3.one * 1.5f;
            var box = shredder.GetComponent<BoxCollider>();
            if (box == null)
                box = shredder.AddComponent<BoxCollider>();
            box.isTrigger = false;
            box.center = new Vector3(0, 1.1f, 0);
            box.size = new Vector3(2.4f, 2.2f, 2.4f);
            var ejection = Child(shredder.transform, "TutorialEjectionPoint");
            ejection.localPosition = new Vector3(.8f, 1.2f, 0);
            var target = Child(shredder.transform, "TutorialEjectionTarget");
            target.localPosition = new Vector3(3, 1, 0);
            var interactable = shredder.AddComponent<ShredderInteractable>();
            var serialized = new SerializedObject(interactable);
            serialized.FindProperty("ejectionPoint").objectReferenceValue = ejection;
            serialized.FindProperty("ejectionTarget").objectReferenceValue = target;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem("Game/Tutorial/Validate Tutorial Hideout", priority = 20)]
        public static void ValidateSceneMenu() => ValidateScene(true);

        public static void ValidateScene(bool logSuccess)
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath);
            var root = GameObject.Find("TutorialHideout").transform;
            foreach (var path in new[] { "Architecture/Ceiling", "Zones/01_Briefing", "Zones/02_Movement/WalkRunLane",
                "Zones/02_Movement/JumpLane", "Zones/02_Movement/CrouchCrawlLane", "Zones/03_Items/PickupDrop/TrainingItem",
                "Zones/03_Items/Throw", "Zones/03_Items/Placement", "Zones/04_Shredder/PurpleBear_Shredder", "Zones/05_Exit",
                "Runtime/PlayerCharacter", "Runtime/PlayerCameraRig", "Runtime/TutorialCompletion" })
                Require(root, path);
            if (Mathf.Abs(root.Find("Architecture/Ceiling").position.y - 7.5f) > .01f)
                throw new InvalidOperationException("Tutorial ceiling clearance changed.");
            var runtime = Require(root, "Runtime");
            if (runtime.GetComponent<TutorialSession>() == null || runtime.GetComponent<TutorialMovementCourse>() == null ||
                runtime.GetComponent<TutorialItemCourse>() == null)
                throw new InvalidOperationException("Tutorial courses missing.");
            TutorialMapValidation.Validate(root);
            if (logSuccess)
                Debug.Log("[Tutorial] Validation passed: rooms, open doors, continuous walls, route and gameplay references.");
        }

        [MenuItem("Game/Tutorial/Render Tutorial Preview", priority = 30)]
        public static void RenderPreview()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var root = GameObject.Find("TutorialHideout").transform;
            var activeObjects = root.GetComponentsInChildren<Transform>(true)
                .ToDictionary(t => t.gameObject, t => t.gameObject.activeSelf);
            try
            {
                Directory.CreateDirectory("Logs/TutorialPreview");
                Require(root, "Runtime").gameObject.SetActive(false);
                Capture(new Vector3(-15.8f, 2.8f, 6.3f), new Vector3(-18, 1.5f, 12), false, 70, "Interior");
                // Presentation cutaway only. Restore every object's state even if capture fails.
                foreach (var path in new[] { "Architecture/Ceiling", "Architecture/CeilingBeams", "Architecture/UpperWalls" })
                    Require(root, path).gameObject.SetActive(false);
                foreach (var t in root.GetComponentsInChildren<Transform>())
                    if (t.name == "Cable")
                        t.gameObject.SetActive(false);
                Capture(new Vector3(0, 30, -24), new Vector3(0, 0, 0), true, 19.4f, "Overview");
                Capture(new Vector3(-13, 9, 2), new Vector3(-17, 1, 11), false, 53, "Lounge");
                Capture(new Vector3(-10, 8, -14), new Vector3(-10, 1, -6), false, 63, "Workshop");
                Capture(new Vector3(5, 8, -15), new Vector3(11, 1, -9), false, 58, "Disposal");
            }
            finally
            {
                foreach (var entry in activeObjects)
                    entry.Key.SetActive(entry.Value);
            }
            Debug.Log("[Tutorial] Preview images: Logs/TutorialPreview");
        }

        private static void Capture(Vector3 position, Vector3 target, bool orthographic, float view, string name)
        {
            var go = new GameObject("PreviewCamera");
            var camera = go.AddComponent<Camera>();
            go.transform.position = position;
            go.transform.LookAt(target);
            camera.orthographic = orthographic;
            camera.orthographicSize = view;
            camera.fieldOfView = view;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.13f, .17f, .22f);
            camera.allowHDR = true;
            var render = new RenderTexture(2000, 1300, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var previous = RenderTexture.active;
            Texture2D texture = null;
            try
            {
                camera.targetTexture = render;
                camera.Render();
                RenderTexture.active = render;
                texture = new Texture2D(render.width, render.height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, render.width, render.height), 0, 0);
                texture.Apply();
                File.WriteAllBytes($"Logs/TutorialPreview/{name}.png", texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                if (texture != null)
                    UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(render);
                UnityEngine.Object.DestroyImmediate(go);
            }
        }


        private static void BuildRuntime(Transform root)
        {
            var runtime = Child(root, "Runtime");
            var start = root.Find("Zones/01_Briefing/PlayerStart");
            if (start == null)
            {
                throw new InvalidOperationException("Tutorial player start marker is missing.");
            }

            var playerObject = InstantiateRuntimePrefab(
                PlayerPrefabPath,
                runtime,
                "PlayerCharacter",
                start.position);
            var player = playerObject.GetComponent<PlayerMovement>();
            var combat = playerObject.GetComponent<PlayerCombatant>();
            if (combat != null)
                combat.enabled = false;
            InstantiateRuntimePrefab(CameraPrefabPath, runtime, "PlayerCameraRig", Vector3.zero);
            BuildMainCamera(runtime);

            var session = runtime.gameObject.AddComponent<TutorialSession>();
            var movement = runtime.gameObject.AddComponent<TutorialMovementCourse>();
            var movementObject = new SerializedObject(movement);
            movementObject.FindProperty("session").objectReferenceValue = session;
            movementObject.FindProperty("player").objectReferenceValue = player;
            movementObject.ApplyModifiedPropertiesWithoutUndo();

            var itemCourse = runtime.gameObject.AddComponent<TutorialItemCourse>();
            var itemObject = new SerializedObject(itemCourse);
            itemObject.FindProperty("session").objectReferenceValue = session;
            itemObject.FindProperty("interactor").objectReferenceValue =
                playerObject.GetComponent<PlayerInteractor>();
            itemObject.FindProperty("trainingItem").objectReferenceValue =
                root.Find("Zones/03_Items/PickupDrop/TrainingItem").GetComponent<CarryableItem>();
            itemObject.FindProperty("trainingItemPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(TrainingItemPrefabPath)
                    .GetComponent<CarryableItem>();
            itemObject.FindProperty("shredder").objectReferenceValue =
                root.Find("Zones/04_Shredder/PurpleBear_Shredder")
                    .GetComponent<ShredderInteractable>();
            itemObject.ApplyModifiedPropertiesWithoutUndo();

            var exitMarker = root.Find("Zones/05_Exit/TutorialComplete");
            if (exitMarker == null)
            {
                throw new InvalidOperationException("Tutorial completion marker is missing.");
            }

            var completion = new GameObject("TutorialCompletion");
            completion.transform.SetParent(runtime, false);
            completion.transform.position = exitMarker.position;
            var trigger = completion.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(4.2f, 2.4f, 2.4f);
            completion.AddComponent<TutorialCompletionTrigger>();
        }

        private static GameObject InstantiateRuntimePrefab(
            string assetPath,
            Transform parent,
            string instanceName,
            Vector3 worldPosition)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
            {
                throw new FileNotFoundException($"Tutorial runtime prefab not found: {assetPath}");
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = instanceName;
            instance.transform.SetPositionAndRotation(worldPosition, Quaternion.identity);
            return instance;
        }

        private static void BuildMainCamera(Transform parent)
        {
            var output = new GameObject("Main Camera");
            output.transform.SetParent(parent, false);
            output.tag = "MainCamera";
            output.AddComponent<Camera>();
            output.AddComponent<AudioListener>();

            var brainType = Type.GetType("Unity.Cinemachine.CinemachineBrain, Unity.Cinemachine");
            if (brainType == null)
            {
                throw new InvalidOperationException("CinemachineBrain type could not be resolved.");
            }

            output.AddComponent(brainType);
        }

        private static void EnsureSceneInBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            var existing = scenes.FindIndex(scene => scene.path == ScenePath);
            if (existing >= 0)
            {
                scenes[existing].enabled = true;
            }
            else
            {
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }


        private static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static Transform Require(Transform parent, string path)
        {
            var found = parent.Find(path);
            if (found == null)
                throw new InvalidOperationException($"Missing tutorial object: {path}");
            return found;
        }
    }
}
