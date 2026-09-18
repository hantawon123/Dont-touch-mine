using Game.Client.Interactions;
using Game.Client.Players;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class PlayerItemAudioTests
    {
        [Test]
        public void CharacterUsesTheApprovedPickupSound()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Content/Prefabs/PlayerCharacter.prefab");
            var clip = new SerializedObject(prefab.GetComponent<PlayerAnimationDriver>())
                .FindProperty("pickupSoundClip").objectReferenceValue as AudioClip;
            Assert.That(clip, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(clip), Is.EqualTo(
                "Assets/_Game/Content/Audio/Items/ItemPickup.wav"));
            Assert.That(clip.channels, Is.EqualTo(1));
            Assert.That(clip.length, Is.EqualTo(.16f).Within(.001f));
            Assert.That(clip.frequency, Is.EqualTo(44100));
        }

        [Test]
        public void CharacterUsesTheApprovedPlaceSound()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Content/Prefabs/PlayerCharacter.prefab");
            var clip = new SerializedObject(prefab.GetComponent<PlayerAnimationDriver>())
                .FindProperty("putDownSoundClip").objectReferenceValue as AudioClip;
            Assert.That(clip, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(clip), Is.EqualTo(
                "Assets/_Game/Content/Audio/Items/ItemPlace.wav"));
            Assert.That(clip.channels, Is.EqualTo(1));
            Assert.That(clip.length, Is.EqualTo(.24f).Within(.001f));
            Assert.That(clip.frequency, Is.EqualTo(44100));
        }

        [Test]
        public void CharacterUsesTheApprovedThrowSound()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Content/Prefabs/PlayerCharacter.prefab");
            var clip = new SerializedObject(prefab.GetComponent<PlayerAnimationDriver>())
                .FindProperty("throwSoundClip").objectReferenceValue as AudioClip;
            Assert.That(clip, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(clip), Is.EqualTo(
                "Assets/_Game/Content/Audio/Items/ItemThrow.wav"));
            Assert.That(clip.channels, Is.EqualTo(1));
            Assert.That(clip.length, Is.EqualTo(.32f).Within(.001f));
            Assert.That(clip.frequency, Is.EqualTo(44100));
        }

        [Test]
        public void ItemCues_PlayOncePerHold()
        {
            Assert.That(PlayerInteractor.ShouldPlayItemCue(null, null), Is.False);
            var item = new GameObject("CueItem", typeof(Rigidbody)).AddComponent<CarryableItem>();
            try
            {
                Assert.That(PlayerInteractor.ShouldPlayItemCue(null, item), Is.True);
                Assert.That(PlayerInteractor.ShouldPlayItemCue(item, item), Is.False);
                var other = new GameObject("OtherCueItem", typeof(Rigidbody)).AddComponent<CarryableItem>();
                try
                {
                    Assert.That(PlayerInteractor.ShouldPlayItemCue(item, other), Is.True);
                }
                finally
                {
                    Object.DestroyImmediate(other.gameObject);
                }
            }
            finally
            {
                Object.DestroyImmediate(item.gameObject);
            }
        }
    }
}
