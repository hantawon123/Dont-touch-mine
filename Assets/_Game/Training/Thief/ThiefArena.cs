using System;
using System.Collections.Generic;
using Game.Training.HideSeek;
using UnityEngine;

namespace Game.Training.Thief
{
    /// <summary>
    /// Runs many independent thief rounds in one mansion scene (docs/planning/thief-npc-v2.md). Players are rule
    /// bots (V3); the thief is driven by two agents on one body: ThiefSeek while empty-handed, ThiefHide while
    /// carrying. Thief modes: Agent (learned or heuristic through the agents) or None (the "no NPC" baseline).
    /// </summary>
    public sealed class ThiefArena : MonoBehaviour
    {
        public const int SeekObservationSize = 4 + ThiefMatch.Players * 7 + ThiefMatch.Players * 9 + 8 * 10 + 2;
        public const int HideObservationSize = 4 + ThiefMatch.Players * 9 + 8 * 18;
        public const string ObservationVersion = "thief-obs-v4";

        public enum ThiefMode { Agent, None }
        public enum PlayerHiding { Rule, Random }

        [SerializeField] private HidingSpotBank bank;
        [SerializeField] private int sizeClass = 1;
        [SerializeField] private ThiefSeekAgent[] seekAgents = Array.Empty<ThiefSeekAgent>();
        [SerializeField] private ThiefHideAgent[] hideAgents = Array.Empty<ThiefHideAgent>();
        [SerializeField] private int seed = 20260926;
        [SerializeField] private float waypointSpacing = 4f;
        [SerializeField] private int waypointSeed = 20260925;
        [SerializeField] private ThiefMode thiefMode = ThiefMode.Agent;
        [SerializeField] private PlayerHiding playerHiding = PlayerHiding.Rule;

        [Header("Evaluation (fixed seeds, never used for training)")]
        [SerializeField] private bool evaluationMode;
        [SerializeField] private int evaluationSeed = 777001;
        [SerializeField, Min(1)] private int evaluationEpisodes = 200;

        [Header("Runs")]
        [SerializeField] private float editorTimeScale;
        [SerializeField] private int logEveryEpisodes = 100;
        [SerializeField] private float simulationStep = 0.05f;

        public ThiefWorld World { get; private set; }
        public bool IsReady { get; private set; }
        public IReadOnlyList<ThiefMatch> Matches => matches;
        public int MatchCount => Mathf.Min(seekAgents.Length, hideAgents.Length);
        public string ThiefLabel => thiefMode == ThiefMode.None ? "none" : seekAgents.Length > 0 ? seekAgents[0].PolicyLabel : "?";

        private readonly List<ThiefMatch> matches = new();
        private System.Random startRng;
        private int nextEpisode;
        private SimulationMode previousSimulationMode;
        private float previousFixedDelta;
        private double simTime, realAtWindow, simAtWindow;
        private bool evaluationReported;
        private long fingerprint = 1469598103934665603L;
        private int activeMatchLimit;
        private bool holdFinishedRounds;

        // Stats: window and total.
        private int wEpisodes, wWinners, wHides, wRecoveries, wThiefStuns, wThiefHoldingAtEnd, wDecisions;
        private int tEpisodes, tWinners, tHides, tRecoveries, tThiefStuns, tThiefHoldingAtEnd, tDecisions, tPropsMoved;
        private double wReward, tReward;
        private int tAttacks, tHitsLanded, tStunnedByThief, tLateStuns, tFromSpot, tFromMugging, tFromOther;
        private double tHoldSeconds;
        private int tHoldTimeouts;
        private int tLostInThiefHands, tLostOnThiefSpot, tLostOnOwnSpot, tLostOnFloor, tLostHeldByOther;
        private int tFloorOwnerMugged, tFloorThiefStunned, tFloorOther, tFloorLate;

        private bool inGame;

