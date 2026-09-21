using System;
using System.Collections.Generic;
using Game.Client.Home;
using Game.Client.Settings;
using Game.Core.Settings;

namespace Game.Architecture.Tests
{
    /// <summary>
    /// The settings screen as its tests need it: what was drawn, and a way to
    /// press what a player would press.
    /// </summary>
    /// <remarks>
    /// Shared by <see cref="SettingsPresenterTests"/> and
    /// <see cref="SettingsFeedbackBridgeTests"/>. Two fakes of one screen drift
    /// apart, and the day they do, one of the two suites is testing a screen
    /// that does not exist.
    /// </remarks>
    internal sealed class FakeSettingsView : ISettingsView
    {
        public event Action Opened;

        public event Action BackRequested;

        public event Action ResetAllRequested;

        public event Action<SettingsTab> TabSelected;

        public event Action<int> LanguageStepRequested;

        public event Action<GraphicsOption, int> GraphicsStepRequested;

        public event Action<InterfaceOption, int> InterfaceStepRequested;

        public event Action<NotificationOption, int> NotificationStepRequested;

        public event Action<SoundVolume, int> VolumeChanged;

        public event Action<int> MicrophoneDeviceStepRequested;

        public event Action<int> InputModeStepRequested;

        public event Action MicrophoneTestToggled;

        public event Action<ControlAction> RebindRequested;

        public event Action<ControlSensitivity, int> SensitivityChanged;

        public event Action<ControlToggle, int> ReversalStepRequested;

        public event Action FeedbackRequested;

        public event Action<string> FeedbackEdited;

        public event Action<string> FeedbackSubmitted;

        public event Action FeedbackDismissed;

        public event Action ResetRequested;

        public event Action ApplyRequested;

        public event Action LeaveGameRequested;

        public event Action ConfirmAccepted;

        public event Action ConfirmDeclined;

        public event Action ConfirmDismissed;

        public SettingsTab ShownTab { get; private set; }

        public string LanguageLabel { get; private set; }

        public Dictionary<GraphicsOption, string> GraphicsLabels { get; } =
            new Dictionary<GraphicsOption, string>();

        public Dictionary<InterfaceOption, string> InterfaceLabels { get; } =
            new Dictionary<InterfaceOption, string>();

        public Dictionary<NotificationOption, string> NotificationLabels { get; } =
            new Dictionary<NotificationOption, string>();

        public Dictionary<SoundVolume, int> Volumes { get; } = new Dictionary<SoundVolume, int>();

        public string DeviceLabel { get; private set; }

        public bool DeviceCanStep { get; private set; }

        public string InputModeLabel { get; private set; }

        public bool TestRunning { get; private set; }

        public Dictionary<ControlAction, string> Bindings { get; } =
            new Dictionary<ControlAction, string>();

        public Dictionary<ControlSensitivity, int> Sensitivities { get; } =
            new Dictionary<ControlSensitivity, int>();

        public Dictionary<ControlToggle, string> Reversals { get; } =
            new Dictionary<ControlToggle, string>();

        /// <summary>The row whose plate is lit, if any.</summary>
        public ControlAction? Listening { get; private set; }

        /// <summary>Whether the screen has stopped taking clicks.</summary>
        public bool ClicksBlocked { get; private set; }

        public bool CanStep { get; private set; }

        public bool ActionsEnabled { get; private set; }

        public bool ConfirmVisible { get; private set; }

        public SettingsConfirmKind ConfirmKind { get; private set; }

        public SettingsTab ConfirmTab { get; private set; }

        public bool FeedbackVisible { get; private set; }

        public bool SubmitEnabled { get; private set; }

        public List<string> Notices { get; } = new List<string>();

        public UiLocale Chrome { get; private set; }

        public void ShowTab(SettingsTab tab) => ShownTab = tab;

        public void ShowChrome(UiLocale locale) => Chrome = locale;

        public void ShowLanguage(string label, bool canStep)
        {
            LanguageLabel = label;
            CanStep = canStep;
        }

        public void ShowGraphics(GraphicsOption option, string label, bool canStep)
        {
            GraphicsLabels[option] = label;
        }

        public void ShowInterface(InterfaceOption option, string label, bool canStep)
        {
            InterfaceLabels[option] = label;
        }

        public void ShowNotification(NotificationOption option, string label, bool canStep)
        {
            NotificationLabels[option] = label;
        }

        public void ShowVolume(SoundVolume volume, int percent) => Volumes[volume] = percent;

        public void ShowMicrophoneDevice(string label, bool canStep)
        {
            DeviceLabel = label;
            DeviceCanStep = canStep;
        }

        public void ShowInputMode(string label, bool canStep) => InputModeLabel = label;

