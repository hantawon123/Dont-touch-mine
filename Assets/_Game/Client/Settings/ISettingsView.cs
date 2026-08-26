using System;
using Game.Core.Settings;

namespace Game.Client.Settings
{
    public interface ISettingsView
    {
        event Action BackRequested;

        event Action<SettingsTab> TabSelected;

        void SetActiveTab(SettingsTab tab);
    }
}