        /// <summary>
        /// Real match (ThiefNpcDirector): only the map knowledge, observations and rule brains are used; no rounds, no
        /// ML-Agents academy, and the game's physics settings stay untouched. Call on an inactive GameObject.
        /// </summary>
        public void ConfigureForGame(HidingSpotBank spotBank)
        {
            inGame = true;
            bank = spotBank;
            seekAgents = Array.Empty<ThiefSeekAgent>();
            hideAgents = Array.Empty<ThiefHideAgent>();
            evaluationMode = false;
            editorTimeScale = 0f;
        }

        public HidingSpotBank Bank => bank;

        private void Awake()
        {
            if (inGame) return;
            var _ = Unity.MLAgents.Academy.Instance;
            previousSimulationMode = Physics.simulationMode;
            previousFixedDelta = Time.fixedDeltaTime;
            Physics.simulationMode = SimulationMode.Script;
            Physics.SyncTransforms();
            Time.fixedDeltaTime = simulationStep;
            if (editorTimeScale > 0f) Time.timeScale = editorTimeScale;
        }

        private void OnDestroy()
        {
            if (inGame) return;
            Physics.simulationMode = previousSimulationMode;
            if (previousFixedDelta > 0f) Time.fixedDeltaTime = previousFixedDelta;
        }

        private void Start()
        {
            if (bank == null || bank.Spots.Count == 0)
            {
                Debug.LogError("[Thief] no hiding spot bank. Build it with Tools > AI > Build Hiding Spot Bank first.", this);
                return;
            }

            World = new ThiefWorld(bank, sizeClass, waypointSpacing, waypointSeed);
            startRng = new System.Random(seed);
            for (var i = 0; i < MatchCount; i++)
            {
                var match = new ThiefMatch(this, World, i);
                matches.Add(match);
                seekAgents[i].Bind(this, match);
                hideAgents[i].Bind(this, match);
                StartNext(match);
            }

            IsReady = matches.Count > 0 && World.Waypoints.Count > 0;
            realAtWindow = Time.realtimeSinceStartupAsDouble;
            Debug.Log($"[Thief] ready. matches {matches.Count}, players {ThiefMatch.Players}, bank spots {World.SpotCount}, waypoints {World.Waypoints.Count}, obs {ObservationVersion} (seek {SeekObservationSize}, hide {HideObservationSize}), thief {thiefMode}, player hiding {playerHiding}, {(evaluationMode ? $"EVALUATION seed {evaluationSeed} x {evaluationEpisodes}" : $"training seed {seed}")}.", this);
        }

        public void ConfigureForViewer()
        {
            activeMatchLimit = 1;
            holdFinishedRounds = true;
            evaluationMode = false;
            editorTimeScale = 0f;
            logEveryEpisodes = int.MaxValue;
        }

        public void RestartMatch(int index, int episodeSeed)
        {
            if (index >= 0 && index < matches.Count) ResetWithSeed(matches[index], episodeSeed);
        }

        private void FixedUpdate()
        {
            if (!IsReady) return;
            var dt = Time.fixedDeltaTime;
            simTime += dt;
            foreach (var match in matches)
            {
                if (match.Done || (activeMatchLimit > 0 && match.Index >= activeMatchLimit)) continue;
                match.Step(dt);
                if (match.CheckGain > 0)
                {
                    // Shaping for search (thief-npc-v2.md 11): a small reward per newly seen space worth checking,
                    // scheduled to 0 by the trainer (environment parameter coverage_reward); evaluation ignores it.
                    if (coverageReward > 0f && !match.ThiefCarrying) seekAgents[match.Index].AddReward(coverageReward * match.CheckGain);
                    tChecked += match.CheckGain;
                    match.CheckGain = 0;
                }

                if (match.Done)
                {
                    Finish(match);
                    continue;
                }

                var t = match.Thief;
                if (match.ThiefActive && t.Macro == ThiefMatch.Macro.Await && !match.ThiefAwaiting && !t.Stunned(match.Time))
                {
                    match.ThiefAwaiting = true;
                    if (match.ThiefCarrying) hideAgents[match.Index].AskForDecision();
                    else seekAgents[match.Index].AskForDecision();
                }
            }
        }

