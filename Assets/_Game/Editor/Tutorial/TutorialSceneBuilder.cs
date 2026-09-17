using System;
using System.Collections.Generic;
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
using UnityEngine.Rendering;

namespace Game.Editor.Tutorial
{
    /// <summary>
    /// Builds the tutorial hideout from project-owned primitives and existing low-poly props.
    /// The generated hierarchy keeps each lesson area separate so gameplay triggers can be added later.
    /// </summary>
    public static class TutorialSceneBuilder
    {
        private const string MenuRoot = "Game/Tutorial/";
        private const string ScenePath = "Assets/_Game/Content/Scenes/Tutorial.unity";
        private const string MaterialFolder = "Assets/_Game/Content/Materials/Tutorial";
        private const string PlayerPrefabPath = "Assets/_Game/Content/Prefabs/PlayerCharacter.prefab";
        private const string CameraPrefabPath = "Assets/_Game/Content/Prefabs/PlayerCameraRig.prefab";
        private const string TrainingItemPrefabPath =
            "Assets/_Game/Content/Prefabs/Carryable/Basement_CardboardBox1 Carryable.prefab";
        private const float CeilingHeight = 7.5f;
        private const float MapWidth = 46f;
        private const float MapDepth = 30f;

        private const string ShredderPath =
            "Assets/PurpleBearShredder/Prefabs/PurpleBear_Shredder.prefab";
        private const string BasementProps =
            "Assets/PolyWorkshop_BasementWorkshop/Props/Prefabs/";
        private const string ShopProps = "Assets/Synty/PolygonShops/Prefabs/Props/";
        private const string MansionProps = "Assets/Synty/PolygonHorrorMansion/Prefabs/Props/";

        private static readonly Dictionary<string, Material> Materials = new();

        [MenuItem(MenuRoot + "Build Tutorial Hideout", priority = 10)]
        public static void BuildScene()
        {
            EnsureFolder(MaterialFolder);
            Materials.Clear();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Tutorial";

            var root = new GameObject("TutorialHideout");
            BuildShell(root.transform);
            BuildBriefingZone(root.transform);
            BuildMovementCourse(root.transform);
            BuildItemCourse(root.transform);
            BuildShredderZone(root.transform);
            BuildExitZone(root.transform);
            BuildSetDressing(root.transform);
            BuildLighting(root.transform);
            BuildAuthoringMarkers(root.transform);

            StaticBatchingUtility.Combine(root);
            BuildRuntime(root.transform);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException($"Failed to save tutorial scene: {ScenePath}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EnsureSceneInBuildSettings();
            ValidateScene(logSuccess: true);
            Debug.Log($"[Tutorial] Hideout built and saved: {ScenePath}");
        }

        [MenuItem(MenuRoot + "Validate Tutorial Hideout", priority = 20)]
        public static void ValidateSceneMenu() => ValidateScene(logSuccess: true);

        [MenuItem(MenuRoot + "Render Tutorial Preview", priority = 30)]
        public static void RenderPreview()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid()) throw new InvalidOperationException($"Could not open {ScenePath}.");

            var ceiling = GameObject.Find("TutorialHideout/Architecture/Ceiling");
            var beams = GameObject.Find("TutorialHideout/Architecture/CeilingBeams");
            if (ceiling == null || beams == null) throw new InvalidOperationException("Tutorial ceiling is missing.");

            var cameraObject = new GameObject("TutorialPreviewCamera");
            var camera = cameraObject.AddComponent<Camera>();
            cameraObject.transform.position = new Vector3(0f, 48f, -24f);
            cameraObject.transform.LookAt(new Vector3(0f, 0f, 0f));
            camera.fieldOfView = 48f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(25, 30, 38, 255);

            var target = new RenderTexture(1600, 1000, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            try
            {
                ceiling.SetActive(false);
                beams.SetActive(false);
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply();
                File.WriteAllBytes("TutorialMapPreview.png", image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);
            }
            finally
            {
                ceiling.SetActive(true);
                beams.SetActive(true);
                camera.targetTexture = null;
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }

            Debug.Log("[Tutorial] Preview rendered: TutorialMapPreview.png");
        }

        public static void ValidateScene(bool logSuccess)
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var root = GameObject.Find("TutorialHideout");
            if (root == null)
            {
                throw new InvalidOperationException("TutorialHideout root is missing.");
            }

            Require(root.transform, "Architecture/Ceiling");
            Require(root.transform, "Zones/01_Briefing");
            Require(root.transform, "Zones/02_Movement/WalkRunLane");
            Require(root.transform, "Zones/02_Movement/JumpLane");
            Require(root.transform, "Zones/02_Movement/CrouchCrawlLane");
            Require(root.transform, "Zones/03_Items/PickupDrop");
            Require(root.transform, "Zones/03_Items/Throw");
            Require(root.transform, "Zones/03_Items/Placement");
            Require(root.transform, "Zones/04_Shredder");
            Require(root.transform, "Zones/05_Exit");
            Require(root.transform, "Runtime/PlayerCharacter");
            Require(root.transform, "Runtime/PlayerCameraRig");
            var completion = Require(root.transform, "Runtime/TutorialCompletion");
            if (completion.GetComponent<TutorialCompletionTrigger>() == null)
            {
                throw new InvalidOperationException("Tutorial completion trigger is not wired.");
            }
            var runtime = Require(root.transform, "Runtime");
            if (runtime.GetComponent<TutorialSession>() == null ||
                runtime.GetComponent<TutorialMovementCourse>() == null ||
                runtime.GetComponent<TutorialItemCourse>() == null)
            {
                throw new InvalidOperationException("Tutorial runtime courses are not wired.");
            }
            Require(root.transform, "Zones/03_Items/PickupDrop/TrainingItem");

            var ceiling = root.transform.Find("Architecture/Ceiling");
            if (Mathf.Abs(ceiling.position.y - CeilingHeight) > 0.01f)
            {
                throw new InvalidOperationException(
                    $"Tutorial ceiling must stay at {CeilingHeight:0.0}m; current={ceiling.position.y:0.0}m.");
            }

            var shredder = root.transform.Find("Zones/04_Shredder/PurpleBear_Shredder");
            if (shredder == null)
            {
                throw new InvalidOperationException("PurpleBear shredder prefab is missing from the tutorial.");
            }

