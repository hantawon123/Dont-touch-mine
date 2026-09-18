using Game.Client.Players;
using Game.Core.Players;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class PlayerPostureAudioTests
    {
        [TestCase(PlayerPosture.Standing, PlayerPosture.Crouching, true)]
        [TestCase(PlayerPosture.Crouching, PlayerPosture.Standing, true)]
        [TestCase(PlayerPosture.Standing, PlayerPosture.Prone, true)]
        [TestCase(PlayerPosture.Prone, PlayerPosture.Standing, true)]
        [TestCase(PlayerPosture.Crouching, PlayerPosture.Prone, true)]
        [TestCase(PlayerPosture.Prone, PlayerPosture.Crouching, true)]
        [TestCase(PlayerPosture.Standing, PlayerPosture.Standing, false)]
        [TestCase(PlayerPosture.Crouching, PlayerPosture.Crouching, false)]
        [TestCase(PlayerPosture.Prone, PlayerPosture.Prone, false)]
        public void OnlyAuthoredPostureChangesSwoosh(
            PlayerPosture from, PlayerPosture to, bool expected)
        {
            Assert.That(
                PlayerAnimationDriver.ShouldPlayPostureSwoosh(from, to),
                Is.EqualTo(expected));
        }

        [Test]
        public void CharacterHasTheApprovedPostureSwoosh()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Content/Prefabs/PlayerCharacter.prefab");
            var clip = new SerializedObject(prefab.GetComponent<PlayerAnimationDriver>())
                .FindProperty("postureSwooshClip").objectReferenceValue as AudioClip;
            Assert.That(clip, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(clip), Is.EqualTo(
                "Assets/_Game/Content/Audio/Combat/PostureSwoosh.wav"));
            Assert.That(clip.channels, Is.EqualTo(1));
            Assert.That(clip.length, Is.EqualTo(.24f).Within(.001f));
            Assert.That(clip.frequency, Is.EqualTo(44100));
        }

        [Test]
        public void SitAndStandSwooshIsSeventyPercentOfCombatOneShots()
        {
            Assert.That(
                PlayerAnimationDriver.PostureSwooshAudioVolume,
                Is.EqualTo(.8f * .7f).Within(.001f));
        }
    }
}
