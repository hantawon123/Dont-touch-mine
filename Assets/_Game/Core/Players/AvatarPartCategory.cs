namespace Game.Core.Players
{
    /// <summary>
    /// The rows the closet offers, in the order the design lists them.
    /// </summary>
    /// <remarks>
    /// <see cref="Shoes"/> and <see cref="Face"/> are drawn and picked like the
    /// other two but nothing wears them yet: the models have no swappable shoe
    /// and the face is still one baked material. They are here so the screen is
    /// laid out for what is coming rather than rebuilt around it later.
    /// </remarks>
    public enum AvatarPartCategory
    {
        BodyColor,
        Hood,
        Shoes,
        Face,
        HoodColor
    }
}