            if (logSuccess)
            {
                Debug.Log("[Tutorial] Validation passed: all lesson zones, 7.5m ceiling, shredder and exit are present.");
            }
        }

        private static void BuildShell(Transform root)
        {
            var architecture = Child(root, "Architecture");
            var floor = Cube(architecture, "Floor", new Vector3(0f, -0.25f, 0f),
                new Vector3(MapWidth, 0.5f, MapDepth), Mat("Concrete", new Color32(70, 79, 88, 255)));
            floor.isStatic = true;

            // At 7.5m, the 2m third-person camera and its 0.2m collision radius retain ample clearance.
            var ceiling = Cube(architecture, "Ceiling", new Vector3(0f, CeilingHeight, 0f),
                new Vector3(MapWidth, 0.35f, MapDepth), Mat("Ceiling", new Color32(39, 45, 54, 255)));
            ceiling.isStatic = true;

            var walls = Child(architecture, "OuterWalls");
            Wall(walls, "North", new Vector3(0f, 3.6f, 15f), new Vector3(MapWidth, 7.2f, 0.45f));
            Wall(walls, "SouthLeft", new Vector3(-13f, 3.6f, -15f), new Vector3(20f, 7.2f, 0.45f));
            Wall(walls, "SouthRight", new Vector3(13f, 3.6f, -15f), new Vector3(20f, 7.2f, 0.45f));
            Wall(walls, "West", new Vector3(-23f, 3.6f, 0f), new Vector3(0.45f, 7.2f, MapDepth));
            Wall(walls, "East", new Vector3(23f, 3.6f, 0f), new Vector3(0.45f, 7.2f, MapDepth));

            var partitions = Child(architecture, "Partitions");
            Wall(partitions, "BriefingEastNorth", new Vector3(-11.5f, 2.2f, 12.2f), new Vector3(0.35f, 4.4f, 5.6f));
            Wall(partitions, "BriefingEastSouth", new Vector3(-11.5f, 2.2f, 6.3f), new Vector3(0.35f, 4.4f, 2.2f));
            Wall(partitions, "NorthDividerWest", new Vector3(-17.3f, 2.2f, 4.8f), new Vector3(11.4f, 4.4f, 0.35f));
            Wall(partitions, "NorthDividerMiddle", new Vector3(-5.5f, 2.2f, 4.8f), new Vector3(5.4f, 4.4f, 0.35f));
            Wall(partitions, "NorthDividerEast", new Vector3(11.8f, 2.2f, 4.8f), new Vector3(14.4f, 4.4f, 0.35f));
            Wall(partitions, "SouthDividerWest", new Vector3(-17f, 2.2f, -4.7f), new Vector3(12f, 4.4f, 0.35f));
            Wall(partitions, "SouthDividerMiddle", new Vector3(-2f, 2.2f, -4.7f), new Vector3(12f, 4.4f, 0.35f));
            Wall(partitions, "SouthDividerEast", new Vector3(15.5f, 2.2f, -4.7f), new Vector3(10f, 4.4f, 0.35f));

            // Recessed panels and a dark base rail keep the large concrete walls from reading as grey boxes.
            var wallDetails = Child(architecture, "WallDetails");
            for (var x = -20f; x <= 20f; x += 5f)
            {
                Cube(wallDetails, $"NorthPanel_{x:+00;-00;00}", new Vector3(x, 3.4f, 14.72f),
                    new Vector3(4.35f, 5.7f, 0.08f), Mat("WallInset", new Color32(70, 82, 96, 255))).isStatic = true;
                Cube(wallDetails, $"NorthBase_{x:+00;-00;00}", new Vector3(x, 0.45f, 14.62f),
                    new Vector3(4.65f, 0.55f, 0.18f), Mat("SteelDark", new Color32(38, 48, 59, 255))).isStatic = true;
                Cube(wallDetails, $"SouthBase_{x:+00;-00;00}", new Vector3(x, 0.45f, -14.62f),
                    new Vector3(4.65f, 0.55f, 0.18f), Mat("SteelDark", new Color32(38, 48, 59, 255))).isStatic = true;
            }

            for (var z = -12f; z <= 12f; z += 4f)
            {
                Cube(wallDetails, $"WestBase_{z:+00;-00;00}", new Vector3(-22.62f, 0.45f, z),
                    new Vector3(0.18f, 0.55f, 3.65f), Mat("SteelDark", new Color32(38, 48, 59, 255))).isStatic = true;
                Cube(wallDetails, $"EastBase_{z:+00;-00;00}", new Vector3(22.62f, 0.45f, z),
                    new Vector3(0.18f, 0.55f, 3.65f), Mat("SteelDark", new Color32(38, 48, 59, 255))).isStatic = true;
            }

            var beams = Child(architecture, "CeilingBeams");
            for (var z = -12f; z <= 12f; z += 6f)
            {
                Cube(beams, $"Beam_{z:+00;-00;00}", new Vector3(0f, 6.9f, z),
                    new Vector3(MapWidth - 1f, 0.35f, 0.35f), Mat("Steel", new Color32(49, 61, 72, 255)));
            }

            // Muted floor insets distinguish rooms without turning the hideout into a colored test grid.
            var route = Child(architecture, "RouteFloor");
            FloorStrip(route, "BriefingBand", new Vector3(-17f, 0.015f, 9f), new Vector3(10f, 0.03f, 9f), "Warm", new Color32(91, 68, 93, 255));
            FloorStrip(route, "MovementBand", new Vector3(5.5f, 0.02f, 9f), new Vector3(32f, 0.04f, 9f), "RouteBlue", new Color32(63, 78, 92, 255));
            FloorStrip(route, "ServiceBand", new Vector3(0f, 0.025f, 0f), new Vector3(45f, 0.05f, 7.5f), "RoutePurple", new Color32(54, 62, 74, 255));
            FloorStrip(route, "ItemBand", new Vector3(-10f, 0.03f, -9f), new Vector3(24f, 0.06f, 8f), "RouteYellow", new Color32(81, 78, 68, 255));
            FloorStrip(route, "ShredderBand", new Vector3(16f, 0.035f, -9f), new Vector3(12f, 0.07f, 8f), "RouteMint", new Color32(63, 65, 75, 255));

            var columns = Child(architecture, "SupportColumns");
            foreach (var x in new[] { -11.5f, 0f, 11.5f })
            {
                Cube(columns, $"Column_{x:+00;-00;00}_North", new Vector3(x, 3.4f, 4.8f), new Vector3(0.65f, 6.8f, 0.65f), Mat("Steel", new Color32(49, 61, 72, 255)));
                Cube(columns, $"Column_{x:+00;-00;00}_South", new Vector3(x, 3.4f, -4.7f), new Vector3(0.65f, 6.8f, 0.65f), Mat("Steel", new Color32(49, 61, 72, 255)));
            }

            BuildServiceCorridor(architecture);
        }

