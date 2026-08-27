using Game.Core.Settings;

namespace Game.Client.Graphics
{
    public interface IGraphicsSettingsStore
    {
        GraphicsSettingsState LoadOrDefault();

        void Save(GraphicsSettingsState settings);
    }
}
