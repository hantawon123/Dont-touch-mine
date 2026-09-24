using System;
using Game.Core.Players;
using UnityEngine;

namespace Game.Bootstrap
{
    /// <summary>
    /// Marks one opt-in position where the room authority should create a bot.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetworkBotSpawnPoint : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The scene object this bot should approach during the movement test.")]
        private Transform target;

        [SerializeField, Min(1)]
        private int botNumber = 1;

        [SerializeField]
        private string botNickname = "Pathfinder Bot";

        [SerializeField]
        private BotDifficulty difficulty = BotDifficulty.Normal;

        [SerializeField]
        private int appearanceSeed;

        [NonSerialized]
        private bool spawnRequested;

        public bool TryBeginSpawn(
            out BotProfile profile,
            out Pose pose,
            out Transform botTarget)
        {
            profile = default;
            pose = new Pose(transform.position, transform.rotation);
            botTarget = target;

            if (spawnRequested || botTarget == null || botNumber < 1)
            {
                return false;
            }

            profile = new BotProfile(
                botNumber,
                botNickname,
                difficulty,
                appearanceSeed);
            spawnRequested = true;
            return true;
        }

        public void CancelSpawn()
        {
            spawnRequested = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (target == null)
            {
                Debug.LogWarning(
                    $"[Bot] '{name}' needs a target before it can spawn a bot.",
                    this);
            }
        }
#endif
    }
}
