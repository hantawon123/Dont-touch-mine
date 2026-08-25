using System;
using Fusion;
using UnityEngine;

namespace Game.Network.Players
{
    /// <summary>
    /// The networked half of a spawned character. Owns nothing on its own yet;
    /// it exists so the spawner has something to spawn and later steps have a
    /// place to hang movement, nicknames and interaction.
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
        }

        private void SetOwnerOnlyEnabled(bool enabled)
        {
            for (var i = 0; i < _ownerOnly.Length; i++)
            {
                var behaviour = _ownerOnly[i];

                // Unity's equality covers a slot left empty in the inspector.
                if (behaviour != null)
                {
                    behaviour.enabled = enabled;
                }
            }
        }
    }
}
