using System;
using Game.Core.Settings;

namespace Game.Client.Settings
{
    /// <summary>
    /// The six tabs down the left of the settings screen, in the order they
    /// are drawn.
    /// </summary>
    public enum SettingsTab
    {
        General,
        Graphics,
        Interface,
        Sound,
        Controls,
        Notifications
    }

    /// <summary>
        /// Which confirmation is being asked for. They share one panel and
        /// differ in their words and in what the two buttons do, so the presenter
        /// has to know which one it put up.
    /// </summary>
    public enum SettingsConfirmKind
    {
        /// <summary>초기화 under the panel: this tab back to its defaults.</summary>
        ResetTab,

        /// <summary>전체설정 초기화 at the top right: every tab back to its defaults.</summary>
        ResetAll,

        /// <summary>
        /// ← 이전 with changes not yet applied. Its right button saves and
        /// leaves, its left button leaves without saving, and its X stays.
        /// </summary>
        Discard,

        /// <summary>Lobby overlay 게임 나가기: leave the room after asking.</summary>
        LeaveGame
    }

    /// <summary>
    /// The settings screen, as the presenter needs it: what it can be asked
    /// to draw and what the player can do on it.
    /// </summary>
    public interface ISettingsView
    {
        /// <summary>The screen became visible, including a cached scene reopening.</summary>
        event Action Opened;

        /// <summary>The arrow at the top left.</summary>
        event Action BackRequested;

        /// <summary>전체설정 초기화 at the top right.</summary>
        event Action ResetAllRequested;

        event Action<SettingsTab> TabSelected;

        /// <summary>
        /// One of the language picker's arrows: -1 for the left, +1 for the
        /// right.
        /// </summary>
        event Action<int> LanguageStepRequested;

        /// <summary>
        /// One of a 그래픽 row's arrows, carrying the row it belongs to and
        /// which way it points.
        /// </summary>
        event Action<GraphicsOption, int> GraphicsStepRequested;

        /// <inheritdoc cref="GraphicsStepRequested"/>
        event Action<InterfaceOption, int> InterfaceStepRequested;

        /// <inheritdoc cref="GraphicsStepRequested"/>
        event Action<NotificationOption, int> NotificationStepRequested;

        /// <summary>A volume slider, wherever it was dragged to, in percent.</summary>
        event Action<SoundVolume, int> VolumeChanged;

        /// <summary>One of the microphone picker's arrows.</summary>
        event Action<int> MicrophoneDeviceStepRequested;

        /// <summary>One of the input mode picker's arrows.</summary>
        event Action<int> InputModeStepRequested;

        /// <summary>마이크 테스트, which starts a test or stops the one running.</summary>
        event Action MicrophoneTestToggled;

        /// <summary>A key plate, which asks to be put on something else.</summary>
        event Action<ControlAction> RebindRequested;

        /// <summary>A 감도 slider, wherever it was dragged to.</summary>
        event Action<ControlSensitivity, int> SensitivityChanged;

        /// <summary>One of a 반전 row's arrows.</summary>
        event Action<ControlToggle, int> ReversalStepRequested;

        /// <summary>피드백 보내기 on the row, which opens the writing panel.</summary>
        event Action FeedbackRequested;

        /// <summary>What is in the writing box, as it is typed.</summary>
        event Action<string> FeedbackEdited;

        /// <summary>Its 보내기, carrying what was written.</summary>
        event Action<string> FeedbackSubmitted;

        /// <summary>Its 취소, its X, or Escape.</summary>
        event Action FeedbackDismissed;

        /// <summary>초기화 under the panel.</summary>
        event Action ResetRequested;

        event Action ApplyRequested;

        /// <summary>Lobby overlay only: 게임 나가기 at the top left.</summary>
        event Action LeaveGameRequested;

        /// <summary>The right, orange button of whichever confirmation is up.</summary>
        event Action ConfirmAccepted;

        /// <summary>Its left, grey button.</summary>
        event Action ConfirmDeclined;

