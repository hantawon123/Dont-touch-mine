using Game.Core.Settings;

namespace Game.Client.Graphics
{
    public interface IGraphicsSettingsApplier
    {
        void Apply(GraphicsSettingsState settings);
    }
}
