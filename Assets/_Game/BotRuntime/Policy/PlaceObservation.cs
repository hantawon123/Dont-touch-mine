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

    public static class PlaceObservationEncoder
    {
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
