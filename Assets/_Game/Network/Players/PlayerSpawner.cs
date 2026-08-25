using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace Game.Network.Players
{
    /// <summary>
    /// Gives every player in the room a character and takes it away when they
    /// leave.
    /// </summary>
    /// <remarks>
    /// Spawning happens only where authority is, so this is gated on
    /// <see cref="NetworkRunner.IsServer"/> rather than on being the host.
    /// Moving to a dedicated server changes nothing here.
    /// <para>
    /// The runner arrives as an argument instead of being held. The session
    /// service already owns it and calls in from its own callbacks, so keeping a
    /// second reference would only create a way for the two to disagree after a
    /// runner is replaced.
    /// </para>
    /// </remarks>
    public sealed class PlayerSpawner
    {
        /// <summary>
        /// Ring size for the fallback layout. Matches the largest room so the
        /// spacing does not change when a seat is empty.
        /// </summary>
        private const int FallbackSeats = 6;

        private const float FallbackRadius = 3f;

        private readonly NetworkPrefabs _prefabs;
        private readonly PlayerRegistry _players;

        private IReadOnlyList<Pose> _spawnPoses = Array.Empty<Pose>();

        public PlayerSpawner(NetworkPrefabs prefabs, PlayerRegistry players)
        {
            _prefabs = prefabs;
            _players = players;
        }

        /// <summary>
        /// Hands over the spawn points the loaded scene marked out. The scene
        /// pushes them rather than this pulling, because this lives for the
        /// whole application and the points belong to one scene.
        /// </summary>
        public void UseSpawnPoses(IReadOnlyList<Pose> poses)
        {
            _spawnPoses = poses ?? Array.Empty<Pose>();

            if (_spawnPoses.Count == 0)
            {
                Debug.LogWarning(
                    "[Spawn] No spawn points in this scene. Characters will be " +
                    "placed in a ring around the origin.");
            }
        }

        public void Spawn(NetworkRunner runner, PlayerRef player)
        {
            if (runner == null || !runner.IsServer)
            {
                return;
            }

            var prefab = _prefabs == null ? null : _prefabs.Player;

            if (prefab == null)
            {
                Debug.LogError(
                    "[Spawn] No player prefab is assigned. Set it on the " +
                    "NetworkPrefabs asset referenced by ProjectLifetimeScope.");
                return;
            }

            var seat = _players.Add(player);
            var pose = PoseFor(seat);

            var avatar = runner.Spawn(prefab, pose.position, pose.rotation, player);

            if (avatar == null)
            {
                // Fusion already logged why. Give the seat back so the next
                // player does not inherit a hole in the numbering.
                _players.Remove(player);
                Debug.LogError($"[Spawn] Could not spawn a character for {player}.");
                return;
            }

            // Fusion's own lookup, so anything else that needs this player's
            // character finds it without a second table to keep in step.
            runner.SetPlayerObject(player, avatar);

            Debug.Log($"[Spawn] {player} took seat {seat} at {pose.position}.");
        }

        public void Despawn(NetworkRunner runner, PlayerRef player)
        {
            if (runner == null || !runner.IsServer)
            {
                return;
            }

            var avatar = runner.GetPlayerObject(player);

            if (avatar != null)
            {
                runner.Despawn(avatar);
            }

            if (_players.Remove(player))
            {
                Debug.Log($"[Spawn] {player} left and freed their seat.");
            }
        }

        /// <summary>
        /// Forgets everyone. The session ending takes the characters with it, so
        /// only the seating has to be cleared for the next room.
        /// </summary>
        public void Clear()
        {
            _players.Clear();
            _spawnPoses = Array.Empty<Pose>();
        }

        private Pose PoseFor(int seat)
        {
            if (_spawnPoses.Count > 0)
            {
                // Wrapping matters only if a room ever holds more players than
                // the scene marked points for. Overlapping beats not spawning.
                return _spawnPoses[seat % _spawnPoses.Count];
            }

            // Scenes without marked points still have to be testable, so the
            // characters are spread far enough apart to be told apart.
            var angle = seat * (2f * Mathf.PI / FallbackSeats);
            var position = new Vector3(
                Mathf.Sin(angle) * FallbackRadius,
                0f,
                Mathf.Cos(angle) * FallbackRadius);

            return new Pose(position, Quaternion.LookRotation(-position));
        }
    }
}
