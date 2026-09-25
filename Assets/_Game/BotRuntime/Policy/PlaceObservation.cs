using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.BotRuntime.Policy
{
    /// <summary>
    /// One candidate drop spot, described only by what the bot could judge from the terrain around it.
    /// The true hiding score is deliberately not here: the policy has to infer it from these cues.
    /// </summary>
    public readonly struct PlaceCandidate
    {
        public PlaceCandidate(
            float directionX,
            float directionZ,
            float pathNorm,
            float straightNorm,
            float[] sortedRayNorms,
            bool covered,
            float coverHeightNorm)
        {
            if (sortedRayNorms == null || sortedRayNorms.Length != PlaceObservationLayout.RayCount)
            {
                throw new ArgumentException(
                    $"exactly {PlaceObservationLayout.RayCount} ray distances are required",
                    nameof(sortedRayNorms));
            }

            Exists = true;
            DirectionX = directionX;
            DirectionZ = directionZ;
            PathNorm = pathNorm;
            StraightNorm = straightNorm;
            RayNorms = (float[])sortedRayNorms.Clone();
            Covered = covered;
            CoverHeightNorm = coverHeightNorm;
            SightNear = null;
            SightFar = null;
        }

        /// <summary>v2.1 candidate: the v2 cues plus sightlines toward eye height (sorted ascending).</summary>
        public PlaceCandidate(
            float directionX,
            float directionZ,
            float pathNorm,
            float straightNorm,
            float[] sortedRayNorms,
            bool covered,
            float coverHeightNorm,
            float[] sightNear,
            float[] sightFar)
            : this(directionX, directionZ, pathNorm, straightNorm, sortedRayNorms, covered, coverHeightNorm)
        {
            if (sightNear == null || sightNear.Length != PlaceObservationLayoutV21.SightRayCount ||
                sightFar == null || sightFar.Length != PlaceObservationLayoutV21.SightRayCount)
            {
                throw new ArgumentException(
                    $"exactly {PlaceObservationLayoutV21.SightRayCount} sightlines per layer are required");
            }

            SightNear = (float[])sightNear.Clone();
            SightFar = (float[])sightFar.Clone();
        }

        public bool Exists { get; }
        public float DirectionX { get; }
        public float DirectionZ { get; }
        public float PathNorm { get; }
        public float StraightNorm { get; }

        /// <summary>Horizontal ray distances from the spot, ascending, each divided by the ray length.</summary>
        public float[] RayNorms { get; }

        public bool Covered { get; }
        public float CoverHeightNorm { get; }

        /// <summary>
        /// v2.1 only. For each of 16 directions, how clear the line is from the prop centre to a point at eye
        /// height 4 m (near) or 12 m (far) away: 1 = fully clear, smaller = blocked sooner. Sorted ascending, so
        /// only "how many directions can see it" remains, not which ones. Null on v2 candidates.
        /// </summary>
        public float[] SightNear { get; }

        public float[] SightFar { get; }

        /// <summary>Mean of the ray distances: small means an enclosed spot. Used by the "most enclosed" rule.</summary>
        public float Openness
        {
            get
            {
                if (!Exists || RayNorms == null)
                {
                    return 1f;
                }

                var sum = 0f;
                for (var i = 0; i < RayNorms.Length; i++)
                {
                    sum += RayNorms[i];
                }

                return sum / RayNorms.Length;
            }
        }
    }

    /// <summary>
    /// Fixed layout of the placement policy (PlaceSelect). Changing any number here is a new model version.
    /// Behavior Parameters must use Vector Observation Space Size = <see cref="VectorSize"/> and one discrete
    /// branch of size <see cref="ActionCount"/>.
    /// </summary>
    public static class PlaceObservationLayout
    {
        public const int SelfSize = 3;          // held item size one-hot
        public const int RayCount = 8;
        public const int CandidateSize = 7 + RayCount; // exists, dirX, dirZ, path, straight, rays×8, covered, coverHeight
        public const int CandidateSlots = 4;
        public const int VectorSize = SelfSize + CandidateSize * CandidateSlots; // 63
        public const int ActionCount = CandidateSlots;
        public const string Version = "place-obs-v2-63";
    }

    public enum PlaceObservationVersion
    {
        /// <summary>place-obs-v2-63: local enclosure cues only.</summary>
        V2 = 0,

        /// <summary>place-obs-v21-191: v2 cues plus 32 sightlines toward eye height per candidate.</summary>
        V21 = 1,
    }

    /// <summary>
    /// v2.1 layout: every v2 slot followed by 16 near + 16 far sightlines. A superset of v2, so any gain over v2
    /// can be attributed to the added sightline information.
    /// </summary>
    public static class PlaceObservationLayoutV21
    {
        public const int SightRayCount = 16;
        public const float NearDistance = 4f;
        public const float FarDistance = 12f;
        public const int CandidateSize = PlaceObservationLayout.CandidateSize + SightRayCount * 2; // 47
        public const int VectorSize = PlaceObservationLayout.SelfSize + CandidateSize * PlaceObservationLayout.CandidateSlots; // 191
        public const string Version = "place-obs-v21-191";
    }

    public static class PlaceObservationVersions
    {
        public static int VectorSize(PlaceObservationVersion version) =>
            version == PlaceObservationVersion.V21 ? PlaceObservationLayoutV21.VectorSize : PlaceObservationLayout.VectorSize;

        public static string Name(PlaceObservationVersion version) =>
            version == PlaceObservationVersion.V21 ? PlaceObservationLayoutV21.Version : PlaceObservationLayout.Version;
    }

    public static class PlaceObservationEncoder
    {
        public static void Encode(
            PlaceObservationVersion version,
            PickSizeClass heldSize,
            IReadOnlyList<PlaceCandidate> candidates,
            float[] buffer)
        {
            if (version == PlaceObservationVersion.V2)
            {
                Encode(heldSize, candidates, buffer);
                return;
            }

            if (buffer == null || buffer.Length != PlaceObservationLayoutV21.VectorSize)
            {
                throw new ArgumentException(
                    $"buffer must have exactly {PlaceObservationLayoutV21.VectorSize} floats",
                    nameof(buffer));
            }

            var index = 0;
            for (var k = 0; k < 3; k++)
            {
                buffer[index++] = k == (int)heldSize ? 1f : 0f;
            }

            for (var slot = 0; slot < PlaceObservationLayout.CandidateSlots; slot++)
            {
                var present = candidates != null && slot < candidates.Count && candidates[slot].Exists;
                var c = present ? candidates[slot] : default;
                index = WriteV2Slot(present, c, buffer, index);
                for (var layer = 0; layer < 2; layer++)
                {
                    var rays = layer == 0 ? c.SightNear : c.SightFar;
                    for (var r = 0; r < PlaceObservationLayoutV21.SightRayCount; r++)
                    {
                        buffer[index++] = present && rays != null ? Mathf.Clamp01(rays[r]) : 0f;
                    }
                }
            }
        }

        private static int WriteV2Slot(bool present, PlaceCandidate c, float[] buffer, int index)
        {
            if (!present)
            {
                for (var k = 0; k < PlaceObservationLayout.CandidateSize; k++)
                {
                    buffer[index++] = 0f;
                }

                return index;
            }

            buffer[index++] = 1f;
            buffer[index++] = Mathf.Clamp(c.DirectionX, -1f, 1f);
            buffer[index++] = Mathf.Clamp(c.DirectionZ, -1f, 1f);
            buffer[index++] = Mathf.Clamp01(c.PathNorm);
            buffer[index++] = Mathf.Clamp01(c.StraightNorm);
            for (var r = 0; r < PlaceObservationLayout.RayCount; r++)
            {
                buffer[index++] = Mathf.Clamp01(c.RayNorms[r]);
            }

            buffer[index++] = c.Covered ? 1f : 0f;
            buffer[index++] = Mathf.Clamp01(c.CoverHeightNorm);
            return index;
        }

        public static void Encode(PickSizeClass heldSize, IReadOnlyList<PlaceCandidate> candidates, float[] buffer)
        {
            if (buffer == null || buffer.Length != PlaceObservationLayout.VectorSize)
            {
                throw new ArgumentException(
                    $"buffer must have exactly {PlaceObservationLayout.VectorSize} floats",
                    nameof(buffer));
            }

            var index = 0;
            for (var k = 0; k < 3; k++)
            {
                buffer[index++] = k == (int)heldSize ? 1f : 0f;
            }

            for (var slot = 0; slot < PlaceObservationLayout.CandidateSlots; slot++)
            {
                if (candidates != null && slot < candidates.Count && candidates[slot].Exists)
                {
                    var c = candidates[slot];
                    buffer[index++] = 1f;
                    buffer[index++] = Mathf.Clamp(c.DirectionX, -1f, 1f);
                    buffer[index++] = Mathf.Clamp(c.DirectionZ, -1f, 1f);
                    buffer[index++] = Mathf.Clamp01(c.PathNorm);
                    buffer[index++] = Mathf.Clamp01(c.StraightNorm);
                    for (var r = 0; r < PlaceObservationLayout.RayCount; r++)
                    {
                        buffer[index++] = Mathf.Clamp01(c.RayNorms[r]);
                    }

                    buffer[index++] = c.Covered ? 1f : 0f;
                    buffer[index++] = Mathf.Clamp01(c.CoverHeightNorm);
                }
                else
                {
                    for (var k = 0; k < PlaceObservationLayout.CandidateSize; k++)
                    {
                        buffer[index++] = 0f;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Placement reward. Hiding dominates; the displacement bonus and the walking cost are small tie-breakers
    /// so the policy neither always runs to the farthest spot nor always drops the prop at its feet.
    /// </summary>
    public static class PlaceReward
    {
        public const float DefaultExposureScale = 20f; // 2026-09-25: 8 -> 20, typical exposure is 10-30 watch points
        public const float DefaultHideWeight = 1f;
        public const float DefaultDisplacementWeight = 0.1f;
        public const float DefaultPathWeight = 0.1f;

        /// <summary>exp(-visible / scale): 1 when no searcher point sees the spot, approaching 0 when many do.</summary>
        public static float Hide(int visibleWatchPoints, float exposureScale = DefaultExposureScale)
        {
            if (visibleWatchPoints <= 0)
            {
                return 1f;
            }

            return Mathf.Exp(-visibleWatchPoints / Mathf.Max(0.001f, exposureScale));
        }

        public static float Total(
            float hide,
            float straightNorm,
            float pathNorm,
            float hideWeight = DefaultHideWeight,
            float displacementWeight = DefaultDisplacementWeight,
            float pathWeight = DefaultPathWeight)
        {
            return hideWeight * hide +
                   displacementWeight * Mathf.Clamp01(straightNorm) -
                   pathWeight * Mathf.Clamp01(pathNorm);
        }
    }
}
