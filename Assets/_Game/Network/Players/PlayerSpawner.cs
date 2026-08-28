using System;
using System.Collections.Generic;
using Fusion;
using Game.Core.Lobby;
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

        /// <summary>
        /// Creates the objects that belong to the room rather than to a player.
        /// Called once when the session starts.
        /// </summary>
        /// <remarks>
        /// Only the authority creates them; everyone else receives them through
        /// replication. Spawned before anyone can ask to start a match, because
        /// the request travels through the object itself.
        /// </remarks>
        public void SpawnRoomObjects(NetworkRunner runner)
        {
            if (runner == null || !runner.IsServer)
            {
                return;
            }

            var prefab = _prefabs == null ? null : _prefabs.MatchSession;

            if (prefab == null)
            {
                Debug.LogError(
                    "[Spawn] No match session prefab is assigned, so no match can " +
                    "be started. Set it on the NetworkPrefabs asset.");
                return;
            }

            var session = runner.Spawn(prefab);

            if (session == null)
            {
                Debug.LogError("[Spawn] Could not spawn the match session object.");
                return;
            }

            // Belongs to the room, not to a scene. Fusion in single-peer mode
            // leaves a spawned object in whichever scene was active, so loading
            // the match scene would destroy the room's record of the match along
            // with its confirmed line-up.
            runner.MakeDontDestroyOnLoad(session.gameObject);
        }

        public void Spawn(NetworkRunner runner, PlayerRef player, string nickname = null)
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

            // In host mode the authority is also a player, so the room's owner
            // is whoever the server is playing as. A dedicated server plays as
            // nobody and LocalPlayer is none, which correctly leaves the flag
            // off everyone until a separate owner is tracked.
            var isHost = player == runner.LocalPlayer;

            // Networked values are set here, not after Spawn returns. Fusion
            // replicates the object as it is created, and a value written
            // afterwards would reach clients a tick late behind its default.
            var avatar = runner.Spawn(
                prefab,
                pose.position,
                pose.rotation,
                player,
                (_, spawned) => Describe(spawned, seat, isHost, nickname));

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

            // A character belongs to the room, which outlives any one scene.
            // Fusion in single-peer mode leaves spawned objects in whichever
            // scene was active, so without this the match scene load destroys
            // everyone as it replaces the lobby scene. This is the same call
            // Fusion makes for objects it is told to keep.
            runner.MakeDontDestroyOnLoad(avatar.gameObject);

            // Spawned() keeps KCC inactive while the room is still changing
            // scenes. A player joining an already-loaded lobby does not receive
            // another scene callback, so activate that avatar immediately when
            // this scene has already supplied valid spawn points.
            if (_spawnPoses.Count > 0 &&
                avatar.TryGetBehaviour<NetworkPlayerMotor>(out var motor))
            {
                motor.TryTeleport(pose);
            }

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
        /// Reconnects Fusion's restored player object to the authoritative seat
        /// table after a host migration.
        /// </summary>
        public bool Restore(
            NetworkRunner runner,
            PlayerRef player,
            NetworkObject restoredObject)
        {
            if (runner == null || !runner.IsServer ||
                !player.IsRealPlayer || restoredObject == null ||
                !restoredObject.TryGetBehaviour<PlayerAvatar>(out var avatar) ||
                !_players.Restore(player, avatar.Seat))
            {
                return false;
            }

            runner.SetPlayerObject(player, restoredObject);
            runner.MakeDontDestroyOnLoad(restoredObject.gameObject);
            return true;
        }

        /// <summary>
        /// Puts every seated character back onto a spawn point. Called after a
        /// networked scene load, because the points the characters were placed
        /// on belonged to the scene that has just gone away.
        /// </summary>
        /// <remarks>
        /// Authority only, for two reasons: it is the peer that holds the
        /// seating, and it is the peer whose transform replicates. Everyone else
        /// receives the move.
        /// <para>
        /// Positions are picked by seat, exactly as on the first spawn, so the
        /// two can never disagree about where a seat stands. Lining spawn points
        /// up with <c>playerIndex</c> instead is the match runtime's job: it owns
        /// the positions array the match rules read, and seats may have gaps.
        /// </para>
        /// </remarks>
        public void RepositionSeated(NetworkRunner runner)
        {
            if (runner == null || !runner.IsServer)
            {
                return;
            }

            var moved = 0;
            var seatsFound = 0;
            var avatarsFound = 0;

            // Seats can have gaps, so every seat is asked rather than counting
            // players and walking that far.
            for (var seat = 0; seat < RoomSettings.MaxPlayerCount; seat++)
            {
                if (!_players.TryGetPlayer(seat, out var player))
                {
                    continue;
                }

                seatsFound++;
                var avatar = runner.GetPlayerObject(player);

                if (avatar == null)
                {
                    continue;
                }

                avatarsFound++;

                var pose = PoseFor(seat);
                var motor = avatar.GetComponent<NetworkPlayerMotor>();

                if (motor == null)
                {
                    Debug.LogWarning(
                        $"[Spawn] '{avatar.name}' has no NetworkPlayerMotor, so it " +
                        "cannot be moved into the loaded scene.",
                        avatar);

                    continue;
                }

                // Teleport rather than assignment: remote peers interpolate
                // between the last two states, and a plain move would show the
                // character sliding across the map from where the lobby put it.
                if (!motor.TryTeleport(pose))
                {
                    continue;
                }

                moved++;

                Debug.Log($"[Spawn] Seat {seat} placed at {pose.position}.");
            }

            // Always reported, and reported in parts. Which of the three numbers
            // is zero says where it stopped: no seats means the registry was
            // cleared, seats without avatars means the characters did not survive
            // the scene change, and avatars without moves means the prefab has no
            // network movement component.
            Debug.Log(
                $"[Spawn] Reposition: {_spawnPoses.Count} poses, {seatsFound} seats, " +
                $"{avatarsFound} avatars, {moved} moved.");
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

        private static void Describe(
            NetworkObject spawned, int seat, bool isHost, string nickname)
        {
            var avatar = spawned.GetComponent<PlayerAvatar>();

            if (avatar == null)
            {
                Debug.LogError(
                    $"[Spawn] '{spawned.name}' has no PlayerAvatar, so it cannot " +
                    "carry a seat and will be missing from the room list.");
                return;
            }

            avatar.Seat = seat;
            avatar.IsHost = isHost;
            avatar.Nickname = nickname ?? string.Empty;
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
