using System;
using Game.Core.Settings;

namespace Game.Client.Controls
{
    public interface IControlSettings
    {
        ControlSettingsState Current { get; }

        ControlAction? ListeningAction { get; }

        event Action<ControlSettingsState> Changed;

        event Action<ControlAction?> RebindListeningChanged;

        event Action<ControlAction> BindingConflict;

        bool TrySetPath(ControlAction action, string path, out ControlSettingsError error);

        bool TryStartRebind(ControlAction action, out ControlSettingsError error);

        void CancelRebind();
    }
}
