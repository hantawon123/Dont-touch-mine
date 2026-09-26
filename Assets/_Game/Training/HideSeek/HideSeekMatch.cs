using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Training.HideSeek
{
    /// <summary>
    /// One hide-seek round (docs/planning/hide-seek-v1.md): the hider starts holding the prop and must put it on a
    /// bank spot within 20 s while the seeker already searches; the round ends when the seeker picks it up (within
    /// 2 m while seeing it) or after 150 s. Brains only choose macro actions; walking, turning and seeing are code.
    /// </summary>
    public sealed class HideSeekMatch
    {
        public enum HiderState { AwaitDecision, ToSpot, Relocating, WalkingAway, Idle }
        public enum SeekerState { AwaitDecision, ToWaypoint, LookAround, CrouchLook, ToObject, Chase }

        public const int HideCandidateSlots = 8;
        public const int HideActionRelocate = 8;
        public const int HideActionCount = 9;

        public const int SeekCandidateSlots = 8;
        public const int SeekActionLook = 8;
        public const int SeekActionCrouchLook = 9;
        public const int SeekActionToObject = 10;
        public const int SeekActionChase = 11;
        public const int SeekActionCount = 12;

        /// <summary>Scanning pace in place (not in MovementConfig): a full turn in 2 s, like a person looking around.</summary>
        public const float ScanDegreesPerSecond = 180f;
        public const float ChaseMemorySeconds = 5f;

        private readonly HideSeekArena arena;

        public readonly int Index;
        public readonly HideSeekBody Hider = new();
        public readonly HideSeekBody Seeker = new();

        public int EpisodeSeed { get; private set; }
        public float Time { get; private set; }
        public bool Done { get; private set; }

        // Prop.
        public bool ObjectHeld { get; private set; }
        public Vector3 ObjectBottom { get; private set; }
        public float PlacedAt { get; private set; }
        public float FoundAt { get; private set; }
        public bool DeadlineMissed { get; private set; }
        public int PlacedSpot { get; private set; }
        public int Relocations { get; private set; }
        public int RejectedPlacements { get; private set; }

        // Hider.
        public HiderState HState { get; private set; }
        public readonly List<int> HideCandidates = new();
        public readonly List<float> HideCandidatePaths = new();
        private int targetSpot = -1;
        public int TargetSpot => targetSpot;
        public bool HiderAwaiting;

        // Seeker.
        public SeekerState SState { get; private set; }
        public SeekerState LastSeekerMacro { get; private set; } = SeekerState.AwaitDecision;
        public readonly List<int> SeekCandidates = new();
        public readonly List<float> SeekCandidatePaths = new();
        public float[] WaypointVisitedAt;
        private float scanLeft;
        private int approachPosture;
        private float settleUntil = -1f;
        private float nextChaseRepath;
        public bool SeekerAwaiting;
        public int SeekerDecisions { get; private set; }

        // What each side saw (memory is the only knowledge; nothing is read from the other side directly).
        public bool SeekerSeesObject { get; private set; }
        public bool SeekerSeesHider { get; private set; }
        public bool HiderSeesSeeker { get; private set; }
        public bool ObjectEverSeen { get; private set; }
        public Vector3 ObjectLastSeen { get; private set; }
        public float ObjectSeenAt { get; private set; }
        public bool ObjectSeenHeld { get; private set; }
        public bool HiderEverSeen { get; private set; }
        public Vector3 HiderLastSeen { get; private set; }
        public float HiderSeenAt { get; private set; }
        public bool HiderSeenHolding { get; private set; }
        public bool SeekerSawHiderHolding { get; private set; }
        public bool SeekerEverSeen { get; private set; }
        public Vector3 SeekerLastSeen { get; private set; }
        public Vector3 SeekerLastSeenEye { get; private set; }
        public float SeekerSeenAt { get; private set; }

        private float nextVision;
        private System.Random rng;

        public HideSeekMatch(HideSeekArena arena, int index)
        {
            this.arena = arena;
            Index = index;
        }

        public System.Random Rng => rng;

        public void Reset(int seed, Vector3 hiderStart, float hiderYaw, Vector3 seekerStart, float seekerYaw)
        {
            EpisodeSeed = seed;
            rng = new System.Random(seed);
            Time = 0f;
            Done = false;
            ObjectHeld = true;
            PlacedAt = -1f;
            FoundAt = -1f;
            DeadlineMissed = false;
            PlacedSpot = -1;
            Relocations = 0;
            RejectedPlacements = 0;
            SeekerDecisions = 0;
            Hider.Teleport(hiderStart, hiderYaw);
            Seeker.Teleport(seekerStart, seekerYaw);
            HState = HiderState.AwaitDecision;
            SState = SeekerState.AwaitDecision;
            LastSeekerMacro = SeekerState.AwaitDecision;
            HiderAwaiting = false;
            SeekerAwaiting = false;
            targetSpot = -1;
            WaypointVisitedAt ??= new float[arena.Waypoints.Count];
            for (var i = 0; i < WaypointVisitedAt.Length; i++) WaypointVisitedAt[i] = float.NegativeInfinity;
            SeekerSeesObject = SeekerSeesHider = HiderSeesSeeker = false;
            ObjectEverSeen = HiderEverSeen = SeekerEverSeen = false;
            ObjectSeenAt = HiderSeenAt = SeekerSeenAt = -999f;
            ObjectSeenHeld = HiderSeenHolding = SeekerSawHiderHolding = false;
            nextVision = 0f;
            arena.FillHideCandidates(this);
            arena.FillSeekCandidates(this);
        }

        public Vector3 HeldObjectBottom => Hider.Position + Vector3.up * 0.8f;
        public Vector3 CurrentObjectBottom => ObjectHeld ? HeldObjectBottom : ObjectBottom;

        // ------------------------------------------------------------------ decisions

        public void ApplyHideAction(int action)
        {
            HiderAwaiting = false;
            if (!ObjectHeld)
            {
                return;
            }

            if (action >= 0 && action < HideCandidates.Count)
            {
                targetSpot = HideCandidates[action];
                if (Hider.TrySetDestination(arena.Bank.Spots[targetSpot].StandPosition))
                {
                    Hider.WantsSprint = true;
                    HState = HiderState.ToSpot;
                    return;
                }
            }

            // Relocate (or a slot that could not be walked to): go somewhere else and look at new spots there.
            Relocations++;
            if (arena.TryPickRelocation(this, out var destination) && Hider.TrySetDestination(destination))
            {
                Hider.WantsSprint = true;
                HState = HiderState.Relocating;
                return;
            }

            HState = HiderState.AwaitDecision;
        }

        public void ApplySeekAction(int action)
        {
            SeekerAwaiting = false;
            SeekerDecisions++;
            Seeker.Crouched = false;
            Seeker.Prone = false;
            Seeker.Pitch = HideSeekRules.WalkPitch;
            Seeker.HasLookTarget = false;
            if (action >= 0 && action < SeekCandidates.Count &&
                Seeker.TrySetDestination(arena.Waypoints[SeekCandidates[action]]))
            {
                Seeker.WantsSprint = Seeker.LastPathLength > 8f;
                SState = LastSeekerMacro = SeekerState.ToWaypoint;
                return;
            }

            switch (action)
            {
                case SeekActionToObject when ObjectEverSeen && !ObjectSeenHeld &&
                                             arena.TryFindApproach(Seeker.Position, ObjectLastSeen, out var approach, out approachPosture) &&
                                             Seeker.TrySetDestination(approach, 0.5f):
                    // Like a player: walk to a floor point within reach from which the prop can be seen (standing or
                    // crouched), not to the NavMesh point nearest the prop (for a prop under a bed that is the bed top).
                    Seeker.WantsSprint = true;
                    Seeker.HasLookTarget = true;
                    settleUntil = -1f;
                    Seeker.LookTarget = ObjectLastSeen + Vector3.up * arena.ObjectHalf.y;
                    SState = LastSeekerMacro = SeekerState.ToObject;
                    return;
                case SeekActionChase when HiderEverSeen && Time - HiderSeenAt <= ChaseMemorySeconds && Seeker.TrySetDestination(HiderLastSeen):
                    Seeker.WantsSprint = true;
                    nextChaseRepath = Time + 0.5f;
                    SState = LastSeekerMacro = SeekerState.Chase;
                    return;
                case SeekActionCrouchLook:
                    Seeker.Stop();
                    Seeker.Crouched = true;
                    Seeker.Pitch = HideSeekRules.CrouchScanPitch;
                    scanLeft = 360f;
                    SState = LastSeekerMacro = SeekerState.CrouchLook;
                    return;
                default:
                    Seeker.Stop();
                    Seeker.Pitch = HideSeekRules.ScanPitch;
                    scanLeft = 360f;
                    SState = LastSeekerMacro = SeekerState.LookAround;
                    return;
            }
        }

        // ------------------------------------------------------------------ simulation

        public void Step(float dt)
        {
            if (Done)
            {
                return;
            }

            Time += dt;
            StepHider();
            StepSeeker(dt);
            Hider.Step(dt);
            Seeker.Step(dt);
            arena.MarkVisited(this);

            if (Time >= nextVision)
            {
                nextVision = Time + HideSeekRules.VisionIntervalSeconds;
                See();
            }

            if (Time >= HideSeekRules.EpisodeSeconds)
            {
                Done = true;
            }
        }

        private void StepHider()
        {
            if (ObjectHeld && Time >= HideSeekRules.HideDeadlineSeconds)
            {
                // Too slow: the prop drops at the hider's feet.
                ObjectBottom = NavMesh.SamplePosition(Hider.Position, out var floor, 1f, NavMesh.AllAreas) ? floor.position : Hider.Position;
                ObjectHeld = false;
                DeadlineMissed = true;
                PlacedAt = Time;
                BeginWalkAway();
                return;
            }

            switch (HState)
            {
                case HiderState.ToSpot when !Hider.Moving:
                    if (arena.TryPlace(this, targetSpot))
                    {
                        ObjectBottom = arena.Bank.Spots[targetSpot].Position + Vector3.up * 0.011f;
                        ObjectHeld = false;
                        PlacedAt = Time;
                        PlacedSpot = targetSpot;
                        BeginWalkAway();
                    }
                    else
                    {
                        RejectedPlacements++;
                        arena.FillHideCandidates(this, targetSpot);
                        HState = HiderState.AwaitDecision;
                    }

                    break;
                case HiderState.Relocating when !Hider.Moving:
                    arena.FillHideCandidates(this);
                    HState = HiderState.AwaitDecision;
                    break;
                case HiderState.WalkingAway when !Hider.Moving:
                    HState = HiderState.Idle;
                    break;
            }
        }

        private void BeginWalkAway()
        {
            // Code, not learned: leave the spot so standing next to it does not give it away.
            Hider.WantsSprint = false;
            HState = arena.TryPickWalkAway(this, out var away) && Hider.TrySetDestination(away)
                ? HiderState.WalkingAway
                : HiderState.Idle;
        }

        private void StepSeeker(float dt)
        {
            switch (SState)
            {
                case SeekerState.ToObject when !Seeker.Moving && approachPosture > 0 && settleUntil < 0f:
                    // Arrived next to a prop that is only visible low (under furniture): crouch or lie down and look.
                    Seeker.Crouched = approachPosture == 1;
                    Seeker.Prone = approachPosture == 2;
                    settleUntil = Time + 0.6f;
                    break;
                case SeekerState.ToObject when !Seeker.Moving && settleUntil >= 0f && Time < settleUntil:
                    break;
                case SeekerState.ToWaypoint:
                case SeekerState.ToObject:
                    if (!Seeker.Moving)
                    {
                        Seeker.HasLookTarget = false;
                        Seeker.Crouched = false;
                        Seeker.Prone = false;
                        settleUntil = -1f;
                        arena.FillSeekCandidates(this);
                        SState = SeekerState.AwaitDecision;
                    }

                    break;
                case SeekerState.Chase:
                    if (SeekerSeesHider && Time >= nextChaseRepath)
                    {
                        nextChaseRepath = Time + 0.5f;
                        Seeker.TrySetDestination(HiderLastSeen);
                    }

                    if (!Seeker.Moving)
                    {
                        arena.FillSeekCandidates(this);
                        SState = SeekerState.AwaitDecision;
                    }

                    break;
                case SeekerState.LookAround:
                case SeekerState.CrouchLook:
                    var turn = ScanDegreesPerSecond * dt * (SState == SeekerState.CrouchLook ? 0.75f : 1f);
                    Seeker.Yaw += turn;
                    scanLeft -= turn;
                    if (scanLeft <= 0f)
                    {
                        Seeker.Crouched = false;
                        Seeker.Pitch = HideSeekRules.WalkPitch;
                        arena.FillSeekCandidates(this);
                        SState = SeekerState.AwaitDecision;
                    }

                    break;
            }
        }

        private void See()
        {
            var mask = arena.OccluderMask;
            var half = arena.ObjectHalf;
            SeekerSeesHider = HideSeekVision.CanSeeBody(Seeker.Eye, Seeker.Yaw, Seeker.Pitch, Hider.Position, HideSeekRules.BodyHeight, mask);
            HiderSeesSeeker = HideSeekVision.CanSeeBody(Hider.Eye, Hider.Yaw, Hider.Pitch, Seeker.Position, HideSeekRules.BodyHeight, mask);
            SeekerSeesObject = ObjectHeld
                ? SeekerSeesHider
                : HideSeekVision.CanSeeBox(Seeker.Eye, Seeker.Yaw, Seeker.Pitch, ObjectBottom, half, mask);

            if (SeekerSeesHider)
            {
                HiderEverSeen = true;
                HiderLastSeen = Hider.Position;
                HiderSeenAt = Time;
                HiderSeenHolding = ObjectHeld;
                SeekerSawHiderHolding |= ObjectHeld;
            }

            if (SeekerSeesObject)
            {
                ObjectEverSeen = true;
                ObjectLastSeen = CurrentObjectBottom;
                ObjectSeenAt = Time;
                ObjectSeenHeld = ObjectHeld;
            }

            if (HiderSeesSeeker)
            {
                SeekerEverSeen = true;
                SeekerLastSeen = Seeker.Position;
                SeekerLastSeenEye = Seeker.Eye;
                SeekerSeenAt = Time;
            }

            // Found: the seeker sees the placed prop and it is within grab distance.
            if (!ObjectHeld && SeekerSeesObject)
            {
                var centre = ObjectBottom + Vector3.up * half.y;
                var flat = new Vector2(centre.x - Seeker.Position.x, centre.z - Seeker.Position.z).magnitude;
                if (flat <= HideSeekRules.GrabDistance && Mathf.Abs(centre.y - Seeker.Position.y) <= 2f)
                {
                    FoundAt = Time;
                    Done = true;
                }
            }
        }

        // ------------------------------------------------------------------ outcome

        /// <summary>Share of the round the prop stayed unfound (1 when never found).</summary>
        public float HiddenShare => FoundAt >= 0f ? FoundAt / HideSeekRules.EpisodeSeconds : 1f;

        /// <summary>
        /// Zero-sum outcome in [-1, 1] (self-play reads its sign as win / loss): hider 2 * hiddenShare - 1, seeker
        /// the opposite. Dropping the prop late costs the hider 0.5 more (not given to the seeker).
        /// </summary>
        public float HiderReward => 2f * HiddenShare - 1f - (DeadlineMissed ? 0.5f : 0f);

        public float SeekerReward => -(2f * HiddenShare - 1f);
    }
}