        private void StartNext(ThiefMatch match)
        {
            int episodeSeed;
            if (evaluationMode)
            {
                if (nextEpisode >= evaluationEpisodes) return;
                episodeSeed = evaluationSeed + nextEpisode;
            }
            else
            {
                episodeSeed = startRng.Next();
            }

            nextEpisode++;
            ResetWithSeed(match, episodeSeed);
        }

        private float coverageReward;
        private long tChecked;

        private void ResetWithSeed(ThiefMatch match, int episodeSeed)
        {
            coverageReward = evaluationMode || inGame ? 0f : Unity.MLAgents.Academy.Instance.EnvironmentParameters.GetWithDefault("coverage_reward", 0f);
            match.Reset(episodeSeed, thiefMode == ThiefMode.Agent);
            if (evaluationMode)
            {
                Mix(episodeSeed);
                Mix(Mathf.RoundToInt(match.Thief.Body.Position.x * 100f));
                Mix(Mathf.RoundToInt(match.Props[0].Bottom.z * 100f));
            }
        }

        /// <summary>V4: how a player hid its prop before the search phase.</summary>
        public int ChoosePlayerHidingSpot(ThiefMatch match, int player, List<int> candidates)
        {
            if (playerHiding == PlayerHiding.Random) return candidates[match.Rng.Next(candidates.Count)];
            var best = candidates[0];
            var bestExposure = float.PositiveInfinity;
            foreach (var s in candidates)
            {
                var e = bank.Spots[s].Exposure(sizeClass);
                if (e >= 0f && e < bestExposure)
                {
                    bestExposure = e;
                    best = s;
                }
            }

            return best;
        }

        private void Finish(ThiefMatch match)
        {
            seekAgents[match.Index].Finish(match.ThiefReward);
            hideAgents[match.Index].Finish(match.ThiefReward);
            Record(match);
            if (!holdFinishedRounds) StartNext(match);
        }