        /// <summary>Its X, or Escape.</summary>
        event Action ConfirmDismissed;

        /// <summary>Marks a tab as the one being looked at and shows its contents.</summary>
        void ShowTab(SettingsTab tab);

        /// <summary>
        /// Draws the chrome — tabs, the language row's name, and the two
        /// buttons — in the applied language. Called when the screen opens
        /// and again when that language actually moves.
        /// </summary>
        void ShowChrome(UiLocale locale);

        /// <summary>
        /// Puts a language's name in the picker. <paramref name="canStep"/>
        /// false draws the arrows as unavailable, for a catalogue with nowhere
        /// else to go.
        /// </summary>
        void ShowLanguage(string label, bool canStep);

        /// <summary>Puts a chosen value in one 그래픽 row's picker.</summary>
        void ShowGraphics(GraphicsOption option, string label, bool canStep);

        /// <inheritdoc cref="ShowGraphics"/>
        void ShowInterface(InterfaceOption option, string label, bool canStep);

        /// <inheritdoc cref="ShowGraphics"/>
        void ShowNotification(NotificationOption option, string label, bool canStep);

        /// <summary>Moves one volume's handle and the figure beside it.</summary>
        void ShowVolume(SoundVolume volume, int percent);

        void ShowMicrophoneDevice(string label, bool canStep);

        void ShowInputMode(string label, bool canStep);

        /// <summary>Paints 마이크 테스트 for whether a test is running.</summary>
        void ShowMicrophoneTest(bool running);

        /// <summary>Puts the name of a key on one action's plate.</summary>
        void ShowBinding(ControlAction action, string label);

        /// <summary>
        /// Says which plate is waiting for a press, or none.
        /// </summary>
        /// <remarks>
        /// One at a time, so one call rather than a flag on every plate. While
        /// a plate is waiting the screen stops taking clicks, because the press
        /// being waited for may well be a mouse button: without that, choosing
        /// 좌클릭 would also press whatever the pointer happened to be over.
        /// </remarks>
        void ShowRebinding(ControlAction? listening);

        void ShowSensitivity(ControlSensitivity sensitivity, int percent);

        void ShowReversal(ControlToggle toggle, string label, bool canStep);

        /// <summary>
        /// Turns 초기화 and 적용하기 on or off, which is the screen's whole
        /// account of whether anything has been changed.
        /// </summary>
        void SetActionsEnabled(bool enabled);

        /// <param name="tab">
        /// The tab a <see cref="SettingsConfirmKind.ResetTab"/> question names.
        /// Ignored by the other two.
        /// </param>
        void ShowConfirm(SettingsConfirmKind kind, SettingsTab tab);

        void HideConfirm();

        /// <summary>
        /// Opens the writing panel, empty. Closing it is what clears it, so
        /// what was typed cannot come back on a later visit.
        /// </summary>
        void ShowFeedback();

        void HideFeedback();

        /// <summary>
        /// The send went through: the panel comes down and listeners hear the
        /// same <see cref="FeedbackDismissed"/> as a panel closed by hand.
        /// </summary>
        /// <remarks>
        /// One method rather than letting the sender call
        /// <see cref="HideFeedback"/> itself. Hiding without the event leaves
        /// the presenter believing the panel is still up, and while it believes
        /// that, Escape closes a panel that is not there instead of the screen.
        /// <para>
        /// The box is not emptied here. <see cref="ShowFeedback"/> empties on
        /// the way in, so the next visit starts blank whatever brought this one
        /// down.
        /// </para>
        /// </remarks>
        void FeedbackSent();

        /// <summary>
        /// Turns 보내기 on or off, which is the panel's whole account of
        /// whether there is anything worth sending.
        /// </summary>
        void SetFeedbackSubmitEnabled(bool enabled);

        /// <summary>
        /// A passing message over the screen. Failures keep the warm plate;
        /// a success is the same plate in green, with a check beside the title.
        /// </summary>
        void ShowNotice(string title, string message, bool success = false);
    }
}
