using System;
using Fusion;
using UnityEngine;

namespace Game.Network.Players
{
    /// <summary>
    /// The networked half of a spawned character. Carries the few facts about
    /// its owner that every peer has to agree on, and turns off whatever must
    /// only run for the person who owns it.
    /// </summary>
    /// <remarks>
    /// The owning player is not stored. Fusion already records it as the
    /// object's input authority when the spawner passes the player in, and a
    /// second copy would replicate the same fact twice.
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class PlayerAvatar : NetworkBehaviour
    {
        // Qualified because Fusion declares a Behaviour of its own.
        [SerializeField]
        [Tooltip("Behaviours that read local input or drive a local camera. " +
                 "They must not run on a copy of someone else's character.")]
        private UnityEngine.Behaviour[] _ownerOnly = Array.Empty<UnityEngine.Behaviour>();


        /// <summary>
        /// Seat number, handed out by the spawner in join order. Replicated
        /// because presentation on every peer orders the room by it, and only
        /// the authority knows who arrived when.
        /// </summary>
        [Networked]
        public int Seat { get; set; }

        /// <summary>
        /// Whether the owner holds authority over the room. Replicated rather
        /// than derived: a peer can tell whether it is itself the host, but not
        /// which of the others is.
        /// </summary>
        [Networked]
        public bool IsHost { get; set; }

        /// <summary>
        /// The player this character belongs to. Comes from the spawner and is
        /// the same value on every peer.
        /// </summary>
        public PlayerRef Owner => Object.InputAuthority;

        /// <summary>True on the peer whose player this character belongs to.</summary>
        public bool IsOwner => Object.HasInputAuthority;

        public override void Spawned()
        {
            // Off on every peer for now, the owner included. Local input cannot
            // move a networked character while the host holds state authority:
            // NetworkTransform replicates the host's value straight back over
            // anything moved locally, so the character would snap back. The
            // input pipeline turns these on for the owner once movement is
            // simulated on the host instead.
            SetOwnerOnlyEnabled(false);

            RosterOf(Runner)?.Add(this);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            // The runner arrives as an argument here because this object is on
            // its way out and Runner may already be gone.
            RosterOf(runner)?.Remove(this, runner);
        }

        /// <summary>
        /// The roster sits on the runner object, which is the one place a
        /// Fusion-spawned object can reach without being injected.
        /// </summary>
        private static PlayerRoster RosterOf(NetworkRunner runner)
        {
            return runner == null ? null : runner.GetComponent<PlayerRoster>();
        }

        private void SetOwnerOnlyEnabled(bool enabled)
        {
            var missing = 0;

            for (var i = 0; i < _ownerOnly.Length; i++)
            {
                var behaviour = _ownerOnly[i];

                // Unity's equality covers a slot left empty in the inspector.
                if (behaviour == null)
                {
                    missing++;
                    continue;
                }

                behaviour.enabled = enabled;
            }

            // An empty slot used to be skipped in silence, which is the worst
            // possible outcome: the behaviour it was meant to name keeps running
            // on every copy of the character, so a peer drives someone else's
            // body and nothing says so.
            if (missing > 0)
            {
                Debug.LogError(
                    $"[Avatar] {missing} of {_ownerOnly.Length} owner-only slots on " +
                    $"'{name}' are empty, so those behaviours run on every peer. " +
                    "Re-assign them on the NetworkedPlayer prefab.",
                    this);
            }

            if (_ownerOnly.Length == 0)
            {
                Debug.LogWarning(
                    $"[Avatar] '{name}' lists no owner-only behaviours. Local " +
                    "input and camera components will run on remote copies too.",
                    this);
            }
        }
    }
}