        private void Record(ThiefMatch match)
        {
            var winners = match.Winners;
            var stuns = match.Thief.TimesStunned;
            var holding = match.ThiefCarrying ? 1 : 0;
            var moved = 0;
            foreach (var p in match.Props) if (p.ThiefMoves > 0) moved++;
            wEpisodes++; tEpisodes++;
            wWinners += winners; tWinners += winners;
            wHides += match.ThiefHides; tHides += match.ThiefHides;
            wRecoveries += match.OwnerRecoveries; tRecoveries += match.OwnerRecoveries;
            wThiefStuns += stuns; tThiefStuns += stuns;
            wThiefHoldingAtEnd += holding; tThiefHoldingAtEnd += holding;
            wDecisions += match.ThiefDecisions; tDecisions += match.ThiefDecisions;
            tPropsMoved += moved;
            wReward += match.ThiefReward; tReward += match.ThiefReward;
            tAttacks += match.ThiefAttacks; tHitsLanded += match.ThiefHitsLanded; tStunnedByThief += match.PlayersStunnedByThief; tLateStuns += match.LateStunsByThief;
            tFromSpot += match.TakenFromHidingSpot; tFromMugging += match.TakenFromMugging; tFromOther += match.TakenOther;
            tHoldSeconds += match.ThiefHoldSeconds;
            tHoldTimeouts += match.ThiefHoldTimeouts;
            for (var p = 0; p < ThiefMatch.Players; p++)
            {
                var prop = match.Props[p];
                if (prop.HeldBy == p) continue; // this player won
                if (prop.HeldBy == ThiefMatch.Players) tLostInThiefHands++;
                else if (prop.HeldBy >= 0) tLostHeldByOther++;
                else if (prop.ThiefMoves > 0 && prop.Spot >= 0) tLostOnThiefSpot++;
                else if (prop.Spot >= 0 && !prop.MovedSinceHidden) tLostOnOwnSpot++;
                else
                {
                    tLostOnFloor++;
                    if (prop.LastDropper == p)
                    {
                        tFloorOwnerMugged++;
                        if (evaluationMode)
                        {
                            Debug.Log($"[Thief case] seed {match.EpisodeSeed} P{p} dropped its own prop at {prop.LastDropTime:F1}s (stunned by the thief) and it was still on the floor at the end; P{p} ended in {match.Actors[p].Macro}, knows where {match.Minds[p].KnowsWhere}, distance {Vector3.Distance(match.Actors[p].Body.Position, prop.Bottom):F1} m", this);
                        }
                    }
                    else if (prop.LastDropper == ThiefMatch.Players) tFloorThiefStunned++;
                    else tFloorOther++;
                    if (prop.LastDropTime > ThiefMatch.RoundSeconds - 30f) tFloorLate++;
                }
            }

            if (evaluationMode)
            {
                Mix(winners * 1000 + match.ThiefHides * 10 + stuns);
                if (!evaluationReported && tEpisodes >= evaluationEpisodes)
                {
                    evaluationReported = true;
                    var n = (double)tEpisodes;
                    Debug.Log(
                        $"[Thief EVAL] thief={ThiefLabel} players rule, hiding {playerHiding}, episodes {tEpisodes} " +
                        $"winners {100.0 * tWinners / (n * ThiefMatch.Players):F1}% of players, thief reward {tReward / n:F3}, " +
                        $"re-hides/round {tHides / n:F2}, props moved by thief {100.0 * tPropsMoved / (n * ThiefMatch.Players):F0}%, " +
                        $"owner recoveries/round {tRecoveries / n:F2}, thief stunned/round {tThiefStuns / n:F2}, thief holding at end {100.0 * tThiefHoldingAtEnd / n:F0}%, " +
                        $"thief decisions/round {tDecisions / n:F0} | seed {evaluationSeed} fingerprint {fingerprint:X16}",
                        this);
                    var lost = Mathf.Max(1, tLostInThiefHands + tLostOnThiefSpot + tLostOnOwnSpot + tLostOnFloor + tLostHeldByOther);
                    Debug.Log(
                        $"[Thief EVAL detail] per round: thief attacks {tAttacks / n:F2}, hits landed {tHitsLanded / n:F2}, players stunned by thief {tStunnedByThief / n:F2} (in the last 30 s {tLateStuns / n:F2}), " +
                        $"props taken from a hiding spot {tFromSpot / n:F2} / from a player the thief stunned {tFromMugging / n:F2} / other {tFromOther / n:F2}, " +
                        $"seconds holding {tHoldSeconds / n:F0}, 30 s hold timeouts {tHoldTimeouts / n:F2}, spaces checked {tChecked / n:F0} of {World.CheckSpots.Count} | losing players' props at the end: in thief's hands {100.0 * tLostInThiefHands / lost:F0}%, " +
                        $"on a spot the thief hid it {100.0 * tLostOnThiefSpot / lost:F0}%, still on the owner's spot {100.0 * tLostOnOwnSpot / lost:F0}%, " +
                        $"on the floor (dropped) {100.0 * tLostOnFloor / lost:F0}%, held by another player {100.0 * tLostHeldByOther / lost:F0}% (n {lost}) | floor props: dropped by the stunned owner {tFloorOwnerMugged}, by the stunned thief {tFloorThiefStunned}, other {tFloorOther}, dropped in the last 30 s {tFloorLate}",
                        this);
                }
            }

            if (wEpisodes >= logEveryEpisodes) LogWindow();
        }

