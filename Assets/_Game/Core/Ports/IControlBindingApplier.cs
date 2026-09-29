using Game.Core.Settings;

namespace Game.Core.Ports
{
    /// <summary>
    /// Makes the keys chosen in the 컨트롤 tab the keys the game listens to.
    /// </summary>
    /// <remarks>
    /// The same division as <see cref="IGraphicsSettingsApplier"/>: the store
    /// answers what the player chose, and this carries a choice into the input
    /// asset. Behind a port so the screen's rules can be tested without the
    /// Input System, and so everything that knows how a binding is spelled
    /// lives in one place.
    /// <para>
    /// <see cref="Apply"/> writes every row every time rather than the ones
    /// that changed. A binding override outlives play mode in the editor, so a
    /// set applied piecemeal drifts; writing all of them makes the asset say
    /// exactly what the settings say, whatever it said before.
    /// </para>
    /// </remarks>
    public interface IControlBindingApplier
    {
        void Apply(ControlSettings settings);
    }
}
