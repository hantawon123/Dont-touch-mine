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
        /// What the owner asked to be called. Replicated because every peer
        /// shows the room's roster, and only the authority saw the name its
        /// owner presented when joining.
        /// </summary>
        /// <remarks>
        /// Display only. Code identifies a player by
        /// <see cref="PlayerRegistry.IdOf"/>, never by this: two people may pick
        /// the same name, and a name may change.
        /// <para>
        /// Replicated state rather than a message, so a peer that joins later is
        /// told every existing name without anyone having to resend them.
        /// </para>
        /// </remarks>
        [Networked]
        public NetworkString<_32> Nickname { get; set; }

        /// <summary>
        /// The backend account the owner signed in as. Empty when they did not.
        /// </summary>
        /// <remarks>
        /// Replicated the same way as <see cref="Nickname"/>, for the same
        /// reason: only the authority saw the value its owner presented when
        /// joining, and every peer needs it — the host to say whose actions it
        /// reports to the play log, anyone to reach that person's account after
        /// the room is gone. A connection token is read once by one peer; a
        /// networked value is there for a late joiner and for the next host.
        /// <para>
        /// Identification, not authentication. Anyone who knows an account id
        /// can already act as it against the REST API, so carrying it here adds
        /// nothing to what a peer could learn. The device id — the credential —
        /// never travels this way. Not shown on screen either: the client guide
        /// forbids exposing another player's account id.
        /// </para>
        /// <para>
        /// The server's public id is a 36-character UUID. Fusion's fixed sizes
        /// are powers of two, so 64 is the smallest that holds it whole; a value
        /// longer than the marker would arrive silently truncated.
        /// </para>
        /// </remarks>
        [Networked]
        public NetworkString<_64> UserId { get; set; }

        /// <summary>
        /// Whether this player manages the lobby; this does not grant simulation authority. Replicated rather
        /// than derived: a peer can tell whether it is itself the host, but not
        /// which of the others is.
        /// </summary>
        [Networked]
        public bool IsHost { get; set; }

        // Hidden until the owner publishes its preference, including late joins.
        [Networked] public int NicknameVisibility { get; set; }
        [Networked] public NetworkString<_512> NicknameViewers { get; set; }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_SetNicknameVisibility(int visibility, string viewers)
        {
            if (visibility < 0 || visibility > 2 || viewers == null || viewers.Length > 511) return;
            NicknameVisibility = visibility;
            NicknameViewers = viewers;
        }


        /// <summary>
        /// The player this character belongs to. Comes from the spawner and is
        /// the same value on every peer.
        /// </summary>
        public PlayerRef Owner => Object.InputAuthority;
        private PlayerRef cachedOwner;
        private string cachedPlayerId;

        public string PlayerId
        {
            get
            {
                if (Object == null || !Object.IsValid) return null;
                var owner = Owner;
                if (owner != cachedOwner)
                {
                    cachedOwner = owner;
                    cachedPlayerId = PlayerRegistry.IdOf(owner);
                }
                return cachedPlayerId;
            }
        }

        /// <summary>True on the peer whose player this character belongs to.</summary>
        public bool IsOwner => Object.HasInputAuthority;

        private bool _publishedIsHost;

        public override void Render()
        {
            if (_publishedIsHost == IsHost) return;
            _publishedIsHost = IsHost;
            RosterOf(Runner)?.Refresh(Runner);
        }

        public override void Spawned()
        {
            _publishedIsHost = IsHost;
            SetOwnerOnlyEnabled(IsOwner);

            // The movement component is kept only as the shared input/config
            // source. NetworkPlayerMotor is the sole component that moves this
            // networked body, including on the owning peer.
            var behaviours = GetComponents<MonoBehaviour>();
            for (var index = 0; index < behaviours.Length; index++)
            {
                if (behaviours[index] is Game.Core.Players.IPlayerInputIntentSource)
                {
                    behaviours[index].enabled = false;
                }
            }

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
