using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Training.HideSeek
{
    /// <summary>
    /// Runs several independent hide-seek rounds in one mansion scene (bodies have no colliders, so rounds never
    /// meet). Owns the shared data (hiding spot bank, seeker waypoints), candidate generation, observations and
    /// the rule / random brains used as baselines. Learned brains are HideSeekHideAgent / HideSeekSeekAgent.
    /// </summary>
    public sealed class HideSeekArena : MonoBehaviour
    {
        public const int HideObservationSize = 8 + HideSeekMatch.HideCandidateSlots * 18;
        public const int SeekObservationSize = 3 + 7 + 7 + HideSeekMatch.SeekCandidateSlots * 9;
        public const string ObservationVersion = "hideseek-obs-v1";

        [SerializeField]
        private HidingSpotBank bank;

        [SerializeField, Tooltip("Prop size class used for the hidden prop (0 small, 1 medium, 2 large).")]
        private int sizeClass = 1;

        [SerializeField]
        private HideSeekHideAgent[] hideAgents = Array.Empty<HideSeekHideAgent>();

        [SerializeField]
        private HideSeekSeekAgent[] seekAgents = Array.Empty<HideSeekSeekAgent>();

        [SerializeField]
        private int seed = 20260925;

        [Header("Seeker waypoints (same generator as Explore v2)")]
        [SerializeField]
        private float waypointSpacing = 4f;

        [SerializeField]
        private int waypointSeed = 20260925;

        [Header("Evaluation (fixed seeds, never used for training)")]
        [SerializeField]
        private bool evaluationMode;

        [SerializeField]
        private int evaluationSeed = 777001;

        [SerializeField, Min(1)]
        private int evaluationEpisodes = 200;

        [Header("Editor runs")]
        [SerializeField, Tooltip("Time scale when played from the editor without mlagents-learn (0 = leave as is).")]
        private float editorTimeScale = 0f;

        [SerializeField]
        private int logEveryEpisodes = 100;

        [SerializeField, Tooltip("Simulation step. 0.05 s = 17.5 cm per step at walking speed; vision runs every 0.1 s.")]
        private float simulationStep = 0.05f;

        public HidingSpotBank Bank => bank;
        public int OccluderMask { get; private set; }
        public Vector3 ObjectHalf { get; private set; }
        public readonly List<Vector3> Waypoints = new();
        public bool IsReady { get; private set; }

        private readonly List<HideSeekMatch> matches = new();
        private readonly List<int> sizeSpots = new();
        private int[] waypointUnder, waypointOnFurniture, waypointCorner, waypointFloor;
        private NavMeshPath path;
        private System.Random startRng;
        private int nextEpisode;
        private int finishedEpisodes;

        // Stats (window and total).
        private int windowEpisodes, windowFound, windowDeadline, windowRejected, windowRelocations;
        private int windowSawPlaced, windowSawHolding, windowWaypoints, windowSeekDecisions;
        private int totalSawPlaced, totalSawHolding;

        // Evaluation analysis of where the hider put the prop (bank score is read here only, never observed).
        private int totalRelocations, totalPlaced, placedUnder, placedOnFurniture, placedCorner, placedOpen;
        private double totalPlacedAt, totalPlacedBankHide;
        private double windowFoundTime, windowHiderReward;
        private int totalEpisodes, totalFound, totalDeadline;
        private double totalFoundTime, totalHiderReward;
        private long fingerprint = 1469598103934665603L;
        private double realAtWindow;
        private double simAtWindow;
        private double simTime;
        private bool evaluationReported;

        public int MatchCount => Mathf.Min(hideAgents.Length, seekAgents.Length);

        private void Awake()
        {
            var _ = Unity.MLAgents.Academy.Instance;
            OccluderMask = Physics.DefaultRaycastLayers;
            path = new NavMeshPath();

            // Only ray queries are needed (nothing falls or collides here): skip physics stepping, larger steps.
            previousSimulationMode = Physics.simulationMode;
            previousFixedDelta = Time.fixedDeltaTime;
            Physics.simulationMode = SimulationMode.Script;
            Physics.SyncTransforms();
            Time.fixedDeltaTime = simulationStep;
            if (editorTimeScale > 0f)
            {
                Time.timeScale = editorTimeScale;
            }
        }

        private SimulationMode previousSimulationMode;
        private float previousFixedDelta;

        private void OnDestroy()
        {
            // These are project-wide; give them back so other scenes (the Fusion sandbox, the game) are unaffected.
            Physics.simulationMode = previousSimulationMode;
            if (previousFixedDelta > 0f)
            {
                Time.fixedDeltaTime = previousFixedDelta;
            }
        }

        private void Start()
        {
            if (bank == null || bank.Spots.Count == 0)
            {
                Debug.LogError("[HideSeek] no hiding spot bank. Build it with Tools > AI > Build Hiding Spot Bank first.", this);
                return;
            }

            ObjectHalf = bank.HalfExtents[Mathf.Clamp(sizeClass, 0, 2)];
            for (var i = 0; i < bank.Spots.Count; i++)
            {
                if (bank.Spots[i].Fits(sizeClass))
                {
                    sizeSpots.Add(i);
                }
            }

            BuildWaypoints();
            startRng = new System.Random(seed);
            for (var i = 0; i < MatchCount; i++)
            {
                var match = new HideSeekMatch(this, i);
                matches.Add(match);
                hideAgents[i].Bind(this, match);
                seekAgents[i].Bind(this, match);
                StartNext(match);
            }

            IsReady = matches.Count > 0 && Waypoints.Count > 0;
            realAtWindow = Time.realtimeSinceStartupAsDouble;
            Debug.Log($"[HideSeek] ready. matches {matches.Count}, bank spots for size {sizeClass}: {sizeSpots.Count}, waypoints {Waypoints.Count}, obs {ObservationVersion} (hide {HideObservationSize}, seek {SeekObservationSize}), fov {HideSeekRules.HorizontalFov:F1}x{HideSeekRules.VerticalFov} deg, sprint {HideSeekRules.BotsCanSprint}, {(evaluationMode ? $"EVALUATION seed {evaluationSeed} x {evaluationEpisodes}" : $"training seed {seed}")}.", this);
        }

        private void FixedUpdate()
        {
            if (!IsReady)
            {
                return;
            }

            var dt = Time.fixedDeltaTime;
            simTime += dt;
            foreach (var match in matches)
            {
                if (match.Done)
                {
                    continue;
                }

                match.Step(dt);
                if (match.Done)
                {
                    Finish(match);
                    continue;
                }

                if (match.ObjectHeld && match.HState == HideSeekMatch.HiderState.AwaitDecision && !match.HiderAwaiting)
                {
                    match.HiderAwaiting = true;
                    hideAgents[match.Index].AskForDecision();
                }

                if (match.SState == HideSeekMatch.SeekerState.AwaitDecision && !match.SeekerAwaiting)
                {
                    match.SeekerAwaiting = true;
                    seekAgents[match.Index].AskForDecision();
                }
            }
        }

        // ------------------------------------------------------------------ rounds

        private void StartNext(HideSeekMatch match)
        {
            int episodeSeed;
            if (evaluationMode)
            {
                if (nextEpisode >= evaluationEpisodes)
                {
                    return; // this round stays done; the others finish their share
                }

                episodeSeed = evaluationSeed + nextEpisode;
            }
            else
            {
                episodeSeed = startRng.Next();
            }

            nextEpisode++;
            var r = new System.Random(episodeSeed);
            var hiderAt = Waypoints[r.Next(Waypoints.Count)];
            var seekerAt = hiderAt;
            for (var tries = 0; tries < 30; tries++)
            {
                seekerAt = Waypoints[r.Next(Waypoints.Count)];
                if (Flat(seekerAt - hiderAt) >= 8f)
                {
                    break;
                }
            }

            var hiderYaw = (float)(r.NextDouble() * 360.0);
            var seekerYaw = (float)(r.NextDouble() * 360.0);
            match.Reset(episodeSeed, hiderAt, hiderYaw, seekerAt, seekerYaw);
            if (evaluationMode)
            {
                Mix(episodeSeed);
                Mix(Mathf.RoundToInt(hiderAt.x * 100f));
                Mix(Mathf.RoundToInt(seekerAt.z * 100f));
            }
        }

        private void Finish(HideSeekMatch match)
        {
            hideAgents[match.Index].Finish(match.HiderReward);
            seekAgents[match.Index].Finish(match.SeekerReward);
            Record(match);
            StartNext(match);
        }

        private void Record(HideSeekMatch match)
        {
            finishedEpisodes++;
            windowEpisodes++;
            totalEpisodes++;
            var found = match.FoundAt >= 0f;
            if (found)
            {
                windowFound++;
                totalFound++;
                windowFoundTime += match.FoundAt;
                totalFoundTime += match.FoundAt;
            }

            if (match.DeadlineMissed)
            {
                windowDeadline++;
                totalDeadline++;
            }

            windowRejected += match.RejectedPlacements;
            totalRelocations += match.Relocations;
            if (match.PlacedSpot >= 0)
            {
                var placed = bank.Spots[match.PlacedSpot];
                totalPlaced++;
                totalPlacedAt += match.PlacedAt;
                totalPlacedBankHide += bank.Hide(placed.Exposure(sizeClass));
                if ((placed.Tags & HidingSpotTags.Under) != 0) placedUnder++;
                if ((placed.Tags & HidingSpotTags.OnFurniture) != 0) placedOnFurniture++;
                if ((placed.Tags & HidingSpotTags.Corner) != 0) placedCorner++;
                if (placed.Tags == HidingSpotTags.None) placedOpen++;
            }
            if (match.ObjectEverSeen && !match.ObjectSeenHeld) { windowSawPlaced++; totalSawPlaced++; }
            if (match.SeekerSawHiderHolding) { windowSawHolding++; totalSawHolding++; }
            windowSeekDecisions += match.SeekerDecisions;
            foreach (var v in match.WaypointVisitedAt) if (!float.IsNegativeInfinity(v)) windowWaypoints++;
            windowRelocations += match.Relocations;
            windowHiderReward += match.HiderReward;
            totalHiderReward += match.HiderReward;

            if (evaluationMode)
            {
                Mix(found ? Mathf.RoundToInt(match.FoundAt * 10f) : -1);
                if (!evaluationReported && finishedEpisodes >= evaluationEpisodes)
                {
                    evaluationReported = true;
                    Debug.Log(
                        $"[HideSeek EVAL] hider={hideAgents[0].PolicyLabel} seeker={seekAgents[0].PolicyLabel} episodes {totalEpisodes} " +
                        $"found {100.0 * totalFound / totalEpisodes:F1}% mean found time {(totalFound > 0 ? totalFoundTime / totalFound : 0):F1}s " +
                        $"hidden share {(totalHiderReward / totalEpisodes + 1) / 2:F3} (reward {totalHiderReward / totalEpisodes:F3}) " +
                        $"late drops {totalDeadline}, seeker saw the placed prop {100.0 * totalSawPlaced / totalEpisodes:F0}%, saw the hider holding it {100.0 * totalSawHolding / totalEpisodes:F0}% | seed {evaluationSeed} fingerprint {fingerprint:X16}",
                        this);
                    var p = Mathf.Max(1, totalPlaced);
                    Debug.Log(
                        $"[HideSeek EVAL detail] relocations/round {(double)totalRelocations / totalEpisodes:F2}, placed on a bank spot {totalPlaced}/{totalEpisodes}, placed at {totalPlacedAt / p:F1}s, " +
                        $"spot tags under {100.0 * placedUnder / p:F0}% on-furniture {100.0 * placedOnFurniture / p:F0}% corner {100.0 * placedCorner / p:F0}% open {100.0 * placedOpen / p:F0}%, " +
                        $"bank hide score of placed spots {totalPlacedBankHide / p:F3} (analysis only)",
                        this);
                }
            }

            if (windowEpisodes >= logEveryEpisodes)
            {
                LogWindow();
            }
        }

        private void LogWindow()
        {
            var real = Time.realtimeSinceStartupAsDouble - realAtWindow;
            var sim = simTime - simAtWindow;
            var n = Mathf.Max(1, windowEpisodes);
            Debug.Log(
                $"[HideSeek] episodes {totalEpisodes} | window {windowEpisodes}: found {100.0 * windowFound / n:F0}%, mean found time {(windowFound > 0 ? windowFoundTime / windowFound : 0):F1}s, " +
                $"hider reward {windowHiderReward / n:F3}, late drops {windowDeadline}, rejected placements {windowRejected}, relocations {windowRelocations}, " +
                $"seeker saw placed prop {100.0 * windowSawPlaced / n:F0}% / hider holding {100.0 * windowSawHolding / n:F0}%, waypoints visited {windowWaypoints / (float)n:F1}, seeker decisions {windowSeekDecisions / (float)n:F1} | " +
                $"hider {hideAgents[0].PolicyLabel}, seeker {seekAgents[0].PolicyLabel} | sim speed {(real > 0 ? sim / real : 0):F1}x",
                this);
            windowEpisodes = windowFound = windowDeadline = windowRejected = windowRelocations = 0;
            windowSawPlaced = windowSawHolding = windowWaypoints = windowSeekDecisions = 0;
            windowFoundTime = windowHiderReward = 0;
            realAtWindow = Time.realtimeSinceStartupAsDouble;
            simAtWindow = simTime;
        }

        private void Mix(long v)
        {
            unchecked
            {
                fingerprint ^= v;
                fingerprint *= 1099511628211L;
            }
        }

        // ------------------------------------------------------------------ waypoints

        private void BuildWaypoints()
        {
            // Same idea as Explore v2: area-weighted over the NavMesh island the rounds start on, 4 m apart.
            var tri = NavMesh.CalculateTriangulation();
            var origin = bank.Spots[0].StandPosition;
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

            var rng = new System.Random(waypointSeed);
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
                    if ((w - p).sqrMagnitude < waypointSpacing * waypointSpacing)
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

            // Terrain summary near each waypoint (counts of bank spots by tag within 3 m): geometry, not scores.
            waypointUnder = new int[Waypoints.Count];
            waypointOnFurniture = new int[Waypoints.Count];
            waypointCorner = new int[Waypoints.Count];
            waypointFloor = new int[Waypoints.Count];
            for (var w = 0; w < Waypoints.Count; w++)
            {
                foreach (var s in sizeSpots)
                {
                    var spot = bank.Spots[s];
                    if ((spot.Position - Waypoints[w]).sqrMagnitude > 9f)
                    {
                        continue;
                    }

                    if ((spot.Tags & HidingSpotTags.Under) != 0) waypointUnder[w]++;
                    if ((spot.Tags & HidingSpotTags.OnFurniture) != 0) waypointOnFurniture[w]++;
                    if ((spot.Tags & HidingSpotTags.Corner) != 0) waypointCorner[w]++;
                    if (spot.Tags == HidingSpotTags.None) waypointFloor[w]++;
                }
            }
        }

        public void MarkVisited(HideSeekMatch match)
        {
            for (var w = 0; w < Waypoints.Count; w++)
            {
                if ((Waypoints[w] - match.Seeker.Position).sqrMagnitude <= 2.5f * 2.5f)
                {
                    match.WaypointVisitedAt[w] = match.Time;
                }
            }
        }

        // ------------------------------------------------------------------ candidates

        public void FillHideCandidates(HideSeekMatch match, int exclude = -1)
        {
            match.HideCandidates.Clear();
            match.HideCandidatePaths.Clear();
            var from = match.Hider.Position;
            var near = new List<int>();
            foreach (var s in sizeSpots)
            {
                if (s != exclude && Flat(bank.Spots[s].Position - from) <= 12f)
                {
                    near.Add(s);
                }
            }

            var rng = match.Rng;
            for (var i = near.Count - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                (near[i], near[j]) = (near[j], near[i]);
            }

            var queries = 0;
            foreach (var s in near)
            {
                if (match.HideCandidates.Count >= HideSeekMatch.HideCandidateSlots || queries >= 40)
                {
                    break;
                }

                queries++;
                if (!FitsNow(s) ||
                    !NavMesh.CalculatePath(from, bank.Spots[s].StandPosition, NavMesh.AllAreas, path) ||
                    path.status != NavMeshPathStatus.PathComplete)
                {
                    continue;
                }

                var length = PathLength(path);
                if (length > 15f)
                {
                    continue;
                }

                match.HideCandidates.Add(s);
                match.HideCandidatePaths.Add(length);
            }
        }

        /// <summary>Placement rule with the props as they are now (the bank ignored props, decision D3).</summary>
        private bool FitsNow(int spotIndex)
        {
            var spot = bank.Spots[spotIndex];
            var centre = spot.Position + Vector3.up * (ObjectHalf.y + 0.011f);
            return !Physics.CheckBox(centre, ObjectHalf - Vector3.one * 0.01f, Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        public bool TryPlace(HideSeekMatch match, int spotIndex)
        {
            if (spotIndex < 0)
            {
                return false;
            }

            var spot = bank.Spots[spotIndex];
            var reach = Flat(spot.Position - match.Hider.Position) <= 2.8f && Mathf.Abs(spot.Position.y - match.Hider.Position.y) <= 2.2f;
            return reach && FitsNow(spotIndex);
        }

        public void FillSeekCandidates(HideSeekMatch match)
        {
            match.SeekCandidates.Clear();
            match.SeekCandidatePaths.Clear();
            var from = match.Seeker.Position;
            var order = new List<int>(Waypoints.Count);
            for (var w = 0; w < Waypoints.Count; w++)
            {
                if (Flat(Waypoints[w] - from) > 2.5f)
                {
                    order.Add(w);
                }
            }

            order.Sort((x, y) => Flat(Waypoints[x] - from).CompareTo(Flat(Waypoints[y] - from)));
            for (var k = 0; k < order.Count && k < 14 && match.SeekCandidates.Count < HideSeekMatch.SeekCandidateSlots; k++)
            {
                var w = order[k];
                if (NavMesh.CalculatePath(from, Waypoints[w], NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete)
                {
                    match.SeekCandidates.Add(w);
                    match.SeekCandidatePaths.Add(PathLength(path));
                }
            }
        }

        public bool TryPickRelocation(HideSeekMatch match, out Vector3 destination)
        {
            var rng = match.Rng;
            for (var tries = 0; tries < 12; tries++)
            {
                var w = Waypoints[rng.Next(Waypoints.Count)];
                var d = Flat(w - match.Hider.Position);
                if (d >= 6f && d <= 20f)
                {
                    destination = w;
                    return true;
                }
            }

            destination = default;
            return false;
        }

        public bool TryPickWalkAway(HideSeekMatch match, out Vector3 destination)
        {
            var rng = match.Rng;
            destination = default;
            var best = -1f;
            for (var tries = 0; tries < 10; tries++)
            {
                var w = Waypoints[rng.Next(Waypoints.Count)];
                var d = Flat(w - match.ObjectBottom);
                if (d > best && d <= 25f)
                {
                    best = d;
                    destination = w;
                }
            }

            return best >= 0f;
        }

        // ------------------------------------------------------------------ observations

        public void WriteHideObservation(HideSeekMatch m, float[] o)
        {
            Array.Clear(o, 0, o.Length);
            var body = m.Hider;
            o[0] = Mathf.Clamp01((HideSeekRules.HideDeadlineSeconds - m.Time) / HideSeekRules.HideDeadlineSeconds);
            o[1] = body.Stamina / HideSeekRules.MaxStamina;
            o[2] = m.HiderSeesSeeker ? 1f : 0f;
            o[3] = m.SeekerEverSeen ? 1f : 0f;
            if (m.SeekerEverSeen)
            {
                var local = Local(body, m.SeekerLastSeen - body.Position);
                o[4] = local.x;
                o[5] = local.z;
                o[6] = Mathf.Clamp01(Flat(m.SeekerLastSeen - body.Position) / 20f);
                o[7] = Mathf.Clamp01((m.Time - m.SeekerSeenAt) / 30f);
            }
            else
            {
                o[6] = 1f;
                o[7] = 1f;
            }

            var k = 8;
            for (var slot = 0; slot < HideSeekMatch.HideCandidateSlots; slot++, k += 18)
            {
                if (slot >= m.HideCandidates.Count)
                {
                    continue;
                }

                var spot = bank.Spots[m.HideCandidates[slot]];
                var centre = spot.Position + Vector3.up * (ObjectHalf.y + 0.011f);
                var local = Local(body, spot.Position - body.Position);
                o[k] = 1f;
                o[k + 1] = local.x;
                o[k + 2] = local.z;
                o[k + 3] = Mathf.Clamp01(m.HideCandidatePaths[slot] / 15f);
                o[k + 4] = Mathf.Clamp((spot.Position.y - body.Position.y) / 2f, -1f, 1f);
                o[k + 5] = (spot.Tags & HidingSpotTags.Under) != 0 ? 1f : 0f;
                o[k + 6] = (spot.Tags & HidingSpotTags.OnFurniture) != 0 ? 1f : 0f;
                o[k + 7] = (spot.Tags & HidingSpotTags.Corner) != 0 ? 1f : 0f;
                // 8 horizontal openness rays from the prop centre, sorted (orientation-free "how enclosed").
                var rays = new float[8];
                for (var r = 0; r < 8; r++)
                {
                    var dir = Quaternion.Euler(0f, r * 45f, 0f) * Vector3.forward;
                    rays[r] = Physics.Raycast(centre, dir, out var hit, 3f, OccluderMask, QueryTriggerInteraction.Ignore) ? hit.distance / 3f : 1f;
                }

                Array.Sort(rays);
                for (var r = 0; r < 8; r++)
                {
                    o[k + 8 + r] = rays[r];
                }

                // What the hider knows about the seeker: could the spot be seen from where it last saw the seeker?
                if (m.SeekerEverSeen)
                {
                    o[k + 16] = HideSeekVision.Clear(m.SeekerLastSeenEye, centre, OccluderMask) ? 1f : 0f;
                    o[k + 17] = Mathf.Clamp01(Vector3.Distance(m.SeekerLastSeen, spot.Position) / 20f);
                }
                else
                {
                    o[k + 17] = 1f;
                }
            }
        }

        public void WriteSeekObservation(HideSeekMatch m, float[] o)
        {
            Array.Clear(o, 0, o.Length);
            var body = m.Seeker;
            o[0] = Mathf.Clamp01(m.Time / HideSeekRules.EpisodeSeconds);
            o[1] = body.Stamina / HideSeekRules.MaxStamina;
            o[2] = body.Crouched ? 1f : 0f;
            WriteMemory(o, 3, body, m.SeekerSeesObject, m.ObjectEverSeen, m.ObjectLastSeen, m.Time - m.ObjectSeenAt, m.ObjectSeenHeld);
            WriteMemory(o, 10, body, m.SeekerSeesHider, m.HiderEverSeen, m.HiderLastSeen, m.Time - m.HiderSeenAt, m.HiderSeenHolding);
            var k = 17;
            for (var slot = 0; slot < HideSeekMatch.SeekCandidateSlots; slot++, k += 9)
            {
                if (slot >= m.SeekCandidates.Count)
                {
                    continue;
                }

                var w = m.SeekCandidates[slot];
                var local = Local(body, Waypoints[w] - body.Position);
                o[k] = 1f;
                o[k + 1] = local.x;
                o[k + 2] = local.z;
                o[k + 3] = Mathf.Clamp01(m.SeekCandidatePaths[slot] / 30f);
                o[k + 4] = float.IsNegativeInfinity(m.WaypointVisitedAt[w]) ? 1f : Mathf.Clamp01((m.Time - m.WaypointVisitedAt[w]) / 60f);
                o[k + 5] = Mathf.Clamp01(waypointUnder[w] / 5f);
                o[k + 6] = Mathf.Clamp01(waypointOnFurniture[w] / 5f);
                o[k + 7] = Mathf.Clamp01(waypointCorner[w] / 5f);
                o[k + 8] = Mathf.Clamp01(waypointFloor[w] / 10f);
            }
        }

        private static void WriteMemory(float[] o, int k, HideSeekBody body, bool now, bool ever, Vector3 at, float since, bool flag)
        {
            o[k] = now ? 1f : 0f;
            o[k + 1] = ever ? 1f : 0f;
            if (ever)
            {
                var local = Local(body, at - body.Position);
                o[k + 2] = local.x;
                o[k + 3] = local.z;
                o[k + 4] = Mathf.Clamp01(Flat(at - body.Position) / 20f);
                o[k + 5] = Mathf.Clamp01(since / 30f);
                o[k + 6] = flag ? 1f : 0f;
            }
            else
            {
                o[k + 4] = 1f;
                o[k + 5] = 1f;
            }
        }

        public bool SeekActionAllowed(HideSeekMatch m, int action)
        {
            if (action < HideSeekMatch.SeekCandidateSlots)
            {
                return action < m.SeekCandidates.Count;
            }

            return action switch
            {
                HideSeekMatch.SeekActionToObject => m.ObjectEverSeen && !m.ObjectSeenHeld,
                HideSeekMatch.SeekActionChase => m.HiderEverSeen && m.Time - m.HiderSeenAt <= HideSeekMatch.ChaseMemorySeconds,
                _ => true,
            };
        }

        // ------------------------------------------------------------------ baseline brains

        /// <summary>Rule hider: the candidate with the best bank hide score (the hand-made score, v3).</summary>
        public int RuleHideAction(HideSeekMatch m)
        {
            var best = HideSeekMatch.HideActionRelocate;
            var bestExposure = float.PositiveInfinity;
            for (var i = 0; i < m.HideCandidates.Count; i++)
            {
                var e = bank.Spots[m.HideCandidates[i]].Exposure(sizeClass);
                if (e >= 0f && e < bestExposure)
                {
                    bestExposure = e;
                    best = i;
                }
            }

            return best;
        }

        public int RandomHideAction(HideSeekMatch m, System.Random rng) =>
            m.HideCandidates.Count > 0 ? rng.Next(m.HideCandidates.Count) : HideSeekMatch.HideActionRelocate;

        /// <summary>
        /// Rule seeker, the way a person would sweep a house: go to a seen prop, chase a seen hider, otherwise walk
        /// to the nearest waypoint not visited for a while, look around on arrival, and crouch to look low where
        /// furniture could hide something.
        /// </summary>
        public int RuleSeekAction(HideSeekMatch m)
        {
            if (SeekActionAllowed(m, HideSeekMatch.SeekActionToObject) && m.LastSeekerMacro != HideSeekMatch.SeekerState.ToObject)
            {
                return HideSeekMatch.SeekActionToObject;
            }

            if (SeekActionAllowed(m, HideSeekMatch.SeekActionChase) && m.HiderSeenHolding)
            {
                return HideSeekMatch.SeekActionChase;
            }

            if (m.LastSeekerMacro == HideSeekMatch.SeekerState.ToObject)
            {
                return HideSeekMatch.SeekActionCrouchLook; // walked to it and still no pickup: look low
            }

            if (m.LastSeekerMacro == HideSeekMatch.SeekerState.ToWaypoint)
            {
                return HideSeekMatch.SeekActionLook;
            }

            if (m.LastSeekerMacro == HideSeekMatch.SeekerState.LookAround && NearbyUnder(m.Seeker.Position) > 0)
            {
                return HideSeekMatch.SeekActionCrouchLook;
            }

            var best = HideSeekMatch.SeekActionLook;
            var bestScore = float.PositiveInfinity;
            for (var i = 0; i < m.SeekCandidates.Count; i++)
            {
                var w = m.SeekCandidates[i];
                var since = float.IsNegativeInfinity(m.WaypointVisitedAt[w]) ? 999f : m.Time - m.WaypointVisitedAt[w];
                var score = m.SeekCandidatePaths[i] + (since < 60f ? 30f : 0f);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }

            return best;
        }

        public int RandomSeekAction(HideSeekMatch m, System.Random rng)
        {
            for (var tries = 0; tries < 20; tries++)
            {
                var a = rng.Next(HideSeekMatch.SeekActionCount);
                if (SeekActionAllowed(m, a))
                {
                    return a;
                }
            }

            return HideSeekMatch.SeekActionLook;
        }

        private int NearbyUnder(Vector3 position)
        {
            var best = -1;
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

            return best >= 0 ? waypointUnder[best] : 0;
        }

        // ------------------------------------------------------------------ helpers

        private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        private static Vector3 Local(HideSeekBody body, Vector3 world)
        {
            var local = Quaternion.Euler(0f, -body.Yaw, 0f) * new Vector3(world.x, 0f, world.z);
            var length = local.magnitude;
            return length > 0.0001f ? local / length : Vector3.zero;
        }

        private static float PathLength(NavMeshPath p)
        {
            var length = 0f;
            var corners = p.corners;
            for (var i = 1; i < corners.Length; i++)
            {
                length += Vector3.Distance(corners[i - 1], corners[i]);
            }

            return length;
        }

        private void OnDrawGizmos()
        {
            if (matches.Count == 0)
            {
                return;
            }

            var m = matches[0];
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(m.Hider.Position + Vector3.up * 0.8f, 0.31f);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(m.Seeker.Position + Vector3.up * 0.8f, 0.31f);
            var eye = m.Seeker.Eye;
            var half = HideSeekRules.HorizontalFov * 0.5f;
            Gizmos.DrawLine(eye, eye + Quaternion.Euler(0f, m.Seeker.Yaw - half, 0f) * Vector3.forward * 5f);
            Gizmos.DrawLine(eye, eye + Quaternion.Euler(0f, m.Seeker.Yaw + half, 0f) * Vector3.forward * 5f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(m.CurrentObjectBottom + Vector3.up * ObjectHalf.y, ObjectHalf * 2f);
        }
    }
}
