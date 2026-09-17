using Game.Client.Players;
using Game.Core.Players;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class PlayerFootstepAudioTests
    {
        [TestCase("Walk", true, PlayerPosture.Standing, 4f, true)]
        [TestCase("Run_Left", true, PlayerPosture.Standing, 7f, true)]
        [TestCase("Carry_TwoHands_Walk", true, PlayerPosture.Standing, 4f, true)]
        [TestCase("Crouch_Walk", true, PlayerPosture.Crouching, 2f, true)]
        [TestCase("Walk", false, PlayerPosture.Standing, 4f, false)]
        [TestCase("Walk", true, PlayerPosture.Standing, 0f, false)]
        [TestCase("Idle", true, PlayerPosture.Standing, 4f, false)]
        [TestCase("Jump", false, PlayerPosture.Standing, 4f, false)]
        [TestCase("Crawl", true, PlayerPosture.Prone, 1f, false)]
        public void OnlyGroundedMovingFootstepsPlay(string state, bool grounded,
            PlayerPosture posture, float speed, bool expected)
        {
            Assert.That(PlayerFootstepAudio.CanPlay(state, grounded, posture, speed), Is.EqualTo(expected));
        }

        [Test]
        public void WalkPlantsMatchTheAuthoredHipSquash()
        {
            Assert.That(
                PlayerFootstepAudio.PlantStep(.08f, PlayerFootstepAudio.WalkPlantA, PlayerFootstepAudio.WalkPlantB),
                Is.EqualTo(-1));
            Assert.That(
                PlayerFootstepAudio.PlantStep(.09f, PlayerFootstepAudio.WalkPlantA, PlayerFootstepAudio.WalkPlantB),
                Is.EqualTo(0));
            Assert.That(
                PlayerFootstepAudio.PlantStep(.58f, PlayerFootstepAudio.WalkPlantA, PlayerFootstepAudio.WalkPlantB),
                Is.EqualTo(0));
            Assert.That(
                PlayerFootstepAudio.PlantStep(.59f, PlayerFootstepAudio.WalkPlantA, PlayerFootstepAudio.WalkPlantB),
                Is.EqualTo(1));
            Assert.That(
                PlayerFootstepAudio.PlantStep(1.09f, PlayerFootstepAudio.WalkPlantA, PlayerFootstepAudio.WalkPlantB),
                Is.EqualTo(2));
        }

        [Test]
        public void CrouchWalkKeepsHalfCyclePlantsAndRunUsesItsOwn()
        {
            PlayerFootstepAudio.ResolvePlants("Crouch_Walk_Forward", out var crouchA, out var crouchB);
            Assert.That(crouchA, Is.EqualTo(0f));
            Assert.That(crouchB, Is.EqualTo(.5f));
            PlayerFootstepAudio.ResolvePlants("Run_Forward", out var runA, out var runB);
            Assert.That(runA, Is.EqualTo(PlayerFootstepAudio.RunPlantA));
            Assert.That(runB, Is.EqualTo(PlayerFootstepAudio.RunPlantB));
            PlayerFootstepAudio.ResolvePlants("Walk_Forward", out var walkA, out var walkB);
            Assert.That(walkA, Is.EqualTo(PlayerFootstepAudio.WalkPlantA));
            Assert.That(walkB, Is.EqualTo(PlayerFootstepAudio.WalkPlantB));
        }

        [Test]
        public void FootfallsFollowPlantCrossingsWithoutDuplicatesOrTransitionBursts()
        {
            var host = new GameObject("FootstepTest");
            try
            {
                var audio = host.AddComponent<PlayerFootstepAudio>();
                Assert.That(audio.AdvanceStep(1, 0f), Is.False);
                Assert.That(audio.AdvanceStep(1, .08f), Is.False);
                Assert.That(audio.AdvanceStep(1, .09f), Is.True);
                Assert.That(audio.AdvanceStep(1, .4f), Is.False);
                Assert.That(audio.AdvanceStep(1, .59f), Is.True);
                Assert.That(audio.AdvanceStep(2, 0f), Is.False);
                Assert.That(audio.AdvanceStep(2, .09f), Is.True);
                audio.Stop();
                Assert.That(audio.AdvanceStep(2, 2f), Is.False);
                // A stalled frame should emit one step, not a catch-up burst.
                Assert.That(audio.AdvanceStep(2, 4f), Is.True);
                Assert.That(audio.AdvanceStep(2, 4f), Is.False);
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void CharacterHasEightImportedOneShots()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Content/Prefabs/PlayerCharacter.prefab");
            var clips = new SerializedObject(prefab.GetComponent<PlayerAnimationDriver>())
                .FindProperty("footstepClips");
            Assert.That(clips.arraySize, Is.EqualTo(8));
            for (var index = 0; index < clips.arraySize; index++)
            {
                var clip = clips.GetArrayElementAtIndex(index).objectReferenceValue as AudioClip;
                Assert.That(clip, Is.Not.Null);
                Assert.That(clip.channels, Is.EqualTo(1));
                Assert.That(clip.length, Is.EqualTo(.18f).Within(.001f));
                Assert.That(clip.frequency, Is.EqualTo(44100));
            }
        }
    }
}