        private void LogWindow()
        {
            var real = Time.realtimeSinceStartupAsDouble - realAtWindow;
            var sim = simTime - simAtWindow;
            var n = Mathf.Max(1, wEpisodes);
            Debug.Log(
                $"[Thief] episodes {tEpisodes} | window {wEpisodes}: winners {100.0 * wWinners / (n * ThiefMatch.Players):F0}%, thief reward {wReward / n:F3}, " +
                $"re-hides {wHides / (float)n:F2}, recoveries {wRecoveries / (float)n:F2}, thief stunned {wThiefStuns / (float)n:F2}, holding at end {100.0 * wThiefHoldingAtEnd / n:F0}%, " +
                $"decisions {wDecisions / (float)n:F0} | thief {ThiefLabel} | sim speed {(real > 0 ? sim / real : 0):F1}x",
                this);
            wEpisodes = wWinners = wHides = wRecoveries = wThiefStuns = wThiefHoldingAtEnd = wDecisions = 0;
            wReward = 0;
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

        // ------------------------------------------------------------------ observations

        /// <summary>Players, nearest last-seen first (the thief's memory only).</summary>
        private int[] PlayerOrder(ThiefMatch m)
        {
            var order = new[] { 0, 1, 2 };
            var at = m.Thief.Body.Position;
            Array.Sort(order, (x, y) =>
            {
                var dx = m.Mind.PlayerEverSeen[x] ? ThiefWorld.Flat(m.Mind.PlayerSeenPos[x] - at) : 999f;
                var dy = m.Mind.PlayerEverSeen[y] ? ThiefWorld.Flat(m.Mind.PlayerSeenPos[y] - at) : 999f;
                return dx.CompareTo(dy);
            });
            return order;
        }

        private void WritePlayers(ThiefMatch m, float[] o, int k)
        {
            var body = m.Thief.Body;
            foreach (var p in PlayerOrder(m))
            {
                var mind = m.Mind;
                o[k] = mind.PlayerSeenNow[p] ? 1f : 0f;
                o[k + 1] = mind.PlayerEverSeen[p] ? 1f : 0f;
                if (mind.PlayerEverSeen[p])
                {
                    var local = ThiefWorld.Local(body, mind.PlayerSeenPos[p] - body.Position);
                    o[k + 2] = local.x;
                    o[k + 3] = local.z;
                    o[k + 4] = Mathf.Clamp01(ThiefWorld.Flat(mind.PlayerSeenPos[p] - body.Position) / 20f);
                    o[k + 5] = Mathf.Clamp01((m.Time - mind.PlayerSeenTime[p]) / 30f);
                    o[k + 6] = mind.PlayerSeenHolding[p] ? 1f : 0f;
                    o[k + 7] = mind.PlayerSeenNow[p] && mind.PlayerChasing[p] ? 1f : 0f;
                    o[k + 8] = mind.PlayerSeenNow[p] && m.Actors[p].Stunned(m.Time) ? 1f : 0f;
                }
                else
                {
                    o[k + 4] = 1f;
                    o[k + 5] = 1f;
                }

                k += 9;
            }
        }

        public void WriteSeekObservation(ThiefMatch m, float[] o)
        {
            Array.Clear(o, 0, o.Length);
            var t = m.Thief;
            var body = t.Body;
            o[0] = Mathf.Clamp01(m.Time / ThiefMatch.RoundSeconds);
            o[1] = t.Stunned(m.Time) ? 1f : 0f;
            o[2] = t.Hits / (float)ThiefMatch.HitsToStun;
            var known = 0;
            for (var p = 0; p < ThiefMatch.Players; p++) if (m.Mind.PropKnown[p]) known++;
            o[3] = known / (float)ThiefMatch.Players;
            var k = 4;
            for (var slot = 0; slot < ThiefMatch.Players; slot++, k += 7)
            {
                var p = m.KnownPropSlots[slot];
                if (p < 0) continue;
                var at = m.Mind.PropSeenAt[p];
                var local = ThiefWorld.Local(body, at - body.Position);
                o[k] = 1f;
                o[k + 1] = local.x;
                o[k + 2] = local.z;
                o[k + 3] = Mathf.Clamp01(ThiefWorld.Flat(at - body.Position) / 30f);
                o[k + 4] = Mathf.Clamp01((m.Time - m.Mind.PropSeenTime[p]) / 60f);
                o[k + 5] = m.Props[p].ThiefMoves > 0 ? 1f : 0f; // it remembers what it moved itself
                o[k + 6] = Mathf.Clamp((at.y - body.Position.y) / 2f, -1f, 1f);
            }

            WritePlayers(m, o, k);
            k += ThiefMatch.Players * 9;
            for (var slot = 0; slot < 8; slot++, k += 10)
            {
                if (slot >= m.SeekCandidates.Count) continue;
                var w = m.SeekCandidates[slot];
                var local = ThiefWorld.Local(body, World.Waypoints[w] - body.Position);
                o[k] = 1f;
                o[k + 1] = local.x;
                o[k + 2] = local.z;
                o[k + 3] = Mathf.Clamp01(m.SeekCandidatePaths[slot] / 30f);
                o[k + 4] = float.IsNegativeInfinity(m.Mind.VisitedAt[w]) ? 1f : Mathf.Clamp01((m.Time - m.Mind.VisitedAt[w]) / 60f);
                o[k + 5] = Mathf.Clamp01(World.WaypointUnder[w] / 5f);
                o[k + 6] = Mathf.Clamp01(World.WaypointOnFurniture[w] / 5f);
                o[k + 7] = Mathf.Clamp01(World.WaypointCorner[w] / 5f);
                o[k + 8] = Mathf.Clamp01(World.WaypointFloor[w] / 10f);
                o[k + 9] = Mathf.Clamp01(m.UncheckedNear(w) / 5f);
            }

            // Coverage so far, and unchecked spaces under furniture right here (crouch or lie down?).
            o[k] = World.CheckSpots.Count > 0 ? m.CheckedCount / (float)World.CheckSpots.Count : 1f;
            o[k + 1] = Mathf.Clamp01(m.UncheckedUnderHere() / 3f);
        }

        public void WriteHideObservation(ThiefMatch m, float[] o)
        {
            Array.Clear(o, 0, o.Length);
            var t = m.Thief;
            var body = t.Body;
            o[0] = Mathf.Clamp01(m.Time / ThiefMatch.RoundSeconds);
            o[1] = t.Stunned(m.Time) ? 1f : 0f;
            o[2] = t.Hits / (float)ThiefMatch.HitsToStun;
            o[3] = m.ThiefCarrying ? Mathf.Clamp01((m.Time - m.ThiefPickedUpAt) / ThiefMatch.ThiefHoldLimit) : 0f;
            WritePlayers(m, o, 4);
            var k = 4 + ThiefMatch.Players * 9;

            // Nearest player the thief remembers, to judge whether a spot is in sight of someone.
            var nearest = -1;
            var nearestDistance = float.PositiveInfinity;
            for (var p = 0; p < ThiefMatch.Players; p++)
            {
                if (!m.Mind.PlayerEverSeen[p]) continue;
                var d = ThiefWorld.Flat(m.Mind.PlayerSeenPos[p] - body.Position);
                if (d < nearestDistance)
                {
                    nearestDistance = d;
                    nearest = p;
                }
            }

            var half = World.ObjectHalf;
            for (var slot = 0; slot < 8; slot++, k += 18)
            {
                if (slot >= m.HideCandidates.Count) continue;
                var spot = bank.Spots[m.HideCandidates[slot]];
                var centre = spot.Position + Vector3.up * (half.y + 0.011f);
                var local = ThiefWorld.Local(body, spot.Position - body.Position);
                o[k] = 1f;
                o[k + 1] = local.x;
                o[k + 2] = local.z;
                o[k + 3] = Mathf.Clamp01(m.HideCandidatePaths[slot] / 15f);
                o[k + 4] = Mathf.Clamp((spot.Position.y - body.Position.y) / 2f, -1f, 1f);
                o[k + 5] = (spot.Tags & HidingSpotTags.Under) != 0 ? 1f : 0f;
                o[k + 6] = (spot.Tags & HidingSpotTags.OnFurniture) != 0 ? 1f : 0f;
                o[k + 7] = (spot.Tags & HidingSpotTags.Corner) != 0 ? 1f : 0f;
                var rays = new float[8];
                for (var r = 0; r < 8; r++)
                {
                    var dir = Quaternion.Euler(0f, r * 45f, 0f) * Vector3.forward;
                    rays[r] = HideSeekVision.Raycast(centre, dir, 3f, World.OccluderMask, out var hitDistance) ? hitDistance / 3f : 1f;
                }

                Array.Sort(rays);
                for (var r = 0; r < 8; r++) o[k + 8 + r] = rays[r];
                if (nearest >= 0)
                {
                    var eye = m.Mind.PlayerSeenPos[nearest] + Vector3.up * HideSeekRules.StandEyeHeight;
                    o[k + 16] = HideSeekVision.Clear(eye, centre, World.OccluderMask) ? 1f : 0f;
                    o[k + 17] = Mathf.Clamp01(Vector3.Distance(m.Mind.PlayerSeenPos[nearest], spot.Position) / 20f);
                }
                else
                {
                    o[k + 17] = 1f;
                }
            }
        }

        // ------------------------------------------------------------------ baseline thieves

        /// <summary>Rule thief: go for a prop it saw, otherwise sweep waypoints; never fights.</summary>
        public int RuleSeekAction(ThiefMatch m)
        {
            if (m.SeekAllowed(ThiefMatch.SeekActionPropFirst)) return ThiefMatch.SeekActionPropFirst;
            var threat = m.NearestThreat(out var threatPos);
            if (threat >= 0 && ThiefWorld.Flat(threatPos - m.Thief.Body.Position) < 4f && m.Mind.PlayerChasing[threat]) return ThiefMatch.SeekActionFlee;
            if (m.LastThiefMacro == ThiefMatch.Macro.ToWaypoint)
            {
                return World.WaypointUnder[World.NearestWaypoint(m.Thief.Body.Position)] > 0 ? ThiefMatch.SeekActionCrouchLook : ThiefMatch.SeekActionLook;
            }

            // Crouching did not show everything under the furniture here: lie down and look once more.
            if (m.LastThiefMacro == ThiefMatch.Macro.CrouchLook && m.UncheckedUnderHere() > 0) return ThiefMatch.SeekActionProneLook;

            var best = ThiefMatch.SeekActionLook;
            var bestScore = float.PositiveInfinity;
            for (var i = 0; i < m.SeekCandidates.Count; i++)
            {
                var w = m.SeekCandidates[i];
                var since = float.IsNegativeInfinity(m.Mind.VisitedAt[w]) ? 999f : m.Time - m.Mind.VisitedAt[w];
                var score = m.SeekCandidatePaths[i] + (since < 60f ? 30f : 0f) - 2f * Mathf.Min(m.UncheckedNear(w), 5);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }

            return best;
        }

        /// <summary>Rule thief while carrying: run from a close player, else the best bank spot of the candidates.</summary>
        public int RuleHideAction(ThiefMatch m)
        {
            var threat = m.NearestThreat(out var threatPos);
            if (threat >= 0 && ThiefWorld.Flat(threatPos - m.Thief.Body.Position) < 6f) return ThiefMatch.HideActionFlee;
            var best = ThiefMatch.HideActionRelocate;
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

        public static int RandomAllowed(Func<int, bool> allowed, int count, System.Random rng, int fallback)
        {
            for (var tries = 0; tries < 30; tries++)
            {
                var a = rng.Next(count);
                if (allowed(a)) return a;
            }

            return fallback;
        }
    }
}
