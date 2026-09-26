using System;
using System.Collections.Generic;
using Game.Training.HideSeek;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Training.Thief
{
    /// <summary>
    /// Shared map knowledge for the thief-NPC simulator (docs/planning/thief-npc-v2.md): hiding spot bank, search
    /// waypoints with terrain summaries, hide candidates, and the player-like approach rule (same rules as the
    /// hide-seek v1.1 simulator). Plain C#; one instance per ThiefArena.
    /// </summary>
    public sealed class ThiefWorld
    {
        public readonly HidingSpotBank Bank;
        public readonly int SizeClass;
        public readonly Vector3 ObjectHalf;
        public readonly int OccluderMask = Physics.DefaultRaycastLayers;
        public readonly List<Vector3> Waypoints = new();
        public int[] WaypointUnder, WaypointOnFurniture, WaypointCorner, WaypointFloor;

        /// <summary>
        /// Spaces worth checking (bank spots under furniture, on furniture or in a corner; open floor is seen anyway),
        /// for the coverage record and observation (thief-npc-v2.md 11). Index = position in this list.
        /// </summary>
        public readonly List<int> CheckSpots = new();
        public List<int>[] WaypointCheckSpots;
        private readonly Dictionary<long, List<int>> checkGrid = new();
        private const float CheckCell = 4f;

        private readonly List<int> sizeSpots = new();
        private readonly NavMeshPath path = new();

        public ThiefWorld(HidingSpotBank bank, int sizeClass, float waypointSpacing, int waypointSeed)
        {
            Bank = bank;
            SizeClass = Mathf.Clamp(sizeClass, 0, 2);
            ObjectHalf = bank.HalfExtents[SizeClass];
            for (var i = 0; i < bank.Spots.Count; i++)
            {
                if (bank.Spots[i].Fits(SizeClass))
                {
                    sizeSpots.Add(i);
                }
            }

            BuildWaypoints(waypointSpacing, waypointSeed);
            BuildCheckSpots();
        }

        private static long Cell(float x, float z) => ((long)Mathf.FloorToInt(x / CheckCell) << 32) ^ (uint)Mathf.FloorToInt(z / CheckCell);

        private void BuildCheckSpots()
        {
            foreach (var s in sizeSpots)
            {
                if (Bank.Spots[s].Tags == HidingSpotTags.None) continue;
                var c = CheckSpots.Count;
                CheckSpots.Add(s);
                var p = Bank.Spots[s].Position;
                var key = Cell(p.x, p.z);
                if (!checkGrid.TryGetValue(key, out var list)) checkGrid[key] = list = new List<int>();
                list.Add(c);
            }

            WaypointCheckSpots = new List<int>[Waypoints.Count];
            for (var w = 0; w < Waypoints.Count; w++)
            {
                WaypointCheckSpots[w] = new List<int>();
                NearbyCheckSpots(Waypoints[w], 3f, WaypointCheckSpots[w]);
            }
        }

        /// <summary>Check-spot indices within <paramref name="radius"/> m (3D) of a point.</summary>
        public void NearbyCheckSpots(Vector3 at, float radius, List<int> into)
        {
            into.Clear();
            var r2 = radius * radius;
            var cells = Mathf.CeilToInt(radius / CheckCell);
            var cx = Mathf.FloorToInt(at.x / CheckCell);
            var cz = Mathf.FloorToInt(at.z / CheckCell);
            for (var dx = -cells; dx <= cells; dx++)
            {
                for (var dz = -cells; dz <= cells; dz++)
                {
                    if (!checkGrid.TryGetValue(((long)(cx + dx) << 32) ^ (uint)(cz + dz), out var list)) continue;
                    foreach (var c in list)
                    {
                        if ((Bank.Spots[CheckSpots[c]].Position - at).sqrMagnitude <= r2) into.Add(c);
                    }
                }
            }
        }

        public int SpotCount => sizeSpots.Count;

        // ------------------------------------------------------------------ waypoints

        private void BuildWaypoints(float spacing, int seed)
        {
            var tri = NavMesh.CalculateTriangulation();
            var origin = Bank.Spots[0].StandPosition;
            var triangles = tri.indices.Length / 3;
            var cdf = new float[triangles];
            var total = 0f;
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

            var rng = new System.Random(seed);
            for (var attempt = 0; attempt < 6000 && Waypoints.Count < 150; attempt++)
            {
                var t = Array.BinarySearch(cdf, (float)rng.NextDouble() * total);
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
                var ok = true;
                foreach (var w in Waypoints)
                {
                    if ((w - p).sqrMagnitude < spacing * spacing)
                    {
                        ok = false;
                        break;
                    }
                }

                if (ok)
                {
                    Waypoints.Add(p);
                }
            }

            WaypointUnder = new int[Waypoints.Count];
            WaypointOnFurniture = new int[Waypoints.Count];
            WaypointCorner = new int[Waypoints.Count];
            WaypointFloor = new int[Waypoints.Count];
            for (var w = 0; w < Waypoints.Count; w++)
            {
                foreach (var s in sizeSpots)
                {
                    var spot = Bank.Spots[s];
                    if ((spot.Position - Waypoints[w]).sqrMagnitude > 9f)
                    {
                        continue;
                    }

                    if ((spot.Tags & HidingSpotTags.Under) != 0) WaypointUnder[w]++;
                    if ((spot.Tags & HidingSpotTags.OnFurniture) != 0) WaypointOnFurniture[w]++;
                    if ((spot.Tags & HidingSpotTags.Corner) != 0) WaypointCorner[w]++;
                    if (spot.Tags == HidingSpotTags.None) WaypointFloor[w]++;
                }
            }
        }

        public int NearestWaypoint(Vector3 position)
        {
            var best = 0;
            var bestDistance = float.PositiveInfinity;
            for (var w = 0; w < Waypoints.Count; w++)
            {
                var d = (Waypoints[w] - position).sqrMagnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = w;
                }
            }

            return best;
        }

        /// <summary>Up to <paramref name="slots"/> nearest waypoints (straight line) with complete paths, excluding the one here.</summary>
        public void FillWaypointCandidates(Vector3 from, int slots, List<int> into, List<float> paths)
        {
            into.Clear();
            paths.Clear();
            var order = new List<int>(Waypoints.Count);
            for (var w = 0; w < Waypoints.Count; w++)
            {
                if (Flat(Waypoints[w] - from) > 2.5f)
                {
                    order.Add(w);
                }
            }

            order.Sort((x, y) => Flat(Waypoints[x] - from).CompareTo(Flat(Waypoints[y] - from)));
            for (var k = 0; k < order.Count && k < slots + 6 && into.Count < slots; k++)
            {
                var w = order[k];
                if (TryPath(from, Waypoints[w], out var length))
                {
                    into.Add(w);
                    paths.Add(length);
                }
            }
        }

        // ------------------------------------------------------------------ hiding

        /// <summary>
        /// Up to <paramref name="slots"/> random bank spots within 12 m (straight) and 15 m (path) of
        /// <paramref name="from"/> that fit the prop right now and hold no other prop.
        /// </summary>
        public void FillHideCandidates(Vector3 from, System.Random rng, IReadOnlyList<Vector3> otherProps, int slots,
            List<int> into, List<float> paths, int exclude = -1)
        {
            into.Clear();
            paths.Clear();
            var near = new List<int>();
            foreach (var s in sizeSpots)
            {
                if (s != exclude && Flat(Bank.Spots[s].Position - from) <= 12f)
                {
                    near.Add(s);
                }
            }

            for (var i = near.Count - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                (near[i], near[j]) = (near[j], near[i]);
            }

            var queries = 0;
            foreach (var s in near)
            {
                if (into.Count >= slots || queries >= 40)
                {
                    break;
                }

                queries++;
                if (!FitsNow(s, otherProps) || !IsGrabbable(s) || !TryPath(from, Bank.Spots[s].StandPosition, out var length) || length > 15f)
                {
                    continue;
                }

                into.Add(s);
                paths.Add(length);
            }
        }

        /// <summary>Placement rule with the props of the scene as they are, and no other player prop within 0.6 m.</summary>
        public bool FitsNow(int spotIndex, IReadOnlyList<Vector3> otherProps)
        {
            var spot = Bank.Spots[spotIndex];
            if (otherProps != null)
            {
                foreach (var p in otherProps)
                {
                    if ((p - spot.Position).sqrMagnitude < 0.36f)
                    {
                        return false;
                    }
                }
            }

            var centre = spot.Position + Vector3.up * (ObjectHalf.y + 0.011f);
            return !Physics.CheckBox(centre, ObjectHalf - Vector3.one * 0.01f, Quaternion.identity,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        public bool CanPlaceFrom(Vector3 bodyPosition, int spotIndex)
        {
            var spot = Bank.Spots[spotIndex];
            return Flat(spot.Position - bodyPosition) <= 2.8f && Mathf.Abs(spot.Position.y - bodyPosition.y) <= 2.2f;
        }

        public float BankHide(int spotIndex) => Bank.Hide(Bank.Spots[spotIndex].Exposure(SizeClass));

        // ------------------------------------------------------------------ approach

        private readonly Dictionary<int, bool> grabbableSpot = new();

        /// <summary>Game-rule pickup plan (walk, optionally jump up, grab in some posture). See HideSeekReach.</summary>
        public bool TryPlanReach(Vector3 from, Vector3 propBottom, out ReachPlan plan) =>
            HideSeekReach.TryPlan(from, propBottom, ObjectHalf, OccluderMask, path, out plan);

        /// <summary>Could a player on the main island pick a prop up from this bank spot at all? (cached)</summary>
        public bool IsGrabbable(int spotIndex)
        {
            if (!grabbableSpot.TryGetValue(spotIndex, out var ok))
            {
                var spot = Bank.Spots[spotIndex];
                ok = TryPlanReach(spot.StandPosition, spot.Position + Vector3.up * 0.011f, out _);
                grabbableSpot[spotIndex] = ok;
            }

            return ok;
        }

        public bool InGrabReach(Vector3 feet, Vector3 propBottom, bool jumping) =>
            HideSeekReach.InGrabReach(feet, propBottom, ObjectHalf, jumping);

        // ------------------------------------------------------------------ movement helpers

        public bool TryPickAway(Vector3 from, Vector3 threat, System.Random rng, out Vector3 destination)
        {
            // A waypoint that increases the distance to the threat, preferring ones 6-18 m away.
            destination = default;
            var best = float.NegativeInfinity;
            for (var tries = 0; tries < 12; tries++)
            {
                var w = Waypoints[rng.Next(Waypoints.Count)];
                var fromMe = Flat(w - from);
                if (fromMe < 4f || fromMe > 18f)
                {
                    continue;
                }

                var score = Flat(w - threat) - Flat(from - threat);
                if (score > best && TryPath(from, w, out _))
                {
                    best = score;
                    destination = w;
                }
            }

            return best > float.NegativeInfinity;
        }

        public bool TryPickRandomWaypoint(Vector3 from, System.Random rng, float min, float max, out Vector3 destination)
        {
            for (var tries = 0; tries < 12; tries++)
            {
                var w = Waypoints[rng.Next(Waypoints.Count)];
                var d = Flat(w - from);
                if (d >= min && d <= max)
                {
                    destination = w;
                    return true;
                }
            }

            destination = default;
            return false;
        }

        public bool TryPath(Vector3 from, Vector3 to, out float length)
        {
            length = 0f;
            if (!NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
            {
                return false;
            }

            var corners = path.corners;
            for (var i = 1; i < corners.Length; i++)
            {
                length += Vector3.Distance(corners[i - 1], corners[i]);
            }

            return true;
        }

        public static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        public static Vector3 Local(HideSeekBody body, Vector3 world)
        {
            var local = Quaternion.Euler(0f, -body.Yaw, 0f) * new Vector3(world.x, 0f, world.z);
            var length = local.magnitude;
            return length > 0.0001f ? local / length : Vector3.zero;
        }
    }
}
