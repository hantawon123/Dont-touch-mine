using System;
using System.IO;
using System.Linq;
using Game.Client;
using Game.Client.Combat;
using Game.Client.Common;
using Game.Client.Interactions;
using Game.Client.Players;
using Game.Client.Tutorial;
using Game.Bootstrap;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor.Tutorial
{
    public static class TutorialSceneBuilder
    {
        private const string ScenePath = "Assets/_Game/Content/Scenes/Tutorial.unity";
        private const string PlayerPrefabPath = "Assets/_Game/Content/Prefabs/PlayerCharacter.prefab";
        private const string CameraPrefabPath = "Assets/_Game/Content/Prefabs/PlayerCameraRig.prefab";
        private const string TrainingItemPrefabPath = "Assets/_Game/Content/Prefabs/Carryable/Basement_CardboardBox1 Carryable.prefab";
        private const string BossPortraitPath = "Assets/_Game/Content/UI/Tutorial/BossThief.png";
        private const string RadioBubblePath = "Assets/_Game/Content/UI/Tutorial/BossRadioBubble.png";
        private const string RadioFontPath = "Assets/_Game/Content/Fonts/Paperlogy-6SemiBold SDF.asset";

        [MenuItem("Game/Tutorial/Play Tutorial", priority = 0)]
        public static void PlayTutorial()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            if (!File.Exists(ScenePath))
                BuildScene();
            else
                EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Game/Tutorial/Build Tutorial Hideout", priority = 10)]
        public static void BuildScene()
        {
            PrepareUiSprite(BossPortraitPath);
            PrepareUiSprite(RadioBubblePath);
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

        [MenuItem("Game/Tutorial/Refresh Boss Radio", priority = 15)]
        public static void RefreshBossRadio()
        {
            PrepareUiSprite(BossPortraitPath);
            PrepareUiSprite(RadioBubblePath);
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var runtime = Require(GameObject.Find("TutorialHideout").transform, "Runtime");
            var existing = runtime.Find("TutorialRadioCanvas");
            if (existing != null)
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            var session = runtime.GetComponent<TutorialSession>();
            if (session == null)
                throw new InvalidOperationException("TutorialSession missing from tutorial runtime.");
            BuildRadioUi(runtime, session);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            ValidateScene(true);
        }

        [MenuItem("Game/Tutorial/Refresh Lobby Look", priority = 16)]
        public static void RefreshLobbyLook()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var root = GameObject.Find("TutorialHideout")?.transform ??
                throw new InvalidOperationException("Tutorial root is missing.");
            TutorialHideoutArt.ApplyLobbyLook(Require(root, "Lighting"));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            ValidateScene(true);
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
                "Runtime/PlayerCharacter", "Runtime/PlayerCameraRig", "Zones/05_Exit/ExitDoor" })
                Require(root, path);
            if (Mathf.Abs(root.Find("Architecture/Ceiling").position.y - 7.5f) > .01f)
                throw new InvalidOperationException("Tutorial ceiling clearance changed.");
            var runtime = Require(root, "Runtime");
            if (runtime.GetComponent<TutorialSession>() == null || runtime.GetComponent<TutorialMovementCourse>() == null ||
                runtime.GetComponent<TutorialItemCourse>() == null)
                throw new InvalidOperationException("Tutorial courses missing.");
            if (runtime.GetComponentInChildren<TutorialRadioView>(true) == null)
                throw new InvalidOperationException("Tutorial boss radio UI missing.");
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
                  Capture(new Vector3(13.3f, 1.65f, 10), new Vector3(17.7f, .8f, 10), false, 70, "Jump");
                Capture(new Vector3(20, 1.1f, 4.2f), new Vector3(20, .8f, 1.5f), false, 70, "CrouchPassage");
                Capture(new Vector3(20, .6f, .5f), new Vector3(20, .4f, -2), false, 70, "PronePassage");
                // Presentation cutaway only. Restore every object's state even if capture fails.
                foreach (var path in new[] { "Architecture/Ceiling", "Architecture/CeilingBeams", "Architecture/UpperWalls" })
                    Require(root, path).gameObject.SetActive(false);
                foreach (var t in root.GetComponentsInChildren<Transform>())
                    if (t.name == "Cable")
                        t.gameObject.SetActive(false);
                Capture(new Vector3(0, 30, -24), new Vector3(0, 0, 0), true, 19.4f, "Overview");
                Capture(new Vector3(-13, 9, 2), new Vector3(-17, 1, 11), false, 53, "Lounge");
                Capture(new Vector3(3, 8, -14), new Vector3(-5, 1, -7), false, 63, "Workshop");
                Capture(new Vector3(-14, 7, -5), new Vector3(-20, 1, 1), false, 58, "Disposal");
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
            ConfigureCombat(runtime.gameObject);
            InstantiateRuntimePrefab(CameraPrefabPath, runtime, "PlayerCameraRig", Vector3.zero);
            BuildMainCamera(runtime);

            var session = runtime.gameObject.AddComponent<TutorialSession>();
            var movement = runtime.gameObject.AddComponent<TutorialMovementCourse>();
            var movementObject = new SerializedObject(movement);
            movementObject.FindProperty("session").objectReferenceValue = session;
            movementObject.FindProperty("player").objectReferenceValue = player;
            movementObject.FindProperty("fallCheckpoint").objectReferenceValue = root.Find("Zones/02_Movement/JumpCheckpoint");
            movementObject.ApplyModifiedPropertiesWithoutUndo();
            ConfigurePassages(root, movement);

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
            var recoveryPaths = new[] { "Zones/03_Items/PickupDrop/TrainingItemSpawn", "Zones/03_Items/DropRecovery",
                "Zones/03_Items/Throw/Recovery", "Zones/03_Items/Placement/Recovery", "Zones/04_Shredder/Recovery" };
            var points = itemObject.FindProperty("recoveryPoints");
            points.arraySize = recoveryPaths.Length;
            for (int i = 0; i < recoveryPaths.Length; i++)
                points.GetArrayElementAtIndex(i).objectReferenceValue = Require(root, recoveryPaths[i]);
            itemObject.ApplyModifiedPropertiesWithoutUndo();

            BuildRadioUi(runtime, session);

            Require(root, "Zones/05_Exit/ExitDoor").gameObject.AddComponent<TutorialExitDoor>();
        }

        private static void ConfigureCombat(GameObject runtime)
        {
            var scope = runtime.GetComponent<TutorialLifetimeScope>()
                ?? runtime.AddComponent<TutorialLifetimeScope>();
            var serialized = new SerializedObject(scope);
            serialized.FindProperty("matchRules").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Game.SOAP.Config.MatchRulesSO>(
                    "Assets/_Game/Content/Config/MatchRules.asset");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            runtime.GetComponentInChildren<PlayerCombatant>(true).enabled = true;
        }

        public static void RepairCombat()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            ConfigureCombat(GameObject.Find("TutorialHideout/Runtime"));
            EditorSceneManager.SaveScene(scene);
        }

        public static void AuditLobbyMaterials()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Game/Content/Scenes/Lobby.unity");
            var materials = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Renderer>(true))
                .SelectMany(r => r.sharedMaterials).Where(m => m != null)
                .GroupBy(m => AssetDatabase.GetAssetPath(m)).OrderByDescending(g => g.Count());
            foreach (var group in materials)
                Debug.Log($"[LobbyMaterial] {group.Count()} {group.Key}");
        }

        public static void RepairLobbySurfaces()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            TutorialHideoutArt.ApplyLobbySurfaces(GameObject.Find("TutorialHideout").transform);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
            RenderPreview();
        }

        public static void RepairSafetyMarkings()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            new TutorialHideoutArt(GameObject.Find("TutorialHideout").transform).RepairSafetyMarkings();
            EditorSceneManager.SaveScene(scene);
            RenderPreview();
        }

        private static void ConfigurePassages(Transform root, TutorialMovementCourse course)
        {
            var serialized = new SerializedObject(course);
            const string path = "Zones/02_Movement/CrouchCrawlLane/";
            serialized.FindProperty("crouchEntrance").objectReferenceValue = root.Find(path + "05_Crouch/Entrance");
            serialized.FindProperty("crouchExit").objectReferenceValue = root.Find(path + "05_Crouch/Exit");
            serialized.FindProperty("proneEntrance").objectReferenceValue = root.Find(path + "06_Prone/Entrance");
            serialized.FindProperty("proneExit").objectReferenceValue = root.Find(path + "06_Prone/Exit");
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void RepairPosturePassages()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var root = GameObject.Find("TutorialHideout").transform;
            new TutorialHideoutArt(root).RepairPosturePassages();
            ConfigurePassages(root, root.GetComponentInChildren<TutorialMovementCourse>());
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
            RenderPreview();
        }

        [MenuItem("Game/Tutorial/Repair Training Box")]
        public static void RepairTrainingBox()
        {
            var prefab = PrefabUtility.LoadPrefabContents(TrainingItemPrefabPath);
            try
            {
                foreach (var child in prefab.GetComponentsInChildren<Transform>(true))
                    GameObjectUtility.SetStaticEditorFlags(child.gameObject, 0);
                PrefabUtility.SaveAsPrefabAsset(prefab, TrainingItemPrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }

            var scene = EditorSceneManager.OpenScene(ScenePath);
            var item = GameObject.Find("TrainingItem");
            foreach (var child in item.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(child.gameObject, 0);
            EditorSceneManager.SaveScene(scene);
        }

        private static void BuildRadioUi(Transform parent, TutorialSession session)
        {
            var canvasObject = new GameObject("TutorialRadioCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler));
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            HudScreenScale.Apply(canvasObject.GetComponent<CanvasScaler>());

            var panel = UiRect("BossRadio", canvasObject.transform, new Vector2(.5f, 1f),
                new Vector2(1120, 260), new Vector2(0, -24));
            panel.pivot = new Vector2(.5f, 1f);

            var portrait = UiImage("BossPortrait", panel, AssetDatabase.LoadAssetAtPath<Sprite>(BossPortraitPath));
            portrait.rectTransform.anchorMin = portrait.rectTransform.anchorMax = new Vector2(0, .5f);
            portrait.rectTransform.pivot = new Vector2(0, .5f);
            portrait.rectTransform.anchoredPosition = new Vector2(0, -2);
            portrait.rectTransform.sizeDelta = new Vector2(185, 242);
            portrait.preserveAspect = true;

            var bubble = UiImage("RadioBubble", panel, AssetDatabase.LoadAssetAtPath<Sprite>(RadioBubblePath));
            bubble.rectTransform.anchorMin = bubble.rectTransform.anchorMax = new Vector2(0, .5f);
            bubble.rectTransform.pivot = new Vector2(0, .5f);
            bubble.rectTransform.anchoredPosition = new Vector2(168, 0);
            bubble.rectTransform.sizeDelta = new Vector2(950, 238);

            var messageRect = UiRect("Message", bubble.transform, Vector2.zero, Vector2.zero, Vector2.zero);
            messageRect.anchorMin = Vector2.zero;
            messageRect.anchorMax = Vector2.one;
            messageRect.offsetMin = new Vector2(145, 58);
            messageRect.offsetMax = new Vector2(-88, -55);
            var message = messageRect.gameObject.AddComponent<TextMeshProUGUI>();
            message.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RadioFontPath);
            message.fontSize = 30;
            message.color = new Color(.96f, .95f, 1f);
            message.alignment = TextAlignmentOptions.MidlineLeft;
            message.textWrappingMode = TextWrappingModes.Normal;
            message.raycastTarget = false;

            var view = canvasObject.AddComponent<TutorialRadioView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("session").objectReferenceValue = session;
            serialized.FindProperty("messageText").objectReferenceValue = message;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var keyGuide = KeySettingGuideView.Ensure(canvasObject.transform);
            keyGuide.AlwaysVisible = true;
        }

        private static RectTransform UiRect(
            string name,
            Transform parent,
            Vector2 anchor,
            Vector2 size,
            Vector2 position)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static Image UiImage(string name, Transform parent, Sprite sprite)
        {
            if (sprite == null)
                throw new FileNotFoundException($"Tutorial UI sprite not found: {name}");
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        private static void PrepareUiSprite(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                throw new FileNotFoundException($"Tutorial UI texture not found: {path}");
            if (importer.textureType == TextureImporterType.Sprite && !importer.mipmapEnabled && importer.alphaIsTransparency)
                return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
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