        private static void BuildBriefingZone(Transform root)
        {
            var zone = Zone(root, "01_Briefing", new Vector3(-17f, 0f, 9f));
            BuildSectionHeader(zone, "BRIEFING", new Vector3(0f, 3.8f, 4.62f), 9f);

            var lounge = Child(zone, "BossLounge");
            BuildSofa(lounge, "CornerSofaWest", new Vector3(-3.9f, 0f, 1.8f), 0f);
            BuildSofa(lounge, "CornerSofaNorth", new Vector3(-1.3f, 0f, 3.25f), 90f);
            Cube(lounge, "LoungeRug", new Vector3(-1.7f, 0.04f, 1.15f), new Vector3(5.4f, 0.08f, 4.1f),
                Mat("LoungeRug", new Color32(91, 52, 122, 255)));
            Cube(lounge, "CoffeeTableTop", new Vector3(-1.35f, 0.55f, 1.1f), new Vector3(2.7f, 0.18f, 1.5f),
                Mat("DeskWood", new Color32(100, 67, 51, 255)));
            for (var x = -2.3f; x <= -0.4f; x += 1.9f)
            {
                Cube(lounge, $"CoffeeTableLeg_{x:0.0}", new Vector3(x, 0.28f, 1.1f), new Vector3(0.16f, 0.55f, 1.1f),
                    Mat("SteelDark", new Color32(38, 48, 59, 255)));
            }

            var command = Child(zone, "CommandDesk");
            PlaceProp(command, "Basement_Desk.prefab", "WorkshopDesk", new Vector3(3.7f, 0f, 2.8f), new Vector3(0f, -90f, 0f));
            PlaceProp(command, "Basement_OfficeChair.prefab", "BossChair", new Vector3(2.4f, 0f, 2.5f), new Vector3(0f, -70f, 0f));
            PlaceAsset(command, ShopProps + "SM_Prop_Computer_Monitor_01.prefab", "MonitorLeft", new Vector3(3.45f, 1.1f, 2.45f), new Vector3(0f, -90f, 0f), Vector3.one);
            PlaceAsset(command, ShopProps + "SM_Prop_Computer_Monitor_02.prefab", "MonitorRight", new Vector3(3.45f, 1.1f, 3.15f), new Vector3(0f, -90f, 0f), Vector3.one);
            PlaceAsset(command, ShopProps + "SM_Prop_Computer_Keyboard_01.prefab", "Keyboard", new Vector3(3.0f, 0.92f, 2.8f), new Vector3(0f, -90f, 0f), Vector3.one);
            Cube(command, "Radio", new Vector3(3.75f, 1.05f, 1.65f), new Vector3(0.75f, 0.38f, 0.5f),
                Mat("Radio", new Color32(48, 53, 60, 255)));
            Cylinder(command, "RadioAntenna", new Vector3(3.75f, 1.65f, 1.65f), new Vector3(0.05f, 0.6f, 0.05f),
                Vector3.zero, "SteelDark", new Color32(38, 48, 59, 255));

            PlaceProp(zone, "Basement_ToolBoard.prefab", "EvidenceBoard", new Vector3(-4.8f, 2.2f, 2.8f), new Vector3(0f, 90f, 0f));
            PlaceProp(zone, "Basement_Locker.prefab", "Locker", new Vector3(4.4f, 0f, -2.8f), Vector3.zero);
            PlaceProp(zone, "Basement_CardboardBox3.prefab", "BriefingCrateA", new Vector3(-4.2f, 0f, -2.9f), new Vector3(0f, 20f, 0f));
            PlaceProp(zone, "Basement_CardboardBox4.prefab", "BriefingCrateB", new Vector3(3.8f, 0f, -2.7f), new Vector3(0f, -15f, 0f));
            Marker(zone, "PlayerStart", new Vector3(0f, 0.1f, -2.4f));
        }

