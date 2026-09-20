using Game.Client.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Architecture.Tests
{
    public sealed class StunScreenGrayscaleViewTests
    {
        [Test]
        public void Apply_GrayscalesTheLocalScreenOnlyWhileStunned()
        {
            var player = InstantiateCharacter();
            try
            {
                var combatant = player.GetComponent<PlayerCombatant>();
                var view = player.GetComponent<StunScreenGrayscaleView>();
                Assert.That(view, Is.Not.Null);

                combatant.ConfigureNetworkPlayer(0, true, true);
                combatant.SetNetworkHitCount(0);
                combatant.SetNetworkStunned(true);
                view.Apply();
                Assert.That(view.IsActive, Is.True);

                combatant.SetNetworkStunned(false);
                view.Apply();
                Assert.That(view.IsActive, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void Apply_DoesNotGrayscaleARemoteStunnedPlayer()
        {
            var player = InstantiateCharacter();
            try
            {
                var combatant = player.GetComponent<PlayerCombatant>();
                var view = player.GetComponent<StunScreenGrayscaleView>();
                Assert.That(view, Is.Not.Null);
                combatant.ConfigureNetworkPlayer(0, false, false);
                combatant.SetNetworkHitCount(0);
                combatant.SetNetworkStunned(true);
                view.Apply();
                Assert.That(view.IsActive, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        private static GameObject InstantiateCharacter()
        {
            var player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Content/Prefabs/PlayerCharacter.prefab"));
            // Runtime adds this in PlayerCombatant.Awake. EditMode Instantiate
            // does not always run Awake, so the remote case was calling Apply
            // on a missing view.
            if (player.GetComponent<StunScreenGrayscaleView>() == null)
            {
                player.AddComponent<StunScreenGrayscaleView>();
            }

            return player;
        }
    }
}