        public void ShowMicrophoneTest(bool running) => TestRunning = running;

        public void ShowBinding(ControlAction action, string label) => Bindings[action] = label;

        public void ShowRebinding(ControlAction? listening)
        {
            Listening = listening;
            ClicksBlocked = listening.HasValue;
        }

        public void ShowSensitivity(ControlSensitivity sensitivity, int percent) =>
            Sensitivities[sensitivity] = percent;

        public void ShowReversal(ControlToggle toggle, string label, bool canStep) =>
            Reversals[toggle] = label;

        public void SetActionsEnabled(bool enabled) => ActionsEnabled = enabled;

        public void ShowConfirm(SettingsConfirmKind kind, SettingsTab tab)
        {
            ConfirmVisible = true;
            ConfirmKind = kind;
            ConfirmTab = tab;
        }

        public void HideConfirm() => ConfirmVisible = false;

        public void ShowFeedback() => FeedbackVisible = true;

        public void HideFeedback() => FeedbackVisible = false;

        public void SetFeedbackSubmitEnabled(bool enabled) => SubmitEnabled = enabled;

        /// <summary>How many times a send was reported as having gone through.</summary>
        /// <remarks>
        /// The bridge tests assert on this rather than on
        /// <see cref="FeedbackVisible"/>: those tests run without a presenter, so
        /// nothing ever opens the panel and its visibility says nothing.
        /// </remarks>
        public int SentCalls { get; private set; }

        /// <summary>
        /// What the real view does: down, and the same event as a panel
        /// closed by hand so the presenter stops believing it is up.
        /// </summary>
        public void FeedbackSent()
        {
            SentCalls++;
            FeedbackVisible = false;
            FeedbackDismissed?.Invoke();
        }

        public void ShowNotice(string title, string message) => Notices.Add(message);

        public void Back() => BackRequested?.Invoke();

        public void ResetAll() => ResetAllRequested?.Invoke();

        public void SelectTab(SettingsTab tab) => TabSelected?.Invoke(tab);

        public void StepLanguage(int steps) => LanguageStepRequested?.Invoke(steps);

        public void StepGraphics(GraphicsOption option, int steps) =>
            GraphicsStepRequested?.Invoke(option, steps);

        public void StepInterface(InterfaceOption option, int steps) =>
            InterfaceStepRequested?.Invoke(option, steps);

        public void StepNotification(NotificationOption option, int steps) =>
            NotificationStepRequested?.Invoke(option, steps);

        public void DragVolume(SoundVolume volume, int percent) => VolumeChanged?.Invoke(volume, percent);

        public void StepDevice(int steps) => MicrophoneDeviceStepRequested?.Invoke(steps);

        public void StepInputMode(int steps) => InputModeStepRequested?.Invoke(steps);

        public void ToggleTest() => MicrophoneTestToggled?.Invoke();

        public void ClickKey(ControlAction action) => RebindRequested?.Invoke(action);

        public void DragSensitivity(ControlSensitivity sensitivity, int percent) =>
            SensitivityChanged?.Invoke(sensitivity, percent);

        public void StepReversal(ControlToggle toggle, int steps) =>
            ReversalStepRequested?.Invoke(toggle, steps);

        public void Feedback() => FeedbackRequested?.Invoke();

        public void TypeFeedback(string message) => FeedbackEdited?.Invoke(message);

        public void SubmitFeedback(string message) => FeedbackSubmitted?.Invoke(message);

        public void DismissFeedback() => FeedbackDismissed?.Invoke();

        public void Reset() => ResetRequested?.Invoke();

        public void Apply() => ApplyRequested?.Invoke();

        public void LeaveGame() => LeaveGameRequested?.Invoke();

        public void Reopen() => Opened?.Invoke();

        public void Accept() => ConfirmAccepted?.Invoke();

        public void Decline() => ConfirmDeclined?.Invoke();

        public void Dismiss() => ConfirmDismissed?.Invoke();
    }

    /// <summary>
    /// Where 나가기 goes, as far as these tests need to know.
    /// </summary>
    /// <remarks>
    /// Moved out beside the view fake when the view fake was shared. It counts
    /// what the settings screen asked for and does none of it.
    /// </remarks>
    internal sealed class FakeApplicationHost : IHomeApplicationHost
    {
        public int HomeOpenCount { get; private set; }

        public void Quit()
        {
        }

        public void OpenHome() => HomeOpenCount++;

        public void OpenRoomBrowser()
        {
        }

        public void OpenCharacterCloset()
        {
        }

        public void OpenSettings()
        {
        }

        public void CreateRoom(string title, bool isPublic, int maxPlayers)
        {
        }

        public void JoinRoom(string roomCode)
        {
        }

        public void OpenLobby()
        {
        }
    }
}
