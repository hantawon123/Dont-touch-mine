using Game.Core.Settings;

namespace Game.Client.Controls
{
    public interface IControlSettingsApplier
    {
        void Apply(ControlSettingsState settings);

        void StartRebind(
            ControlAction action,
            System.Action<string> completed,
            System.Action cancelled);

        void CancelRebind();
    }
}
