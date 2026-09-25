using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Game.BotRuntime.Policy;
using Game.Client.Interactions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Debug = UnityEngine.Debug;

namespace Game.Training.EditorTools
{
    /// <summary>
    /// Tools > AI > Build Hiding Spot Bank: place v3 (docs/planning/place-v3-hiding-spots.md, decisions D1~D5).
    /// Run with the mansion sandbox scene open (its bot-sized NavMesh is used for reachability). Nothing in the
    /// scene is saved; carryable colliders are switched off during the build and restored afterwards (D3).
    /// </summary>
    public static class HidingSpotBankBuilder
    {
        private const string BankPath = "Assets/_Game/Content/Training/Local/Mansion_HidingSpotBank.asset";
        private const float GridSpacing = 0.25f;          // D4
        private const float PlacementReach = 2.8f;         // D5, InteractionConfig.PlacementMaxDistance
        private const float StandEye = 1.5f;               // D2
        private const float CrouchEye = 0.9f;              // D2
        private const int WatchPointCount = 400;
        private const int WatchSeed = 20260926;
        private const int MaxLayersPerColumn = 8;
        private const float SkinWidth = 0.01f;             // PhysicsPlacementValidator default
        private const float MaxSupportDistance = 0.05f;    // PhysicsPlacementValidator default
        private static readonly float[] HandHeights = { 1.2f, 0.6f, 0.3f };

        [MenuItem("Tools/AI/Build Hiding Spot Bank (open sandbox scene)")]
        public static void Build()
        {
            var total = Stopwatch.StartNew();
            var log = new StringBuilder("[Hiding Spot Bank]\n");
            var scene = EditorSceneManager.GetActiveScene();
            var spawn = GameObject.Find("BotSandbox_SpawnPoint");
            var tri = NavMesh.CalculateTriangulation();
            if (spawn == null || tri.indices == null || tri.indices.Length < 3 ||
                !NavMesh.SamplePosition(spawn.transform.position, out var spawnHit, 2f, NavMesh.AllAreas))
            {
                Debug.LogError("[Hiding Spot Bank] open the mansion sandbox scene (Tools > AI > Build Mansion Bot Sandbox) first.");
                return;
            }

            var origin = spawnHit.position;
            var items = UnityEngine.Object.FindObjectsByType<CarryableItem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (items.Length == 0)
            {
                Debug.LogError("[Hiding Spot Bank] no carryable props in the open scene.");
                return;
            }

            var half = MedianHalfExtents(items, log);
            var region = new Bounds(items[0].transform.position, Vector3.zero);
            foreach (var item in items)
            {
                region.Encapsulate(item.transform.position);
            }

            region.Expand(new Vector3(2f, 3f, 2f));
            log.AppendLine($"region {region.min:F1} .. {region.max:F1}");

            // D3: props move, so they neither support, block nor hide a spot in the bank.
            var disabled = new List<Collider>();
            foreach (var item in items)
            {
                foreach (var c in item.GetComponentsInChildren<Collider>(true))
                {
                    if (c.enabled)
                    {
                        c.enabled = false;
                        disabled.Add(c);
                    }
                }
            }

            try
            {
                Physics.SyncTransforms();
                var eyes = BuildEyes(tri, origin, log);
                var spots = FindSpots(region, half, origin, log);
                ScoreSpots(spots, half, eyes, log);
                Save(scene.name, half, eyes, spots, log);
            }
            finally
            {
                foreach (var c in disabled)
                {
                    c.enabled = true;
                }

                Physics.SyncTransforms();
            }

            log.AppendLine($"total {total.Elapsed.TotalSeconds:F1} s; scene dirty {scene.isDirty}");
            Debug.Log(log.ToString());
        }

        // ------------------------------------------------------------------ sizes

