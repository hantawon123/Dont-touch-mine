using Game.Client.Players;
using Game.Core.Players;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class PlayerJumpAudioTests
    {
        [TestCase(true, false, false, .05f, PlayerPosture.Standing, true)]
        [TestCase(true, true, false, .05f, PlayerPosture.Standing, false)]
        [TestCase(false, false, false, .05f, PlayerPosture.Standing, false)]
        [TestCase(true, false, true, .05f, PlayerPosture.Standing, false)]
        [TestCase(true, false, false, -.05f, PlayerPosture.Standing, false)]
        [TestCase(true, false, false, 0f, PlayerPosture.Standing, false)]
        [TestCase(true, false, false, .05f, PlayerPosture.Crouching, false)]
        [TestCase(true, false, false, .05f, PlayerPosture.Prone, false)]
        [TestCase(true, false, false, 5f, PlayerPosture.Standing, false)]
        public void OnlyUpwardTakeoffPlays(bool groundedSeen, bool alreadyPlayed,
            bool grounded, float rise, PlayerPosture posture, bool expected)
        {
            Assert.That(PlayerAnimationDriver.ShouldPlayJumpSound(
                groundedSeen, alreadyPlayed, grounded, rise, posture), Is.EqualTo(expected));
        }

        [Test]
        public void CharacterHasTheApprovedJumpSound()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Content/Prefabs/PlayerCharacter.prefab");
            var clip = new SerializedObject(prefab.GetComponent<PlayerAnimationDriver>())
                .FindProperty("jumpClip").objectReferenceValue as AudioClip;
            Assert.That(clip, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(clip), Is.EqualTo(
                "Assets/_Game/Content/Audio/Combat/Jump.wav"));
            Assert.That(clip.channels, Is.EqualTo(1));
            Assert.That(clip.length, Is.EqualTo(.28f).Within(.001f));
        }

        [TestCase(true, true, true, PlayerPosture.Standing, true)]
        [TestCase(true, false, true, PlayerPosture.Standing, false)]
        [TestCase(false, true, true, PlayerPosture.Standing, false)]
        [TestCase(true, true, false, PlayerPosture.Standing, false)]
        [TestCase(true, true, true, PlayerPosture.Crouching, false)]
        [TestCase(true, true, true, PlayerPosture.Prone, false)]
        public void OnlyAJumpLandingPlays(bool wasAirborne, bool jumped,
            bool grounded, PlayerPosture posture, bool expected)
        {
            Assert.That(PlayerAnimationDriver.ShouldPlayLandSound(
                wasAirborne, jumped, grounded, posture), Is.EqualTo(expected));
        }

        [Test]
        public void CharacterHasTheApprovedLandSound()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Content/Prefabs/PlayerCharacter.prefab");
            var clip = new SerializedObject(prefab.GetComponent<PlayerAnimationDriver>())
                .FindProperty("landClip").objectReferenceValue as AudioClip;
            Assert.That(clip, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(clip), Is.EqualTo(
                "Assets/_Game/Content/Audio/Combat/Land.wav"));
            Assert.That(clip.channels, Is.EqualTo(1));
            Assert.That(clip.length, Is.EqualTo(.22f).Within(.001f));
            Assert.That(clip.frequency, Is.EqualTo(44100));
        }

        [Test]
        public void LandSoundPlaysWhenTheLandClipStarts()
        {
            Assert.That(PlayerAnimationDriver.LandAudioVolume, Is.EqualTo(.64f).Within(.001f));
            Assert.That(PlayerAnimationDriver.ShouldPlayPendingLandSound(true, false), Is.False);
            Assert.That(PlayerAnimationDriver.ShouldPlayPendingLandSound(false, true), Is.False);
            Assert.That(PlayerAnimationDriver.ShouldPlayPendingLandSound(true, true), Is.True);
        }
    }
}
