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
        public const float AttackCooldown = 0.5f;        // CombatConfig.attackCooldownSeconds
        public const float AttackHitDelay = 0.35f;       // CombatConfig.attackHitDelaySeconds
        public const float AttackReach = 0.8f + 0.7f;    // CombatConfig forward offset + radius
        public const float ScanDegreesPerSecond = 180f;

        public enum Macro { Await, ToWaypoint, Look, CrouchLook, ToProp, Flee, Attack, ToSpot, Relocate, Wander, Guard, Chase, Idle }

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
            nextVision = 0f;
            var wp = world.Waypoints;

            // Players' props are already hidden (the hiding phase is not simulated, V9); V4 decides how.
            for (var i = 0; i < Players; i++)
            {
                var from = wp[rng.Next(wp.Count)];
                Props[i].HeldBy = -1;
                Props[i].ThiefMoves = 0;
                Props[i].MovedSinceHidden = false;
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
        public const int SeekActionCount = 15;

        public const int HideActionRelocate = 8;
        public const int HideActionFlee = 9;
        public const int HideActionCount = 10;

        public bool SeekAllowed(int action)
        {
            if (action < 8) return action < SeekCandidates.Count;
            if (action >= SeekActionPropFirst && action < SeekActionPropFirst + Players) return KnownPropSlots[action - SeekActionPropFirst] >= 0;
            if (action == SeekActionFlee) return NearestThreat(out _) >= 0;
            if (action == SeekActionAttack) return AttackTargetCandidate() >= 0;
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
                    if (!b.Moving) Finish(t);
                    break;
                case Macro.ToProp:
                    if (!b.Moving && !StepReachArrival(t))
                    {
                        Finish(t);
                    }

                    break;
                case Macro.ToSpot:
                    if (!b.Moving)
                    {
                        var spot = t.Target;
                        CollectOtherProps(t.Holding);
                        if (t.Holding >= 0 && world.CanPlaceFrom(b.Position, spot) && world.FitsNow(spot, otherProps))
                        {
                            var prop = Props[t.Holding];
                            prop.HeldBy = -1;
                            prop.Spot = spot;
                            prop.Bottom = world.Bank.Spots[spot].Position + Vector3.up * 0.011f;
                            prop.ThiefMoves++;
                            prop.MovedSinceHidden = true;
                            t.Holding = -1;
                            ThiefHides++;
                        }

                        Finish(t);
                    }

                    break;
                case Macro.Attack:
                    StepAttack(t, t.Target, 3f, dt);
                    break;
                case Macro.Look:
                case Macro.CrouchLook:
                    var turn = ScanDegreesPerSecond * dt * (t.Macro == Macro.CrouchLook ? 0.75f : 1f);
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
                    Drop(target);
                }
            }
        }

        private void Drop(Actor a)
        {
            if (a.Holding < 0) return;
            var prop = Props[a.Holding];
            prop.HeldBy = -1;
            prop.Spot = -1;
            prop.Bottom = NavMesh.SamplePosition(a.Body.Position, out var floor, 1f, NavMesh.AllAreas) ? floor.position : a.Body.Position;
            prop.MovedSinceHidden = true;
            a.Holding = -1;
            if (a.IsThief)
            {
                Finish(a);
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

            if (a.ApproachPosture > 0 && a.SettleUntil < 0f)
            {
                b.Crouched = a.ApproachPosture == 1;
                b.Prone = a.ApproachPosture == 2;
                b.Jumping = a.ApproachPosture == 3;
                a.SettleUntil = Time + 0.6f;
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

        private bool CanSee(Actor observer, Actor other)
        {
            if (other.IsThief && !ThiefActive) return false;
            var b = observer.Body;
            return HideSeekVision.CanSeeBody(b.Eye, b.Yaw, b.Pitch, other.Body.Position, HideSeekRules.BodyHeight, world.OccluderMask);
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
            for (var i = 0; i < Players; i++)
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
                    prop.HeldBy = i;
                    a.Holding = p;
                    if (!a.IsThief && prop.MovedSinceHidden) OwnerRecoveries++;
                    if (a.IsThief)
                    {
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