        private static Vector3[] MedianHalfExtents(CarryableItem[] items, StringBuilder log)
        {
            var bySize = new[] { new List<Vector3>(), new List<Vector3>(), new List<Vector3>() };
            foreach (var item in items)
            {
                var colliders = item.GetComponentsInChildren<Collider>(true);
                if (colliders.Length == 0)
                {
                    continue;
                }

                var b = colliders[0].bounds;
                foreach (var c in colliders)
                {
                    b.Encapsulate(c.bounds);
                }

                var s = b.size;
                var cls = (int)PickObservationEncoder.ClassifySize(Mathf.Max(s.x, s.y, s.z));
                // Footprint sorted so x >= z: boxes are axis-aligned in the bank; orientation is not searched.
                bySize[cls].Add(new Vector3(Mathf.Max(s.x, s.z), s.y, Mathf.Min(s.x, s.z)));
            }

            var half = new Vector3[3];
            for (var c = 0; c < 3; c++)
            {
                var list = bySize[c];
                if (list.Count == 0)
                {
                    half[c] = Vector3.one * (c == 0 ? 0.1f : c == 1 ? 0.25f : 0.5f);
                    continue;
                }

                float Median(Func<Vector3, float> f)
                {
                    var v = list.Select(f).OrderBy(x => x).ToList();
                    return v[v.Count / 2];
                }

                half[c] = new Vector3(
                    Mathf.Max(0.05f, Median(v => v.x) * 0.5f),
                    Mathf.Max(0.05f, Median(v => v.y) * 0.5f),
                    Mathf.Max(0.05f, Median(v => v.z) * 0.5f));
                log.AppendLine($"size {(PickSizeClass)c}: {list.Count} props, median box {half[c] * 2f:F2} m");
            }

            return half;
        }

        // ------------------------------------------------------------------ seekers

        private static List<Vector3> BuildEyes(NavMeshTriangulation tri, Vector3 origin, StringBuilder log)
        {
            // Seekers stand where the bot can walk (the start island), not on roofs or in the garden.
            var triangles = tri.indices.Length / 3;
            var cdf = new float[triangles];
            var total = 0f;
            var path = new NavMeshPath();
            for (var t = 0; t < triangles; t++)
            {
                var a = tri.vertices[tri.indices[t * 3]];
                var b = tri.vertices[tri.indices[t * 3 + 1]];
                var c = tri.vertices[tri.indices[t * 3 + 2]];
                if (NavMesh.CalculatePath(origin, (a + b + c) / 3f, NavMesh.AllAreas, path) &&
                    path.status == NavMeshPathStatus.PathComplete)
                {
                    total += Vector3.Cross(b - a, c - a).magnitude * 0.5f;
                }

                cdf[t] = total;
            }

            var rng = new System.Random(WatchSeed);
            var eyes = new List<Vector3>(WatchPointCount * 2);
            for (var i = 0; i < WatchPointCount; i++)
            {
                var target = (float)rng.NextDouble() * total;
                var t = Array.BinarySearch(cdf, target);
                if (t < 0) t = ~t;
                t = Mathf.Clamp(t, 0, triangles - 1);
                var a = tri.vertices[tri.indices[t * 3]];
                var b = tri.vertices[tri.indices[t * 3 + 1]];
                var c = tri.vertices[tri.indices[t * 3 + 2]];
                var r1 = (float)rng.NextDouble();
                var r2 = (float)rng.NextDouble();
                if (r1 + r2 > 1f)
                {
                    r1 = 1f - r1;
                    r2 = 1f - r2;
                }

                var p = a + r1 * (b - a) + r2 * (c - a);
                eyes.Add(p + Vector3.up * StandEye);
                eyes.Add(p + Vector3.up * CrouchEye);
            }

            log.AppendLine($"seeker eyes: {WatchPointCount} points on the reachable NavMesh ({total:F0} m2) x heights {StandEye}/{CrouchEye} m");
            return eyes;
        }

        // ------------------------------------------------------------------ spots

        private static List<HidingSpot> FindSpots(Bounds region, Vector3[] half, Vector3 origin, StringBuilder log)
        {
            var timer = Stopwatch.StartNew();
            var spots = new List<HidingSpot>();
            var islandCache = new Dictionary<Vector3Int, bool>();
            var path = new NavMeshPath();
            int columns = 0, surfaces = 0, fitNone = 0, noStand = 0;
            var overlap = new Collider[8];
            var mask = Physics.DefaultRaycastLayers;

            for (var x = region.min.x; x <= region.max.x; x += GridSpacing)
            {
                for (var z = region.min.z; z <= region.max.z; z += GridSpacing)
                {
                    columns++;
                    var from = new Vector3(x, region.max.y, z);
                    for (var layer = 0; layer < MaxLayersPerColumn; layer++)
                    {
                        var remaining = from.y - region.min.y;
                        if (remaining <= 0f ||
                            !Physics.Raycast(from, Vector3.down, out var hit, remaining, mask, QueryTriggerInteraction.Ignore))
                        {
                            break;
                        }

                        from = hit.point + Vector3.down * 0.02f;
                        if (hit.normal.y < 0.9f)
                        {
                            continue;
                        }

                        surfaces++;
                        byte sizeMask = 0;
                        for (var c = 0; c < 3; c++)
                        {
                            if (Fits(hit.point, half[c], overlap, mask))
                            {
                                sizeMask |= (byte)(1 << c);
                            }
                        }

                        if (sizeMask == 0)
                        {
                            fitNone++;
                            continue;
                        }

                        var smallest = half[LowestSize(sizeMask)];
                        if (!TryFindStand(hit.point, smallest, origin, islandCache, path, out var stand))
                        {
                            noStand++;
                            continue;
                        }

                        spots.Add(new HidingSpot
                        {
                            Position = hit.point,
                            StandPosition = stand,
                            SizeMask = sizeMask,
                            Tags = Tag(hit.point, smallest, stand),
                            ExposureSmall = -1f,
                            ExposureMedium = -1f,
                            ExposureLarge = -1f,
                        });
                    }
                }
            }

            log.AppendLine($"columns {columns} (grid {GridSpacing} m), upward surfaces {surfaces}, no size fits {fitNone}, bot cannot reach {noStand}, spots {spots.Count} ({timer.Elapsed.TotalSeconds:F1} s)");
            for (var c = 0; c < 3; c++)
            {
                log.AppendLine($"  fits {(PickSizeClass)c}: {spots.Count(s => s.Fits(c))}");
            }

            return spots;
        }

