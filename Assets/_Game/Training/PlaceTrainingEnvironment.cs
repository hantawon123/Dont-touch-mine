using System;
using System.Collections.Generic;
using System.Text;
using Game.BotRuntime.Policy;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Training
{
    /// <summary>One placement decision: where the bot stands and the four spots it may choose from.</summary>
    public sealed class PlaceEpisode
    {
        public Vector3 BotPosition;
        public float BotYaw;
        public PickSizeClass HeldSize;
        public readonly List<PlaceCandidate> Candidates = new(PlaceObservationLayout.CandidateSlots);
        public readonly List<Vector3> SpotPositions = new(PlaceObservationLayout.CandidateSlots);
        public readonly List<float> Rewards = new(PlaceObservationLayout.CandidateSlots);
        public readonly List<float> Hides = new(PlaceObservationLayout.CandidateSlots);
        public readonly List<int> VisibleCounts = new(PlaceObservationLayout.CandidateSlots);
        public readonly List<float> PathLengths = new(PlaceObservationLayout.CandidateSlots);

        public int Count => Candidates.Count;

        public int BestSlot
        {
            get
            {
                var best = 0;
                for (var i = 1; i < Rewards.Count; i++)
                {
                    if (Rewards[i] > Rewards[best])
                    {
                        best = i;
                    }
                }

                return best;
            }
        }
    }

    /// <summary>
    /// v2 placement task on the real mansion geometry, without a physical bot (decision D3: choose, then score
    /// immediately; walking is validated separately in the 3a sandbox). One episode is one decision.
    ///
    /// Hiding is measured from 400 searcher points spread over the NavMesh by area at eye height. A spot counts as
    /// seen by a point within <see cref="watchRange"/> if a ray to the prop centre is not blocked by static level
    /// geometry. The hiding score is never shown to the policy; it only sees terrain cues around each spot.
    /// </summary>
    public sealed class PlaceTrainingEnvironment : MonoBehaviour
    {
        [Header("Searchers")]
        [SerializeField, Min(10)]
        private int watchPointCount = 400;

        [SerializeField, Min(1f)]
        private float watchEyeHeight = 1.5f;

        [SerializeField, Min(1f)]
        private float watchRange = 15f;

        [Header("Candidates")]
        [SerializeField, Min(0.5f)]
        private float minDistance = 1.5f;

        [SerializeField, Min(1f)]
        private float maxDistance = 8f;

        [SerializeField, Min(1f)]
        private float maxPathLength = 12f;

        [SerializeField, Min(0.2f)]
        private float minSpacing = 1f;

        [SerializeField, Min(0.5f)]
        private float rayLength = 5f;

        [SerializeField, Min(0.1f)]
        private float rayHeight = 0.4f;

        [SerializeField, Min(0.5f)]
        private float coverProbeHeight = 2.5f;

        [Header("Reward (decision D4)")]
        [SerializeField]
        private float exposureScale = PlaceReward.DefaultExposureScale;

        [SerializeField]
        private float hideWeight = PlaceReward.DefaultHideWeight;

        [SerializeField]
        private float displacementWeight = PlaceReward.DefaultDisplacementWeight;

        [SerializeField]
        private float pathWeight = PlaceReward.DefaultPathWeight;

        [Header("Run")]
        [SerializeField]
        [Tooltip("0 = random every run (training). Fixed = reproducible problem sheet (evaluation).")]
        private int seed;

        [SerializeField, Min(0)]
        [Tooltip("0 = unlimited (training). N = stop after N decisions and print the final table.")]
        private int evaluationEpisodes;

        [SerializeField, Min(10)]
        private int logEvery = 1000;

        [SerializeField]
        [Tooltip("In evaluation mode only this agent acts, so the problem sheet does not depend on agent update order.")]
        private PlaceSelectAgent evaluationAgent;

        [SerializeField, Min(0f)]
        [Tooltip("Speed test without a trainer. The trainer's --time-scale wins when connected.")]
        private float editorTimeScale;

        private readonly List<Vector3> watchPoints = new();
        private readonly RaycastHit[] rayHits = new RaycastHit[8];
        private readonly StringBuilder report = new();
        private NavMeshTriangulation triangulation;
        private float[] triangleAreaCdf;
        private float totalArea;
        private NavMeshPath path;
        private System.Random rng;
        private int staticMask;

        // Stats (per window, and totals for the evaluation table)
        private int windowCount;
        private double windowReward;
        private double windowHide;
        private double windowPath;
        private int windowOracleMatches;
        private double windowRegret;
        private int totalCount;
        private double totalReward;
        private double totalHide;
        private double totalPath;
        private double totalVisible;
        private int totalOracleMatches;
        private double totalRegret;
        private ulong fingerprint = 1469598103934665603UL;
        private string policyName = "?";
        private double realAtWindowStart;

        public bool IsReady { get; private set; }
        public bool IsFinished { get; private set; }
        public float ExposureScale => exposureScale;

        private void Awake()
        {
            var _ = Unity.MLAgents.Academy.Instance;
            rng = seed == 0 ? new System.Random() : new System.Random(seed);
            staticMask = LayerMask.GetMask("Default", "Water");
            path = new NavMeshPath();
            if (editorTimeScale > 0f)
            {
                Time.timeScale = editorTimeScale;
            }
        }

        private void OnDestroy()
        {
            if (editorTimeScale > 0f)
            {
                Time.timeScale = 1f;
            }
        }

        private void Start()
        {
            triangulation = NavMesh.CalculateTriangulation();
            if (triangulation.indices == null || triangulation.indices.Length < 3)
            {
                Debug.LogError("[Place Env] no NavMesh found. Build the sandbox with Tools > AI first.", this);
                return;
            }

            BuildAreaCdf();
            for (var i = 0; i < watchPointCount; i++)
            {
                if (TrySampleNavMeshPoint(out var p))
                {
                    watchPoints.Add(p + Vector3.up * watchEyeHeight);
                }
            }

            IsReady = watchPoints.Count > 0;
            realAtWindowStart = Time.realtimeSinceStartupAsDouble;
            Debug.Log($"[Place Env] ready. watch points {watchPoints.Count}, NavMesh area {totalArea:F0} m2, obs {PlaceObservationLayout.Version}, seed {seed}.", this);
        }

        private void BuildAreaCdf()
        {
            var triangles = triangulation.indices.Length / 3;
            triangleAreaCdf = new float[triangles];
            totalArea = 0f;
            for (var t = 0; t < triangles; t++)
            {
                var a = triangulation.vertices[triangulation.indices[t * 3]];
                var b = triangulation.vertices[triangulation.indices[t * 3 + 1]];
                var c = triangulation.vertices[triangulation.indices[t * 3 + 2]];
                totalArea += Vector3.Cross(b - a, c - a).magnitude * 0.5f;
                triangleAreaCdf[t] = totalArea;
            }
        }

        private float NextFloat() => (float)rng.NextDouble();

        /// <summary>Uniform point on the NavMesh surface (area-weighted triangle, then uniform inside it).</summary>
        private bool TrySampleNavMeshPoint(out Vector3 point)
        {
            point = default;
            if (triangleAreaCdf == null || triangleAreaCdf.Length == 0)
            {
                return false;
            }

            var target = NextFloat() * totalArea;
            var t = Array.BinarySearch(triangleAreaCdf, target);
            if (t < 0)
            {
                t = ~t;
            }

            t = Mathf.Clamp(t, 0, triangleAreaCdf.Length - 1);
            var a = triangulation.vertices[triangulation.indices[t * 3]];
            var b = triangulation.vertices[triangulation.indices[t * 3 + 1]];
            var c = triangulation.vertices[triangulation.indices[t * 3 + 2]];
            var r1 = NextFloat();
            var r2 = NextFloat();
            if (r1 + r2 > 1f)
            {
                r1 = 1f - r1;
                r2 = 1f - r2;
            }

            point = a + r1 * (b - a) + r2 * (c - a);
            return true;
        }

        private static float PathLength(NavMeshPath p)
        {
            var length = 0f;
            for (var i = 1; i < p.corners.Length; i++)
            {
                length += Vector3.Distance(p.corners[i - 1], p.corners[i]);
            }

            return length;
        }

        public void SetPolicyName(string name) => policyName = name;

        public bool IsEvaluation => evaluationEpisodes > 0;

        /// <summary>Training: every agent acts. Evaluation: only the designated agent (deterministic sheet).</summary>
        public bool MayAct(PlaceSelectAgent agent) => !IsEvaluation || evaluationAgent == null || agent == evaluationAgent;

        public void SetEvaluationAgent(PlaceSelectAgent agent) => evaluationAgent = agent;

        /// <summary>Builds one decision: a bot pose and up to four reachable floor spots with their terrain cues and true scores.</summary>
        public bool TryCreateEpisode(out PlaceEpisode episode)
        {
            episode = null;
            if (!IsReady || IsFinished)
            {
                return false;
            }

            for (var attempt = 0; attempt < 20; attempt++)
            {
                if (!TrySampleNavMeshPoint(out var bot))
                {
                    continue;
                }

                var ep = new PlaceEpisode
                {
                    BotPosition = bot,
                    BotYaw = NextFloat() * 360f,
                    HeldSize = (PickSizeClass)SampleSize(),
                };

                var tries = 0;
                while (ep.Count < PlaceObservationLayout.CandidateSlots && tries < 60)
                {
                    tries++;
                    var angle = NextFloat() * Mathf.PI * 2f;
                    var distance = Mathf.Lerp(minDistance, maxDistance, NextFloat());
                    var guess = bot + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
                    if (!NavMesh.SamplePosition(guess, out var hit, 1f, NavMesh.AllAreas))
                    {
                        continue;
                    }

                    var spot = hit.position;
                    if (Vector3.Distance(spot, bot) < minDistance || TooClose(ep, spot))
                    {
                        continue;
                    }

                    if (!NavMesh.CalculatePath(bot, spot, NavMesh.AllAreas, path) ||
                        path.status != NavMeshPathStatus.PathComplete)
                    {
                        continue;
                    }

                    var length = PathLength(path);
                    if (length > maxPathLength)
                    {
                        continue;
                    }

                    AddCandidate(ep, spot, length);
                }

                if (ep.Count >= 2)
                {
                    MixFingerprint(ep);
                    episode = ep;
                    return true;
                }
            }

            return false;
        }

        private int SampleSize()
        {
            var r = NextFloat();
            return r < 0.5f ? 0 : r < 0.85f ? 1 : 2;
        }

        private bool TooClose(PlaceEpisode ep, Vector3 spot)
        {
            foreach (var other in ep.SpotPositions)
            {
                if ((other - spot).sqrMagnitude < minSpacing * minSpacing)
                {
                    return true;
                }
            }

            return false;
        }

        private void AddCandidate(PlaceEpisode ep, Vector3 spot, float pathLength)
        {
            // Terrain cues the policy may see.
            var rays = new float[PlaceObservationLayout.RayCount];
            var origin = spot + Vector3.up * rayHeight;
            for (var r = 0; r < rays.Length; r++)
            {
                var yaw = r * (360f / rays.Length);
                var dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                rays[r] = Physics.Raycast(origin, dir, out var hit, rayLength, staticMask, QueryTriggerInteraction.Ignore)
                    ? hit.distance / rayLength
                    : 1f;
            }

            Array.Sort(rays); // orientation-free "how enclosed is it"

            var covered = Physics.Raycast(spot + Vector3.up * 0.1f, Vector3.up, out var roof, coverProbeHeight, staticMask, QueryTriggerInteraction.Ignore);
            var toSpot = Quaternion.Euler(0f, -ep.BotYaw, 0f) * (spot - ep.BotPosition);
            toSpot.y = 0f;
            var dirLocal = toSpot.sqrMagnitude > 0.0001f ? toSpot.normalized : Vector3.forward;
            var straight = Vector3.Distance(new Vector3(spot.x, 0f, spot.z), new Vector3(ep.BotPosition.x, 0f, ep.BotPosition.z));
            var straightNorm = straight / maxDistance;
            var pathNorm = pathLength / maxPathLength;

            ep.Candidates.Add(new PlaceCandidate(
                dirLocal.x,
                dirLocal.z,
                pathNorm,
                straightNorm,
                rays,
                covered,
                covered ? roof.distance / coverProbeHeight : 0f));
            ep.SpotPositions.Add(spot);
            ep.PathLengths.Add(pathLength);

            // Ground truth, never observed by the policy.
            var visible = CountVisibleWatchPoints(spot + Vector3.up * CenterHeight(ep.HeldSize));
            var hide = PlaceReward.Hide(visible, exposureScale);
            ep.VisibleCounts.Add(visible);
            ep.Hides.Add(hide);
            ep.Rewards.Add(PlaceReward.Total(hide, straightNorm, pathNorm, hideWeight, displacementWeight, pathWeight));
        }

        private static float CenterHeight(PickSizeClass size) => size switch
        {
            PickSizeClass.Small => 0.12f,
            PickSizeClass.Medium => 0.3f,
            _ => 0.5f,
        };

        private int CountVisibleWatchPoints(Vector3 target)
        {
            var visible = 0;
            var rangeSq = watchRange * watchRange;
            foreach (var watch in watchPoints)
            {
                var offset = target - watch;
                var distSq = offset.sqrMagnitude;
                if (distSq > rangeSq || distSq < 0.01f)
                {
                    continue;
                }

                var dist = Mathf.Sqrt(distSq);
                if (!Physics.Raycast(watch, offset / dist, dist - 0.05f, staticMask, QueryTriggerInteraction.Ignore))
                {
                    visible++;
                }
            }

            return visible;
        }

        private void MixFingerprint(PlaceEpisode ep)
        {
            void Mix(long v)
            {
                unchecked
                {
                    fingerprint ^= (ulong)v;
                    fingerprint *= 1099511628211UL;
                }
            }

            Mix(Mathf.RoundToInt(ep.BotPosition.x * 100f));
            Mix(Mathf.RoundToInt(ep.BotPosition.z * 100f));
            foreach (var s in ep.SpotPositions)
            {
                Mix(Mathf.RoundToInt(s.x * 100f));
                Mix(Mathf.RoundToInt(s.z * 100f));
            }
        }

        public void Record(PlaceEpisode ep, int slot)
        {
            if (IsFinished || ep == null || slot < 0 || slot >= ep.Count)
            {
                return;
            }

            var best = ep.BestSlot;
            var reward = ep.Rewards[slot];
            var regret = ep.Rewards[best] - reward;

            windowCount++;
            windowReward += reward;
            windowHide += ep.Hides[slot];
            windowPath += ep.PathLengths[slot];
            windowRegret += regret;
            if (slot == best)
            {
                windowOracleMatches++;
            }

            totalCount++;
            totalReward += reward;
            totalHide += ep.Hides[slot];
            totalPath += ep.PathLengths[slot];
            totalVisible += ep.VisibleCounts[slot];
            totalRegret += regret;
            if (slot == best)
            {
                totalOracleMatches++;
            }

            if (evaluationEpisodes > 0 && totalCount >= evaluationEpisodes)
            {
                LogFinal();
                IsFinished = true;
                return;
            }

            if (windowCount >= logEvery)
            {
                LogWindow();
            }
        }

        private void LogWindow()
        {
            var real = Time.realtimeSinceStartupAsDouble - realAtWindowStart;
            Debug.Log(
                $"[Place Env] {totalCount} decisions ({policyName}): window mean reward {windowReward / windowCount:F3}, " +
                $"hide {windowHide / windowCount:F3}, path {windowPath / windowCount:F1} m, " +
                $"oracle match {100.0 * windowOracleMatches / windowCount:F1}%, regret {windowRegret / windowCount:F3}, " +
                $"{windowCount / Math.Max(0.001, real):F0} decisions/s",
                this);
            windowCount = 0;
            windowReward = windowHide = windowPath = windowRegret = 0;
            windowOracleMatches = 0;
            realAtWindowStart = Time.realtimeSinceStartupAsDouble;
        }

        private void LogFinal()
        {
            var n = Math.Max(1, totalCount);
            report.Clear();
            report.AppendLine("[Place Eval] ===== final =====");
            report.AppendLine($"policy: {policyName}");
            report.AppendLine($"seed {seed}, decisions {totalCount}, obs {PlaceObservationLayout.Version}");
            report.AppendLine($"mean reward {totalReward / n:F3}, mean hide {totalHide / n:F3}, mean visible watchers {totalVisible / n:F1}");
            report.AppendLine($"mean path {totalPath / n:F2} m, oracle match {100.0 * totalOracleMatches / n:F1}%, mean regret {totalRegret / n:F3}");
            report.AppendLine($"fingerprint {fingerprint:X16} (same seed + settings must match across policies)");
            Debug.Log(report.ToString(), this);
        }
    }
}
