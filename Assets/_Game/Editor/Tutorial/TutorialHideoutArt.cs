using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Editor.Tutorial
{
    /// <summary>Editor-only construction of the hideout; gameplay is wired by TutorialSceneBuilder.</summary>
    internal sealed class TutorialHideoutArt
    {
        internal const float WallHeight = 7.3f;
        internal const float DoorWidth = 2.8f;
        internal const float DoorHeight = 3.4f;
        private const string Folder = "Assets/_Game/Content/Materials/Tutorial";
        private const string MeshPath = "Assets/_Game/Content/Materials/Tutorial/TutorialBeveledBox.asset";
        private const string Props = "Assets/PolyWorkshop_BasementWorkshop/Props/Prefabs/";
        private readonly Transform root;
        private readonly Dictionary<string, Material> materials = new();
        private readonly System.Random random = new(1072);
        private Transform upperWalls;
        private Mesh beveledBox;

        internal TutorialHideoutArt(Transform root) => this.root = root;

        internal void Build()
        {
            beveledBox = BeveledBox();
            Architecture();
            Lounge();
            Movement();
            Corridor();
            Workshop();
            Disposal();
            Lighting();
        }

        private void Architecture()
        {
            var p = Group(root, "Architecture");
            B(p, "Foundation", V(0, -.25f, 0), V(47, .5f, 31), "243344", true, false);
            B(p, "Floor", V(0, .01f, 0), V(46, .16f, 30), "898C8B", true, false);
            StoneFloor(p);
            upperWalls = Group(p, "UpperWalls");
            Wall(p, "North", V(-23, 0, 15), V(23, 0, 15));
            Wall(p, "South", V(-23, 0, -15), V(23, 0, -15));
            Wall(p, "West", V(-23, 0, -15), V(-23, 0, 15));
            Wall(p, "East", V(23, 0, -15), V(23, 0, 15));
            Wall(p, "NorthRooms", V(-23, 0, 5), V(23, 0, 5), 9, 20, 43);
            Wall(p, "SouthRooms", V(-23, 0, -3.5f), V(23, 0, -3.5f), 8, 33);
            Wall(p, "LoungeTraining", V(-10, 0, 5), V(-10, 0, 15), 3);
            Wall(p, "TrainingTraversal", V(4, 0, 5), V(4, 0, 15), 3);
            Wall(p, "WorkshopDisposal", V(4, 0, -15), V(4, 0, -3.5f), 6);
            Wall(p, "DisposalService", V(17, 0, -15), V(17, 0, -3.5f), 4);
            Wall(p, "ExitVestibule", V(17, 0, -8), V(23, 0, -8), 3);

            B(p, "Ceiling", V(0, 7.5f, 0), V(46.5f, .3f, 30.5f), "435365", true, false)
                .GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var beams = Group(p, "CeilingBeams");
            foreach (float z in new[] { -13f, -3.5f, 5f, 13f })
                B(beams, "CeilingJoist", V(0, 7.08f, z), V(46, .45f, .45f), "35495E", true);
            var marker = Group(root, "AuthoringMarkers");
            Group(marker, "CeilingReference_7_5m", V(-22, 7.5f, -14));
        }

        private void StoneFloor(Transform parent)
        {
            var group = Group(parent, "StoneFloor");
            var seed = new System.Random(72);
            var corners = new Vector3[24, 16];
            for (int x = 0; x < 24; x++)
                for (int z = 0; z < 16; z++)
                {
                    float dx = x == 0 || x == 23 ? 0 : (float)seed.NextDouble() * .95f - .475f;
                    float dz = z == 0 || z == 15 ? 0 : (float)seed.NextDouble() * .95f - .475f;
                    corners[x, z] = V(-23 + x * 2 + dx, .11f, -15 + z * 2 + dz);
                }
            var vertices = Enumerable.Range(0, 5).Select(_ => new List<Vector3>()).ToArray();
            var indices = Enumerable.Range(0, 5).Select(_ => new List<int>()).ToArray();
            for (int x = 0; x < 23; x++)
                for (int z = 0; z < 15; z++)
                {
                    var points = new[] { corners[x, z], corners[x, z + 1], corners[x + 1, z + 1], corners[x + 1, z] };
                    var center = points.Aggregate(Vector3.zero, (sum, point) => sum + point) * .25f;
                    int shade = seed.Next(5);
                    int first = vertices[shade].Count;
                    foreach (var point in points)
                        vertices[shade].Add(Vector3.Lerp(center, point, .997f));
                    indices[shade].AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
                }
            for (int i = 0; i < 5; i++)
            {
                string path = $"{Folder}/TutorialStoneFloor_{i}.asset";
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (mesh == null)
                {
                    mesh = new Mesh { name = $"TutorialStoneFloor_{i}" };
                    AssetDatabase.CreateAsset(mesh, path);
                }
                mesh.Clear();
                mesh.SetVertices(vertices[i]);
                mesh.SetTriangles(indices[i], 0);
                mesh.SetUVs(0, vertices[i].Select(v => new Vector2(v.x, v.z) / 3.2f).ToList());
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                EditorUtility.SetDirty(mesh);
                var go = Group(group, $"ConcreteSlabs_{i}").gameObject;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                path = $"{Folder}/MAT_Hideout_Concrete_{i}.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    AssetDatabase.CreateAsset(material, path);
                }
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Assets/PolyWorkshop_BasementWorkshop/Modular/Materials/Textures/Basement_Concrete_Albedo.png"));
                material.SetColor("_BaseColor", Color.white * (1.3f + i * .035f));
                material.SetFloat("_Smoothness", .15f);
                material.enableInstancing = true;
                EditorUtility.SetDirty(material);
                go.AddComponent<MeshRenderer>().sharedMaterial = material;
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            }
        }

        private void Wall(Transform parent, string name, Vector3 start, Vector3 end, params float[] doors)
        {
            var wall = Group(parent, name, start);
            var length = Vector3.Distance(start, end);
            wall.rotation = Quaternion.FromToRotation(Vector3.right, (end - start).normalized);
            Array.Sort(doors);
            float from = 0;
            foreach (var door in doors)
            {
                WallSpan(wall, from, door - DoorWidth * .5f);
                Door(wall, door);
                from = door + DoorWidth * .5f;
            }
            WallSpan(wall, from, length);
        }

        private void WallSpan(Transform p, float from, float to)
        {
            var length = to - from;
            if (length <= 0)
                throw new InvalidOperationException("Overlapping tutorial door apertures.");
            float x = (from + to) * .5f;
            B(p, "SolidWall", V(x, 1.8f, 0), V(length + .015f, 3.6f, .48f), "78818A", true, false);
            var upper = B(p, "UpperWall", V(x, 5.45f, 0), V(length + .015f, 3.7f, .48f), "78818A", true, false);
            upper.transform.SetParent(upperWalls, true);
            B(p, "WallCap", V(x, 3.64f, 0), V(length + .1f, .22f, .72f), "435C75");
            B(p, "BaseRail", V(x, .32f, 0), V(length, .45f, .62f), "516379");
            for (float sx = from + .14f; sx < to; sx += 2.7f)
            {
                B(p, "PanelJoin", V(sx, 1.9f, 0), V(.035f, 2.8f, .5f), "68757F", false, false);
                B(p, "BaseBoltFront", V(sx, .32f, -.325f), V(.085f, .085f, .04f), "A6B1BB");
                B(p, "BaseBoltBack", V(sx, .32f, .325f), V(.085f, .085f, .04f), "A6B1BB");
            }
            foreach (float px in new[] { from + .12f, to - .12f })
                B(p, "WallPier", V(px, 1.88f, 0), V(.24f, 3.76f, .66f), "4C637A");
        }

        private void Door(Transform wall, float x)
        {
            var p = Group(wall, "Door", V(x, 0, 0));
            // The aperture remains open; the leaf is hinged into the room rather than deleted.
            var lintel = B(p, "Lintel", V(0, (DoorHeight + WallHeight) * .5f, 0),
                V(DoorWidth, WallHeight - DoorHeight, .48f), "78818A", true, false);
            lintel.transform.SetParent(upperWalls, true);
            foreach (float side in new[] { -1f, 1f })
            {
                B(p, "Jamb", V(side * (DoorWidth * .5f - .08f), DoorHeight * .5f, 0),
                    V(.16f, DoorHeight, .7f), "35485B", true);
                B(p, "JambInset", V(side * (DoorWidth * .5f - .12f), DoorHeight * .5f, -.38f),
                    V(.075f, DoorHeight, .07f), "AFB7B9");
            }
            B(p, "Header", V(0, DoorHeight, 0), V(DoorWidth + .22f, .25f, .72f), "35485B", true);
            B(p, "Threshold", V(0, .1f, 0), V(DoorWidth, .035f, .68f), "677C8A");
            var hinge = Group(p, "OpenLeaf", V(-DoorWidth * .5f + .19f, .14f, .12f));
            hinge.localRotation = Quaternion.Euler(0, -102, 0);
            float width = DoorWidth - .42f;
            B(hinge, "DoorSlab", V(width * .5f, 1.53f, 0), V(width, 3.04f, .16f), "4F5D6C", true);
            B(hinge, "DoorInset", V(width * .5f, 1.5f, -.1f), V(width - .22f, 2.72f, .035f), "737C82");
            B(hinge, "KickPlate", V(width * .5f, .3f, -.14f), V(width - .32f, .34f, .045f), "9CA7AA");
            B(hinge, "HandlePlate", V(width - .3f, 1.45f, -.15f), V(.15f, .32f, .07f), "C3C4B7");
            B(hinge, "Handle", V(width - .41f, 1.5f, -.24f), V(.32f, .07f, .09f), "E0D4A5");
            Fixture(p, "DoorLight", V(0, 3.75f, -.52f), false);
            Group(p, "PassageCenter", V(0, 1.15f, 0));
        }

        private void Lounge()
        {
            var p = Zone("01_Briefing");
            for (int i = 0; i < 18; i++)
                B(p, "WoodPanel", V(-22.35f + i * .65f, 1.72f, 14.66f), V(.61f, 3.12f, .12f),
                    new[] { "956F42", "A47D4B", "8E6940" }[i % 3]);
            B(p, "PurpleRug", V(-18.7f, .12f, 9.3f), V(6.8f, .045f, 7.4f), "65447D");
            B(p, "RugBorder", V(-18.7f, .117f, 9.3f), V(7.05f, .025f, 7.65f), "9B7CAA");
            Sofa(p, V(-18.8f, .16f, 12.2f), 0, 3);
            Sofa(p, V(-21.25f, .16f, 9.25f), -90, 2);
            Table(p, "CoffeeTable", V(-18.8f, .13f, 8.5f), V(2.7f, .78f, 2.0f));
            Papers(p, V(-18.8f, 1.015f, 8.5f));
            EvidenceBoard(p, V(-18.6f, 2.25f, 14.48f), 4.9f);
            Plant(p, V(-21.5f, .12f, 6.45f), 1.65f);
            Plant(p, V(-15.1f, .12f, 13.2f), 1.4f);
            var desk = Group(p, "CommandStation", V(-12.2f, .12f, 11.4f));
            desk.localRotation = Quaternion.Euler(0, 90, 0);
            Table(desk, "Desk", V(0, 0, 0), V(4.5f, 1.08f, 1.35f));
            for (int i = -1; i <= 1; i++)
                Monitor(desk, V(i * 1.34f, 1.18f, .15f), i * -8);
            Prop(desk, "Basement_OfficeChair.prefab", "BossChair", V(0, 0, -1.45f), 180, 1.55f);
            Radio(desk, V(1.65f, 1.2f, -.32f));
            B(desk, "ComputerTower", V(-1.6f, .59f, .1f), V(.7f, 1.05f, .88f), "293740", true);
            for (int i = 0; i < 4; i++)
                B(desk, "TowerVent", V(-1.6f, .42f + i * .12f, -.36f), V(.43f, .035f, .03f), "647A85");
            Cardboard(p, V(-11.4f, .12f, 13.5f), 1.15f, -9);
            Cardboard(p, V(-12.4f, 1.2f, 13.7f), .9f, 12);
            Pipe(p, "LoungePipe", "8296A6", .1f, V(-22.3f, .3f, 6), V(-22.3f, 3.2f, 6), V(-22.3f, 3.2f, 14), V(-11, 3.2f, 14));
            Fixture(p, "LoungeWallLight", V(-12, 3.05f, 14.43f), false);
            Pendant(p, V(-20, 3.6f, 10.4f));
            Group(p, "PlayerStart", V(-15.8f, .2f, 8.6f));
        }

        private void Movement()
        {
            var p = Zone("02_Movement");
            Group(p, "WalkRunLane", V(-6, 0, 9));
            var jump = Group(p, "JumpLane");
            Rack(p, V(-2, .12f, 13.8f), 6.8f);
            Barrel(p, V(-6.2f, .12f, 13.3f), "427B9E");
            Prop(p, "Basement_Ladder.prefab", "TrainingLadder", V(-8.5f, .12f, 13.6f), 0, 2.6f);
            foreach (var position in new[] { V(-6.5f, .12f, 10.1f), V(-1.9f, .12f, 10.1f), V(-4.1f, .12f, 7f) })
                Crate(jump, position, V(1.8f, .85f, 1.25f), 0);
            foreach (var position in new[] { V(-7.7f, .12f, 11.2f), V(-3.9f, .12f, 10.5f), V(.8f, .12f, 9.8f), V(-1.6f, .12f, 6.5f) })
                Prop(p, "Basement_RoadCone.prefab", "Cone", position, 0, .72f);
            Hazard(p, V(-6.6f, .13f, 7.4f), new Vector2(2.6f, 4.2f));
            foreach (float x in new[] { -7.7f, -5.5f })
            {
                B(jump, "HurdlePost", V(x, .67f, 7.4f), V(.2f, 1.1f, .25f), "9B784B", true);
                B(jump, "HurdleFoot", V(x, .22f, 7.4f), V(.5f, .2f, 1.1f), "435568", true);
            }
            B(jump, "Hurdle", V(-6.6f, .72f, 7.4f), V(2.4f, .24f, .18f), "D5AC61", true);
            Pipe(p, "TrainingPipe", "9B4E37", .11f, V(2.9f, .5f, 5.7f), V(2.9f, 1.6f, 5.7f), V(-.9f, 1.6f, 5.7f));
            Pendant(p, V(-6.6f, 3.6f, 11.5f));
            Pendant(p, V(-.4f, 3.6f, 8.2f));

            var traversal = Group(p, "TraversalRoom");
            Pipe(traversal, "RedUtilities", "A9533F", .14f, V(4.6f, 2.3f, 14.1f), V(8, 2.3f, 14.1f), V(8, .5f, 14.1f));
            Pipe(traversal, "SilverUtilities", "9BA8AF", .12f, V(4.6f, 3.05f, 14.1f), V(11.4f, 3.05f, 14.1f), V(11.4f, 1.2f, 14.1f));
            B(traversal, "CoveredEquipment", V(7.2f, 1.05f, 12.3f), V(2.8f, 1.9f, 1.8f), "567D6B", true);
            B(traversal, "EquipmentPallet", V(7.2f, .22f, 12.3f), V(3.1f, .3f, 2), "A48350", true);
            Prop(traversal, "Basement_Fusebox.prefab", "PowerCabinet", V(11, .14f, 13.5f), 0, 1.7f);
            B(traversal, "TrenchDarkFloor", V(16.5f, .13f, 10.1f), V(3.9f, .045f, 8), "28323C");
            // Faceted broken edges and planks suggest a repair trench; the bottom is walkable for novice recovery.
            for (int i = 0; i < 9; i++)
            {
                foreach (float x in new[] { 14.45f, 18.55f })
                {
                    var rubble = B(traversal, "BrokenSlab", V(x, .2f, 6.4f + i * .86f), V(.8f, .28f, .75f), "777F82", true);
                    rubble.transform.localRotation = Quaternion.Euler(0, random.Next(-25, 25), 0);
                }
            }
            PlankBridge(traversal, V(16.5f, .36f, 7.8f), -14);
            PlankBridge(traversal, V(16.5f, .55f, 12.1f), 12);
            Crate(traversal, V(13.2f, .12f, 8.3f), V(1.4f, 1.4f, 1.4f), -8);
            Crate(traversal, V(20.6f, .12f, 13.2f), V(2, 2, 1.8f), 0);
            Crate(traversal, V(20.5f, 2.12f, 13.2f), V(1.7f, 1.4f, 1.6f), -4);
            Hazard(traversal, V(20.1f, .13f, 9.7f), new Vector2(2.2f, 4.5f));
            Fixture(traversal, "TraversalLight", V(16, 3.05f, 14.43f), false);

            var crawl = Group(p, "CrouchCrawlLane", V(20, 0, -5.1f));
            crawl.localRotation = Quaternion.Euler(0, 90, 0);
            B(crawl, "CrouchLintel", V(0, 1.65f, 0), V(2f, .24f, 1.25f), "7E909E", true);
            B(crawl, "CrawlLintel", V(0, .9f, -1.4f), V(2f, .18f, 1.1f), "7E909E", true);
            foreach (float x in new[] { -1.05f, 1.05f })
                B(crawl, "TunnelRail", V(x, .85f, -.7f), V(.18f, 1.5f, 2.8f), "435B71", true);
        }

        private void Corridor()
        {
            var p = Group(root, "ServiceCorridor");
            Pipe(p, "HeatingFeed", "B25F42", .16f, V(-11, .6f, 4.35f), V(-11, 2.9f, 4.35f), V(7, 2.9f, 4.35f), V(7, 1.2f, 4.35f), V(14, 1.2f, 4.35f));
            Pipe(p, "ReturnPipe", "97A7B2", .11f, V(-22, 3.18f, 4.24f), V(21, 3.18f, 4.24f), V(21, 1.8f, 4.24f));
            Prop(p, "Basement_ToolBoard.prefab", "ToolBoard", V(-10.8f, 1.75f, 4.35f), 180, 1.6f);
            Prop(p, "Basement_Cable1.prefab", "CableCoil", V(-8.6f, 1.65f, 4.22f), 180, 1.1f);
            ToolCart(p, V(-10.5f, .12f, 3));
            Bench(p, V(-5.2f, .12f, 3.2f), 3.2f, "715786");
            Vent(p, V(.5f, 1.4f, 4.43f), 2.8f, 2.4f);
            for (int i = 0; i < 4; i++)
                Locker(p, V(6 + i * .9f, .12f, 3.55f));
            Prop(p, "Basement_Fusebox.prefab", "ServiceFusebox", V(13.5f, 1.1f, 4.4f), 180, 1.3f);
            ToolCart(p, V(13.6f, .12f, 3.3f));
            Barrel(p, V(-19.2f, .12f, 3.5f), "485E70");
            Rack(p, V(-21.1f, .12f, 1.8f), 3.3f, 90);
            Cardboard(p, V(-20, .12f, -1.7f), 1.2f, 12);
            Fence(p, V(-18.6f, 0, -1.65f), 3.4f, 90);
            Fence(p, V(-21, 0, -3.15f), 3.6f, 0);
            Grate(p, V(0, .135f, -1), new Vector2(2.3f, 2.1f));
            foreach (float x in new[] { -19f, -7f, 4.5f, 15f })
                Fixture(p, "CorridorLight", V(x, 3.05f, 4.45f), false);
            Duct(p, V(17.5f, 3.2f, 3.6f), V(17.5f, 3.2f, -5.8f), .85f);
            Duct(p, V(17.5f, 3.2f, -5.8f), V(21.3f, 3.2f, -5.8f), .85f);
        }

        private void Workshop()
        {
            var p = Zone("03_Items");
            var pickup = Group(p, "PickupDrop", V(-9, 0, -10));
            Group(pickup, "TrainingItemSpawn", V(3, 1, -.5f));
            var throwing = Group(p, "Throw", V(-9, 0, -7.1f));
            Hazard(throwing, V(0, .13f, -1.5f), new Vector2(4.8f, 3.8f));
            B(throwing, "TargetBackboard", V(0, 1.75f, 2.86f), V(4.6f, 3.1f, .2f), "75664E", true);
            Target(throwing, V(0, 2.15f, 2.68f), 1.15f);
            for (int i = -1; i <= 1; i++)
                B(throwing, "CrashPad", V(i * 1.35f, .46f, 1.15f), V(1.3f, .64f, 1.6f), "4E78AE", true);
            var placement = Group(p, "Placement", V(-9, 0, -11.9f));
            Hazard(placement, V(0, .13f, 0), new Vector2(4.8f, 3.2f));
            Bench(p, V(-18.4f, .12f, -9), 4.1f, "667879");
            Crate(p, V(-18.3f, 1.32f, -9), V(.95f, .85f, .8f), 10);
            for (int i = 0; i < 3; i++)
            {
                B(p, "SilhouetteBoard", V(-21 + i * 1.55f, 2.2f, -4), V(1.35f, 1.8f, .07f), "DBCAA8");
                B(p, "SilhouetteTorso", V(-21 + i * 1.55f, 2.04f, -4.05f), V(.55f, .8f, .045f), "4E5050");
                Sphere(p, "SilhouetteHead", V(-21 + i * 1.55f, 2.63f, -4.06f), V(.38f, .38f, .045f), "4E5050");
            }
            Rack(p, V(-21.7f, .12f, -11.6f), 4.5f, 90);
            Rack(p, V(-2.4f, .12f, -13.6f), 7.6f);
            for (int i = 0; i < 4; i++)
                Locker(p, V(-3.5f + i * 1.02f, .12f, -4.55f));
            Prop(p, "Basement_ToolBoard.prefab", "WorkshopToolBoard", V(2.8f, 1.4f, -6), -90, 2.1f);
            ToolCart(p, V(2.35f, .12f, -5.9f));
            Prop(p, "Basement_GasCylinder.prefab", "WeldingCylinder", V(-13, .12f, -4.5f), 0, 1.5f);
            Prop(p, "Basement_Ladder.prefab", "WorkshopLadder", V(-4.3f, .12f, -5.1f), 0, 2.2f);
            Cardboard(p, V(-20.4f, .12f, -13.3f), 1.15f, 3);
            Pipe(p, "WorkshopPipe", "B26445", .14f, V(-13, 1.2f, -4.25f), V(-13, 3, -4.25f), V(3, 3, -4.25f), V(3, 1, -4.25f));
            Pendant(p, V(-20.4f, 3.6f, -7.7f));
            Pendant(p, V(-15.6f, 3.6f, -7.7f));
            Pendant(p, V(-8.9f, 3.6f, -7.2f));
            Fixture(p, "WorkshopLight", V(-1.2f, 3.05f, -4.05f), false);
        }

        private void Disposal()
        {
            var p = Zone("04_Shredder");
            Group(p, "ShredderOrigin", V(10.7f, .12f, -10.1f));
            Hazard(p, V(10.7f, .13f, -10.1f), new Vector2(6.6f, 6.4f));
            Bench(p, V(6.2f, .12f, -11.2f), 2.2f, "777D76");
            Rack(p, V(15.8f, .12f, -13.7f), 1.9f, 90);
            Cardboard(p, V(6.1f, .12f, -6.3f), 1.05f, 0);
            Cardboard(p, V(14.9f, .12f, -6), 1.15f, 8);
            Prop(p, "Basement_Fusebox.prefab", "DisposalSwitch", V(14.5f, 1.1f, -4.08f), 180, 1.2f);
            Pipe(p, "DisposalRedPipe", "B56041", .15f, V(5, 2.9f, -4.2f), V(7.3f, 2.9f, -4.2f), V(7.3f, 1, -4.2f));
            Duct(p, V(15.9f, 3.15f, -4.4f), V(15.9f, 3.15f, -8.1f), .88f);
            Fixture(p, "DisposalLight", V(12.5f, 3.05f, -4.05f), false);
            Pendant(p, V(10.7f, 3.9f, -9.5f));
            var exit = Zone("05_Exit");
            // Closed exterior entrance is a destination marker; all inter-room doors are open.
            var door = Group(exit, "ExitDoor", V(20, 0, -14.62f));
            B(door, "Frame", V(0, 1.8f, 0), V(2.6f, 3.6f, .18f), "D7A553");
            B(door, "Panel", V(0, 1.7f, .12f), V(2.28f, 3.18f, .16f), "59626A", true);
            B(door, "Handle", V(.75f, 1.5f, .3f), V(.32f, .07f, .12f), "D8CCAA");
            Fixture(exit, "ExitLamp", V(20, 3.85f, -14.3f), false);
            Prop(exit, "Basement_PlasticBox_Blue.prefab", "ExitBin", V(22, .12f, -13.5f), 0, .85f);
            Pipe(exit, "ExitConduit", "A65E43", .09f, V(17.5f, 2.8f, -14.2f), V(18.2f, 2.8f, -14.2f), V(18.2f, .4f, -14.2f));
            Group(exit, "TutorialComplete", V(20, .2f, -12.7f));
        }

        private void Lighting()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.44f, .49f, .57f);
            RenderSettings.fog = false;
            var p = Group(root, "Lighting");
            var key = Group(p, "Directional Light").gameObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.transform.rotation = Quaternion.Euler(65, -28, 0);
            key.color = new Color(1, .95f, .85f);
            key.intensity = 1.05f;
            key.shadows = LightShadows.Soft;
            foreach (var point in new[] { V(-17, 5.5f, 10), V(-3, 5.5f, 10), V(13, 5.5f, 10), V(-14, 5.5f, 0), V(0, 5.5f, 0), V(14, 5.5f, 0), V(-17, 5.5f, -10), V(-4, 5.5f, -10), V(11, 5.5f, -10), V(20, 5.5f, -11) })
            {
                var light = Group(p, "RoomFill", point).gameObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1, .85f, .68f);
                light.range = 11;
                light.intensity = 6;
                light.shadows = LightShadows.None;
            }
        }

        private void Sofa(Transform p, Vector3 pos, float yaw, int seats)
        {
            var g = Group(p, "TuftedSofa", pos);
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            float width = seats * 1.45f;
            B(g, "Base", V(0, .4f, 0), V(width + .4f, .65f, 1.75f), "57326F", true);
            for (int i = 0; i < seats; i++)
            {
                float x = (i - (seats - 1) * .5f) * 1.45f;
                B(g, "SeatCushion", V(x, .8f, -.18f), V(1.4f, .4f, 1.4f), "8850B4");
                B(g, "BackCushion", V(x, 1.45f, .63f), V(1.4f, 1.1f, .44f), "975ABC");
                Sphere(g, "TuftButton", V(x, 1.4f, .39f), V(.08f, .08f, .03f), "744194");
            }
            foreach (float side in new[] { -1f, 1f })
            {
                B(g, "Arm", V(side * (width * .5f + .12f), 1, 0), V(.38f, 1.1f, 1.85f), "814BA9", true);
                B(g, "Foot", V(side * (width * .5f - .15f), .16f, 0), V(.17f, .25f, 1.2f), "293442");
            }
        }

        private void Table(Transform p, string name, Vector3 pos, Vector3 size)
        {
            var g = Group(p, name, pos);
            B(g, "Top", V(0, size.y, 0), V(size.x, .18f, size.z), "BA915E", true);
            foreach (float x in new[] { -size.x * .42f, size.x * .42f })
                foreach (float z in new[] { -size.z * .35f, size.z * .35f })
                    B(g, "Leg", V(x, size.y * .5f, z), V(.15f, size.y, .15f), "4B4F51", true);
            B(g, "Apron", V(0, size.y - .2f, size.z * .38f), V(size.x - .3f, .3f, .12f), "8D6A43");
        }

        private void Monitor(Transform p, Vector3 pos, float yaw)
        {
            var g = Group(p, "Monitor", pos);
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            B(g, "Stand", V(0, .1f, 0), V(.55f, .1f, .4f), "344550");
            B(g, "Stem", V(0, .3f, .05f), V(.12f, .45f, .12f), "344550");
            B(g, "Bezel", V(0, .7f, .05f), V(1.13f, .8f, .13f), "25353D");
            B(g, "Screen", V(0, .72f, -.03f), V(.98f, .63f, .018f), "72BFCC");
            B(g, "ScreenWindow", V(-.14f, .75f, -.042f), V(.58f, .37f, .006f), "B4EBE7");
            for (int i = 0; i < 3; i++)
                B(g, "ScreenText", V(-.13f, .68f + i * .09f, -.05f), V(.38f, .025f, .005f), "548B98");
            B(g, "Keyboard", V(0, .08f, -.55f), V(.85f, .06f, .3f), "3E5260");
            for (int i = 0; i < 9; i++)
                B(g, "Keys", V(-.34f + i * .082f, .119f, -.55f), V(.054f, .015f, .23f), "BAC4C3");
        }

        private void Radio(Transform p, Vector3 pos)
        {
            var g = Group(p, "BossRadio", pos);
            B(g, "Body", V(0, .23f, 0), V(.64f, .45f, .36f), "3B4647");
            for (int i = 0; i < 5; i++)
                B(g, "SpeakerGrille", V(-.1f, .12f + i * .045f, -.19f), V(.27f, .019f, .02f), "1D2B31");
            B(g, "Dial", V(.2f, .27f, -.22f), V(.1f, .1f, .06f), "C7B27E");
            Rod(g, "Antenna", V(.24f, .42f, 0), V(.32f, 1.03f, 0), .018f, "9DAFB8");
        }

        private void EvidenceBoard(Transform p, Vector3 pos, float width)
        {
            var g = Group(p, "EvidenceBoard", pos);
            B(g, "Frame", Vector3.zero, V(width, 1.8f, .12f), "583D2D");
            B(g, "Cork", V(0, 0, -.085f), V(width - .18f, 1.62f, .06f), "AC8553");
            for (int i = 0; i < 14; i++)
            {
                float x = -width * .42f + (i % 7) * width * .14f;
                float y = i < 7 ? .36f : -.35f;
                var photo = Group(g, "PinnedPhoto", V(x, y, -.14f));
                photo.localRotation = Quaternion.Euler(0, 0, random.Next(-12, 13));
                B(photo, "Paper", Vector3.zero, V(.49f, .59f, .025f), "E1D9BE");
                B(photo, "Print", V(0, .04f, -.02f), V(.4f, .4f, .012f), new[] { "526F7B", "7F6457", "7F8993", "49544B" }[i % 4]);
                B(photo, "Building", V(-.07f, .025f, -.029f), V(.11f, .21f, .01f), "A6B1BB");
                B(photo, "Building", V(.07f, -.02f, -.029f), V(.12f, .12f, .01f), "C6CAB9");
                B(photo, "Caption", V(0, -.23f, -.025f), V(.26f, .025f, .01f), "7F6457");
                Sphere(photo, "Pin", V(0, .27f, -.05f), V(.08f, .08f, .05f), "AF523C");
            }
        }

        private void Papers(Transform p, Vector3 pos)
        {
            for (int i = 0; i < 4; i++)
            {
                var page = B(p, "PlanSheet", pos + V((i % 2 - .5f) * .65f, i * .004f, (i / 2 - .5f) * .55f), V(.52f, .014f, .43f), "E1D8BF");
                page.transform.localRotation = Quaternion.Euler(0, random.Next(-25, 25), 0);
            }
        }

        private void Plant(Transform p, Vector3 pos, float height)
        {
            var g = Group(p, "PottedPlant", pos);
            Cylinder(g, "Pot", V(0, .28f, 0), V(.75f, .28f, .75f), "776C55");
            Cylinder(g, "Soil", V(0, .56f, 0), V(.65f, .018f, .65f), "3F3C2E");
            for (int i = 0; i < 9; i++)
            {
                float angle = i * Mathf.PI * 2 / 9;
                var leaf = Sphere(g, "Leaf", V(Mathf.Cos(angle) * .3f, .75f + height * .22f, Mathf.Sin(angle) * .3f), V(.26f, height * .75f, .38f), i % 2 == 0 ? "4F7652" : "6D9354");
                leaf.transform.localRotation = Quaternion.Euler(Mathf.Sin(angle) * 32, 0, -Mathf.Cos(angle) * 32);
            }
        }

        private void Rack(Transform p, Vector3 pos, float width, float yaw = 0)
        {
            var g = Group(p, "StockRack", pos);
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            foreach (float x in new[] { -width * .5f, width * .5f })
                foreach (float z in new[] { -.5f, .5f })
                    B(g, "Post", V(x, 1.55f, z), V(.12f, 3.1f, .12f), "4D6371", true);
            for (int row = 0; row < 3; row++)
            {
                float y = .2f + row * 1.0f;
                B(g, "Shelf", V(0, y, 0), V(width + .15f, .14f, 1.16f), "697C85", true);
                int count = Mathf.FloorToInt(width / 1.05f);
                for (int i = 0; i < count; i++)
                {
                    float x = -width * .5f + .55f + i * 1.05f;
                    Cardboard(g, V(x, y + .07f, 0), .72f + random.Next(3) * .07f, 0);
                }
            }
            Rod(g, "RearBrace", V(-width * .5f, .2f, .55f), V(width * .5f, 3, .55f), .045f, "405461");
        }

        private void Cardboard(Transform p, Vector3 pos, float size, float yaw)
        {
            var g = Group(p, "CardboardBox", pos);
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            B(g, "Box", V(0, size * .43f, 0), V(size, size * .86f, size * .86f), "AF9169", true);
            B(g, "Tape", V(0, size * .864f, 0), V(size * .17f, .014f, size * .84f), "D5BD88");
            B(g, "Label", V(-size * .19f, size * .51f, -size * .434f), V(size * .27f, size * .2f, .016f), "D4CEB5");
        }

        private void Crate(Transform p, Vector3 pos, Vector3 size, float yaw)
        {
            var g = Group(p, "TimberCrate", pos);
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            B(g, "CrateBody", V(0, size.y * .5f, 0), size, "A27B49", true);
            for (int i = 0; i < 4; i++)
                B(g, "PlankSeam", V(-size.x * .38f + i * size.x * .25f, size.y * .5f, -size.z * .504f), V(.014f, size.y * .86f, .015f), "75563A");
            foreach (float side in new[] { -1f, 1f })
            {
                B(g, "FrameSide", V(side * size.x * .43f, size.y * .5f, -size.z * .53f), V(.14f, size.y, .12f), "C5A26A");
                B(g, "FrameEnd", V(0, size.y * (side > 0 ? .92f : .08f), -size.z * .53f), V(size.x, .14f, .12f), "C5A26A");
            }
            var brace = B(g, "DiagonalBrace", V(0, size.y * .5f, -size.z * .59f), V(Mathf.Sqrt(size.x * size.x + size.y * size.y) * .82f, .14f, .09f), "C7A66F");
            brace.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(size.y, size.x) * Mathf.Rad2Deg);
        }

        private void Bench(Transform p, Vector3 pos, float width, string color)
        {
            var g = Group(p, "Workbench", pos);
            Table(g, "Bench", Vector3.zero, V(width, 1.08f, 1.15f));
            B(g, "DrawerCase", V(-width * .25f, .53f, 0), V(width * .36f, .9f, .88f), color, true);
            for (int i = 0; i < 3; i++)
                B(g, "DrawerHandle", V(-width * .25f, .35f + i * .24f, -.48f), V(width * .18f, .06f, .06f), "A2ADB0");
            Prop(g, "Basement_Tools_Vise.prefab", "Vise", V(width * .27f, 1.2f, -.15f), 0, .43f);
            Prop(g, "Basement_Tools_Hammer.prefab", "Hammer", V(-width * .1f, 1.2f, -.25f), 40, .42f);
        }

        private void ToolCart(Transform p, Vector3 pos)
        {
            var g = Group(p, "ToolCart", pos);
            B(g, "Cabinet", V(0, .6f, 0), V(1.25f, .82f, .65f), "A74839", true);
            B(g, "Tray", V(0, 1.05f, 0), V(1.35f, .12f, .75f), "BE6151");
            for (int i = 0; i < 3; i++)
                B(g, "Drawer", V(0, .35f + i * .24f, -.34f), V(1.06f, .035f, .04f), "303D46");
            foreach (float x in new[] { -.45f, .45f })
                Sphere(g, "Wheel", V(x, .16f, -.25f), V(.23f, .23f, .16f), "293C46");
            Prop(g, "Basement_Tools_CordlessDrill.prefab", "Drill", V(.23f, 1.13f, 0), 20, .34f);
        }

        private void Locker(Transform p, Vector3 pos)
        {
            var g = Group(p, "Locker", pos);
            B(g, "Body", V(0, 1.35f, 0), V(.86f, 2.7f, .8f), "5C726A", true);
            B(g, "Door", V(0, 1.38f, -.43f), V(.72f, 2.49f, .04f), "718575");
            for (int i = 0; i < 4; i++)
                B(g, "VentSlot", V(0, 2.25f + i * .075f, -.459f), V(.4f, .025f, .018f), "394D4E");
            B(g, "NameLabel", V(0, 2, -.47f), V(.28f, .15f, .025f), "C6CAB9");
            B(g, "Pull", V(.23f, 1.25f, -.49f), V(.055f, .26f, .07f), "B5BAB3");
        }

        private void Barrel(Transform p, Vector3 pos, string color)
        {
            var g = Group(p, "Drum", pos);
            Cylinder(g, "Body", V(0, .68f, 0), V(.9f, .68f, .9f), color, true);
            foreach (float y in new[] { .13f, .68f, 1.23f })
                Cylinder(g, "Rib", V(0, y, 0), V(.94f, .035f, .94f), "738794");
        }

        private void Target(Transform p, Vector3 pos, float radius)
        {
            for (int i = 0; i < 5; i++)
            {
                float diameter = radius * 2 * (1 - i * .18f);
                var ring = Cylinder(p, "TargetRing", pos + V(0, 0, -i * .026f), V(diameter, .022f, diameter), i % 2 == 0 ? "DDD3B4" : "AA5649");
                ring.transform.localRotation = Quaternion.Euler(90, 0, 0);
            }
        }

        private void PlankBridge(Transform p, Vector3 pos, float yaw)
        {
            var g = Group(p, "RepairBridge", pos);
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            for (int i = 0; i < 5; i++)
            {
                B(g, "Plank", V(0, 0, (i - 2) * .3f), V(4.4f, .15f, .28f), i % 2 == 0 ? "BC9259" : "D0AC72", true);
                foreach (float x in new[] { -1.7f, 1.7f })
                    B(g, "Nail", V(x, .079f, (i - 2) * .3f), V(.055f, .02f, .055f), "535B60");
            }
        }

        private void Fence(Transform p, Vector3 pos, float width, float yaw)
        {
            var g = Group(p, "StorageCage", pos);
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            foreach (float x in new[] { -width * .5f, width * .5f })
                B(g, "Post", V(x, 1.15f, 0), V(.12f, 2.3f, .12f), "758794", true);
            foreach (float y in new[] { .2f, 2.15f })
                B(g, "Rail", V(0, y, 0), V(width, .1f, .1f), "758794");
            for (float x = -width * .5f + .15f; x < width * .5f; x += .3f)
                Rod(g, "MeshWire", V(x, .2f, 0), V(x, 2.15f, 0), .014f, "8B9BA0");
            for (float y = .25f; y < 2.15f; y += .3f)
                Rod(g, "MeshWire", V(-width * .5f, y, 0), V(width * .5f, y, 0), .014f, "8B9BA0");
            var collision = g.gameObject.AddComponent<BoxCollider>();
            collision.center = V(0, 1.15f, 0);
            collision.size = V(width, 2.3f, .1f);
        }

        private void Vent(Transform p, Vector3 pos, float width, float height)
        {
            B(p, "VentFrame", pos, V(width + .22f, height + .22f, .16f), "9BA9B3");
            B(p, "VentDark", pos + V(0, 0, -.1f), V(width, height, .04f), "263744");
            for (float y = -height * .45f; y < height * .5f; y += .18f)
                B(p, "VentLouvre", pos + V(0, y, -.15f), V(width - .1f, .08f, .12f), "7C919F");
        }

        private void Grate(Transform p, Vector3 pos, Vector2 size)
        {
            B(p, "GrateFrame", pos, V(size.x + .12f, .045f, size.y + .12f), "89969F");
            B(p, "GrateRecess", pos + V(0, .025f, 0), V(size.x, .02f, size.y), "273C4C");
            for (float x = -size.x * .45f; x < size.x * .5f; x += .15f)
                B(p, "GrateBar", pos + V(x, .05f, 0), V(.035f, .03f, size.y), "667D8E");
            for (float z = -size.y * .45f; z < size.y * .5f; z += .2f)
                B(p, "GrateBar", pos + V(0, .05f, z), V(size.x, .03f, .035f), "667D8E");
        }

        private void Hazard(Transform p, Vector3 pos, Vector2 size)
        {
            var g = Group(p, "SafetyMarking", pos);
            foreach (float side in new[] { -1f, 1f })
            {
                for (float z = -size.y * .5f + .15f; z < size.y * .5f; z += .5f)
                {
                    var stripe = B(g, "DiagonalStripe", V(side * size.x * .5f, 0, z), V(.46f, .014f, .19f), "E5B84E", false, false);
                    stripe.transform.localRotation = Quaternion.Euler(0, -40, 0);
                }
                B(g, "EndStripe", V(0, 0, side * size.y * .5f), V(size.x, .014f, .14f), "E5B84E", false, false);
            }
        }

        private void Pipe(Transform p, string name, string color, float radius, params Vector3[] points)
        {
            var g = Group(p, name);
            for (int i = 1; i < points.Length; i++)
            {
                Rod(g, "PipeRun", points[i - 1], points[i], radius, color);
                Sphere(g, "Elbow", points[i - 1], Vector3.one * radius * 2.05f, color);
                var delta = points[i] - points[i - 1];
                for (float t = .7f; t < delta.magnitude; t += 2.1f)
                {
                    var center = points[i - 1] + delta.normalized * t;
                    Rod(g, "Clamp", center - delta.normalized * .035f, center + delta.normalized * .035f, radius * 1.25f, "526778");
                }
            }
        }

        private void Duct(Transform p, Vector3 a, Vector3 b, float width)
        {
            var g = Group(p, "VentilationDuct");
            var run = B(g, "Duct", (a + b) * .5f, V(width, width, Vector3.Distance(a, b)), "92A5B2");
            run.transform.rotation = Quaternion.LookRotation(b - a);
            for (float t = 0; t < Vector3.Distance(a, b); t += 1.5f)
            {
                var seam = B(g, "DuctSeam", a + (b - a).normalized * t, V(width + .07f, width + .07f, .08f), "718697");
                seam.transform.rotation = Quaternion.LookRotation(b - a);
            }
        }

        private void Fixture(Transform p, string name, Vector3 pos, bool hanging)
        {
            var g = Group(p, name, pos);
            B(g, "Housing", Vector3.zero, V(.92f, .34f, .3f), "4E6172");
            B(g, "Diffuser", V(0, -.01f, -.17f), V(.72f, .2f, .055f), "FFE4AA");
            var light = Group(g, "WarmWallPool", V(0, -.12f, -.32f)).gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1, .73f, .4f);
            light.range = 3.2f;
            light.intensity = 2;
            light.shadows = LightShadows.None;
        }

        private void Pendant(Transform p, Vector3 pos)
        {
            var g = Group(p, "Pendant", pos);
            Rod(g, "Cable", V(0, .1f, 0), V(0, 3.6f, 0), .025f, "384C5B");
            var shade = Group(g, "FlaredShade").gameObject;
            shade.AddComponent<MeshFilter>().sharedMesh = PendantShade();
            shade.AddComponent<MeshRenderer>().sharedMaterial = Material("475C6B");
            Cylinder(g, "Socket", V(0, .26f, 0), V(.17f, .12f, .17f), "384C5B");
            Cylinder(g, "Rim", V(0, -.17f, 0), V(.84f, .025f, .84f), "475C6B");
            var light = Group(g, "PendantFill", V(0, -.25f, 0)).gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 5;
            light.intensity = 3;
            light.color = new Color(1, .82f, .55f);
            light.shadows = LightShadows.None;
            Cylinder(g, "Bulb", V(0, -.205f, 0), V(.73f, .022f, .73f), "FFE4AA");
        }

        private static Mesh PendantShade()
        {
            string path = $"{Folder}/TutorialPendantShade.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null)
                return mesh;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            const int sides = 16;
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2 / sides;
                float b = (i + 1) * Mathf.PI * 2 / sides;
                int first = vertices.Count;
                vertices.Add(V(Mathf.Cos(a) * .42f, -.17f, Mathf.Sin(a) * .42f));
                vertices.Add(V(Mathf.Cos(a) * .13f, .2f, Mathf.Sin(a) * .13f));
                vertices.Add(V(Mathf.Cos(b) * .13f, .2f, Mathf.Sin(b) * .13f));
                vertices.Add(V(Mathf.Cos(b) * .42f, -.17f, Mathf.Sin(b) * .42f));
                triangles.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
            }
            mesh = new Mesh { name = "TutorialPendantShade" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private GameObject Prop(Transform p, string file, string name, Vector3 pos, float yaw, float height)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Props + file);
            if (prefab == null)
                throw new FileNotFoundException(Props + file);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, p);
            go.name = name;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            // Imported prop pivots and units differ; normalize from actual renderer bounds.
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                throw new InvalidOperationException($"No renderer: {file}");
            var bounds = renderers[0].bounds;
            foreach (var r in renderers)
                bounds.Encapsulate(r.bounds);
            float extent = file.Contains("Tools_") || file.Contains("Cable")
                ? Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z) : bounds.size.y;
            go.transform.localScale *= height / Mathf.Max(.01f, extent);
            bounds = renderers[0].bounds;
            foreach (var r in renderers)
                bounds.Encapsulate(r.bounds);
            var bottom = V(bounds.center.x, bounds.min.y, bounds.center.z);
            go.transform.position += p.TransformPoint(pos) - bottom;
            return go;
        }

        private GameObject B(Transform p, string name, Vector3 pos, Vector3 size, string color, bool collision = false, bool bevel = true)
        {
            var go = Group(p, name, pos).gameObject;
            go.transform.localScale = size;
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = bevel ? beveledBox : Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            go.AddComponent<MeshRenderer>().sharedMaterial = Material(color);
            if (collision)
                go.AddComponent<BoxCollider>();
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            return go;
        }

        private GameObject Cylinder(Transform p, string name, Vector3 pos, Vector3 scale, string color, bool collision = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(p, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = Material(color);
            if (!collision)
                UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private GameObject Sphere(Transform p, string name, Vector3 pos, Vector3 scale, string color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(p, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = Material(color);
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private void Rod(Transform p, string name, Vector3 a, Vector3 b, float radius, string color)
        {
            var go = Cylinder(p, name, (a + b) * .5f, V(radius * 2, Vector3.Distance(a, b) * .5f, radius * 2), color);
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
        }

        private Material Material(string hex)
        {
            if (materials.TryGetValue(hex, out var found))
                return found;
            var path = $"{Folder}/MAT_Hideout_{hex}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = $"MAT_Hideout_{hex}" };
                AssetDatabase.CreateAsset(mat, path);
            }
            ColorUtility.TryParseHtmlString("#" + hex, out var color);
            mat.SetColor("_BaseColor", color);
            mat.SetColor("_Color", color);
            mat.SetFloat("_Smoothness", .25f);
            if (hex == "FFE4AA")
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * 1.8f);
            }
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            materials.Add(hex, mat);
            return mat;
        }

        private static Mesh BeveledBox()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (mesh != null)
                return mesh;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            float[] steps = { -.5f, -.47f, -.42f, .42f, .47f, .5f };
            foreach (var normal in new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back })
            {
                var u = Mathf.Abs(normal.y) > .5f ? Vector3.right : Vector3.up;
                var v = Vector3.Cross(normal, u);
                int first = vertices.Count;
                foreach (float y in steps)
                foreach (float x in steps)
                {
                    var point = normal * .5f + u * x + v * y;
                    var core = V(Mathf.Clamp(point.x, -.42f, .42f), Mathf.Clamp(point.y, -.42f, .42f), Mathf.Clamp(point.z, -.42f, .42f));
                    var n = (point - core).normalized;
                    vertices.Add(core + n * .08f);
                    normals.Add(n);
                }
                for (int y = 0; y < 5; y++)
                for (int x = 0; x < 5; x++)
                {
                    int a = first + y * 6 + x;
                    triangles.AddRange(new[] { a, a + 1, a + 7, a, a + 7, a + 6 });
                }
            }
            mesh = new Mesh { name = "TutorialBeveledBox" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, MeshPath);
            return mesh;
        }

        private Transform Zone(string name)
        {
            var zones = root.Find("Zones");
            if (zones == null)
                zones = Group(root, "Zones");
            return Group(zones, name);
        }
        private static Transform Group(Transform p, string name, Vector3 pos = default)
        {
            var go = new GameObject(name);
            go.transform.SetParent(p, false);
            go.transform.localPosition = pos;
            return go.transform;
        }
        private static Vector3 V(float x, float y, float z) => new(x, y, z);
    }
}
