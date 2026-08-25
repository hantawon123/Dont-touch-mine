using System;

namespace Game.Core.Rooms
{
    /// <summary>
    /// Opaque handle to a room in the browser list.
    /// </summary>
    /// <remarks>
    /// Deliberately opaque: only the layer that produced it knows what it maps
    /// to. Presentation code passes it back to enter a room but never displays,
    /// parses, or constructs one, so the underlying addressing scheme can change
    /// without touching the UI.
    /// </remarks>
    public readonly struct RoomId : IEquatable<RoomId>
    {
        public static readonly RoomId None = default;

        private readonly string _value;

        public RoomId(string value)
        {
            _value = value;
        }

        public bool IsValid => !string.IsNullOrEmpty(_value);

        /// <summary>
        /// For the implementing layer only. Presentation code must not read this.
        /// </summary>
        public string Value => _value;

        public bool Equals(RoomId other) => string.Equals(_value, other._value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is RoomId other && Equals(other);

        public override int GetHashCode() => _value == null ? 0 : _value.GetHashCode();

        public override string ToString() => IsValid ? "Room(...)" : "Room(None)";

        public static bool operator ==(RoomId left, RoomId right) => left.Equals(right);

        public static bool operator !=(RoomId left, RoomId right) => !left.Equals(right);
    }
}