        private static int LowestSize(byte mask) => (mask & 1) != 0 ? 0 : (mask & 2) != 0 ? 1 : 2;

        /// <summary>Same test as PhysicsPlacementValidator: box free of blockers, support within 5 cm below.</summary>
        private static bool Fits(Vector3 surface, Vector3 half, Collider[] buffer, int mask)
        {
            var centre = surface + Vector3.up * (half.y + SkinWidth);
            var count = Physics.OverlapBoxNonAlloc(centre, half - Vector3.one * SkinWidth, buffer, Quaternion.identity, mask, QueryTriggerInteraction.Ignore);
            if (count > 0)
            {
                return false;
            }

            return Physics.Raycast(centre, Vector3.down, half.y + SkinWidth + MaxSupportDistance, mask, QueryTriggerInteraction.Ignore);
        }

        private static bool TryFindStand(Vector3 surface, Vector3 half, Vector3 origin,
            Dictionary<Vector3Int, bool> islandCache, NavMeshPath path, out Vector3 stand)
        {
            stand = default;
            var centre = surface + Vector3.up * half.y;
            for (var ring = 0; ring < 3; ring++)
            {
                var steps = ring == 0 ? 1 : 8;
                for (var k = 0; k < steps; k++)
                {
                    var probe = surface;
                    if (ring > 0)
                    {
                        var yaw = k * 45f * Mathf.Deg2Rad;
                        probe += new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw)) * ring;
                    }

                    if (!NavMesh.SamplePosition(probe, out var nav, 1.5f, NavMesh.AllAreas))
                    {
                        continue;
                    }

                    var p = nav.position;
                    var flat = new Vector2(p.x - surface.x, p.z - surface.z).magnitude;
                    if (flat > PlacementReach || Mathf.Abs(surface.y - p.y) > 2.2f)
                    {
                        continue;
                    }

                    // Line of sight from a hand at some height (standing, crouched, prone) to the prop.
                    var seen = false;
                    foreach (var h in HandHeights)
                    {
                        var hand = p + Vector3.up * h;
                        if (Vector3.Distance(hand, centre) <= PlacementReach &&
                            !Physics.Linecast(hand, centre, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                        {
                            seen = true;
                            break;
                        }
                    }

                    if (!seen)
                    {
                        continue;
                    }

                    var key = Vector3Int.RoundToInt(p * 2f);
                    if (!islandCache.TryGetValue(key, out var onIsland))
                    {
                        onIsland = NavMesh.CalculatePath(origin, p, NavMesh.AllAreas, path) &&
                                   path.status == NavMeshPathStatus.PathComplete;
                        islandCache[key] = onIsland;
                    }

                    if (onIsland)
                    {
                        stand = p;
                        return true;
                    }
                }
            }

            return false;
        }

        private static HidingSpotTags Tag(Vector3 surface, Vector3 half, Vector3 stand)
        {
            var tags = HidingSpotTags.None;
            var mask = Physics.DefaultRaycastLayers;
            if (Physics.Raycast(surface + Vector3.up * 0.05f, Vector3.up, 1.2f, mask, QueryTriggerInteraction.Ignore))
            {
                tags |= HidingSpotTags.Under;
            }

            if (surface.y - stand.y > 0.3f)
            {
                tags |= HidingSpotTags.OnFurniture;
            }

            var centre = surface + Vector3.up * half.y;
            var blocked = 0;
            for (var k = 0; k < 4; k++)
            {
                var dir = Quaternion.Euler(0f, k * 90f, 0f) * Vector3.forward;
                if (Physics.Raycast(centre, dir, 1f + half.x, mask, QueryTriggerInteraction.Ignore))
                {
                    blocked++;
                }
            }

            if (blocked >= 3)
            {
                tags |= HidingSpotTags.Corner;
            }

            return tags;
        }

