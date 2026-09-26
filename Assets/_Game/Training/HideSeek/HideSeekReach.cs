using UnityEngine;
using UnityEngine.AI;

namespace Game.Training.HideSeek
{
    /// <summary>How a player gets to a prop it saw, so that it can pick it up.</summary>
    public struct ReachPlan
    {
        /// <summary>NavMesh point on the walker's island to walk to.</summary>
        public Vector3 Walk;

        /// <summary>Where the body stands to grab: Walk, or a surface it jumps up onto (Hop).</summary>
        public Vector3 Stand;

        public bool Hop;

        /// <summary>0 stand, 1 crouch, 2 prone, 3 jump (eye and feet 0.85 m higher for a moment).</summary>
        public int Posture;

        public float Cost;
    }

    /// <summary>
    /// Pickup reach with the game rules, shared by the hide-seek and thief simulators:
    /// grab = 3D distance from the feet to the prop within 2 m (PlayerInteractor / InteractionAuthorityRules), the prop
    /// visible from the eye in some posture; a jump (0.85 m, MovementConfig.jumpHeight) raises feet and eye for a
    /// moment; a player can jump up onto a surface up to 1.1 m above the floor next to it (jump + step) and stand there.
    /// </summary>
    public static class HideSeekReach
    {
        public const float GrabDistance = 2f;
        public const float JumpHeight = 0.85f;
        public const float HopUpMax = 1.1f;
        public const float HopFlatMax = 1.2f;
        public const float HopSeconds = 0.6f;

        private static readonly float[] EyeHeights =
        {
            HideSeekRules.StandEyeHeight, HideSeekRules.CrouchEyeHeight, HideSeekRules.ProneEyeHeight,
            HideSeekRules.StandEyeHeight + JumpHeight,
        };

        public static float EyeHeight(int posture) => EyeHeights[Mathf.Clamp(posture, 0, 3)];

        /// <summary>Closest point of the prop box to the feet within 2 m (optionally during a jump).</summary>
        public static bool InGrabReach(Vector3 feet, Vector3 propBottom, Vector3 half, bool jumping)
        {
            var centre = propBottom + Vector3.up * half.y;
            var box = new Bounds(centre, half * 2f);
            var reachFrom = jumping ? feet + Vector3.up * JumpHeight : feet;
            return (box.ClosestPoint(reachFrom) - reachFrom).sqrMagnitude <= GrabDistance * GrabDistance;
        }

        /// <summary>
        /// Cheapest plan to grab the prop from <paramref name="from"/> (walker's NavMesh position): walk to a floor
        /// point, or walk then jump up onto a surface; then grab standing, crouched, prone or while jumping.
        /// </summary>
        public static bool TryPlan(Vector3 from, Vector3 propBottom, Vector3 half, int occluders, NavMeshPath path, out ReachPlan plan)
        {
            plan = default;
            var centre = propBottom + Vector3.up * half.y;
            var best = float.PositiveInfinity;
            for (var ring = 0; ring < 5; ring++)
            {
                var radius = ring * 0.5f;
                var steps = ring == 0 ? 1 : 8;
                for (var k = 0; k < steps; k++)
                {
                    var yaw = (k * 45f + ring * 22.5f) * Mathf.Deg2Rad;
                    var probe = propBottom + new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw)) * radius;
                    // Look both near the prop's height and below it (a prop on a tall wardrobe has its floor ~2 m down).
                    for (var drop = 0; drop < 2; drop++)
                    {
                        var sample = probe + Vector3.down * (drop * 1.6f);
                        if (!NavMesh.SamplePosition(sample, out var nav, 1f, NavMesh.AllAreas))
                        {
                            continue;
                        }

                        var stand = nav.position;
                        if (stand.y > propBottom.y + 0.3f)
                        {
                            continue; // on top of the furniture the prop is under
                        }

                        var posture = BestPosture(stand, propBottom, half, centre, occluders);
                        if (posture < 0)
                        {
                            continue;
                        }

                        if (TryPathLength(from, stand, path, out var walk))
                        {
                            Consider(ref plan, ref best, stand, stand, false, posture, walk);
                        }
                        else if (TryHop(from, stand, path, out var floor, out var toFloor))
                        {
                            Consider(ref plan, ref best, floor, stand, true, posture, toFloor + 2f);
                        }
                    }
                }
            }

            return best < float.PositiveInfinity;
        }

        private static void Consider(ref ReachPlan plan, ref float best, Vector3 walk, Vector3 stand, bool hop, int posture, float cost)
        {
            cost += posture == 3 ? 0.5f : posture * 0.2f;
            if (cost < best)
            {
                best = cost;
                plan = new ReachPlan { Walk = walk, Stand = stand, Hop = hop, Posture = posture, Cost = cost };
            }
        }

        /// <summary>Lowest-effort posture from which the prop is both in reach and in sight, or -1.</summary>
        private static int BestPosture(Vector3 stand, Vector3 propBottom, Vector3 half, Vector3 centre, int occluders)
        {
            for (var posture = 0; posture < 4; posture++)
            {
                var jumping = posture == 3;
                if (!InGrabReach(stand, propBottom, half, jumping))
                {
                    continue;
                }

                var eye = stand + Vector3.up * EyeHeights[posture];
                if (HideSeekVision.Clear(eye, centre, occluders) ||
                    HideSeekVision.Clear(eye, centre + Vector3.up * (half.y * 0.9f), occluders))
                {
                    return posture;
                }
            }

            return -1;
        }

        /// <summary>A floor point reachable from <paramref name="from"/> next to and below <paramref name="top"/>, so one can jump up.</summary>
        private static bool TryHop(Vector3 from, Vector3 top, NavMeshPath path, out Vector3 floor, out float length)
        {
            floor = default;
            length = float.PositiveInfinity;
            var found = false;
            for (var k = 0; k < 8; k++)
            {
                var yaw = k * 45f * Mathf.Deg2Rad;
                var probe = top + new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw)) * 0.9f + Vector3.down * 0.9f;
                if (!NavMesh.SamplePosition(probe, out var nav, 0.8f, NavMesh.AllAreas))
                {
                    continue;
                }

                var p = nav.position;
                var rise = top.y - p.y;
                var flat = new Vector2(top.x - p.x, top.z - p.z).magnitude;
                if (rise < 0.2f || rise > HopUpMax || flat > HopFlatMax)
                {
                    continue;
                }

                // Headroom for the jump and nothing solid between the floor point and the top surface.
                if (Physics.Linecast(p + Vector3.up * 0.3f, top + Vector3.up * 0.3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                if (TryPathLength(from, p, path, out var d) && d < length)
                {
                    length = d;
                    floor = p;
                    found = true;
                }
            }

            return found;
        }

        public static bool TryPathLength(Vector3 from, Vector3 to, NavMeshPath path, out float length)
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
    }
}