        private static void BuildMovementCourse(Transform root)
        {
            var movement = Zone(root, "02_Movement", Vector3.zero);
            BuildSectionHeader(movement, "MOVEMENT", new Vector3(5.5f, 3.8f, 14.58f), 31f);

            var walk = Child(movement, "WalkRunLane");
            walk.localPosition = new Vector3(-6f, 0f, 9f);
            for (var i = 0; i < 5; i++)
            {
                Cube(walk, $"LaneMarker_{i + 1}", new Vector3(i * 3f, 0.06f, 0f), new Vector3(1.4f, 0.12f, 2.4f),
                    Mat(i % 2 == 0 ? "LaneLight" : "LaneDark",
                        i % 2 == 0 ? new Color32(103, 148, 166, 255) : new Color32(53, 83, 99, 255)));
            }
            PlaceProp(walk, "Basement_RoadCone.prefab", "StartCone", new Vector3(-1.6f, 0f, -2f), Vector3.zero);
            PlaceProp(walk, "Basement_RoadCone.prefab", "FinishCone", new Vector3(13.6f, 0f, -2f), Vector3.zero);
            Cube(walk, "StartLine", new Vector3(-0.1f, 0.08f, 0f), new Vector3(0.25f, 0.12f, 3.2f),
                Mat("Warning", new Color32(232, 174, 50, 255)));

            var jump = Child(movement, "JumpLane");
            jump.localPosition = new Vector3(10.5f, 0f, 9f);
            Cube(jump, "StepLow", new Vector3(-2.8f, 0.3f, 0f), new Vector3(2.2f, 0.6f, 3.2f), Mat("Obstacle", new Color32(185, 111, 52, 255)));
            Cube(jump, "StepHigh", new Vector3(0f, 0.65f, 0f), new Vector3(2.2f, 1.3f, 3.2f), Mat("Obstacle", new Color32(185, 111, 52, 255)));
            Cube(jump, "Landing", new Vector3(3.4f, 0.4f, 0f), new Vector3(3f, 0.8f, 3.2f), Mat("Obstacle", new Color32(185, 111, 52, 255)));
            BuildHazardBorder(jump, "JumpHazard", new Vector3(0.3f, 0.045f, 0f), new Vector2(10f, 4.2f));

            var crouch = Child(movement, "CrouchCrawlLane");
            crouch.localPosition = new Vector3(14f, 0f, 1.3f);
            Cube(crouch, "CrouchFrameLeft", new Vector3(-2.2f, 1.25f, 0f), new Vector3(0.35f, 2.5f, 4f), Mat("Hazard", new Color32(196, 155, 45, 255)));
            Cube(crouch, "CrouchFrameRight", new Vector3(2.2f, 1.25f, 0f), new Vector3(0.35f, 2.5f, 4f), Mat("Hazard", new Color32(196, 155, 45, 255)));
            Cube(crouch, "CrouchHeader", new Vector3(0f, 2.2f, 0f), new Vector3(4.4f, 0.35f, 4f), Mat("Hazard", new Color32(196, 155, 45, 255)));
            Cube(crouch, "CrawlHeader", new Vector3(0f, 1.1f, -3.7f), new Vector3(4.4f, 0.3f, 3f), Mat("Steel", new Color32(49, 61, 72, 255)));
            PlaceProp(crouch, "Basement_RoadCone.prefab", "EntryConeLeft", new Vector3(-3f, 0f, 2.4f), Vector3.zero);
            PlaceProp(crouch, "Basement_RoadCone.prefab", "EntryConeRight", new Vector3(3f, 0f, 2.4f), Vector3.zero);
            PlaceProp(crouch, "Basement_RoadSign.prefab", "CourseSign", new Vector3(-4.1f, 0f, 1.8f), new Vector3(0f, 90f, 0f));
            Pipe(crouch, "CrawlPipeLeft", new Vector3(-2.05f, 0.8f, -3.7f), new Vector3(0.18f, 3f, 0.18f), new Vector3(90f, 0f, 0f), "SafetyRed", new Color32(183, 64, 49, 255));
            Pipe(crouch, "CrawlPipeRight", new Vector3(2.05f, 0.8f, -3.7f), new Vector3(0.18f, 3f, 0.18f), new Vector3(90f, 0f, 0f), "SafetyRed", new Color32(183, 64, 49, 255));
        }

        private static void BuildItemCourse(Transform root)
        {
            var items = Zone(root, "03_Items", Vector3.zero);
            BuildSectionHeader(items, "TOOLS & LOOT", new Vector3(-10f, 3.8f, -14.58f), 24f);

            var pickup = Child(items, "PickupDrop");
            pickup.localPosition = new Vector3(-17f, 0f, -8.5f);
            PlaceProp(pickup, "Basement_CardboardBox1.prefab", "PickupBoxA", new Vector3(-1.2f, 0f, 0f), Vector3.zero);
            PlaceProp(pickup, "Basement_CardboardBox2.prefab", "PickupBoxB", new Vector3(1.2f, 0f, 0.7f), new Vector3(0f, 25f, 0f));
            PlaceProp(pickup, "Basement_PlasticBox_Blue.prefab", "PickupBin", new Vector3(-3.5f, 0f, 1.7f), new Vector3(0f, 10f, 0f));
            Cube(pickup, "DropPad", new Vector3(2.8f, 0.04f, -1.4f), new Vector3(2.5f, 0.08f, 2.5f), Mat("Pad", new Color32(96, 177, 159, 255)));
            InstantiateRuntimePrefab(
                TrainingItemPrefabPath,
                pickup,
                "TrainingItem",
                pickup.TransformPoint(new Vector3(0f, 1f, -1f)));
            PlaceProp(pickup, "Basement_ToolBoard.prefab", "WallTools", new Vector3(-5.1f, 2.1f, -3.8f), Vector3.zero);
            PlaceProp(pickup, "Basement_Locker.prefab", "GearLockerA", new Vector3(-4.3f, 0f, 3.3f), new Vector3(0f, 180f, 0f));
            PlaceProp(pickup, "Basement_Locker.prefab", "GearLockerB", new Vector3(-2.8f, 0f, 3.3f), new Vector3(0f, 180f, 0f));

            var throwing = Child(items, "Throw");
            throwing.localPosition = new Vector3(-8f, 0f, -8.5f);
            Cube(throwing, "ThrowLine", new Vector3(-2f, 0.04f, 0f), new Vector3(0.25f, 0.08f, 5f), Mat("Warning", new Color32(232, 174, 50, 255)));
            Cube(throwing, "TargetLow", new Vector3(2.8f, 0.65f, -1.4f), new Vector3(1.4f, 1.3f, 1.4f), Mat("Target", new Color32(183, 75, 69, 255)));
            Cube(throwing, "TargetHigh", new Vector3(3.2f, 1.25f, 1.2f), new Vector3(1.8f, 2.5f, 1.8f), Mat("Target", new Color32(183, 75, 69, 255)));
            BuildTarget(throwing, "WallTargetA", new Vector3(4.4f, 1.5f, -2.4f));
            BuildTarget(throwing, "WallTargetB", new Vector3(4.4f, 1.5f, 2.4f));

            var placement = Child(items, "Placement");
            placement.localPosition = new Vector3(1f, 0f, -8.5f);
            Cube(placement, "PlacementBench", new Vector3(0f, 0.65f, 0f), new Vector3(4.8f, 1.3f, 1.4f), Mat("DeskWood", new Color32(100, 67, 51, 255)));
            for (var i = -1; i <= 1; i++)
            {
                    Cube(placement, $"PlacementSlot_{i + 2}", new Vector3(i * 1.35f, 1.33f, 0f), new Vector3(1f, 0.06f, 1f), Mat("Slot", new Color32(142, 112, 188, 255)));
            }
            PlaceProp(placement, "Basement_ToolBoard.prefab", "PlacementTools", new Vector3(0f, 2.25f, 3.9f), new Vector3(0f, 180f, 0f));
            PlaceProp(placement, "Basement_GasCylinder.prefab", "GasCylinder", new Vector3(4.3f, 0f, -2.8f), Vector3.zero);
            PlaceProp(placement, "Basement_Ladder.prefab", "WallLadder", new Vector3(4.7f, 0f, 3.1f), new Vector3(0f, -90f, 0f));
        }

