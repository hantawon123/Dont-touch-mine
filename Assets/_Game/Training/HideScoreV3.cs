using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace Game.Training
{
    /// <summary>
    /// Hide score v3: how much of the placed prop seekers can see.
    /// For every seeker eye within range, the fraction of ten points on the prop's box that have a clear line of
    /// sight, weighted by closeness (min(1, near / distance)); the sum is the exposure. Hide = exp(-exposure / scale).
    /// Occluders are whatever colliders the physics scene holds at call time (the bank builder disables carryable
    /// props first, decision D3).
    /// </summary>
    public static class HideScoreV3
    {
        public const int SamplesPerBox = 10;
        public const float DefaultRange = 15f;
        public const float DefaultNearDistance = 3f;

        public static float Hide(float exposure, float scale) =>
            scale > 0f ? Mathf.Exp(-Mathf.Max(0f, exposure) / scale) : 0f;

        /// <summary>Centre, top centre and the eight corners pulled 10% inward (axis-aligned box).</summary>
        public static void SamplePoints(Vector3 bottomCentre, Vector3 halfExtents, Vector3[] into)
        {
            var centre = bottomCentre + Vector3.up * halfExtents.y;
            var h = halfExtents * 0.9f;
            into[0] = centre;
            into[1] = centre + Vector3.up * h.y;
            var k = 2;
            for (var i = 0; i < 8; i++)
            {
                into[k++] = centre + new Vector3(
                    (i & 1) == 0 ? -h.x : h.x,
                    (i & 2) == 0 ? -h.y : h.y,
                    (i & 4) == 0 ? -h.z : h.z);
            }
        }

        /// <summary>
        /// Exposure of many boxes at once with batched raycasts. boxes[i] = (bottom centre, half extents).
        /// </summary>
        public static float[] Exposures(
            IReadOnlyList<(Vector3 bottom, Vector3 half)> boxes,
            IReadOnlyList<Vector3> eyes,
            int occluderMask,
            float range = DefaultRange,
            float nearDistance = DefaultNearDistance,
            int maxCommandsPerBatch = 400000)
        {
            var result = new float[boxes.Count];
            var samples = new Vector3[SamplesPerBox];
            var rangeSq = range * range;
            var query = new QueryParameters(occluderMask, false, QueryTriggerInteraction.Ignore, false);

            // Work list: (box, eye) pairs within range; each pair becomes SamplesPerBox rays.
            var pairBox = new List<int>();
            var pairWeight = new List<float>();
            var commands = new List<RaycastCommand>();

            void Flush()
            {
                if (commands.Count == 0)
                {
                    return;
                }

                using var cmd = new NativeArray<RaycastCommand>(commands.ToArray(), Allocator.TempJob);
                using var hits = new NativeArray<RaycastHit>(commands.Count, Allocator.TempJob);
                RaycastCommand.ScheduleBatch(cmd, hits, 256, 1).Complete();
                for (var p = 0; p < pairBox.Count; p++)
                {
                    var clear = 0;
                    for (var s = 0; s < SamplesPerBox; s++)
                    {
                        if (hits[p * SamplesPerBox + s].colliderInstanceID == 0)
                        {
                            clear++;
                        }
                    }

                    result[pairBox[p]] += pairWeight[p] * clear / SamplesPerBox;
                }

                pairBox.Clear();
                pairWeight.Clear();
                commands.Clear();
            }

            for (var b = 0; b < boxes.Count; b++)
            {
                SamplePoints(boxes[b].bottom, boxes[b].half, samples);
                var centre = samples[0];
                foreach (var eye in eyes)
                {
                    var distSq = (centre - eye).sqrMagnitude;
                    if (distSq > rangeSq || distSq < 0.01f)
                    {
                        continue;
                    }

                    pairBox.Add(b);
                    pairWeight.Add(Mathf.Min(1f, nearDistance / Mathf.Sqrt(distSq)));
                    for (var s = 0; s < SamplesPerBox; s++)
                    {
                        var offset = samples[s] - eye;
                        var length = offset.magnitude;
                        commands.Add(new RaycastCommand(eye, offset / length, query, Mathf.Max(0f, length - 0.02f)));
                    }

                    if (commands.Count >= maxCommandsPerBatch)
                    {
                        Flush();
                    }
                }
            }

            Flush();
            return result;
        }
    }
}
