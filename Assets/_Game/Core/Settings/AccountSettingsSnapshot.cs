using System;

namespace Game.Core.Settings
{
    /// <summary>Versioned account preferences. Graphics/resolution remain in their existing local store.</summary>
    [Serializable]
    public sealed class AccountSettingsSnapshot
    {
        public int schemaVersion = 1;
        public string languageCode;
        public string[] bindings, toggles, interfaceOptions, notifications;
        public int[] sensitivities, volumes;
        public string deviceName, inputMode;

        public bool IsValid => schemaVersion == 1 && !string.IsNullOrEmpty(languageCode)
            && bindings?.Length == Enum.GetValues(typeof(ControlAction)).Length
            && toggles?.Length == Enum.GetValues(typeof(ControlToggle)).Length
            && sensitivities?.Length == Enum.GetValues(typeof(ControlSensitivity)).Length
            && interfaceOptions?.Length == Enum.GetValues(typeof(InterfaceOption)).Length
            && notifications?.Length == Enum.GetValues(typeof(NotificationOption)).Length
            && volumes?.Length == Enum.GetValues(typeof(SoundVolume)).Length;

        public static AccountSettingsSnapshot Capture(GeneralSettingsSystem general, ControlSettingsSystem control,
            InterfaceSettingsSystem ui, SoundSettingsSystem sound, NotificationSettingsSystem notification) => new()
        {
            languageCode = general.Current.LanguageCode,
            bindings = Read<ControlAction, string>(control.Current.Get),
            toggles = Read<ControlToggle, string>(control.Current.Get),
            sensitivities = Read<ControlSensitivity, int>(control.Current.Get),
            interfaceOptions = Read<InterfaceOption, string>(ui.Current.Get),
            notifications = Read<NotificationOption, string>(notification.Current.Get),
            volumes = Read<SoundVolume, int>(sound.Current.Get),
            deviceName = sound.Current.DeviceName, inputMode = sound.Current.InputMode
        };

        // Bits: general, control, interface, sound, notifications. Dirty local sections win a late read/conflict.
        public void Apply(GeneralSettingsSystem general, ControlSettingsSystem control,
            InterfaceSettingsSystem ui, SoundSettingsSystem sound, NotificationSettingsSystem notification,
            int preserveMask = 0)
        {
            if (!IsValid) return;
            if ((preserveMask & 1) == 0) general.Apply(new GeneralSettings(languageCode));
            if ((preserveMask & 2) == 0)
            {
                var next = ControlSettings.Empty;
                for (int i = 0; i < bindings.Length; i++) next = next.With((ControlAction)i, bindings[i]);
                for (int i = 0; i < toggles.Length; i++) next = next.With((ControlToggle)i, toggles[i]);
                for (int i = 0; i < sensitivities.Length; i++) next = next.With((ControlSensitivity)i, sensitivities[i]);
                control.Apply(next);
            }
            if ((preserveMask & 4) == 0)
            {
                var next = InterfaceSettings.Empty;
                for (int i = 0; i < interfaceOptions.Length; i++) next = next.With((InterfaceOption)i, interfaceOptions[i]);
                ui.Apply(next);
            }
            if ((preserveMask & 8) == 0)
            {
                var next = SoundSettings.Empty.WithDevice(deviceName).WithInputMode(inputMode);
                for (int i = 0; i < volumes.Length; i++) next = next.With((SoundVolume)i, volumes[i]);
                sound.Apply(next);
            }
            if ((preserveMask & 16) == 0)
            {
                var next = NotificationSettings.Empty;
                for (int i = 0; i < notifications.Length; i++) next = next.With((NotificationOption)i, notifications[i]);
                notification.Apply(next);
            }
        }

        private static T[] Read<E, T>(Func<E, T> get) where E : Enum
        {
            var values = (E[])Enum.GetValues(typeof(E));
            var result = new T[values.Length];
            for (int i = 0; i < values.Length; i++) result[i] = get(values[i]);
            return result;
        }
    }
}
