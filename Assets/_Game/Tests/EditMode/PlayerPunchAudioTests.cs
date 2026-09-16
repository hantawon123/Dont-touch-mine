using Game.Client.Players;
using Game.Client.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class PlayerPunchAudioTests
    {
        [Test]
        public void CharacterHasTheProceduralHitSound()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Content/Prefabs/PlayerCharacter.prefab");
            var clip = new SerializedObject(prefab.GetComponent<PlayerAnimationDriver>())
                .FindProperty("punchHitClip").objectReferenceValue as AudioClip;
            Assert.That(clip, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(clip), Is.EqualTo(
                "Assets/_Game/Content/Audio/Combat/SoftPunchHit.wav"));
            Assert.That(clip.channels, Is.EqualTo(1));
            Assert.That(clip.length, Is.EqualTo(.22f).Within(.001f));
        }

        [Test]
        public void ReplicatedHitsNotifyOnceIncludingTheStunningHit()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Content/Prefabs/PlayerCharacter.prefab");
            var player = Object.Instantiate(prefab);
            try
            {
                var combatant = player.GetComponent<PlayerCombatant>();
                var hits = 0;
                combatant.HitReceived += () => hits++;
                combatant.SetNetworkStunned(false);
                combatant.SetNetworkHitCount(0);
                Assert.That(hits, Is.Zero);
                combatant.SetNetworkHitCount(1);
                combatant.SetNetworkHitCount(1);
                Assert.That(hits, Is.EqualTo(1));
                combatant.SetNetworkHitCount(2);
                Assert.That(hits, Is.EqualTo(2));
                combatant.SetNetworkStunned(true);
                combatant.SetNetworkHitCount(0);
                combatant.SetNetworkStunned(true);
                combatant.SetNetworkHitCount(0);
                Assert.That(hits, Is.EqualTo(3));
                combatant.SetNetworkStunned(false);
                combatant.SetNetworkHitCount(0);
                Assert.That(hits, Is.EqualTo(3));
                combatant.SetNetworkHitCount(1);
                Assert.That(hits, Is.EqualTo(4));
            }
            finally { Object.DestroyImmediate(player); }
        }

        [Test]
        public void InitialStunnedSnapshotDoesNotReplayAHit()
        {
            var player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Content/Prefabs/PlayerCharacter.prefab"));
            try
            {
                var combatant = player.GetComponent<PlayerCombatant>();
                var hits = 0;
                combatant.HitReceived += () => hits++;
                combatant.SetNetworkStunned(true);
                combatant.SetNetworkHitCount(0);
                Assert.That(hits, Is.Zero);
            }
            finally { Object.DestroyImmediate(player); }
        }

        [Test]
        public void CharacterUsesTheApprovedProceduralSwing()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Content/Prefabs/PlayerCharacter.prefab");
            var driver = prefab.GetComponent<PlayerAnimationDriver>();
            var clip = new SerializedObject(driver).FindProperty("punchSwingClip")
                .objectReferenceValue as AudioClip;
            Assert.That(clip, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(clip), Is.EqualTo(
                "Assets/_Game/Content/Audio/Combat/PunchSwing.wav"));
            Assert.That(clip.channels, Is.EqualTo(1));
            Assert.That(clip.length, Is.EqualTo(.18f).Within(.001f));
            Assert.That(clip.frequency, Is.EqualTo(44100));
        }
    }
}
