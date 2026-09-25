using System.Collections.Generic;
using Game.BotRuntime.Policy;
using NUnit.Framework;

namespace Game.Architecture.Tests
{
    /// <summary>Locks the placement observation layout and reward shape (place-obs-v2-63).</summary>
    public sealed class PlaceObservationEncoderTests
    {
        private static float[] Rays(float value)
        {
            var rays = new float[PlaceObservationLayout.RayCount];
            for (var i = 0; i < rays.Length; i++)
            {
                rays[i] = value;
            }

            return rays;
        }

        private static PlaceCandidate Candidate(float ray = 0.5f, bool covered = false) =>
            new(0.2f, 0.9f, 0.4f, 0.3f, Rays(ray), covered, covered ? 0.3f : 0f);

        [Test]
        public void Layout_IsSixtyThreeFloatsAndFourActions()
        {
            Assert.That(PlaceObservationLayout.CandidateSize, Is.EqualTo(15));
            Assert.That(PlaceObservationLayout.VectorSize, Is.EqualTo(63));
            Assert.That(PlaceObservationLayout.ActionCount, Is.EqualTo(4));
            Assert.That(PlaceObservationLayout.Version, Is.EqualTo("place-obs-v2-63"));
        }

        [Test]
        public void Encode_SelfBlockIsSizeOneHot()
        {
            var buffer = new float[PlaceObservationLayout.VectorSize];
            PlaceObservationEncoder.Encode(PickSizeClass.Large, null, buffer);
            Assert.That(new[] { buffer[0], buffer[1], buffer[2] }, Is.EqualTo(new[] { 0f, 0f, 1f }));
        }

        [Test]
        public void Encode_CandidateBlockIsInFixedOrder()
        {
            var buffer = new float[PlaceObservationLayout.VectorSize];
            PlaceObservationEncoder.Encode(PickSizeClass.Small, new List<PlaceCandidate> { Candidate(0.25f, covered: true) }, buffer);

            var b = PlaceObservationLayout.SelfSize;
            Assert.That(buffer[b + 0], Is.EqualTo(1f));    // exists
            Assert.That(buffer[b + 1], Is.EqualTo(0.2f));  // dir X
            Assert.That(buffer[b + 2], Is.EqualTo(0.9f));  // dir Z
            Assert.That(buffer[b + 3], Is.EqualTo(0.4f));  // path
            Assert.That(buffer[b + 4], Is.EqualTo(0.3f));  // straight
            for (var r = 0; r < PlaceObservationLayout.RayCount; r++)
            {
                Assert.That(buffer[b + 5 + r], Is.EqualTo(0.25f), $"ray {r}");
            }

            Assert.That(buffer[b + 13], Is.EqualTo(1f));   // covered
            Assert.That(buffer[b + 14], Is.EqualTo(0.3f)); // cover height
        }

        [Test]
        public void Encode_MissingSlotsAreZero()
        {
            var buffer = new float[PlaceObservationLayout.VectorSize];
            PlaceObservationEncoder.Encode(PickSizeClass.Small, new List<PlaceCandidate> { Candidate() }, buffer);
            for (var i = PlaceObservationLayout.SelfSize + PlaceObservationLayout.CandidateSize; i < buffer.Length; i++)
            {
                Assert.That(buffer[i], Is.EqualTo(0f), $"index {i}");
            }
        }

        [Test]
        public void Candidate_RequiresExactlyEightRays()
        {
            Assert.Throws<System.ArgumentException>(() => new PlaceCandidate(0, 0, 0, 0, new float[3], false, 0));
        }

        [Test]
        public void Openness_IsMeanRayDistance()
        {
            Assert.That(Candidate(0.25f).Openness, Is.EqualTo(0.25f).Within(1e-5f));
            Assert.That(default(PlaceCandidate).Openness, Is.EqualTo(1f));
        }

