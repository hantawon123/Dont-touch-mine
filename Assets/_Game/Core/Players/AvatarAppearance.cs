using System;

namespace Game.Core.Players
{
    /// <summary>
    /// What a character is wearing: one part id per category, or none.
    /// </summary>
    /// <remarks>
    /// A value rather than a mutable object because the closet keeps two of
    /// these side by side — what was applied and what is being tried on — and
    /// compares them to decide whether anything changed. Sharing one instance
    /// between the two would make that comparison always agree.
    /// <para>
    /// Ids are the catalogue's, not the art's: nothing here knows what a
    /// texture or a mesh is, so the same value survives an art pass and can be
    /// stored by the account or sent over the wire.
    /// </para>
    /// </remarks>
    public readonly struct AvatarAppearance : IEquatable<AvatarAppearance>
    {
        /// <summary>
        /// Nothing worn in that category. Also what an unknown id becomes, so a
        /// part that leaves the catalogue leaves the character rather than
        /// leaving it half-dressed in something that no longer exists.
        /// </summary>
        public const string NoPart = "";

        /// <summary>
        /// Every category empty, which wears whatever the model was authored
        /// with. The catalogue, not this, decides what a new player starts in.
        /// </summary>
        public static readonly AvatarAppearance Default = default;

        private readonly string bodyColorId;
        private readonly string hoodId;
        private readonly string shoesId;
        private readonly string faceId;

        public AvatarAppearance(
            string bodyColorId, string hoodId, string shoesId, string faceId)
        {
            this.bodyColorId = Normalise(bodyColorId);
            this.hoodId = Normalise(hoodId);
            this.shoesId = Normalise(shoesId);
            this.faceId = Normalise(faceId);
        }

        // Read through fields rather than as auto-properties, so the default
        // value answers with empty strings too. A struct's default skips every
        // constructor, and one whose properties hand back nulls while its own
        // comparisons treat null as empty is a value that disagrees with
        // itself — which is exactly what it did.
        public string BodyColorId => Normalise(bodyColorId);

        public string HoodId => Normalise(hoodId);
        public string HoodShapeId => AvatarHoodSelection.Shape(HoodId);
        public string HoodColorId => AvatarHoodSelection.Color(HoodId);

        public string ShoesId => Normalise(shoesId);

        public string FaceId => Normalise(faceId);

        public static bool operator ==(AvatarAppearance left, AvatarAppearance right) =>
            left.Equals(right);

        public static bool operator !=(AvatarAppearance left, AvatarAppearance right) =>
            !left.Equals(right);

        /// <summary>What is worn in one category.</summary>
        public string Get(AvatarPartCategory category)
        {
            switch (category)
            {
                case AvatarPartCategory.BodyColor:
                    return BodyColorId;
                case AvatarPartCategory.Hood:
                    return HoodShapeId;
                case AvatarPartCategory.HoodColor:
                    return HoodColorId;
                case AvatarPartCategory.Shoes:
                    return ShoesId;
                case AvatarPartCategory.Face:
                    return FaceId;
                default:
                    throw new ArgumentOutOfRangeException(nameof(category));
            }
        }

        /// <summary>
        /// The same appearance with one category changed. Returns a new value;
        /// this one is left alone.
        /// </summary>
        public AvatarAppearance With(AvatarPartCategory category, string partId)
        {
            switch (category)
            {
                case AvatarPartCategory.BodyColor:
                    return new AvatarAppearance(partId, HoodId, ShoesId, FaceId);
                case AvatarPartCategory.Hood:
                    return new AvatarAppearance(BodyColorId,
                        AvatarHoodSelection.IsShape(partId)
                            ? AvatarHoodSelection.Compose(partId, HoodColorId)
                            : partId, ShoesId, FaceId);
                case AvatarPartCategory.HoodColor:
                    return new AvatarAppearance(BodyColorId,
                        AvatarHoodSelection.Compose(HoodShapeId, partId), ShoesId, FaceId);
                case AvatarPartCategory.Shoes:
                    return new AvatarAppearance(BodyColorId, HoodId, partId, FaceId);
                case AvatarPartCategory.Face:
                    return new AvatarAppearance(BodyColorId, HoodId, ShoesId, partId);
                default:
                    throw new ArgumentOutOfRangeException(nameof(category));
            }
        }

        public bool Equals(AvatarAppearance other) =>
            string.Equals(BodyColorId, other.BodyColorId, StringComparison.Ordinal) &&
            string.Equals(HoodId, other.HoodId, StringComparison.Ordinal) &&
            string.Equals(ShoesId, other.ShoesId, StringComparison.Ordinal) &&
            string.Equals(FaceId, other.FaceId, StringComparison.Ordinal);

        public override bool Equals(object obj) =>
            obj is AvatarAppearance other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + BodyColorId.GetHashCode();
                hash = (hash * 31) + HoodId.GetHashCode();
                hash = (hash * 31) + ShoesId.GetHashCode();
                hash = (hash * 31) + FaceId.GetHashCode();
                return hash;
            }
        }

        public override string ToString() =>
            $"body={Describe(BodyColorId)}, hood={Describe(HoodId)}, " +
            $"shoes={Describe(ShoesId)}, face={Describe(FaceId)}";

        /// <summary>
        /// A default value carries nulls rather than empty strings, and a store
        /// that round-trips one may hand back either. Both mean the same thing,
        /// so they are made the same thing on the way in and on the way out.
        /// </summary>
        private static string Normalise(string partId) => partId ?? NoPart;

        private static string Describe(string partId) =>
            string.IsNullOrEmpty(partId) ? "none" : partId;
    }
}
