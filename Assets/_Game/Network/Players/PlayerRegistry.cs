using System.Collections.Generic;
using Fusion;
using Game.Core.Lobby;
using Game.Core.Players;

namespace Game.Network.Players
{
    /// <summary>
    /// Maps between the three ways a player is named: Fusion's
    /// <see cref="PlayerRef"/>, the seat number the match rules use, and the
    /// neutral id that leaves this layer.
    /// </summary>
    /// <remarks>
    /// Seats are handed out in join order and reused once freed, so they stay
    /// inside 0..maxPlayers-1 and can index a spawn point directly. A player who
    /// leaves the room before the match starts gives their seat back; once the
    /// match starts the roster is frozen and seats must not move, which is why
    /// nothing here renumbers a seat that is already taken.
    /// <para>
    /// Only the peer that spawns keeps this. Fusion raises join and leave
    /// callbacks on every peer, but the order a late client sees them in is not
    /// the order the host saw, so a client-side copy would disagree about who
    /// sits where. Replicating the authoritative mapping is a later step.
    /// </para>
    /// </remarks>
    public sealed class PlayerRegistry
    {
        /// <summary>Empty slots hold <see cref="PlayerRef.None"/>.</summary>
        private readonly List<PlayerRef> _seats = new List<PlayerRef>();

        private readonly Dictionary<PlayerRef, int> _seatByPlayer =
            new Dictionary<PlayerRef, int>();

        private readonly Dictionary<string, int> _seatByBot =
            new Dictionary<string, int>(System.StringComparer.Ordinal);

        private readonly Dictionary<int, string> _botBySeat =
            new Dictionary<int, string>();

        public int Count => _seatByPlayer.Count + _seatByBot.Count;

        /// <summary>
        /// Seats a player, or returns the seat they already hold. Takes the
        /// lowest free seat so the numbers stay dense after someone leaves.
        /// </summary>
        public int Add(PlayerRef player)
        {
            if (_seatByPlayer.TryGetValue(player, out var seated))
            {
                return seated;
            }

            var seat = FindLowestFreeSeat();

            if (seat < 0)
            {
                _seats.Add(player);
                seat = _seats.Count - 1;
            }
            else
            {
                _seats[seat] = player;
            }

            _seatByPlayer[player] = seat;
            return seat;
        }

        /// <summary>
        /// Seats a bot without inventing a Fusion player connection.
        /// </summary>
        public bool TryAddBot(BotProfile profile, out int seat)
        {
            if (_seatByBot.TryGetValue(profile.PlayerId, out seat))
            {
                return true;
            }

            if (!BotProfile.IsBotPlayerId(profile.PlayerId) ||
                Count >= RoomSettings.MaxPlayerCount)
            {
                seat = -1;
                return false;
            }

            seat = FindLowestFreeSeat();

            if (seat < 0)
            {
                _seats.Add(PlayerRef.None);
                seat = _seats.Count - 1;
            }

            _seatByBot.Add(profile.PlayerId, seat);
            _botBySeat.Add(seat, profile.PlayerId);
            return true;
        }

        /// <summary>
        /// Restores the exact seat recorded in a host-migration snapshot.
        /// Unlike <see cref="Add"/>, this never chooses a different free seat.
        /// </summary>
        public bool Restore(PlayerRef player, int seat)
        {
            if (!player.IsRealPlayer ||
                seat < 0 || seat >= RoomSettings.MaxPlayerCount)
            {
                return false;
            }

            if (_seatByPlayer.TryGetValue(player, out var currentSeat))
            {
                return currentSeat == seat;
            }

            while (_seats.Count <= seat)
            {
                _seats.Add(PlayerRef.None);
            }

            if (_seats[seat] != PlayerRef.None || _botBySeat.ContainsKey(seat))
            {
                return false;
            }

            _seats[seat] = player;
            _seatByPlayer.Add(player, seat);
            return true;
        }

        /// <summary>Frees a player's seat. False if they held none.</summary>
        public bool Remove(PlayerRef player)
        {
            if (!_seatByPlayer.TryGetValue(player, out var seat))
            {
                return false;
            }

            _seatByPlayer.Remove(player);
            _seats[seat] = PlayerRef.None;

            // A trailing PlayerRef.None may still be a bot seat. Only remove
            // slots that are empty in both maps.
            TrimTrailingEmptySeats();

            return true;
        }

        /// <summary>Frees a bot seat. False when the id is not seated.</summary>
        public bool RemoveBot(string botPlayerId)
        {
            if (string.IsNullOrWhiteSpace(botPlayerId) ||
                !_seatByBot.TryGetValue(botPlayerId, out var seat))
            {
                return false;
            }

            _seatByBot.Remove(botPlayerId);
            _botBySeat.Remove(seat);
            TrimTrailingEmptySeats();
            return true;
        }

        public bool TryGetSeat(PlayerRef player, out int seat)
        {
            return _seatByPlayer.TryGetValue(player, out seat);
        }

        public bool TryGetPlayer(int seat, out PlayerRef player)
        {
            if (seat < 0 || seat >= _seats.Count || _seats[seat] == PlayerRef.None)
            {
                player = PlayerRef.None;
                return false;
            }

            player = _seats[seat];
            return true;
        }

        public bool TryGetBotId(int seat, out string botPlayerId)
        {
            return _botBySeat.TryGetValue(seat, out botPlayerId);
        }

        /// <summary>
        /// The id other layers use. Derived from <see cref="PlayerRef"/> rather
        /// than stored, so it is the same on every peer without being sent.
        /// </summary>
        /// <remarks>
        /// Unique within a room only. Recognising the same person across rooms
        /// or reconnects needs an account id, which replaces this once Steam is
        /// connected.
        /// </remarks>
        public static string IdOf(PlayerRef player)
        {
            return player.IsRealPlayer ? "P" + player.PlayerId : null;
        }

        public void Clear()
        {
            _seats.Clear();
            _seatByPlayer.Clear();
            _seatByBot.Clear();
            _botBySeat.Clear();
        }

        private int FindLowestFreeSeat()
        {
            for (var seat = 0; seat < _seats.Count; seat++)
            {
                if (_seats[seat] == PlayerRef.None && !_botBySeat.ContainsKey(seat))
                {
                    return seat;
                }
            }

            return -1;
        }

        private void TrimTrailingEmptySeats()
        {
            while (_seats.Count > 0)
            {
                var last = _seats.Count - 1;
                if (_seats[last] != PlayerRef.None || _botBySeat.ContainsKey(last))
                {
                    break;
                }

                _seats.RemoveAt(last);
            }
        }
    }
}
