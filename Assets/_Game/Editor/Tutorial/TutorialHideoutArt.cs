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
        private const string LobbySkyboxPath = "Assets/PolyWorkshop_BasementWorkshop/Modular/Materials/Skybox1_Material.mat";
        private const string LobbyPostProfilePath = "Assets/_Game/Content/Lighting/LobbyPostProcess.asset";
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
            RouteGuides();
            Workshop();
            Disposal();
            Lighting();
            ApplyLobbySurfaces(root);
        }

        internal static void ApplyLobbySurfaces(Transform root)
        {
            const string source = "Assets/PolyWorkshop_BasementWorkshop/Modular/Materials/";
            const string meshPath = Folder + "/TutorialSurfaceMeshes.asset";
            var meshes = AssetDatabase.LoadAllAssetsAtPath(meshPath).OfType<Mesh>()
                .ToDictionary(m => m.name);
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var materialName = filter.name switch
                {
                    "SolidWall" or "UpperWall" or "Lintel" => "Basement_BrickWall_Material",
                    "WallPier" or "WallCap" or "BaseRail" or "CeilingJoist" => "Basement_WoodenPillar_Material",
                    "Ceiling" => "Basement_WoodenCeiling_Material",
                    "TunnelSide" or "LowLintel" or "PitFace" => "Basement_Concrete_Material",
                    _ => null
                };
                if (filter.name.StartsWith("ConcreteSlabs_"))
                {
                    filter.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                        source + "Basement_Concrete_Material.mat");
                    continue;
                }
                if (materialName == null) continue;
                filter.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(source + materialName + ".mat");
                var size = filter.transform.localScale;
                var key = FormattableString.Invariant($"Surface_{filter.sharedMesh.vertexCount}_{size.x:F3}_{size.y:F3}_{size.z:F3}");
                if (!meshes.TryGetValue(key, out var mesh))
                {
                    mesh = UnityEngine.Object.Instantiate(filter.sharedMesh);
                    mesh.name = key;
                    var vertices = mesh.vertices;
                    var normals = mesh.normals;
                    var uv = new Vector2[vertices.Length];
                    for (var i = 0; i < vertices.Length; i++)
                    {
                        var v = Vector3.Scale(vertices[i], size);
                        var n = normals[i];
                        uv[i] = Mathf.Abs(n.y) > .7f ? new Vector2(v.x, v.z) / 3f
                            : Mathf.Abs(n.x) > .7f ? new Vector2(v.z, v.y) / 3f
                            : new Vector2(v.x, v.y) / 3f;
                    }
                    mesh.uv = uv;
                    if (meshes.Count == 0) AssetDatabase.CreateAsset(mesh, meshPath);
                    else AssetDatabase.AddObjectToAsset(mesh, meshPath);
                    meshes.Add(key, mesh);
                }
                filter.sharedMesh = mesh;
            }
        }

        private void Architecture()
        {
            var p = Group(root, "Architecture");
            // Three separate slabs leave a real 2m gap through both floor and foundation.
            foreach (var slab in new[] { V(-4, 0, 0), V(20, 0, 0), V(16, 0, -5) })
            {
                var size = slab.x == -4 ? V(38, .6f, 30) : slab.x == 20 ? V(6, .6f, 30) : V(2, .6f, 20);
                B(p, "FloorSlab", slab + V(0, -.21f, 0), size, "898C8B", true, false);
            }
            B(p, "PitBottom", V(16, -5, 10), V(2, .2f, 10), "243344", true, false);
            foreach (float x in new[] { 14.96f, 17.04f })
                B(p, "PitFace", V(x, -2.4f, 10), V(.08f, 4.8f, 10), "435365", false, false);
            StoneFloor(p);
            upperWalls = Group(p, "UpperWalls");
            Wall(p, "North", V(-23, 0, 15), V(23, 0, 15));
            Wall(p, "South", V(-23, 0, -15), V(23, 0, -15));
            Wall(p, "West", V(-23, 0, -15), V(-23, 0, 15));
            Wall(p, "East", V(23, 0, -15), V(23, 0, 15));
            Wall(p, "NorthRooms", V(-23, 0, 5), V(23, 0, 5), 43);
            Wall(p, "SouthRooms", V(-23, 0, -3.5f), V(23, 0, -3.5f), 4, 43);
            Wall(p, "BriefingWalk", V(-10, 0, 5), V(-10, 0, 15), 5);
            Wall(p, "WalkSprint", V(0, 0, 5), V(0, 0, 15), 5);
            Wall(p, "SprintJump", V(12, 0, 5), V(12, 0, 15), 5);
            Wall(p, "PickupDrop", V(7, 0, -15), V(7, 0, -3.5f), 5);
            Wall(p, "ThrowPlacement", V(-13, 0, -15), V(-13, 0, -3.5f), 5);
            Wall(p, "CoreWest", V(-15, 0, -3.5f), V(-15, 0, 5));
            Wall(p, "CoreEast", V(17, 0, -3.5f), V(17, 0, 5));
            B(p, "SealedServiceCore", V(1, 1.7f, .75f), V(31.5f, 3.2f, 8), "35495E", true, false);
            Vent(p, V(-1, 2, -3.86f), 3.5f, 2);
            // Former corridor entrances remain visibly shut, backed by a continuous wall.
            foreach (float x in new[] { -14f, -3f, 10f })
            {
                var closed = Group(p, "ClosedServiceDoor", V(x, 0, 5.39f));
                B(closed, "Panel", V(0, 1.7f, 0), V(2.5f, 3.2f, .12f), "4F5D6C", true);
                B(closed, "LockBar", V(0, 1.5f, .12f), V(2.6f, .12f, .12f), "B25F42");
            }

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
                    float dx = x == 0 || x == 19 || x == 20 || x == 23 ? 0 : (float)seed.NextDouble() * .95f - .475f;
                    float dz = z == 0 || z == 10 || z == 15 ? 0 : (float)seed.NextDouble() * .95f - .475f;
                    corners[x, z] = V(-23 + x * 2 + dx, .11f, -15 + z * 2 + dz);
                }
            var vertices = Enumerable.Range(0, 5).Select(_ => new List<Vector3>()).ToArray();
            var indices = Enumerable.Range(0, 5).Select(_ => new List<int>()).ToArray();
            for (int x = 0; x < 23; x++)
                for (int z = 0; z < 15; z++)
                {
                    if (x == 19 && z >= 10)
                        continue;
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
            var desk = Group(p, "CommandStation", V(-12.2f, .12f, 12.9f));
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
            Group(p, "WalkRunLane", V(-5, 0, 10));
            Group(p, "JumpLane", V(16, 0, 10));
            var checkpoint = Group(p, "JumpCheckpoint", V(13.6f, .2f, 10));
            checkpoint.localRotation = Quaternion.Euler(0, 90, 0);
            Rack(p, V(-5, .12f, 13.8f), 5.8f);
            Barrel(p, V(-8.5f, .12f, 13.4f), "427B9E");
            for (int i = 0; i < 4; i++)
                Prop(p, "Basement_RoadCone.prefab", "WalkingCone", V(-8 + i * 1.8f, .12f, i % 2 == 0 ? 8.2f : 11.8f), 0, .7f);
            Rack(p, V(5, .12f, 13.8f), 5);
            Pipe(p, "SprintPipe", "A9533F", .12f, V(1, 2.8f, 14.3f), V(10.5f, 2.8f, 14.3f), V(10.5f, 1, 14.3f));
            foreach (float x in new[] { 14.65f, 17.35f })
                Hazard(p, V(x, .13f, 10), new Vector2(.25f, 8.8f));
            Crate(p, V(20.7f, .12f, 13.4f), V(1.4f, 1.4f, 1.4f), 0);
            Pendant(p, V(-5, 3.7f, 10));
            Pendant(p, V(6, 3.7f, 10));
            Pendant(p, V(20, 3.7f, 10));
            var crawl = Group(p, "CrouchCrawlLane");
            // Solid bulkheads extend to the ceiling: neither side-stepping nor jumping over bypasses the lesson.
            PosturePassage(crawl, "05_Crouch", 2.9f, 1.6f);
            PosturePassage(crawl, "06_Prone", -1.3f, .78f);
            Duct(p, V(22.4f, 3.6f, 4.5f), V(22.4f, 3.6f, -3), .65f);
        }

        private void PosturePassage(Transform parent, string name, float z, float openingHeight)
        {
            var p = Group(parent, name, V(20, 0, z));
            foreach (float x in new[] { -2f, 2f })
                  B(p, "TunnelSide", V(x, 3.65f, 0), V(2f, 7.3f, 3f), "435B71", true, false);
            B(p, "LowLintel", V(0, (7.3f + openingHeight) * .5f, 0),
                  V(2.1f, 7.3f - openingHeight, 3f), "7E909E", true, false);
            B(p, "PurpleClearanceStrip", V(0, openingHeight + .04f, 1.53f), V(1.95f, .08f, .08f), "975ABC");
            Hazard(p, V(0, .13f, 0), new Vector2(1.8f, 3f));
            Group(p, "Entrance", V(0, .2f, 1.8f));
            Group(p, "Exit", V(0, .2f, -2f));
        }

        internal void RepairPosturePassages()
        {
            beveledBox = BeveledBox();
            var parent = root.Find("Zones/02_Movement/CrouchCrawlLane");
            foreach (var child in parent.Cast<Transform>().ToArray())
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            PosturePassage(parent, "05_Crouch", 2.9f, 1.6f);
            PosturePassage(parent, "06_Prone", -1.3f, .78f);
            ApplyLobbySurfaces(root);
        }

        private void RouteGuides()
        {
            var p = Group(root, "RouteGuides");
            var points = new[] { V(-15.8f,.14f,8.6f), V(-5,.14f,10), V(6,.14f,10), V(13.7f,.14f,10),
                V(20,.14f,3.7f), V(20,.14f,.4f), V(18,.14f,-9), V(3,.14f,-10), V(-6,.14f,-10),
                V(-18,.14f,-10), V(-19,.14f,-2), V(-19,.14f,2.4f) };
            for (int i = 0; i < points.Length; i++)
            {
                var marker = Group(p, $"Stage{i + 1:00}", points[i]);
                var text = Group(marker, "Number", V(0, .015f, 0)).gameObject.AddComponent<TextMesh>();
                text.text = $"{i + 1:00}";
                text.fontSize = 64;
                text.characterSize = .12f;
                text.anchor = TextAnchor.MiddleCenter;
                text.color = new Color(1, .77f, .35f);
                text.transform.localRotation = Quaternion.Euler(90, 0, 0);
                float yaw = i < 4 ? 90 : i < 6 ? 180 : i < 9 ? -90 : 0;
                var arrow = Group(marker, "Direction", V(0, 0, -1.2f));
                arrow.localRotation = Quaternion.Euler(0, yaw, 0);
                B(arrow, "Stem", V(0, 0, 0), V(.2f, .015f, 1), "975ABC", false, false);
                foreach (float side in new[] { -1f, 1f })
                {
                    var tip = B(arrow, "Head", V(side * .18f, 0, .43f), V(.18f, .015f, .6f), "975ABC", false, false);
                    tip.transform.localRotation = Quaternion.Euler(0, side * -40, 0);
                }
            }
        }

        private void Workshop()
        {
            var p = Zone("03_Items");
            var pickup = Group(p, "PickupDrop", V(18, 0, -10));
            Group(pickup, "TrainingItemSpawn", V(0, 1.4f, 0));
            Bench(p, V(18, .12f, -10), 3.4f, "667879");
            Hazard(p, V(18, .13f, -10), new Vector2(4.8f, 3.5f));
            Rack(p, V(14, .12f, -13.7f), 6);
            Cardboard(p, V(21.5f, .12f, -12.5f), 1.1f, 10);
            Group(p, "DropRecovery", V(3, 1, -10));
            Hazard(p, V(3, .13f, -10), new Vector2(3.8f, 3.5f));
            var throwing = Group(p, "Throw", V(-6, 0, -10));
            Group(throwing, "Recovery", V(0, 1, 0));
            B(throwing, "TargetBackboard", V(0, 1.75f, 4.6f), V(4.6f, 3.1f, .2f), "75664E", true);
            Target(throwing, V(0, 2.15f, 4.45f), 1.15f);
            for (int i = -1; i <= 1; i++)
                B(throwing, "CrashPad", V(i * 1.35f, .46f, 3), V(1.3f, .64f, 1.6f), "4E78AE", true);
            var placement = Group(p, "Placement", V(-18, 0, -10));
            Group(placement, "Recovery", V(0, 1, -1.8f));
            Bench(placement, V(0, .12f, 0), 3.8f, "777D76");
            Hazard(placement, V(0, .13f, 0), new Vector2(4.8f, 3.5f));
            Rack(p, V(-21.5f, .12f, -12.5f), 3, 90);
            Pipe(p, "WorkshopPipe", "B26445", .14f, V(-11, 1, -4.2f), V(-11, 3, -4.2f), V(5, 3, -4.2f), V(5, 1, -4.2f));
            for (int i = 0; i < 3; i++)
                Locker(p, V(10 + i, .12f, -4.5f));
            foreach (float x in new[] { 18f, 3f, -6f, -18f })
                Pendant(p, V(x, 3.7f, -10));
        }

        private void Disposal()
        {
            var p = Zone("04_Shredder");
            Group(p, "ShredderOrigin", V(-21, .12f, .5f));
            Group(p, "Recovery", V(-17.7f, 1, -1.5f));
            Hazard(p, V(-21, .13f, .5f), new Vector2(4.5f, 4.5f));
            Pipe(p, "DisposalRedPipe", "B56041", .15f, V(-22.5f, 2.9f, -2.8f), V(-22.5f, 2.9f, 3.8f));
            var exit = Zone("05_Exit");
            var door = Group(exit, "ExitDoor", V(-19, 0, 4.53f));
            B(door, "Frame", V(0, 1.8f, 0), V(2.8f, 3.6f, .2f), "D7A553");
            B(door, "Panel", V(0, 1.7f, -.12f), V(2.4f, 3.18f, .16f), "59626A", true);
            B(door, "Handle", V(.75f, 1.5f, -.3f), V(.32f, .07f, .12f), "D8CCAA");
            Fixture(exit, "ExitLamp", V(-19, 3.9f, 4.3f), false);
            Group(exit, "TutorialComplete", V(-19, .2f, 2.8f));
        }

        private void Lighting()
        {
            var p = Group(root, "Lighting");
            ApplyLobbyLook(p);
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

        internal static void ApplyLobbyLook(Transform lightingRoot)
        {
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(.38f, .443f, .5f);
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = .03f;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientSkyColor = new Color(.212f, .227f, .259f);
            RenderSettings.ambientEquatorColor = new Color(.114f, .125f, .133f);
            RenderSettings.ambientGroundColor = new Color(.047f, .043f, .035f);
            RenderSettings.ambientIntensity = 1.5f;
            RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>(LobbySkyboxPath);

            var volumeTransform = lightingRoot.Find("Lobby Post Volume");
            if (volumeTransform == null)
            {
                volumeTransform = new GameObject("Lobby Post Volume").transform;
                volumeTransform.SetParent(lightingRoot, false);
            }

            var volume = volumeTransform.GetComponent<Volume>();
            if (volume == null)
                volume = volumeTransform.gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(LobbyPostProfilePath);
        }

        private void Sofa(Transform p, Vector3 pos, float yaw, int seats)
        {
            var group = Group(p, "BriefingSeats", pos);
            group.localRotation = Quaternion.Euler(0, yaw, 0);
            for (var i = 0; i < seats; i++)
                Prop(group, "Basement_OfficeChair.prefab", "BriefingChair", V((i - (seats - 1) * .5f) * 1.45f, 0, 0), 180, 1.6f);
        }

        private void Table(Transform p, string name, Vector3 pos, Vector3 size)
        {
            var group = Group(p, name, pos);
            var rotation = group.rotation;
            group.rotation = Quaternion.identity;
            var desk = Prop(group, "Basement_Desk.prefab", "LobbyDesk", Vector3.zero, 0, size.y + .09f);
            foreach (var collider in desk.GetComponentsInChildren<Collider>())
                UnityEngine.Object.DestroyImmediate(collider);
            var renderers = desk.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            desk.transform.localScale = Vector3.Scale(desk.transform.localScale,
                V(size.x / bounds.size.x, 1, size.z / bounds.size.z));
            bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            desk.transform.position += group.position - V(bounds.center.x, bounds.min.y, bounds.center.z);
            // This lobby desk has a lower side shelf. Only the actual highest
            // tabletop is a placement target; do not bridge its empty space.
            var topBounds = new Bounds();
            var hasTop = false;
            foreach (var filter in desk.GetComponentsInChildren<MeshFilter>())
            {
                filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
                foreach (var vertex in filter.sharedMesh.vertices)
                {
                    var point = group.InverseTransformPoint(filter.transform.TransformPoint(vertex));
                    if (point.y < size.y + .07f) continue;
                    if (!hasTop) { topBounds = new Bounds(point, Vector3.zero); hasTop = true; }
                    else topBounds.Encapsulate(point);
                }
            }
            if (!hasTop) throw new InvalidOperationException("Lobby desk tabletop vertices were not found.");
            group.rotation = rotation;
            var top = Group(group, "Top", V(topBounds.center.x, size.y, topBounds.center.z)).gameObject.AddComponent<BoxCollider>();
            top.size = V(topBounds.size.x, .18f, topBounds.size.z);
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
            var group = Group(p, "WorkshopStorage", pos);
            group.localRotation = Quaternion.Euler(0, yaw, 0);
            var count = Mathf.Max(1, Mathf.FloorToInt(width / 1.25f));
            for (var i = 0; i < count; i++)
                Prop(group, "Basement_Locker.prefab", "LobbyStorage", V((i - (count - 1) * .5f) * 1.25f, 0, 0), 0, 2.8f);
        }

        private void Cardboard(Transform p, Vector3 pos, float size, float yaw)
        {
            Prop(p, "Basement_CardboardBox1.prefab", "CardboardBox", pos, yaw, size * .86f);
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
            var top = g.Find("Bench/Top").GetComponent<BoxCollider>();
            var center = top.transform.localPosition;
            Prop(g, "Basement_Tools_Vise.prefab", "Vise", center + V(top.size.x * .36f, .12f, -.15f), 0, .43f);
            Prop(g, "Basement_Tools_Hammer.prefab", "Hammer", center + V(-top.size.x * .36f, .12f, -.25f), 40, .42f);
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
            Prop(p, "Basement_Locker.prefab", "Locker", pos, 0, 2.7f);
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
            FillSafetyMarking(g, size);
        }

        internal void RepairSafetyMarkings()
        {
            foreach (var group in root.GetComponentsInChildren<Transform>().Where(t => t.name == "SafetyMarking").ToArray())
            {
                var end = group.Find("EndStripe");
                var size = new Vector2(end.localScale.x, Mathf.Abs(end.localPosition.z) * 2f);
                foreach (Transform child in group.Cast<Transform>().ToArray())
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                FillSafetyMarking(group, size);
            }
        }

        private void FillSafetyMarking(Transform g, Vector2 size)
        {
            foreach (float side in new[] { -1f, 1f })
            {
                B(g, "SideStripe", V(side * (size.x * .5f - .07f), 0, 0), V(.14f, .014f, Mathf.Max(.01f, size.y - .14f)), "E5B84E", false, false);
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
