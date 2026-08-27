using Game.Core.Settings;

namespace Game.Client.Controls
{
    public interface IControlSettingsStore
    {
        ControlSettingsState LoadOrDefault();

        void Save(ControlSettingsState settings);
    }
}
