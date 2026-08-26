using System.Collections.Generic;
using Fusion;
using Game.Core.Lobby;

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

        public int Count => _seatByPlayer.Count;

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

            var seat = _seats.IndexOf(PlayerRef.None);

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

            if (_seats[seat] != PlayerRef.None)
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

            // Trailing empties would keep the next Add pushing the list longer
            // than the number of players, so they are dropped.
            while (_seats.Count > 0 && _seats[_seats.Count - 1] == PlayerRef.None)
            {
                _seats.RemoveAt(_seats.Count - 1);
            }

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
        }
    }
}