        private static void BuildShredderZone(Transform root)
        {
            var zone = Zone(root, "04_Shredder", new Vector3(15.5f, 0f, -8.5f));
            BuildSectionHeader(zone, "DISPOSAL", new Vector3(0f, 3.8f, -6.08f), 11f);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShredderPath);
            if (prefab == null)
            {
                throw new FileNotFoundException("PurpleBear shredder prefab not found.", ShredderPath);
            }

            var shredder = (GameObject)PrefabUtility.InstantiatePrefab(prefab, zone);
            shredder.name = "PurpleBear_Shredder";
            shredder.transform.localPosition = new Vector3(0.5f, 0f, 0.4f);
            shredder.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);

            var interactionCollider = shredder.GetComponent<Collider>();
            if (interactionCollider == null) interactionCollider = shredder.AddComponent<BoxCollider>();
            interactionCollider.isTrigger = false;
            if (interactionCollider is BoxCollider box)
            {
                box.center = new Vector3(0f, 1.1f, 0f);
                box.size = new Vector3(2.4f, 2.2f, 2.4f);
            }

            var ejectionPoint = Child(shredder.transform, "TutorialEjectionPoint");
            ejectionPoint.localPosition = new Vector3(0.8f, 1.2f, 0f);
            var ejectionTarget = Child(shredder.transform, "TutorialEjectionTarget");
            ejectionTarget.localPosition = new Vector3(3f, 1f, 0f);
            var interactable = shredder.AddComponent<ShredderInteractable>();
            var shredderObject = new SerializedObject(interactable);
            shredderObject.FindProperty("ejectionPoint").objectReferenceValue = ejectionPoint;
            shredderObject.FindProperty("ejectionTarget").objectReferenceValue = ejectionTarget;
            shredderObject.ApplyModifiedPropertiesWithoutUndo();

            Cube(zone, "SafetyPad", new Vector3(0.5f, 0.03f, 0.4f), new Vector3(5.4f, 0.06f, 4.6f), Mat("ShredderPad", new Color32(115, 77, 150, 255)));
            BuildHazardBorder(zone, "ShredderHazard", new Vector3(0.5f, 0.045f, 0.4f), new Vector2(7.2f, 6.2f));
            PlaceProp(zone, "Basement_PlasticBox_Blue.prefab", "InputBin", new Vector3(-3.1f, 0f, 0.4f), new Vector3(0f, 15f, 0f));
            PlaceProp(zone, "Basement_PlasticBox_Green.prefab", "OutputBin", new Vector3(3.5f, 0f, 0.4f), new Vector3(0f, -15f, 0f));
            PlaceProp(zone, "Basement_Fusebox.prefab", "PowerBox", new Vector3(4.7f, 1.6f, 3.3f), new Vector3(0f, 180f, 0f));
            PlaceAsset(zone, ShopProps + "SM_Prop_AirDuct_Ceiling_End_01.prefab", "ExtractionDuct", new Vector3(2.8f, 5.4f, 3.7f), new Vector3(0f, 90f, 0f), new Vector3(1.35f, 1.35f, 1.35f));
            PlaceProp(zone, "Basement_GasCylinder.prefab", "CylinderA", new Vector3(4.4f, 0f, -2.5f), Vector3.zero);
            PlaceProp(zone, "Basement_GasCylinder.prefab", "CylinderB", new Vector3(3.8f, 0f, -3.1f), Vector3.zero);
            Marker(zone, "InteractionPoint", new Vector3(-1.8f, 0.1f, 0.4f));
        }

        private static void BuildExitZone(Transform root)
        {
            var zone = Zone(root, "05_Exit", new Vector3(0f, 0f, -14.7f));
            Cube(zone, "ExitFrameLeft", new Vector3(-2.4f, 2.2f, 0f), new Vector3(0.5f, 4.4f, 0.6f), Mat("Exit", new Color32(51, 132, 117, 255)));
            Cube(zone, "ExitFrameRight", new Vector3(2.4f, 2.2f, 0f), new Vector3(0.5f, 4.4f, 0.6f), Mat("Exit", new Color32(51, 132, 117, 255)));
            Cube(zone, "ExitFrameTop", new Vector3(0f, 4.15f, 0f), new Vector3(5.2f, 0.5f, 0.6f), Mat("Exit", new Color32(51, 132, 117, 255)));
            Cube(zone, "ExitLamp", new Vector3(0f, 4.65f, 0.15f), new Vector3(2.8f, 0.25f, 0.25f), Mat("LightMint", new Color32(117, 238, 197, 255)));
            Marker(zone, "TutorialComplete", new Vector3(0f, 0.1f, 1.8f));
        }

        private static void BuildLighting(Transform root)
        {
            var lighting = Child(root, "Lighting");
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color32(154, 167, 187, 255);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color32(37, 44, 54, 255);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 28f;
            RenderSettings.fogEndDistance = 65f;

            var key = new GameObject("Directional Light");
            key.transform.SetParent(lighting, false);
            key.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            var keyLight = key.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.color = new Color32(190, 211, 232, 255);
            keyLight.intensity = 1.15f;
            keyLight.shadows = LightShadows.Soft;

            AddAreaLight(lighting, "BriefingLight", new Vector3(-17f, 6.2f, 9f), new Color32(255, 188, 132, 255), 18f, 950f);
            AddAreaLight(lighting, "MovementLight", new Vector3(3f, 6.2f, 9f), new Color32(154, 215, 255, 255), 22f, 1100f);
            AddAreaLight(lighting, "ItemLight", new Vector3(-8f, 6.2f, -8.5f), new Color32(255, 218, 133, 255), 20f, 1000f);
            AddAreaLight(lighting, "ShredderLight", new Vector3(15.5f, 6.2f, -8.5f), new Color32(197, 149, 255, 255), 18f, 1000f);
            AddAreaLight(lighting, "CorridorLight", new Vector3(0f, 6.2f, 0f), new Color32(192, 210, 222, 255), 20f, 900f);

            for (var x = -18f; x <= 18f; x += 9f)
            {
                PlaceProp(lighting, "Basement_FluorescentLamp.prefab", $"CeilingLamp_{x:+00;-00;00}", new Vector3(x, 6.65f, 0f), new Vector3(0f, 90f, 0f));
            }
        }

        private static void BuildSetDressing(Transform root)
        {
            var dressing = Child(root, "SetDressing");

            // North training warehouse: stocked wall racks and a broken-floor traversal vignette.
            var north = Child(dressing, "NorthWarehouse");
            BuildStorageRack(north, "RackA", new Vector3(-8f, 0f, 13.6f), 4.6f);
            BuildStorageRack(north, "RackB", new Vector3(-2.8f, 0f, 13.6f), 4.6f);
            BuildStorageRack(north, "RackC", new Vector3(2.4f, 0f, 13.6f), 4.6f);
            BuildCrateStack(north, "CratesA", new Vector3(-8f, 0f, 11.8f), 3);
            BuildCrateStack(north, "CratesB", new Vector3(-1f, 0f, 11.8f), 4);
            PlacePropScaled(north, "Basement_Ladder.prefab", "TrainingLadder", new Vector3(5.2f, 0f, 13.5f),
                new Vector3(0f, 180f, 0f), 1.35f);

            var pit = Child(north, "TraversalPit");
            Cube(pit, "Pit", new Vector3(17.8f, 0.01f, 10.1f), new Vector3(8.2f, 0.04f, 6.8f),
                Mat("Pit", new Color32(24, 29, 35, 255)));
            Cube(pit, "BridgeA", new Vector3(15f, 0.35f, 9.1f), new Vector3(3.2f, 0.7f, 1.3f),
                Mat("CrateWood", new Color32(137, 93, 55, 255)));
            Cube(pit, "BridgeB", new Vector3(18.2f, 0.6f, 10.2f), new Vector3(3.2f, 1.2f, 1.3f),
                Mat("CrateWood", new Color32(137, 93, 55, 255)));
            Cube(pit, "BridgeC", new Vector3(21f, 0.3f, 11.4f), new Vector3(2.4f, 0.6f, 1.3f),
                Mat("CrateWood", new Color32(137, 93, 55, 255)));
            BuildHazardBorder(pit, "PitBorder", new Vector3(17.8f, 0.055f, 10.1f), new Vector2(8.5f, 7.1f));

            // South workshop: equipment stays near the walls so carry/throw/placement exercises remain clear.
            var workshop = Child(dressing, "SouthWorkshop");
            BuildWorkbench(workshop, "BenchWest", new Vector3(-20.7f, 0f, -8.2f), 90f);
            BuildWorkbench(workshop, "BenchSouth", new Vector3(-14.5f, 0f, -13.2f), 0f);
            BuildStorageRack(workshop, "WorkshopRack", new Vector3(-20.8f, 0f, -12f), 4.2f, 90f);
            BuildCrateStack(workshop, "WorkshopCrates", new Vector3(-4.4f, 0f, -12.4f), 5);
            PlacePropScaled(workshop, "Basement_PlasticBox_Blue.prefab", "PartsBinA", new Vector3(-8.8f, 0f, -13.2f), Vector3.zero, 1.4f);
            PlacePropScaled(workshop, "Basement_PlasticBox_Green.prefab", "PartsBinB", new Vector3(-7.3f, 0f, -13.2f), Vector3.zero, 1.4f);
            PlacePropScaled(workshop, "Basement_GasCylinder.prefab", "WorkshopCylinder", new Vector3(-21.4f, 0f, -5.8f), Vector3.zero, 1.3f);

            var shredderRoom = Child(dressing, "ShredderRoom");
            BuildStorageRack(shredderRoom, "DisposalRack", new Vector3(20.7f, 0f, -12.4f), 4.4f, -90f);
            BuildCrateStack(shredderRoom, "DisposalCrates", new Vector3(20.4f, 0f, -6.2f), 4);
            BuildWallPipes(shredderRoom, new Vector3(22.25f, 2.5f, -9f));

            // Repeated floor plates add scale and make the hideout feel assembled rather than painted.
            var plates = Child(dressing, "FloorPlates");
            for (var x = -20f; x <= 20f; x += 5f)
            {
                for (var z = -12.5f; z <= 12.5f; z += 5f)
                {
                    var plate = Cube(plates, $"Plate_{x:+00;-00;00}_{z:+00.0;-00.0;00.0}",
                        new Vector3(x, 0.008f, z), new Vector3(4.8f, 0.016f, 4.8f),
                        Mat("FloorPlate", new Color32(74, 83, 91, 255)));
                    plate.isStatic = true;
                    UnityEngine.Object.DestroyImmediate(plate.GetComponent<Collider>());
                }
            }
        }

        private static void BuildStorageRack(
            Transform parent,
            string name,
            Vector3 localPosition,
            float width,
            float yaw = 0f)
        {
            var rack = Child(parent, name);
            rack.localPosition = localPosition;
            rack.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var steel = Mat("RackSteel", new Color32(55, 67, 75, 255));
            var shelf = Mat("RackShelf", new Color32(122, 94, 60, 255));
            foreach (var x in new[] { -width * 0.5f, width * 0.5f })
            {
                Cube(rack, $"Post_{x:0.0}", new Vector3(x, 1.45f, 0f), new Vector3(0.14f, 2.9f, 0.65f), steel);
            }
            for (var y = 0.25f; y <= 2.55f; y += 1.15f)
            {
                Cube(rack, $"Shelf_{y:0.0}", new Vector3(0f, y, 0f), new Vector3(width, 0.14f, 0.75f), shelf);
            }
            for (var i = 0; i < 5; i++)
            {
                var x = -width * 0.38f + i * width * 0.19f;
                Cube(rack, $"StoredBox_{i}", new Vector3(x, 0.78f + (i % 2) * 1.12f, 0f),
                    new Vector3(width * 0.15f, 0.72f, 0.58f), Mat("CrateWood", new Color32(137, 93, 55, 255)));
            }
        }

        private static void BuildCrateStack(Transform parent, string name, Vector3 localPosition, int count)
        {
            var stack = Child(parent, name);
            stack.localPosition = localPosition;
            for (var i = 0; i < count; i++)
            {
                var row = i / 3;
                var column = i % 3;
                Cube(stack, $"Crate_{i + 1}", new Vector3(column * 1.05f, 0.45f + row * 0.9f, row * 0.12f),
                    new Vector3(0.9f, 0.85f, 0.9f), Mat("CrateWood", new Color32(137, 93, 55, 255)));
            }
        }

        private static void BuildWorkbench(Transform parent, string name, Vector3 localPosition, float yaw)
        {
            var bench = Child(parent, name);
            bench.localPosition = localPosition;
            bench.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var wood = Mat("DeskWood", new Color32(100, 67, 51, 255));
            var steel = Mat("SteelDark", new Color32(38, 48, 59, 255));
            Cube(bench, "Top", new Vector3(0f, 1f, 0f), new Vector3(3.6f, 0.22f, 1.15f), wood);
            foreach (var x in new[] { -1.55f, 1.55f })
            {
                Cube(bench, $"Leg_{x:0.0}", new Vector3(x, 0.5f, 0f), new Vector3(0.18f, 1f, 0.85f), steel);
            }
            Cube(bench, "Toolbox", new Vector3(-0.75f, 1.25f, 0f), new Vector3(1.1f, 0.32f, 0.55f),
                Mat("SafetyRed", new Color32(183, 64, 49, 255)));
        }

        private static void BuildWallPipes(Transform parent, Vector3 origin)
        {
            for (var i = 0; i < 3; i++)
            {
                Pipe(parent, $"VerticalPipe_{i}", origin + new Vector3(0f, 0f, i * 0.7f),
                    new Vector3(0.16f, 2.5f, 0.16f), Vector3.zero,
                    i == 1 ? "SafetyRed" : "PipeBlue",
                    i == 1 ? new Color32(183, 64, 49, 255) : new Color32(65, 98, 126, 255));
            }
        }

        private static void BuildServiceCorridor(Transform architecture)
        {
            var corridor = Child(architecture, "ServiceCorridor");
            BuildSectionHeader(corridor, "HIDEOUT B-12", new Vector3(0f, 3.8f, 4.58f), 20f);

            // Utilities stay above head height or against the partitions, leaving a clear 5m-wide route.
            Pipe(corridor, "NorthPipeRed", new Vector3(0f, 3.45f, 4.35f), new Vector3(0.2f, 20.5f, 0.2f),
                new Vector3(0f, 0f, 90f), "SafetyRed", new Color32(183, 64, 49, 255));
            Pipe(corridor, "NorthPipeBlue", new Vector3(0f, 2.9f, 4.35f), new Vector3(0.13f, 20.5f, 0.13f),
                new Vector3(0f, 0f, 90f), "PipeBlue", new Color32(65, 98, 126, 255));
            Pipe(corridor, "SouthPipeRed", new Vector3(5f, 3.35f, -4.35f), new Vector3(0.18f, 15.5f, 0.18f),
                new Vector3(0f, 0f, 90f), "SafetyRed", new Color32(183, 64, 49, 255));

            for (var x = -18f; x <= 18f; x += 6f)
            {
                Pipe(corridor, $"PipeClamp_{x:+00;-00;00}", new Vector3(x, 3.2f, 4.34f),
                    new Vector3(0.27f, 0.08f, 0.27f), Vector3.zero, "SteelDark", new Color32(38, 48, 59, 255));
            }

            PlaceAsset(corridor, ShopProps + "SM_Prop_AirDuct_Ceiling_01.prefab", "MainAirDuct",
                new Vector3(14.5f, 5.5f, -3.7f), new Vector3(0f, 90f, 0f), new Vector3(1.2f, 1.2f, 1.2f));
            PlaceProp(corridor, "Basement_Locker.prefab", "CorridorLockerA", new Vector3(8.2f, 0f, 3.7f), new Vector3(0f, 180f, 0f));
            PlaceProp(corridor, "Basement_Locker.prefab", "CorridorLockerB", new Vector3(9.7f, 0f, 3.7f), new Vector3(0f, 180f, 0f));
            PlaceProp(corridor, "Basement_ToolBoard.prefab", "CorridorToolBoard", new Vector3(-7f, 2f, -4.45f), Vector3.zero);
            PlaceProp(corridor, "Basement_Fusebox.prefab", "CorridorFusebox", new Vector3(-16f, 1.7f, 4.4f), new Vector3(0f, 180f, 0f));

            for (var x = -19f; x <= 19f; x += 4.75f)
            {
                Cube(corridor, $"FloorGuide_{x:+00.0;-00.0;00.0}", new Vector3(x, 0.055f, 0f),
                    new Vector3(2.8f, 0.06f, 0.12f), Mat("Guide", new Color32(109, 118, 124, 255)));
            }
        }

        private static void BuildSofa(Transform parent, string name, Vector3 localPosition, float yaw)
        {
            var sofa = Child(parent, name);
            sofa.localPosition = localPosition;
            sofa.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var fabric = Mat("SofaPurple", new Color32(104, 55, 145, 255));
            var fabricDark = Mat("SofaPurpleDark", new Color32(73, 38, 103, 255));
            Cube(sofa, "Base", new Vector3(0f, 0.38f, 0f), new Vector3(3.3f, 0.55f, 1.25f), fabricDark);
            Cube(sofa, "Seat", new Vector3(0f, 0.68f, -0.05f), new Vector3(2.75f, 0.28f, 0.95f), fabric);
            Cube(sofa, "Back", new Vector3(0f, 1.18f, 0.52f), new Vector3(3.25f, 1.3f, 0.28f), fabric);
            Cube(sofa, "ArmLeft", new Vector3(-1.55f, 0.85f, -0.02f), new Vector3(0.32f, 1.05f, 1.2f), fabric);
            Cube(sofa, "ArmRight", new Vector3(1.55f, 0.85f, -0.02f), new Vector3(0.32f, 1.05f, 1.2f), fabric);
        }

        private static void BuildSectionHeader(Transform parent, string label, Vector3 localPosition, float width)
        {
            var header = Child(parent, label.Replace(' ', '_'));
            header.localPosition = localPosition;
            Cube(header, "HeaderRail", Vector3.zero, new Vector3(width, 0.26f, 0.16f),
                Mat("SteelDark", new Color32(38, 48, 59, 255)));
            Cube(header, "HeaderAccent", new Vector3(-width * 0.32f, 0f, -0.11f), new Vector3(width * 0.25f, 0.12f, 0.08f),
                Mat("AccentPurple", new Color32(147, 78, 195, 255)));
        }

        private static void BuildHazardBorder(Transform parent, string name, Vector3 center, Vector2 size)
        {
            var border = Child(parent, name);
            var material = Mat("Warning", new Color32(232, 174, 50, 255));
            Cube(border, "North", center + new Vector3(0f, 0f, size.y * 0.5f), new Vector3(size.x, 0.08f, 0.18f), material);
            Cube(border, "South", center + new Vector3(0f, 0f, -size.y * 0.5f), new Vector3(size.x, 0.08f, 0.18f), material);
            Cube(border, "West", center + new Vector3(-size.x * 0.5f, 0f, 0f), new Vector3(0.18f, 0.08f, size.y), material);
            Cube(border, "East", center + new Vector3(size.x * 0.5f, 0f, 0f), new Vector3(0.18f, 0.08f, size.y), material);
        }

        private static void BuildTarget(Transform parent, string name, Vector3 localPosition)
        {
            var target = Child(parent, name);
            target.localPosition = localPosition;
            target.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Cylinder(target, "Outer", Vector3.zero, new Vector3(1.1f, 0.08f, 1.1f),
                Vector3.zero, "TargetLight", new Color32(220, 221, 214, 255));
            Cylinder(target, "Middle", new Vector3(0f, -0.09f, 0f), new Vector3(0.72f, 0.08f, 0.72f),
                Vector3.zero, "Target", new Color32(183, 75, 69, 255));
            Cylinder(target, "Bullseye", new Vector3(0f, -0.18f, 0f), new Vector3(0.3f, 0.08f, 0.3f),
                Vector3.zero, "TargetDark", new Color32(75, 47, 51, 255));
        }

        private static void BuildAuthoringMarkers(Transform root)
        {
            var markers = Child(root, "AuthoringMarkers");
            Marker(markers, "CameraClearanceReference_2m", new Vector3(-21f, 2f, -13f));
            Marker(markers, "CeilingReference_7_5m", new Vector3(-21f, CeilingHeight, -13f));
            Marker(markers, "RouteStart", new Vector3(-17f, 0.1f, 6f));
            Marker(markers, "RouteEnd", new Vector3(0f, 0.1f, -13f));
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
            if (combat != null) combat.enabled = false;
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

        private static Transform Zone(Transform root, string name, Vector3 localPosition)
        {
            var zones = root.Find("Zones") ?? Child(root, "Zones");
            var zone = Child(zones, name);
            zone.localPosition = localPosition;
            return zone;
        }

        private static void Wall(Transform parent, string name, Vector3 position, Vector3 scale)
        {
            var wall = Cube(parent, name, position, scale, Mat("Wall", new Color32(91, 102, 113, 255)));
            wall.isStatic = true;
        }

        private static void FloorStrip(Transform parent, string name, Vector3 position, Vector3 scale, string materialName, Color color)
        {
            var strip = Cube(parent, name, position, scale, Mat(materialName, color));
            strip.isStatic = true;
        }

        private static GameObject Cube(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        private static GameObject Cylinder(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Vector3 euler,
            string materialName,
            Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = localScale;
            go.GetComponent<MeshRenderer>().sharedMaterial = Mat(materialName, color);
            return go;
        }

        private static void Pipe(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Vector3 euler,
            string materialName,
            Color color)
        {
            Cylinder(parent, name, localPosition, localScale, euler, materialName, color).isStatic = true;
        }

        private static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static void Marker(Transform parent, string name, Vector3 localPosition)
        {
            var marker = Child(parent, name);
            marker.localPosition = localPosition;
        }

        private static GameObject PlaceProp(Transform parent, string prefabName, string instanceName, Vector3 localPosition, Vector3 euler)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasementProps + prefabName);
            if (prefab == null)
            {
                throw new FileNotFoundException($"Basement prop not found: {prefabName}");
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = instanceName;
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = Quaternion.Euler(euler);
            return instance;
        }

        private static GameObject PlacePropScaled(
            Transform parent,
            string prefabName,
            string instanceName,
            Vector3 localPosition,
            Vector3 euler,
            float uniformScale)
        {
            var instance = PlaceProp(parent, prefabName, instanceName, localPosition, euler);
            instance.transform.localScale = Vector3.one * uniformScale;
            return instance;
        }

        private static GameObject PlaceAsset(
            Transform parent,
            string assetPath,
            string instanceName,
            Vector3 localPosition,
            Vector3 euler,
            Vector3 localScale)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
            {
                throw new FileNotFoundException($"Tutorial prop not found: {assetPath}");
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = instanceName;
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = Quaternion.Euler(euler);
            instance.transform.localScale = localScale;
            return instance;
        }

        private static void AddAreaLight(Transform parent, string name, Vector3 position, Color color, float range, float intensity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.Soft;
        }

        private static Material Mat(string name, Color color)
        {
            if (Materials.TryGetValue(name, out var cached)) return cached;

            var path = $"{MaterialFolder}/MAT_Tutorial_{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (shader == null) throw new InvalidOperationException("No compatible Lit shader was found.");
                material = new Material(shader) { name = $"MAT_Tutorial_{name}" };
                AssetDatabase.CreateAsset(material, path);
            }

            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            else if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            material.SetFloat("_Smoothness", 0.15f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            Materials[name] = material;
            return material;
        }

        private static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static Transform Require(Transform root, string path)
        {
            var found = root.Find(path);
            if (found == null)
            {
                throw new InvalidOperationException($"Tutorial object is missing: {path}");
            }

            return found;
        }
    }
}
