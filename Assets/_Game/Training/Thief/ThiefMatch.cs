using System.Collections.Generic;
using Game.Training.HideSeek;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Training.Thief
{
    /// <summary>
    /// One search phase with a thief NPC (docs/planning/thief-npc-v2.md): three player bots whose props are already
    /// hidden, and one thief that finds player props and hides them elsewhere, keeps moving until the end, and drops
    /// what it holds when stunned. Combat, reach and movement follow the game config (MatchRules, CombatConfig,
    /// InteractionConfig, MovementConfig). Players win if they hold their own prop at 360 s.
    /// </summary>
    public sealed class ThiefMatch
    {
        public const int Players = 3;
        public const float RoundSeconds = 360f;          // MatchRules.searchingDurationSeconds
        public const int HitsToStun = 3;                 // MatchRules.hitsRequiredToStun
        public const float StunSeconds = 2f;             // MatchRules.stunDurationSeconds
        // NPC design (thief-npc-v2.md 9.6): the thief never hits players; it must hide what it takes within 30 s
        // (the players' hiding turn) or it drops the prop where it stands.
        public const bool ThiefCanAttack = false;
        public const float ThiefHoldLimit = 30f;

        public const float AttackCooldown = 0.5f;        // CombatConfig.attackCooldownSeconds
        public const float AttackHitDelay = 0.35f;       // CombatConfig.attackHitDelaySeconds
        public const float AttackReach = 0.8f + 0.7f;    // CombatConfig forward offset + radius
        public const float ScanDegreesPerSecond = 180f;

        public enum Macro { Await, ToWaypoint, Look, CrouchLook, ToProp, Flee, Attack, ToSpot, Relocate, Wander, Guard, Chase, Idle, ProneLook }

        public sealed class Actor
        {
            public int Id;
            public bool IsThief;
            public readonly HideSeekBody Body = new();
            public int Holding = -1;
            public int Hits;
            public float StunnedUntil = -1f;
            public float NextAttackAt;
            public float PendingHitAt = -1f;
            public int PendingTarget = -1;
            public Macro Macro = Macro.Await;
            public int Target = -1;          // attack target id, prop index or spot index depending on the macro
            public float MacroUntil;
            public float ScanLeft;
            public int ApproachPosture;
            public float SettleUntil = -1f;
            public float NextRepath;
            public int TimesStunned;
            public float LastSawTargetAt;
            public ReachPlan Reach;
            public bool Hopped;
            public float HopAt = -1f;
            public Vector3 LastTargetPos;
            public bool Stunned(float t) => t < StunnedUntil;
        }

        public sealed class Prop
        {
            public int Owner;
            public int HeldBy = -1;
            public Vector3 Bottom;
            public int Spot = -1;
            public int ThiefMoves;
            public bool MovedSinceHidden;
            public bool DroppedByThiefStun;
            public int LastDropper = -1;
            public float LastDropTime = -1f;
        }

        /// <summary>Rule player bot: what it knows and how it plays this round (randomised per round, V3).</summary>
        public sealed class PlayerMind
        {
            public bool KnowsWhere = true;
            public Vector3 Believed;
            public float Reaction;
            public float Aggression;
            public bool HoldEarly;
            public float GrabAt;
            public float NextThink;
            public float NextWander;
            public int HolderSeen = -1;
            public float HolderSeenAt = -999f;
            public Vector3 HolderSeenPos;
            public float HolderLastSeenAt = -999f;
            public float[] VisitedAt;
            public float ThreatSeenAt = -999f;
            public Vector3 ThreatPos;
        }

        /// <summary>Thief memory: only what it saw.</summary>
        public sealed class ThiefMind
        {
            public readonly bool[] PropKnown = new bool[Players];
            public readonly Vector3[] PropSeenAt = new Vector3[Players];
            public readonly float[] PropSeenTime = new float[Players];
            public readonly int[] PropSeenHeldBy = new int[Players];
            public readonly bool[] PlayerEverSeen = new bool[Players];
            public readonly bool[] PlayerSeenNow = new bool[Players];
            public readonly Vector3[] PlayerSeenPos = new Vector3[Players];
            public readonly float[] PlayerSeenTime = new float[Players];
            public readonly bool[] PlayerSeenHolding = new bool[Players];
            public readonly bool[] PlayerChasing = new bool[Players];
            public readonly float[] PlayerLastDistance = new float[Players];
            public float[] VisitedAt;
        }

        private readonly ThiefArena arena;
        private readonly ThiefWorld world;

        public readonly int Index;
        public readonly Actor[] Actors = new Actor[Players + 1];
        public readonly Prop[] Props = new Prop[Players];
        public readonly PlayerMind[] Minds = new PlayerMind[Players];
        public readonly ThiefMind Mind = new();

        public readonly List<int> HideCandidates = new();
        public readonly List<float> HideCandidatePaths = new();
        public readonly List<int> SeekCandidates = new();
        public readonly List<float> SeekCandidatePaths = new();
        public readonly int[] KnownPropSlots = new int[Players];

        public int EpisodeSeed { get; private set; }
        public float Time { get; private set; }
        public bool Done { get; private set; }
        public bool ThiefActive { get; private set; } = true;
        public bool ThiefAwaiting;
        public int ThiefDecisions;
        public int ThiefHides;
        public int OwnerRecoveries;
        public Macro LastThiefMacro = Macro.Await;

        // Behaviour counters (analysis only; never observed by the brains).
        public int ThiefAttacks, ThiefHitsLanded, PlayersStunnedByThief, LateStunsByThief;
        public int TakenFromHidingSpot, TakenFromMugging, TakenOther;
        public float ThiefHoldSeconds;
        public int ThiefHoldTimeouts;
        public int PropChoiceOffered, PropChoiceTaken, PropsSpotted; // analysis only
        public readonly int[] PropSlotChosen = new int[Players];
        public int ToPropPlanFailed, ToPropArrived, ToPropEndedWithoutPickup;
        public int MissHeld, MissMoved, MissReach, MissSight, MissOther; // why a go-for-prop ended without a pickup
        public float ThiefPickedUpAt = -1f;

        /// <summary>Coverage (thief-npc-v2.md 11): which spaces worth checking the thief has actually seen.</summary>
        public bool[] Checked;
        public int CheckedCount;
        public int CheckGain; // since the arena last paid it out
        private float nextCoverage;
        private readonly List<int> scratchChecks = new();

        private System.Random rng;
        private float nextVision;
        private readonly List<int> scratchIds = new();
        private readonly List<float> scratchPaths = new();
        private readonly List<Vector3> otherProps = new();

        public ThiefMatch(ThiefArena arena, ThiefWorld world, int index)
        {
            this.arena = arena;
            this.world = world;
            Index = index;
            for (var i = 0; i <= Players; i++)
            {
                Actors[i] = new Actor { Id = i, IsThief = i == Players };
                Actors[i].Body.CanSprint = i < Players;
            }

            for (var i = 0; i < Players; i++)
            {
                Props[i] = new Prop { Owner = i };
                Minds[i] = new PlayerMind();
            }
        }

        public Actor Thief => Actors[Players];
        public System.Random Rng => rng;
        public bool ThiefCarrying => Thief.Holding >= 0;

        public int Winners
        {
            get
            {
                var n = 0;
                for (var i = 0; i < Players; i++)
                {
                    if (Props[i].HeldBy == i) n++;
                }

                return n;
            }
        }

        /// <summary>Thief reward in [-1, 1]: +1 when no player ends holding their prop, -1 when all do.</summary>
        public float ThiefReward => (Players - 2f * Winners) / Players;

        // ------------------------------------------------------------------ setup

        public void Reset(int seed, bool thiefActive)
        {
            EpisodeSeed = seed;
            rng = new System.Random(seed);
            Time = 0f;
            Done = false;
            ThiefActive = thiefActive;
            ThiefAwaiting = false;
            ThiefDecisions = ThiefHides = OwnerRecoveries = 0;
            ThiefAttacks = ThiefHitsLanded = PlayersStunnedByThief = LateStunsByThief = 0;
            TakenFromHidingSpot = TakenFromMugging = TakenOther = 0;
            ThiefHoldSeconds = 0f;
            ThiefHoldTimeouts = 0;
            ThiefPickedUpAt = -1f;
            PropChoiceOffered = PropChoiceTaken = PropsSpotted = 0;
            Reflexes = 0;
            SearchStartedAt = 0f;
            ReflexGiveUps = 0;
            System.Array.Clear(ReflexBlockedUntil, 0, Players);
            System.Array.Clear(reflexMisses, 0, Players);
            ToPropPlanFailed = ToPropArrived = ToPropEndedWithoutPickup = 0;
            MissHeld = MissMoved = MissReach = MissSight = MissOther = 0;
            System.Array.Clear(PropSlotChosen, 0, Players);
            nextVision = 0f;
            ResetCoverage();
            var wp = world.Waypoints;

            // Players' props are already hidden (the hiding phase is not simulated, V9); V4 decides how.
            for (var i = 0; i < Players; i++)
            {
                var from = wp[rng.Next(wp.Count)];
                Props[i].HeldBy = -1;
                Props[i].ThiefMoves = 0;
                Props[i].MovedSinceHidden = false;
                Props[i].DroppedByThiefStun = false;
                Props[i].LastDropper = -1;
                Props[i].LastDropTime = -1f;
                CollectOtherProps(i);
                world.FillHideCandidates(from, rng, otherProps, 8, scratchIds, scratchPaths);
                var spot = scratchIds.Count > 0 ? arena.ChoosePlayerHidingSpot(this, i, scratchIds) : -1;
                Props[i].Spot = spot;
                Props[i].Bottom = spot >= 0
                    ? world.Bank.Spots[spot].Position + Vector3.up * 0.011f
                    : (NavMesh.SamplePosition(from, out var floor, 1f, NavMesh.AllAreas) ? floor.position : from);
            }

            for (var i = 0; i <= Players; i++)
            {
                var a = Actors[i];
                a.Holding = -1;
                a.Hits = 0;
                a.StunnedUntil = -1f;
                a.NextAttackAt = 0f;
                a.PendingHitAt = -1f;
                a.PendingTarget = -1;
                a.Macro = Macro.Await;
                a.Target = -1;
                a.SettleUntil = -1f;
                a.Hopped = false;
                a.HopAt = -1f;
                a.TimesStunned = 0;
                a.Body.HasLookTarget = false;
                Vector3 start = default;
                for (var tries = 0; tries < 30; tries++)
                {
                    start = wp[rng.Next(wp.Count)];
                    var ok = true;
                    if (a.IsThief)
                    {
                        for (var p = 0; p < Players && ok; p++)
                        {
                            ok = ThiefWorld.Flat(start - Actors[p].Body.Position) >= 8f;
                        }
                    }
                    else
                    {
                        var d = ThiefWorld.Flat(start - Props[i].Bottom);
                        ok = d >= 6f && d <= 22f;
                    }

                    if (ok) break;
                }

                a.Body.Teleport(start, (float)(rng.NextDouble() * 360.0));
            }

            for (var i = 0; i < Players; i++)
            {
                var m = Minds[i];
                m.KnowsWhere = true;
                m.Believed = Props[i].Bottom;
                m.Reaction = Mathf.Lerp(0.3f, 1.2f, (float)rng.NextDouble());
                m.Aggression = Mathf.Lerp(0.3f, 1f, (float)rng.NextDouble());
                m.HoldEarly = rng.NextDouble() < 0.5;
                m.GrabAt = Mathf.Lerp(240f, 330f, (float)rng.NextDouble());
                m.NextThink = 0f;
                m.NextWander = 0f;
                m.HolderSeen = -1;
                m.HolderSeenAt = -999f;
                m.HolderLastSeenAt = -999f;
                m.ThreatSeenAt = -999f;
                m.VisitedAt ??= new float[wp.Count];
                for (var w = 0; w < wp.Count; w++) m.VisitedAt[w] = float.NegativeInfinity;
            }

            for (var p = 0; p < Players; p++)
            {
                Mind.PropKnown[p] = false;
                Mind.PropSeenTime[p] = -999f;
                Mind.PropSeenHeldBy[p] = -1;
                Mind.PlayerEverSeen[p] = false;
                Mind.PlayerSeenNow[p] = false;
                Mind.PlayerSeenTime[p] = -999f;
                Mind.PlayerSeenHolding[p] = false;
                Mind.PlayerChasing[p] = false;
                Mind.PlayerLastDistance[p] = 99f;
            }

            Mind.VisitedAt ??= new float[wp.Count];
            for (var w = 0; w < wp.Count; w++) Mind.VisitedAt[w] = float.NegativeInfinity;

            if (!ThiefActive)
            {
                Thief.Macro = Macro.Idle; // "no NPC" baseline: the thief never acts and is invisible (see IsVisibleThief)
            }

            RefreshThiefCandidates();
        }

        private void CollectOtherProps(int except)
        {
            otherProps.Clear();
            for (var p = 0; p < Players; p++)
            {
                if (p != except && Props[p].HeldBy < 0 && (Props[p].Spot >= 0 || Props[p].MovedSinceHidden))
                {
                    otherProps.Add(Props[p].Bottom);
                }
            }
        }

        public void RefreshThiefCandidates()
        {
            world.FillWaypointCandidates(Thief.Body.Position, 8, SeekCandidates, SeekCandidatePaths);
            if (ThiefCarrying)
            {
                CollectOtherProps(Thief.Holding);
                world.FillHideCandidates(Thief.Body.Position, rng, otherProps, 8, HideCandidates, HideCandidatePaths);
            }
            else
            {
                HideCandidates.Clear();
                HideCandidatePaths.Clear();
            }

            // Known placed props, nearest first (at most 3 slots).
            for (var k = 0; k < Players; k++) KnownPropSlots[k] = -1;
            scratchIds.Clear();
            for (var p = 0; p < Players; p++)
            {
                if (Mind.PropKnown[p] && Mind.PropSeenHeldBy[p] < 0 && Props[p].HeldBy != Players)
                {
                    scratchIds.Add(p);
                }
            }

            var at = Thief.Body.Position;
            scratchIds.Sort((x, y) => ThiefWorld.Flat(Mind.PropSeenAt[x] - at).CompareTo(ThiefWorld.Flat(Mind.PropSeenAt[y] - at)));
            for (var k = 0; k < scratchIds.Count && k < Players; k++) KnownPropSlots[k] = scratchIds[k];
        }

        // ------------------------------------------------------------------ thief decisions

        public const int SeekActionLook = 8;
        public const int SeekActionCrouchLook = 9;
        public const int SeekActionPropFirst = 10;   // 10, 11, 12
        public const int SeekActionFlee = 13;
        public const int SeekActionAttack = 14;
        public const int SeekActionProneLook = 15;
        public const int SeekActionCount = 16;

        public const int HideActionRelocate = 8;
        public const int HideActionFlee = 9;
        public const int HideActionCount = 10;

        /// <summary>
        /// Reflex (thief-npc-v2.md 11.3): an empty-handed thief that knows where a player's prop lies goes for the
        /// nearest one without asking the seek brain, the way pickup itself is automatic. The brains learn where to
        /// search, how to look (stand / crouch / lie down), when to avoid players and where to hide; with only the
        /// round outcome as reward the brains never learned to take a prop they saw (about 1 in 10, v2g/v2h).
        /// </summary>
        public const bool GoForSeenPropReflex = true;

        public int Reflexes;

        /// <summary>Start of the current search (round start, or the last time the thief let go of a prop).</summary>
        public float SearchStartedAt;

        /// <summary>Props the reflex gave up on for a while (no way to grab them, or twice there without a pickup).</summary>
        public readonly float[] ReflexBlockedUntil = new float[Players];
        private readonly int[] reflexMisses = new int[Players];
        public int ReflexGiveUps;

        public bool TryReflexGoForProp()
        {
            if (!GoForSeenPropReflex || ThiefCarrying || !SeekAllowed(SeekActionPropFirst)) return false;
            var p = KnownPropSlots[0];
            if (p < 0 || Time < ReflexBlockedUntil[p]) return false;
            Reflexes++;
            ApplyThiefAction(SeekActionPropFirst);
            if (Thief.Macro != Macro.ToProp) GiveUpReflex(p); // no reach plan: it fell back to looking around
            return true;
        }

        private void GiveUpReflex(int p)
        {
            ReflexBlockedUntil[p] = Time + 30f;
            reflexMisses[p] = 0;
            ReflexGiveUps++;
        }

        public bool SeekAllowed(int action)
        {
            if (action < 8) return action < SeekCandidates.Count;
            if (action >= SeekActionPropFirst && action < SeekActionPropFirst + Players) return KnownPropSlots[action - SeekActionPropFirst] >= 0;
            if (action == SeekActionFlee) return NearestThreat(out _) >= 0;
            if (action == SeekActionAttack) return ThiefCanAttack && AttackTargetCandidate() >= 0;
            return true;
        }

        public bool HideAllowed(int action)
        {
            if (action < 8) return action < HideCandidates.Count;
            if (action == HideActionFlee) return NearestThreat(out _) >= 0;
            return true;
        }

        public void ApplyThiefAction(int action)
        {
            ThiefAwaiting = false;
            ThiefDecisions++;
            if (!ThiefCarrying && SeekAllowed(SeekActionPropFirst))
            {
                PropChoiceOffered++;
                if (action >= SeekActionPropFirst && action < SeekActionPropFirst + Players)
                {
                    PropChoiceTaken++;
                    PropSlotChosen[action - SeekActionPropFirst]++;
                }
            }

            var t = Thief;
            var b = t.Body;
            b.Crouched = b.Prone = false;
            b.Pitch = HideSeekRules.WalkPitch;
            b.HasLookTarget = false;
            t.SettleUntil = -1f;
            b.WantsSprint = false;

            if (ThiefCarrying)
            {
                if (action < 8 && action < HideCandidates.Count && b.TrySetDestination(world.Bank.Spots[HideCandidates[action]].StandPosition))
                {
                    t.Macro = Macro.ToSpot;
                    t.Target = HideCandidates[action];
                    return;
                }

                if (action == HideActionFlee && NearestThreat(out var threatPos) >= 0 && world.TryPickAway(b.Position, threatPos, rng, out var away) && b.TrySetDestination(away))
                {
                    t.Macro = Macro.Flee;
                    return;
                }

                if (world.TryPickRandomWaypoint(b.Position, rng, 6f, 20f, out var somewhere) && b.TrySetDestination(somewhere))
                {
                    t.Macro = Macro.Relocate;
                    return;
                }

                b.Stop();
                t.ScanLeft = 360f;
                t.Macro = Macro.Look;
                return;
            }

            if (action < 8 && action < SeekCandidates.Count && b.TrySetDestination(world.Waypoints[SeekCandidates[action]]))
            {
                t.Macro = Macro.ToWaypoint;
                t.Target = SeekCandidates[action];
                return;
            }

            if (action >= SeekActionPropFirst && action < SeekActionPropFirst + Players)
            {
                var p = KnownPropSlots[action - SeekActionPropFirst];
                if (p >= 0 && world.TryPlanReach(b.Position, Mind.PropSeenAt[p], out t.Reach) &&
                    b.TrySetDestination(t.Reach.Walk, 0.5f))
                {
                    BeginReach(t);
                    b.HasLookTarget = true;
                    b.LookTarget = Mind.PropSeenAt[p] + Vector3.up * world.ObjectHalf.y;
                    t.Macro = Macro.ToProp;
                    t.Target = p;
                    return;
                }

                ToPropPlanFailed++;
            }

            if (action == SeekActionFlee && NearestThreat(out var threat) >= 0 && world.TryPickAway(b.Position, threat, rng, out var fleeTo) && b.TrySetDestination(fleeTo))
            {
                t.Macro = Macro.Flee;
                return;
            }

            if (action == SeekActionAttack)
            {
                var target = AttackTargetCandidate();
                if (target >= 0 && b.TrySetDestination(Mind.PlayerSeenPos[target]))
                {
                    ThiefAttacks++;
                    t.Macro = Macro.Attack;
                    t.Target = target;
                    t.MacroUntil = Time + 8f;
                    t.NextRepath = Time + 0.5f;
                    t.LastSawTargetAt = Mind.PlayerSeenTime[target];
                    t.LastTargetPos = Mind.PlayerSeenPos[target];
                    return;
                }
            }

            b.Stop();
            t.ScanLeft = 360f;
            if (action == SeekActionCrouchLook)
            {
                b.Crouched = true;
                b.Pitch = HideSeekRules.CrouchScanPitch;
                t.Macro = Macro.CrouchLook;
            }
            else if (action == SeekActionProneLook)
            {
                b.Prone = true;
                b.Pitch = HideSeekRules.ProneScanPitch;
                t.Macro = Macro.ProneLook;
            }
            else
            {
                b.Pitch = HideSeekRules.ScanPitch;
                t.Macro = Macro.Look;
            }
        }

        /// <summary>Nearest player the thief saw in the last 2 s within 10 m (their last seen position).</summary>
        public int NearestThreat(out Vector3 position)
        {
            position = default;
            var best = -1;
            var bestDistance = 10f;
            for (var p = 0; p < Players; p++)
            {
                if (Time - Mind.PlayerSeenTime[p] > 2f) continue;
                var d = ThiefWorld.Flat(Mind.PlayerSeenPos[p] - Thief.Body.Position);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = p;
                    position = Mind.PlayerSeenPos[p];
                }
            }

            return best;
        }

        /// <summary>A player seen holding something in the last 3 s within 8 m.</summary>
        public int AttackTargetCandidate()
        {
            var best = -1;
            var bestDistance = 8f;
            for (var p = 0; p < Players; p++)
            {
                if (!Mind.PlayerSeenHolding[p] || Time - Mind.PlayerSeenTime[p] > 3f || Actors[p].Stunned(Time)) continue;
                var d = ThiefWorld.Flat(Mind.PlayerSeenPos[p] - Thief.Body.Position);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = p;
                }
            }

            return best;
        }

        // ------------------------------------------------------------------ simulation

        public void Step(float dt)
        {
            if (Done)
            {
                return;
            }

            Time += dt;
            if (ThiefCarrying)
            {
                ThiefHoldSeconds += dt;
                if (Time - ThiefPickedUpAt >= ThiefHoldLimit)
                {
                    ThiefHoldTimeouts++;
                    Drop(Thief);
                }
            }
            ResolveHits();
            if (ThiefActive)
            {
                StepThief(dt);
            }

            for (var i = 0; i < Players; i++)
            {
                StepPlayer(i, dt);
            }

            for (var i = 0; i <= Players; i++)
            {
                var a = Actors[i];
                if (a.Stunned(Time))
                {
                    a.Body.Stop();
                    continue;
                }

                if (!a.IsThief || ThiefActive)
                {
                    a.Body.Step(dt);
                }
            }

            MarkVisited();
            if (Time >= nextVision)
            {
                nextVision = Time + HideSeekRules.VisionIntervalSeconds;
                See();
                AutoPickups();
            }

            UpdateCoverage();
            if (Time >= RoundSeconds)
            {
                Done = true;
            }
        }

        private void StepThief(float dt)
        {
            var t = Thief;
            if (t.Stunned(Time))
            {
                return;
            }

            var b = t.Body;
            switch (t.Macro)
            {
                case Macro.ToWaypoint:
                case Macro.Relocate:
                case Macro.Flee:
                    if (!b.Moving)
                    {
                        // The real NPC can stop short of a waypoint (a blocked step); count the walk as the visit so the
                        // search brain does not send it back to the room it just left.
                        if (Mirrored && t.Macro == Macro.ToWaypoint && t.Target >= 0 && t.Target < Mind.VisitedAt.Length) Mind.VisitedAt[t.Target] = Time;
                        Finish(t);
                    }

                    break;
                case Macro.ToProp:
                    if (!b.Moving && t.SettleUntil < 0f && !t.Hopped) ToPropArrived++;
                    if (!b.Moving && !StepReachArrival(t))
                    {
                        ToPropEndedWithoutPickup++;

                        // It stood where it planned to grab from, in the posture that shows that spot, and still does not
                        // see the prop: it is gone (someone took it). Without this the thief kept walking back to an empty
                        // spot under furniture, because the old "gone" check only looked from standing eye height.
                        if (t.Target >= 0 && !CanSeeProp(t, t.Target)) Mind.PropKnown[t.Target] = false;
                        if (t.Target >= 0 && ++reflexMisses[t.Target] >= 2) GiveUpReflex(t.Target);
                        if (t.Target >= 0)
                        {
                            var miss = Props[t.Target];
                            if (miss.HeldBy >= 0) MissHeld++;
                            else if ((miss.Bottom - Mind.PropSeenAt[t.Target]).sqrMagnitude > 0.25f) MissMoved++;
                            else if (!world.InGrabReach(b.Position, miss.Bottom, b.Jumping)) MissReach++;
                            else if (!CanSeeProp(t, t.Target)) MissSight++;
                            else MissOther++;
                        }

                        Finish(t);
                    }

                    break;
                case Macro.ToSpot:
                    if (!b.Moving)
                    {
                        var spot = t.Target;
                        CollectOtherProps(t.Holding);
                        if (t.Holding >= 0 && world.CanPlaceFrom(b.Position, spot) && world.FitsNow(spot, otherProps) &&
                            (!Mirrored || MirrorPlace(t.Holding, spot)))
                        {
                            var prop = Props[t.Holding];
                            prop.HeldBy = -1;
                            prop.Spot = spot;
                            prop.Bottom = world.Bank.Spots[spot].Position + Vector3.up * 0.011f;
                            prop.ThiefMoves++;
                            prop.MovedSinceHidden = true;
                            t.Holding = -1;
                            ThiefHides++;
                            SearchStartedAt = Time;
                        }

                        Finish(t);
                    }

                    break;
                case Macro.Attack:
                    StepAttack(t, t.Target, 3f, dt);
                    break;
                case Macro.Look:
                case Macro.CrouchLook:
                case Macro.ProneLook:
                    var turn = ScanDegreesPerSecond * dt * (t.Macro == Macro.CrouchLook ? 0.75f : t.Macro == Macro.ProneLook ? 0.5f : 1f);
                    b.Yaw += turn;
                    t.ScanLeft -= turn;
                    if (t.ScanLeft <= 0f) Finish(t);
                    break;
            }
        }

        private void Finish(Actor t)
        {
            var b = t.Body;
            b.HasLookTarget = false;
            b.Crouched = b.Prone = b.Jumping = false;
            b.Pitch = HideSeekRules.WalkPitch;
            t.SettleUntil = -1f;
            if (t.Hopped)
            {
                b.Position = t.Reach.Walk; // jump back down to the floor
                t.Hopped = false;
            }
            if (t.IsThief)
            {
                LastThiefMacro = t.Macro;
            }

            t.Macro = Macro.Await;
            if (t.IsThief)
            {
                RefreshThiefCandidates();
            }
        }

        /// <summary>Walk to the target, face it, punch when in reach (cooldown 0.5 s, hit lands 0.35 s later).</summary>
        private void StepAttack(Actor attacker, int targetId, float loseAfter, float dt)
        {
            var target = Actors[targetId];
            var b = attacker.Body;
            if (CanSee(attacker, target))
            {
                attacker.LastSawTargetAt = Time;
                attacker.LastTargetPos = target.Body.Position;
            }

            if (target.Stunned(Time) || Time >= attacker.MacroUntil || Time - attacker.LastSawTargetAt > loseAfter)
            {
                Finish(attacker);
                return;
            }

            // Only where the target was last seen is known; a punch still needs it to be in front and in reach.
            var toTarget = attacker.LastTargetPos - b.Position;
            var distance = ThiefWorld.Flat(toTarget);
            if (distance <= AttackReach - 0.1f && Time - attacker.LastSawTargetAt < 0.2f)
            {
                b.Stop();
                b.TurnToward(Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg, dt);
                if (Time >= attacker.NextAttackAt && attacker.PendingHitAt < 0f)
                {
                    attacker.NextAttackAt = Time + AttackCooldown;
                    attacker.PendingHitAt = Time + AttackHitDelay;
                    attacker.PendingTarget = targetId;
                }
            }
            else if (Time >= attacker.NextRepath)
            {
                attacker.NextRepath = Time + 0.5f;
                b.TrySetDestination(attacker.LastTargetPos);
            }
        }

        private void ResolveHits()
        {
            for (var i = 0; i <= Players; i++)
            {
                var a = Actors[i];
                if (a.PendingHitAt < 0f || Time < a.PendingHitAt) continue;
                a.PendingHitAt = -1f;
                if (a.Stunned(Time) || a.PendingTarget < 0) continue;
                var target = Actors[a.PendingTarget];
                var offset = target.Body.Position - a.Body.Position;
                var forward = Quaternion.Euler(0f, a.Body.Yaw, 0f) * Vector3.forward;
                if (target.Stunned(Time) || ThiefWorld.Flat(offset) > AttackReach || Vector3.Angle(forward, new Vector3(offset.x, 0f, offset.z)) > 60f)
                {
                    continue; // swung and missed
                }

                target.Hits++;
                if (a.IsThief) ThiefHitsLanded++;
                if (!target.IsThief)
                {
                    var mind = Minds[target.Id];
                    mind.ThreatSeenAt = Time;
                    mind.ThreatPos = a.Body.Position;
                }

                if (target.Hits >= HitsToStun)
                {
                    target.Hits = 0;
                    target.StunnedUntil = Time + StunSeconds;
                    target.TimesStunned++;
                    target.Body.Stop();
                    if (a.IsThief) PlayersStunnedByThief++;
                    if (a.IsThief && Time > RoundSeconds - 30f) LateStunsByThief++;
                    Drop(target, a.IsThief);
                }
            }
        }

        private void Drop(Actor a, bool byThief = false)
        {
            if (a.Holding < 0) return;
            if (Mirrored && a.IsThief && !MirrorDrop()) return;
            if (a.IsThief) SearchStartedAt = Time;
            var prop = Props[a.Holding];
            prop.DroppedByThiefStun = byThief;
            prop.LastDropper = a.Id;
            prop.LastDropTime = Time;
            prop.HeldBy = -1;
            prop.Spot = -1;
            prop.Bottom = NavMesh.SamplePosition(a.Body.Position, out var floor, 1f, NavMesh.AllAreas) ? floor.position : a.Body.Position;
            prop.MovedSinceHidden = true;
            a.Holding = -1;
            if (a.IsThief)
            {
                Finish(a);
            }

            // The owner knows where its prop fell if it was the one holding it or can see the spot (red outline).
            // Without this the player bot would walk away from its own prop lying at its feet (it looks level and
            // the floor right below is outside the view cone) -- a bot flaw a learned thief exploited.
            var owner = Actors[prop.Owner];
            if (owner == a || CanSeeProp(owner, prop.Owner) ||
                HideSeekVision.Clear(owner.Body.Eye, prop.Bottom + Vector3.up * world.ObjectHalf.y, world.OccluderMask) &&
                ThiefWorld.Flat(owner.Body.Position - prop.Bottom) < 8f)
            {
                var mind = Minds[prop.Owner];
                mind.KnowsWhere = true;
                mind.Believed = prop.Bottom;
                if (owner.Macro != Macro.Attack)
                {
                    Finish(owner);
                }
            }
        }

        // ------------------------------------------------------------------ player rule bot (V3)

        private void StepPlayer(int i, float dt)
        {
            var a = Actors[i];
            var m = Minds[i];
            var b = a.Body;
            if (a.Stunned(Time))
            {
                return;
            }

            switch (a.Macro)
            {
                case Macro.Attack:
                    StepAttack(a, a.Target, 4f, dt);
                    return;
                case Macro.Look:
                case Macro.CrouchLook:
                    var turn = ScanDegreesPerSecond * dt * (a.Macro == Macro.CrouchLook ? 0.75f : 1f);
                    b.Yaw += turn;
                    a.ScanLeft -= turn;
                    if (a.ScanLeft <= 0f) Finish(a);
                    return;
                case Macro.ToProp or Macro.Guard when !b.Moving && StepReachArrival(a):
                    return;
            }

            if (Time < m.NextThink)
            {
                if ((a.Macro == Macro.ToWaypoint || a.Macro == Macro.ToProp || a.Macro == Macro.Wander || a.Macro == Macro.Flee || a.Macro == Macro.Guard) && !b.Moving &&
                    (a.SettleUntil < 0f || Time >= a.SettleUntil))
                {
                    var arrivedAtWaypoint = a.Macro == Macro.ToWaypoint;
                    var arrivedAtProp = a.Macro == Macro.ToProp || a.Macro == Macro.Guard;
                    var stillThere = !arrivedAtProp || CanSeeProp(a, i); // judged before jumping back down
                    Finish(a);
                    if (arrivedAtWaypoint)
                    {
                        a.Macro = Macro.Look;
                        a.ScanLeft = 360f;
                        b.Pitch = HideSeekRules.ScanPitch;
                    }
                    else if (arrivedAtProp && !stillThere)
                    {
                        m.KnowsWhere = false; // it is not where I left it
                    }
                }

                return;
            }

            m.NextThink = Time + 0.25f;
            var ownHeldByMe = Props[i].HeldBy == i;

            // Someone carries my prop (I see the outline): chase and punch once I react.
            if (m.HolderSeen >= 0 && Props[i].HeldBy == m.HolderSeen && Time - m.HolderLastSeenAt <= 2f &&
                Time >= m.HolderSeenAt + m.Reaction && a.Macro != Macro.Attack)
            {
                BeginAttack(a, m.HolderSeen, 10f);
                return;
            }

            if (ownHeldByMe)
            {
                // Holding it: avoid or fight a thief that comes close, otherwise roam a little.
                if (Time - m.ThreatSeenAt <= 1f && ThiefWorld.Flat(m.ThreatPos - b.Position) <= 6f && a.Macro != Macro.Flee)
                {
                    if (rng.NextDouble() < m.Aggression * 0.5f)
                    {
                        BeginAttack(a, Players, 6f);
                    }
                    else if (world.TryPickAway(b.Position, m.ThreatPos, rng, out var away) && b.TrySetDestination(away))
                    {
                        b.WantsSprint = true;
                        a.Macro = Macro.Flee;
                    }

                    return;
                }

                if (a.Macro == Macro.Await && Time >= m.NextWander &&
                    world.TryPickRandomWaypoint(b.Position, rng, 3f, 10f, out var roam) && b.TrySetDestination(roam))
                {
                    b.WantsSprint = false;
                    a.Macro = Macro.Wander;
                    m.NextWander = Time + Mathf.Lerp(8f, 20f, (float)rng.NextDouble());
                }

                return;
            }

            if (a.Macro != Macro.Await)
            {
                return;
            }

            var ownPlaced = Props[i].HeldBy < 0;
            var wantsToHold = m.HoldEarly || Time >= m.GrabAt || Props[i].MovedSinceHidden;
            if (ownPlaced && m.KnowsWhere)
            {
                var d = ThiefWorld.Flat(m.Believed - b.Position);
                if (!wantsToHold && d <= 6f)
                {
                    // Guard: stay near it and look around.
                    a.Macro = Macro.Look;
                    a.ScanLeft = 360f;
                    b.Pitch = HideSeekRules.ScanPitch;
                    return;
                }

                if (world.TryPlanReach(b.Position, m.Believed, out a.Reach) && b.TrySetDestination(a.Reach.Walk, 0.5f))
                {
                    BeginReach(a);
                    b.WantsSprint = Time > 300f;
                    b.HasLookTarget = true;
                    b.LookTarget = m.Believed + Vector3.up * world.ObjectHalf.y;
                    a.Macro = wantsToHold ? Macro.ToProp : Macro.Guard;
                    return;
                }

                m.KnowsWhere = false;
            }

            // Lost it: search like the v1 rule seeker.
            SearchStep(a, m);
        }

        private void SearchStep(Actor a, PlayerMind m)
        {
            var b = a.Body;
            world.FillWaypointCandidates(b.Position, 8, scratchIds, scratchPaths);
            var best = -1;
            var bestScore = float.PositiveInfinity;
            for (var k = 0; k < scratchIds.Count; k++)
            {
                var w = scratchIds[k];
                var since = float.IsNegativeInfinity(m.VisitedAt[w]) ? 999f : Time - m.VisitedAt[w];
                var score = scratchPaths[k] + (since < 60f ? 30f : 0f);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = w;
                }
            }

            if (best >= 0 && b.TrySetDestination(world.Waypoints[best]))
            {
                b.WantsSprint = Time > 300f;
                a.Macro = Macro.ToWaypoint;
            }
            else
            {
                a.Macro = Macro.CrouchLook;
                a.ScanLeft = 360f;
                b.Crouched = true;
                b.Pitch = HideSeekRules.CrouchScanPitch;
            }
        }

        private void BeginReach(Actor a)
        {
            a.ApproachPosture = a.Reach.Posture;
            a.Hopped = false;
            a.HopAt = -1f;
            a.SettleUntil = -1f;
        }

        /// <summary>
        /// After walking to the reach plan's floor point: jump up onto the surface if needed, then take the posture
        /// (crouch, prone or a jump) and hold it briefly so vision and pickup can happen. False once all that is done.
        /// </summary>
        private bool StepReachArrival(Actor a)
        {
            var b = a.Body;
            if (a.Reach.Hop && !a.Hopped)
            {
                if (a.HopAt < 0f) a.HopAt = Time + HideSeekReach.HopSeconds;
                if (Time >= a.HopAt)
                {
                    b.Position = a.Reach.Stand;
                    a.Hopped = true;
                }

                return true;
            }

            if (a.SettleUntil < 0f)
            {
                // Hold the posture while looking at the prop for a moment, whatever the posture. Arriving where one
                // already stands takes one tick; without this pause the look-down tick and the vision tick (every
                // 0.1 s) could stay out of phase forever and the prop at one's feet was never seen.
                b.Crouched = a.ApproachPosture == 1;
                b.Prone = a.ApproachPosture == 2;
                b.Jumping = a.ApproachPosture == 3;
                a.SettleUntil = Time + (a.ApproachPosture > 0 ? 0.6f : 0.4f);
                return true;
            }

            return a.SettleUntil >= 0f && Time < a.SettleUntil;
        }

        private void BeginAttack(Actor a, int target, float seconds)
        {
            var b = a.Body;
            b.Crouched = b.Prone = false;
            b.HasLookTarget = false;
            b.WantsSprint = true;
            a.Macro = Macro.Attack;
            a.Target = target;
            a.MacroUntil = Time + seconds;
            a.NextRepath = Time;
            a.LastSawTargetAt = Time;
            a.LastTargetPos = target == Players ? Minds[a.Id].ThreatPos : Minds[a.Id].HolderSeenPos;
        }

        // ------------------------------------------------------------------ perception

        /// <summary>In-game mirror: which actor the next sight line is aimed at (its own body must not block it), -1 none.</summary>
        public System.Action<int> MirrorSightTarget;

        /// <summary>In-game mirror: the thief saw a player's prop for the first time (prop, seen held).</summary>
        public System.Action<int, bool> MirrorPropSpotted;

        private bool CanSee(Actor observer, Actor other)
        {
            if (other.IsThief && !ThiefActive) return false;
            var b = observer.Body;
            MirrorSightTarget?.Invoke(other.Id);
            var seen = HideSeekVision.CanSeeBody(b.Eye, b.Yaw, b.Pitch, other.Body.Position, HideSeekRules.BodyHeight, world.OccluderMask);
            MirrorSightTarget?.Invoke(-1);
            return seen;
        }

        private bool CanSeeProp(Actor observer, int prop)
        {
            var p = Props[prop];
            if (p.HeldBy >= 0) return p.HeldBy != observer.Id && CanSee(observer, Actors[p.HeldBy]);
            var b = observer.Body;
            return HideSeekVision.CanSeeBox(b.Eye, b.Yaw, b.Pitch, p.Bottom, world.ObjectHalf, world.OccluderMask);
        }

        private void See()
        {
            // Players: their own prop (outline) and whoever carries it; a thief close by as a threat.
            for (var i = 0; i < Players && !Mirrored; i++)
            {
                var a = Actors[i];
                if (a.Stunned(Time)) continue;
                var m = Minds[i];
                var own = Props[i];
                if (own.HeldBy >= 0 && own.HeldBy != i)
                {
                    if (CanSee(a, Actors[own.HeldBy]))
                    {
                        if (m.HolderSeen != own.HeldBy || Time - m.HolderLastSeenAt > 6f) m.HolderSeenAt = Time; // reaction clock
                        m.HolderSeen = own.HeldBy;
                        m.HolderLastSeenAt = Time;
                        m.HolderSeenPos = Actors[own.HeldBy].Body.Position;
                    }
                }
                else if (own.HeldBy < 0 && CanSeeProp(a, i))
                {
                    m.KnowsWhere = true;
                    m.Believed = own.Bottom;
                }

                if (ThiefActive && CanSee(a, Thief))
                {
                    m.ThreatSeenAt = Time;
                    m.ThreatPos = Thief.Body.Position;
                }
            }

            if (!ThiefActive || Thief.Stunned(Time))
            {
                return;
            }

            // Thief: player props it sees (it recognises them, V2) and the players.
            for (var p = 0; p < Players; p++)
            {
                var player = Actors[p];
                var seen = CanSee(Thief, player);
                Mind.PlayerSeenNow[p] = seen;
                if (seen)
                {
                    var d = ThiefWorld.Flat(player.Body.Position - Thief.Body.Position);
                    Mind.PlayerChasing[p] = Mind.PlayerEverSeen[p] && Time - Mind.PlayerSeenTime[p] < 0.5f && d < Mind.PlayerLastDistance[p] - 0.05f;
                    Mind.PlayerLastDistance[p] = d;
                    Mind.PlayerEverSeen[p] = true;
                    Mind.PlayerSeenPos[p] = player.Body.Position;
                    Mind.PlayerSeenTime[p] = Time;
                    Mind.PlayerSeenHolding[p] = player.Holding >= 0;
                }

                var prop = Props[p];
                if (prop.HeldBy == Players) continue;
                if (CanSeeProp(Thief, p))
                {
                    if (!Mind.PropKnown[p])
                    {
                        PropsSpotted++;
                        MirrorPropSpotted?.Invoke(p, prop.HeldBy >= 0);
                    }
                    Mind.PropKnown[p] = true;
                    Mind.PropSeenAt[p] = prop.HeldBy >= 0 ? Actors[prop.HeldBy].Body.Position : prop.Bottom;
                    Mind.PropSeenTime[p] = Time;
                    Mind.PropSeenHeldBy[p] = prop.HeldBy;
                }
                else if (Mind.PropKnown[p] && Mind.PropSeenHeldBy[p] < 0 && ThiefWorld.Flat(Mind.PropSeenAt[p] - Thief.Body.Position) < 2f &&
                         HideSeekVision.Clear(Thief.Body.Eye, Mind.PropSeenAt[p] + Vector3.up * world.ObjectHalf.y, world.OccluderMask))
                {
                    Mind.PropKnown[p] = false; // standing next to where it was and it is gone
                }
            }
        }

        private void AutoPickups()
        {
            for (var i = 0; i <= Players; i++)
            {
                var a = Actors[i];
                if (a.Holding >= 0 || a.Stunned(Time) || (a.IsThief && !ThiefActive)) continue;
                for (var p = 0; p < Players; p++)
                {
                    var prop = Props[p];
                    if (prop.HeldBy >= 0) continue;
                    if (!a.IsThief)
                    {
                        // Players only recognise their own prop. A guard waits until it wants to hold it.
                        if (p != i) continue;
                        var m = Minds[i];
                        if (!(m.HoldEarly || Time >= m.GrabAt || prop.MovedSinceHidden)) continue;
                    }

                    if (!world.InGrabReach(a.Body.Position, prop.Bottom, a.Body.Jumping) || !CanSeeProp(a, p)) continue;
                    if (Mirrored && (!a.IsThief || !MirrorPickup(p))) continue;
                    var mugged = prop.DroppedByThiefStun;
                    prop.HeldBy = i;
                    prop.DroppedByThiefStun = false;
                    a.Holding = p;
                    if (!a.IsThief && prop.MovedSinceHidden) OwnerRecoveries++;
                    if (a.IsThief)
                    {
                        if (mugged) TakenFromMugging++;
                        else if (prop.Spot >= 0) TakenFromHidingSpot++;
                        else TakenOther++;
                        ThiefPickedUpAt = Time;
                        Mind.PropKnown[p] = true;
                        Mind.PropSeenHeldBy[p] = Players;
                        Finish(a); // switch to the hiding brain
                        a.Body.Stop();
                    }
                    else
                    {
                        ClearHolder(i);
                        Finish(a);
                        a.Body.Stop();
                    }

                    break;
                }
            }
        }

        private void ClearHolder(int i)
        {
            Minds[i].HolderSeen = -1;
            Minds[i].HolderSeenAt = -999f;
            Minds[i].HolderLastSeenAt = -999f;
            Minds[i].KnowsWhere = true;
        }

        // ------------------------------------------------------------------ in-game mirror (ThiefNpcDirector)
        // The real match drives this instance on the host: players and their items are copied in every tick, the
        // real NPC avatar walks and the thief body follows it; only the thief's mind, vision and macros run here, so
        // the brains see exactly what they saw in training. Pickup, placement and drops go through the real authority.

        public bool Mirrored { get; private set; }
        public System.Func<int, bool> MirrorPickup;      // prop -> the authority let the NPC hold it
        public System.Func<int, int, bool> MirrorPlace;  // prop, bank spot -> the authority accepted the release
        public System.Func<bool> MirrorDrop;             // drop where the NPC stands

        public void BeginMirror(int seed, Vector3 thiefFeet, float thiefYaw)
        {
            Mirrored = true;
            SearchStartedAt = 0f;
            ReflexGiveUps = 0;
            System.Array.Clear(ReflexBlockedUntil, 0, Players);
            System.Array.Clear(reflexMisses, 0, Players);
            EpisodeSeed = seed;
            rng = new System.Random(seed);
            Time = 0f;
            Done = false;
            ThiefActive = true;
            ThiefAwaiting = false;
            ThiefDecisions = ThiefHides = OwnerRecoveries = 0;
            ThiefHoldSeconds = 0f;
            ThiefHoldTimeouts = 0;
            ThiefPickedUpAt = -1f;
            nextVision = 0f;
            ResetCoverage();
            var wp = world.Waypoints;
            for (var i = 0; i < Players; i++)
            {
                var prop = Props[i];
                prop.HeldBy = -1;
                prop.Spot = -1;
                prop.ThiefMoves = 0;
                prop.MovedSinceHidden = false;
                prop.DroppedByThiefStun = false;
                prop.LastDropper = -1;
                prop.LastDropTime = -1f;
                Minds[i].VisitedAt ??= new float[wp.Count];
            }

            for (var i = 0; i <= Players; i++)
            {
                var a = Actors[i];
                a.Holding = -1;
                a.Hits = 0;
                a.StunnedUntil = -1f;
                a.Macro = Macro.Await;
                a.Target = -1;
                a.SettleUntil = -1f;
                a.Hopped = false;
                a.HopAt = -1f;
                a.Body.HasLookTarget = false;
            }

            Thief.Body.Teleport(thiefFeet, thiefYaw);
            for (var p = 0; p < Players; p++)
            {
                Mind.PropKnown[p] = false;
                Mind.PropSeenTime[p] = -999f;
                Mind.PropSeenHeldBy[p] = -1;
                Mind.PlayerEverSeen[p] = false;
                Mind.PlayerSeenNow[p] = false;
                Mind.PlayerSeenTime[p] = -999f;
                Mind.PlayerSeenHolding[p] = false;
                Mind.PlayerChasing[p] = false;
                Mind.PlayerLastDistance[p] = 99f;
            }

            Mind.VisitedAt ??= new float[wp.Count];
            for (var w = 0; w < wp.Count; w++) Mind.VisitedAt[w] = float.NegativeInfinity;
            RefreshThiefCandidates();
        }

        /// <summary>A real player (or an empty slot far below the map when fewer than three play).</summary>
        public void MirrorPlayer(int i, bool present, Vector3 feet, float yaw, bool crouched, bool holding)
        {
            var b = Actors[i].Body;
            b.Position = present ? feet : new Vector3(0f, -1000f, 0f);
            b.Yaw = yaw;
            b.Crouched = present && crouched;
            Actors[i].Holding = present && holding ? i : -1;
        }

        /// <summary>A player's item: where its bottom is, and who holds it (-1 nobody, 0-2 a player, 3 the thief).</summary>
        public void MirrorProp(int p, bool present, Vector3 bottom, int heldBy)
        {
            var prop = Props[p];
            prop.Bottom = present ? bottom : new Vector3(0f, -1000f, 0f);
            prop.HeldBy = present ? heldBy : -1;
            Thief.Holding = heldBy == Players ? p : Thief.Holding == p ? -1 : Thief.Holding;
        }

        /// <summary>One simulator step: the thief follows the real NPC position; macros, vision and pickups run.</summary>
        public void StepMirror(float dt, Vector3 npcFeet, bool npcStillMoving, bool npcStunned)
        {
            Time += dt;
            var t = Thief;
            t.StunnedUntil = npcStunned ? Time + 0.1f : -1f;
            if (ThiefCarrying)
            {
                ThiefHoldSeconds += dt;
                if (Time - ThiefPickedUpAt >= ThiefHoldLimit)
                {
                    ThiefHoldTimeouts++;
                    Drop(t);
                }
            }

            if (!t.Stunned(Time))
            {
                t.Body.Follow(npcFeet, npcStillMoving && t.Body.Moving, dt);
                StepThief(dt);
            }

            MarkVisited();
            if (Time >= nextVision)
            {
                nextVision = Time + HideSeekRules.VisionIntervalSeconds;
                See();
                AutoPickups();
            }

            UpdateCoverage();
        }

        /// <summary>The thief stood on a spot the NPC could not reach: give up the current macro.</summary>
        public void AbortThiefMacro()
        {
            Thief.Body.Stop();
            Finish(Thief);
        }

        private void ResetCoverage()
        {
            if (Checked == null || Checked.Length != world.CheckSpots.Count) Checked = new bool[world.CheckSpots.Count];
            else System.Array.Clear(Checked, 0, Checked.Length);
            CheckedCount = 0;
            CheckGain = 0;
            nextCoverage = 0f;
        }

        /// <summary>
        /// Every 0.3 s: spaces within 8 m that are in the thief's view and in plain line of sight from its eye (its
        /// posture counts: crouching or lying down sees under furniture) become checked.
        /// </summary>
        private void UpdateCoverage()
        {
            if (!ThiefActive || Time < nextCoverage || Thief.Stunned(Time)) return;
            nextCoverage = Time + 0.3f;
            var b = Thief.Body;
            var eye = b.Eye;
            world.NearbyCheckSpots(b.Position, 8f, scratchChecks);
            var rays = 0;
            var lift = world.ObjectHalf.y + 0.011f;
            foreach (var c in scratchChecks)
            {
                if (Checked[c]) continue;
                var centre = world.Bank.Spots[world.CheckSpots[c]].Position + Vector3.up * lift;
                if (!HideSeekVision.InCone(eye, b.Yaw, b.Pitch, centre)) continue;
                if (++rays > 48) break;
                if (!HideSeekVision.Clear(eye, centre, world.OccluderMask)) continue;
                Checked[c] = true;
                CheckedCount++;
                CheckGain++;
            }
        }

        /// <summary>Spaces worth checking within 3 m of a waypoint that the thief has not seen yet.</summary>
        public int UncheckedNear(int waypoint)
        {
            var n = 0;
            foreach (var c in world.WaypointCheckSpots[waypoint]) if (!Checked[c]) n++;
            return n;
        }

        /// <summary>Unchecked spaces under furniture within 3 m of the thief (where crouching or lying down helps).</summary>
        public int UncheckedUnderHere()
        {
            world.NearbyCheckSpots(Thief.Body.Position, 3f, scratchChecks);
            var n = 0;
            foreach (var c in scratchChecks)
            {
                if (!Checked[c] && (world.Bank.Spots[world.CheckSpots[c]].Tags & HidingSpotTags.Under) != 0) n++;
            }

            return n;
        }

        private void MarkVisited()
        {
            var wp = world.Waypoints;
            for (var w = 0; w < wp.Count; w++)
            {
                if (ThiefActive && (wp[w] - Thief.Body.Position).sqrMagnitude <= 6.25f) Mind.VisitedAt[w] = Time;
                for (var i = 0; i < Players; i++)
                {
                    if ((wp[w] - Actors[i].Body.Position).sqrMagnitude <= 6.25f) Minds[i].VisitedAt[w] = Time;
                }
            }
        }
    }
}