        // ------------------------------------------------------------------ score

        private static void ScoreSpots(List<HidingSpot> spots, Vector3[] half, List<Vector3> eyes, StringBuilder log)
        {
            var timer = Stopwatch.StartNew();
            var boxes = new List<(Vector3, Vector3)>();
            var owner = new List<(int spot, int size)>();
            for (var i = 0; i < spots.Count; i++)
            {
                for (var c = 0; c < 3; c++)
                {
                    if (spots[i].Fits(c))
                    {
                        boxes.Add((spots[i].Position + Vector3.up * SkinWidth, half[c]));
                        owner.Add((i, c));
                    }
                }
            }

            var exposures = HideScoreV3.Exposures(boxes, eyes, Physics.DefaultRaycastLayers);
            for (var k = 0; k < owner.Count; k++)
            {
                var s = spots[owner[k].spot];
                switch (owner[k].size)
                {
                    case 0: s.ExposureSmall = exposures[k]; break;
                    case 1: s.ExposureMedium = exposures[k]; break;
                    default: s.ExposureLarge = exposures[k]; break;
                }

                spots[owner[k].spot] = s;
            }

            log.AppendLine($"scored {boxes.Count} (spot, size) boxes against {eyes.Count} eyes ({timer.Elapsed.TotalSeconds:F1} s)");
        }

        private static void Save(string sceneName, Vector3[] half, List<Vector3> eyes, List<HidingSpot> spots, StringBuilder log)
        {
            // Scale so that the median medium-size exposure maps to hide 0.5.
            var medium = spots.Where(s => s.Fits(1)).Select(s => s.ExposureMedium).OrderBy(e => e).ToList();
            var scale = medium.Count > 0 && medium[medium.Count / 2] > 0f ? medium[medium.Count / 2] / Mathf.Log(2f) : 1f;

            var bank = ScriptableObject.CreateInstance<HidingSpotBank>();
            bank.SceneName = sceneName;
            bank.BuiltAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            bank.GridSpacing = GridSpacing;
            bank.ExposureScale = scale;
            bank.HalfExtents = half;
            bank.Eyes = eyes;
            bank.Spots = spots;
            AssetDatabase.DeleteAsset(BankPath);
            AssetDatabase.CreateAsset(bank, BankPath);
            AssetDatabase.SaveAssets();
            log.AppendLine($"exposure scale {scale:F2} (median medium exposure -> hide 0.5); saved {BankPath}");

            // Distribution per tag (medium size), to check the score before any learning.
            string Pct(List<float> v)
            {
                if (v.Count == 0) return "-";
                v.Sort();
                return $"n {v.Count}, hide p10 {bank.Hide(v[(int)(v.Count * 0.9f)]):F2} / p50 {bank.Hide(v[v.Count / 2]):F2} / p90 {bank.Hide(v[(int)(v.Count * 0.1f)]):F2}";
            }

            var withMedium = spots.Where(s => s.Fits(1)).ToList();
            log.AppendLine("hide (medium) by tag:");
            log.AppendLine("  floor-open  " + Pct(withMedium.Where(s => s.Tags == HidingSpotTags.None).Select(s => s.ExposureMedium).ToList()));
            log.AppendLine("  under       " + Pct(withMedium.Where(s => (s.Tags & HidingSpotTags.Under) != 0).Select(s => s.ExposureMedium).ToList()));
            log.AppendLine("  on-furniture " + Pct(withMedium.Where(s => (s.Tags & HidingSpotTags.OnFurniture) != 0).Select(s => s.ExposureMedium).ToList()));
            log.AppendLine("  corner      " + Pct(withMedium.Where(s => (s.Tags & HidingSpotTags.Corner) != 0).Select(s => s.ExposureMedium).ToList()));
            log.AppendLine($"tag counts: under {spots.Count(s => (s.Tags & HidingSpotTags.Under) != 0)}, on-furniture {spots.Count(s => (s.Tags & HidingSpotTags.OnFurniture) != 0)}, corner {spots.Count(s => (s.Tags & HidingSpotTags.Corner) != 0)}, floor-open {spots.Count(s => s.Tags == HidingSpotTags.None)}");
        }
    }
}
