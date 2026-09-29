using Game.Core.Settings;

namespace Game.Core.Ports
{
    /// <summary>
    /// Makes the 그래픽 settings actually change the picture.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="IGraphicsSettingsStore"/> because the two are
    /// different jobs at different times: the store answers what the player
    /// last chose, and this carries a choice into the renderer. Kept behind a
    /// port so the screen's rules can be tested without a graphics device, and
    /// so everything that has to know about URP lives in one place.
    /// </remarks>
    public interface IGraphicsSettingsApplier
    {
        void Apply(GraphicsSettings settings);
    }
}