        [Test]
        public void Hide_IsOneWhenUnseenAndFallsWithExposure()
        {
            Assert.That(PlaceReward.Hide(0), Is.EqualTo(1f));
            Assert.That(PlaceReward.Hide(4), Is.GreaterThan(PlaceReward.Hide(8)));
            Assert.That(PlaceReward.Hide(20), Is.EqualTo(UnityEngine.Mathf.Exp(-1f)).Within(1e-5f));
        }

        private static float[] Sight(float value)
        {
            var rays = new float[PlaceObservationLayoutV21.SightRayCount];
            for (var i = 0; i < rays.Length; i++)
            {
                rays[i] = value;
            }

            return rays;
        }

        private static PlaceCandidate CandidateV21(float near, float far) =>
            new(0.2f, 0.9f, 0.4f, 0.3f, Rays(0.5f), false, 0f, Sight(near), Sight(far));

        [Test]
        public void V21_Layout_IsSupersetOfV2()
        {
            Assert.That(PlaceObservationLayoutV21.CandidateSize, Is.EqualTo(PlaceObservationLayout.CandidateSize + 32));
            Assert.That(PlaceObservationLayoutV21.VectorSize, Is.EqualTo(191));
            Assert.That(PlaceObservationVersions.VectorSize(PlaceObservationVersion.V2), Is.EqualTo(63));
            Assert.That(PlaceObservationVersions.VectorSize(PlaceObservationVersion.V21), Is.EqualTo(191));
            Assert.That(PlaceObservationVersions.Name(PlaceObservationVersion.V21), Is.EqualTo("place-obs-v21-191"));
        }

        [Test]
        public void V21_SlotKeepsV2BlockThenNearThenFar()
        {
            var v2 = new float[PlaceObservationLayout.VectorSize];
            var v21 = new float[PlaceObservationLayoutV21.VectorSize];
            var candidates = new List<PlaceCandidate> { CandidateV21(0.25f, 0.75f) };
            PlaceObservationEncoder.Encode(PlaceObservationVersion.V2, PickSizeClass.Medium, candidates, v2);
            PlaceObservationEncoder.Encode(PlaceObservationVersion.V21, PickSizeClass.Medium, candidates, v21);

            // Self block and the first slot's v2 block are identical in both versions.
            for (var i = 0; i < PlaceObservationLayout.SelfSize + PlaceObservationLayout.CandidateSize; i++)
            {
                Assert.That(v21[i], Is.EqualTo(v2[i]), $"index {i}");
            }

            var near = PlaceObservationLayout.SelfSize + PlaceObservationLayout.CandidateSize;
            var far = near + PlaceObservationLayoutV21.SightRayCount;
            for (var r = 0; r < PlaceObservationLayoutV21.SightRayCount; r++)
            {
                Assert.That(v21[near + r], Is.EqualTo(0.25f), $"near {r}");
                Assert.That(v21[far + r], Is.EqualTo(0.75f), $"far {r}");
            }

            // Slots 2..4 are empty: all zero.
            for (var i = PlaceObservationLayout.SelfSize + PlaceObservationLayoutV21.CandidateSize; i < v21.Length; i++)
            {
                Assert.That(v21[i], Is.EqualTo(0f), $"index {i}");
            }
        }

        [Test]
        public void V21_CandidateRequiresSixteenSightlinesPerLayer()
        {
            Assert.Throws<System.ArgumentException>(() =>
                new PlaceCandidate(0, 0, 0, 0, Rays(0.5f), false, 0, new float[3], Sight(1f)));
        }

        [Test]
        public void Total_HideDominatesTieBreakers()
        {
            var hiddenFarWalk = PlaceReward.Total(hide: 1f, straightNorm: 0f, pathNorm: 1f);
            var exposedNoWalk = PlaceReward.Total(hide: 0.1f, straightNorm: 1f, pathNorm: 0f);
            Assert.That(hiddenFarWalk, Is.GreaterThan(exposedNoWalk));
        }
    }
}
