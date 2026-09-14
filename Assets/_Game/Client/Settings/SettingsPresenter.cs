using System;
using Game.Client.Home;
using Game.Core.Flow;
using Game.Core.Ports;
using Game.Core.Settings;
using UnityEngine;
using VContainer.Unity;

namespace Game.Client.Settings
{
    /// <summary>
    /// Keeps the settings screen's draft: what the player has changed but not
    /// yet applied.
    /// </summary>
    /// <remarks>
    /// The draft lives here and nowhere else. The picker shows it, the two
    /// buttons under the panel light up when it differs from what is applied,
    /// and <see cref="GeneralSettingsSystem"/> is left holding what was last
    /// applied — so leaving without applying is nothing more than this object
    /// going away.
    /// <para>
    /// Applied and draft are held side by side rather than as a dirty flag,
    /// because the buttons have to go dark again when a player undoes their
    /// own change by hand, and 저장하고 나가기 has to know there is something
    /// to save.
    /// </para>
    /// <para>
    /// 초기화 puts the defaults into the draft rather than into the system:
    /// the panel says the values go back to their defaults, and 적용하기 is
    /// what makes anything stick, so a reset the player thinks better of
    /// costs them nothing.
    /// </para>
    /// </remarks>
    public sealed class SettingsPresenter : IStartable, IDisposable
    {
        private readonly ISettingsView view;
        private readonly GeneralSettingsSystem general;
        private readonly GraphicsSettingsSystem graphics;
        private readonly InterfaceSettingsSystem ui;
        private readonly SoundSettingsSystem sound;
        private readonly IMicrophoneTest microphoneTest;
        private readonly ControlSettingsSystem controls;
        private readonly IKeyCapture keyCapture;
        private readonly NotificationSettingsSystem notifications;
        private readonly IHomeApplicationHost applicationHost;
        private readonly AppFlowSystem appFlow;
        private readonly Action closeSettings;

        /// <summary>
        /// Lobby overlay: the player confirmed 게임 나가기. Home never raises
        /// this; there is no leave-game control on that screen.
        /// </summary>
        public event Action LeaveGameConfirmed;

        private GeneralSettings generalDraft;
        private GeneralSettings generalApplied;
        private GraphicsSettings graphicsDraft;
        private GraphicsSettings graphicsApplied;
        private InterfaceSettings uiDraft;
        private InterfaceSettings uiApplied;
        private SoundSettings soundDraft;
        private SoundSettings soundApplied;
        private ControlSettings controlDraft;
        private ControlSettings controlApplied;
        private NotificationSettings noticeDraft;
        private NotificationSettings noticeApplied;

        /// <summary>
        /// The row whose plate is waiting for a press, if any. One at a time:
        /// the capture itself allows only one, and two plates lit at once would
        /// say otherwise.
        /// </summary>
        private ControlAction? listening;
        private SettingsTab shownTab = SettingsTab.General;
        private SettingsConfirmKind? pending;
        private bool isWritingFeedback;

        public SettingsPresenter(
            ISettingsView view,
            GeneralSettingsSystem general,
            GraphicsSettingsSystem graphics,
            InterfaceSettingsSystem ui,
            SoundSettingsSystem sound,
            IMicrophoneTest microphoneTest,
            ControlSettingsSystem controls,
            IKeyCapture keyCapture,
            NotificationSettingsSystem notifications,
            IHomeApplicationHost applicationHost,
            AppFlowSystem appFlow,
            Action closeSettings = null)
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.general = general ?? throw new ArgumentNullException(nameof(general));
            this.graphics = graphics ?? throw new ArgumentNullException(nameof(graphics));
            this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
            this.sound = sound ?? throw new ArgumentNullException(nameof(sound));
            this.microphoneTest = microphoneTest ?? throw new ArgumentNullException(nameof(microphoneTest));
            this.controls = controls ?? throw new ArgumentNullException(nameof(controls));
            this.keyCapture = keyCapture ?? throw new ArgumentNullException(nameof(keyCapture));
            this.notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
            this.applicationHost = applicationHost
                                   ?? throw new ArgumentNullException(nameof(applicationHost));
            this.appFlow = appFlow ?? throw new ArgumentNullException(nameof(appFlow));
            this.closeSettings = closeSettings;
        }

        /// <summary>What has been changed but not applied. For tests.</summary>
        public GeneralSettings GeneralDraft => generalDraft;

        /// <inheritdoc cref="GeneralDraft"/>
        public GraphicsSettings GraphicsDraft => graphicsDraft;

        /// <inheritdoc cref="GeneralDraft"/>
        public InterfaceSettings InterfaceDraft => uiDraft;

        /// <inheritdoc cref="GeneralDraft"/>
        public SoundSettings SoundDraft => soundDraft;

        /// <inheritdoc cref="GeneralDraft"/>
        public ControlSettings ControlDraft => controlDraft;

        /// <inheritdoc cref="GeneralDraft"/>
        public NotificationSettings NotificationDraft => noticeDraft;

        /// <summary>
        /// Whether there is anything to apply or to undo, on any tab. The two
        /// buttons under the panel are about the screen rather than about the
        /// tab being looked at: a language changed on 일반 is still waiting to
        /// be applied while 그래픽 is open.
        /// </summary>
        private bool IsChanged =>
            generalDraft != generalApplied
            || graphicsDraft != graphicsApplied
            || uiDraft != uiApplied
            || soundDraft != soundApplied
            || controlDraft != controlApplied
            || noticeDraft != noticeApplied;

        /// <summary>
        /// Whether a panel is over the screen. Everything behind one is deaf
        /// while it is up: the design gives no way to reach the settings
        /// underneath, and a click that got through anyway would change what
        /// the panel is asking about.
        /// </summary>
        private bool IsPanelUp => pending.HasValue || isWritingFeedback;

        public void Start()
        {
            view.Opened += Open;
            view.BackRequested += OnBackRequested;
            view.ResetAllRequested += OnResetAllRequested;
            view.TabSelected += OnTabSelected;
            view.LanguageStepRequested += OnLanguageStepRequested;
            view.GraphicsStepRequested += OnGraphicsStepRequested;
            view.InterfaceStepRequested += OnInterfaceStepRequested;
            view.NotificationStepRequested += OnNotificationStepRequested;
            view.VolumeChanged += OnVolumeChanged;
            view.MicrophoneDeviceStepRequested += OnMicrophoneDeviceStepRequested;
            view.InputModeStepRequested += OnInputModeStepRequested;
            view.MicrophoneTestToggled += OnMicrophoneTestToggled;
            view.RebindRequested += OnRebindRequested;
            view.SensitivityChanged += OnSensitivityChanged;
            view.ReversalStepRequested += OnReversalStepRequested;
            view.FeedbackRequested += OnFeedbackRequested;
            view.FeedbackEdited += OnFeedbackEdited;
            view.FeedbackSubmitted += OnFeedbackSubmitted;
            view.FeedbackDismissed += OnFeedbackDismissed;
            view.ResetRequested += OnResetRequested;
            view.ApplyRequested += OnApplyRequested;
            view.LeaveGameRequested += OnLeaveGameRequested;
            view.ConfirmAccepted += OnConfirmAccepted;
            view.ConfirmDeclined += OnConfirmDeclined;
            view.ConfirmDismissed += OnConfirmDismissed;
            general.Changed += OnGeneralApplied;
            graphics.Changed += OnGraphicsApplied;
            ui.Changed += OnInterfaceApplied;
            sound.Changed += OnSoundApplied;
            controls.Changed += OnControlsApplied;
            notifications.Changed += OnNotificationsApplied;

            Open();
        }

        /// <summary>Starts a fresh draft, including when a cached screen is reopened.</summary>
        public void Open()
        {
            StopMicrophoneTest();
            StopListening();
            pending = null;
            isWritingFeedback = false;
            generalApplied = general.Current;
            generalDraft = generalApplied;
            graphicsApplied = graphics.Current;
            graphicsDraft = graphicsApplied;
            uiApplied = ui.Current;
            uiDraft = uiApplied;
            soundApplied = sound.Current;
            soundDraft = soundApplied;
            controlApplied = controls.Current;
            controlDraft = controlApplied;
            noticeApplied = notifications.Current;
            noticeDraft = noticeApplied;
            view.HideConfirm();
            view.HideFeedback();
            view.ShowTab(shownTab);
            ShowDraft();
        }

        public void Dispose()
        {
            if (soundDraft != soundApplied) sound.ApplyToAudio();
            view.Opened -= Open;
            view.BackRequested -= OnBackRequested;
            view.ResetAllRequested -= OnResetAllRequested;
            view.TabSelected -= OnTabSelected;
            view.LanguageStepRequested -= OnLanguageStepRequested;
            view.GraphicsStepRequested -= OnGraphicsStepRequested;
            view.InterfaceStepRequested -= OnInterfaceStepRequested;
            view.NotificationStepRequested -= OnNotificationStepRequested;
            view.VolumeChanged -= OnVolumeChanged;
            view.MicrophoneDeviceStepRequested -= OnMicrophoneDeviceStepRequested;
            view.InputModeStepRequested -= OnInputModeStepRequested;
            view.MicrophoneTestToggled -= OnMicrophoneTestToggled;
            view.RebindRequested -= OnRebindRequested;
            view.SensitivityChanged -= OnSensitivityChanged;
            view.ReversalStepRequested -= OnReversalStepRequested;
            view.FeedbackRequested -= OnFeedbackRequested;
            view.FeedbackEdited -= OnFeedbackEdited;
            view.FeedbackSubmitted -= OnFeedbackSubmitted;
            view.FeedbackDismissed -= OnFeedbackDismissed;
            view.ResetRequested -= OnResetRequested;
            view.ApplyRequested -= OnApplyRequested;
            view.LeaveGameRequested -= OnLeaveGameRequested;
            view.ConfirmAccepted -= OnConfirmAccepted;
            view.ConfirmDeclined -= OnConfirmDeclined;
            view.ConfirmDismissed -= OnConfirmDismissed;
            general.Changed -= OnGeneralApplied;
            graphics.Changed -= OnGraphicsApplied;
            ui.Changed -= OnInterfaceApplied;
            sound.Changed -= OnSoundApplied;
            controls.Changed -= OnControlsApplied;
            notifications.Changed -= OnNotificationsApplied;

            // A test left running would go on listening after the screen is
            // gone, with nothing on screen to say so; a capture left waiting
            // would swallow the next key pressed anywhere.
            StopMicrophoneTest();
            StopListening();
        }

        /// <summary>
        /// Follows the applied settings while the screen is open, so the
        /// buttons agree with the system even if something else applies a
        /// change underneath it.
        /// </summary>
        private void OnGeneralApplied(GeneralSettings settled)
        {
            generalApplied = settled;
            view.SetActionsEnabled(IsChanged);
        }

        /// <inheritdoc cref="OnGeneralApplied"/>
        private void OnGraphicsApplied(GraphicsSettings settled)
        {
            graphicsApplied = settled;
            view.SetActionsEnabled(IsChanged);
        }

        /// <inheritdoc cref="OnGeneralApplied"/>
        private void OnInterfaceApplied(InterfaceSettings settled)
        {
            uiApplied = settled;
            view.SetActionsEnabled(IsChanged);
        }

        /// <inheritdoc cref="OnGeneralApplied"/>
        private void OnSoundApplied(SoundSettings settled)
        {
            soundApplied = settled;
            view.SetActionsEnabled(IsChanged);
        }

        /// <inheritdoc cref="OnGeneralApplied"/>
        private void OnControlsApplied(ControlSettings settled)
        {
            controlApplied = settled;
            view.SetActionsEnabled(IsChanged);
        }

        /// <inheritdoc cref="OnGeneralApplied"/>
        private void OnNotificationsApplied(NotificationSettings settled)
        {
            noticeApplied = settled;
            view.SetActionsEnabled(IsChanged);
        }

        private void OnTabSelected(SettingsTab tab)
        {
            if (IsPanelUp || tab == shownTab)
            {
                return;
            }

            shownTab = tab;
            view.ShowTab(tab);
        }

        /// <summary>
        /// Moves the picker at once. Nothing is saved: the draft is what the
        /// picker shows, and only 적용하기 moves it into the system.
        /// </summary>
        private void OnLanguageStepRequested(int steps)
        {
            if (IsPanelUp || steps == 0)
            {
                return;
            }

            var next = generalDraft.WithLanguage(
                general.Languages.Step(generalDraft.LanguageCode, steps).Code);
            if (next == generalDraft)
            {
                return;
            }

            generalDraft = next;
            ShowDraft();
        }

        /// <summary>
        /// Moves one 그래픽 row at once. Nothing is saved and nothing reaches
        /// the renderer: the draft is what the pickers show, and only 적용하기
        /// moves it into the system.
        /// </summary>
        private void OnGraphicsStepRequested(GraphicsOption option, int steps)
        {
            if (IsPanelUp || steps == 0)
            {
                return;
            }

            var choices = graphics.Catalog.For(option);
            var next = graphicsDraft.With(
                option, choices.Step(graphicsDraft.Get(option), steps).Code);
            if (next == graphicsDraft)
            {
                return;
            }

            graphicsDraft = next;
            ShowDraft();
        }

        /// <inheritdoc cref="OnGraphicsStepRequested"/>
        private void OnInterfaceStepRequested(InterfaceOption option, int steps)
        {
            if (IsPanelUp || steps == 0)
            {
                return;
            }

            var choices = ui.Catalog.For(option);
            var next = uiDraft.With(option, choices.Step(uiDraft.Get(option), steps).Code);
            if (next == uiDraft)
            {
                return;
            }

            uiDraft = next;
            ShowDraft();
        }

        /// <inheritdoc cref="OnGraphicsStepRequested"/>
        private void OnNotificationStepRequested(NotificationOption option, int steps)
        {
            if (IsPanelUp || steps == 0)
            {
                return;
            }

            var choices = notifications.Catalog.For(option);
            var next = noticeDraft.With(option, choices.Step(noticeDraft.Get(option), steps).Code);
            if (next == noticeDraft)
            {
                return;
            }

            noticeDraft = next;
            ShowDraft();
        }

        /// <summary>
        /// Follows a slider as it is dragged. The draft moves at once and
        /// nothing is heard differently: like every other row, a volume is
        /// settled by 적용하기. Hearing it change under the handle would be a
        /// kinder rule for this one row, and is a small change if wanted.
        /// </summary>
        private void OnVolumeChanged(SoundVolume volume, int percent)
        {
            if (IsPanelUp)
            {
                return;
            }

            var next = soundDraft.With(volume, SoundCatalog.Clamp(percent));
            if (next == soundDraft)
            {
                return;
            }

            soundDraft = next;
            sound.Preview(soundDraft);
            ShowDraft();
        }

        private void OnMicrophoneDeviceStepRequested(int steps)
        {
            if (IsPanelUp || steps == 0)
            {
                return;
            }

            var next = soundDraft.WithDevice(sound.DeviceChoices.Step(soundDraft.DeviceName, steps).Code);
            if (next == soundDraft)
            {
                return;
            }

            soundDraft = next;
            ShowDraft();
        }

        private void OnInputModeStepRequested(int steps)
        {
            if (IsPanelUp || steps == 0)
            {
                return;
            }

            var next = soundDraft.WithInputMode(
                SoundCatalog.InputModes.Step(soundDraft.InputMode, steps).Code);
            if (next == soundDraft)
            {
                return;
            }

            soundDraft = next;
            ShowDraft();
        }

        /// <summary>
        /// Starts a test on the microphone in the draft — the one being
        /// considered, not the one applied — or stops the one running.
        /// </summary>
        private void OnMicrophoneTestToggled()
        {
            if (IsPanelUp)
            {
                return;
            }

            if (microphoneTest.IsRunning)
            {
                microphoneTest.Stop();
            }
            else
            {
                microphoneTest.Start(soundDraft.DeviceName);
            }

            view.ShowMicrophoneTest(microphoneTest.IsRunning);
        }

        private void StopMicrophoneTest()
        {
            if (microphoneTest.IsRunning)
            {
                microphoneTest.Stop();
                view.ShowMicrophoneTest(false);
            }
        }

        /// <summary>
        /// Waits for a key and puts it on this action.
        /// </summary>
        /// <remarks>
        /// Nothing is checked for a clash, because the design has several
        /// actions share a key on purpose — see <see cref="ControlSettings"/>.
        /// Pressing a second plate while one is waiting moves the wait to the
        /// second, which is what pressing it means.
        /// </remarks>
        private void OnRebindRequested(ControlAction action)
        {
            if (IsPanelUp)
            {
                return;
            }

            StopListening();
            listening = action;
            ShowBindings();

            keyCapture.Begin(code =>
            {
                // The capture is spent; whether it was answered or abandoned,
                // this row is no longer waiting.
                listening = null;

                if (!string.IsNullOrEmpty(code))
                {
                    // Refused the same way a key another row holds is: named,
                    // so the player knows it is not theirs to give.
                    if (ControlCatalog.IsReserved(code, out var reservedBy))
                    {
                        view.ShowNotice(
                            SettingsStyle.Controls.InUseTitle,
                            SettingsStyle.Controls.InUseMessage(
                                ControlCatalog.KeyLabel(code), reservedBy));
                    }
                    else if (controlDraft.TryRebind(action, code, out var moved, out var holder))
                    {
                        controlDraft = moved;
                    }
                    else
                    {
                        view.ShowNotice(
                            SettingsStyle.Controls.InUseTitle,
                            SettingsStyle.Controls.InUseMessage(
                                ControlCatalog.KeyLabel(code),
                                SettingsStyle.Controls.ActionLabel(holder)));
                    }
                }

                ShowDraft();
            });
        }

        private void OnSensitivityChanged(ControlSensitivity sensitivity, int percent)
        {
            if (IsPanelUp)
            {
                return;
            }

            var next = controlDraft.With(sensitivity, ControlCatalog.Clamp(percent));
            if (next == controlDraft)
            {
                return;
            }

            controlDraft = next;
            ShowDraft();
        }

        private void OnReversalStepRequested(ControlToggle toggle, int steps)
        {
            if (IsPanelUp || steps == 0)
            {
                return;
            }

            var next = controlDraft.With(
                toggle, ControlCatalog.Reversals.Step(controlDraft.Get(toggle), steps).Code);
            if (next == controlDraft)
            {
                return;
            }

            controlDraft = next;
            ShowDraft();
        }

        private void StopListening()
        {
            if (keyCapture.IsCapturing)
            {
                keyCapture.Cancel();
            }

            if (listening.HasValue)
            {
                listening = null;
                ShowBindings();
            }
        }

        /// <summary>
        /// Opens the panel to write in, empty and with 보내기 unavailable:
        /// there is nothing to send yet.
        /// </summary>
        private void OnFeedbackRequested()
        {
            if (IsPanelUp)
            {
                return;
            }

            isWritingFeedback = true;
            view.ShowFeedback();
            view.SetFeedbackSubmitEnabled(false);
        }

        /// <summary>
        /// Whitespace alone is not feedback, so 보내기 stays dark until
        /// something has actually been written. The length is the field's own
        /// affair; it stops taking keys at its limit.
        /// </summary>
        private void OnFeedbackEdited(string message)
        {
            if (!isWritingFeedback)
            {
                return;
            }

            view.SetFeedbackSubmitEnabled(!string.IsNullOrWhiteSpace(message));
        }

        /// <summary>
        /// Hands the press on and waits.
        /// </summary>
        /// <remarks>
        /// The sending happens outside this screen — SettingsFeedbackBridge
        /// listens for the same event — so all this does is darken 보내기 so a
        /// second press cannot start a second send.
        /// <para>
        /// <b>Nothing is said and nothing is closed here.</b> Whether the panel
        /// comes down depends on the answer, and this presenter is not the one
        /// that gets it. Closing the panel now would throw away what the player
        /// wrote in the case where the send fails, and that is the one thing
        /// they took the trouble to produce.
        /// </para>
        /// </remarks>
        private void OnFeedbackSubmitted(string message)
        {
            if (!isWritingFeedback || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            view.SetFeedbackSubmitEnabled(false);
        }

        private void OnFeedbackDismissed()
        {
            if (!isWritingFeedback)
            {
                return;
            }

            isWritingFeedback = false;
            view.HideFeedback();
        }

        /// <summary>
        /// Settles on the draft. Nothing is asked first: applying is what the
        /// player came to do, and the design only confirms the ways of throwing
        /// work away.
        /// </summary>
        private void OnApplyRequested()
        {
            if (IsPanelUp || !IsChanged)
            {
                return;
            }

            Apply();
        }

        private void OnResetRequested()
        {
            if (IsPanelUp || !IsChanged)
            {
                return;
            }

            Ask(SettingsConfirmKind.ResetTab);
        }

        /// <summary>
        /// Always on offer, unlike the tab's own 초기화: this one is about
        /// what is applied as much as about the draft, and there is no way to
        /// tell from the buttons whether the applied values are the defaults.
        /// </summary>
        private void OnResetAllRequested()
        {
            if (IsPanelUp)
            {
                return;
            }

            Ask(SettingsConfirmKind.ResetAll);
        }

        private void OnLeaveGameRequested()
        {
            if (IsPanelUp)
            {
                return;
            }

            Ask(SettingsConfirmKind.LeaveGame);
        }

        /// <summary>Leaves, or asks first if there is something to lose.</summary>
        private void OnBackRequested()
        {
            if (IsPanelUp)
            {
                return;
            }

            if (!IsChanged)
            {
                Leave();
                return;
            }

            Ask(SettingsConfirmKind.Discard);
        }

        /// <summary>The right, orange button: the thing the panel is named for.</summary>
        private void OnConfirmAccepted()
        {
            if (!pending.HasValue)
            {
                return;
            }

            var kind = pending.Value;
            pending = null;
            view.HideConfirm();

            switch (kind)
            {
                case SettingsConfirmKind.ResetTab:
                    ResetTab(shownTab);
                    break;
                case SettingsConfirmKind.ResetAll:
                    generalDraft = general.Current;
                    graphicsDraft = graphics.Current;
                    uiDraft = ui.Current;
                    soundDraft = sound.Current;
                    sound.Preview(soundDraft);
                    controlDraft = controls.Current;
                    noticeDraft = notifications.Current;
                    ShowDraft();
                    break;
                case SettingsConfirmKind.Discard:
                    Apply();
                    Leave();
                    break;
                case SettingsConfirmKind.LeaveGame:
                    LeaveGameConfirmed?.Invoke();
                    break;
            }
        }

        /// <summary>
        /// The left, grey button. For the two resets it is 취소 and does what
        /// the X does; for leaving it is 바로 나가기, which goes without saving.
        /// </summary>
        private void OnConfirmDeclined()
        {
            if (!pending.HasValue)
            {
                return;
            }

            var kind = pending.Value;
            pending = null;
            view.HideConfirm();

            if (kind == SettingsConfirmKind.Discard)
            {
                Leave();
            }
        }

        private void OnConfirmDismissed()
        {
            if (!pending.HasValue)
            {
                return;
            }

            pending = null;
            view.HideConfirm();
        }

        private void Ask(SettingsConfirmKind kind)
        {
            pending = kind;
            view.ShowConfirm(kind, shownTab);
        }

        /// <summary>
        /// Settles every tab at once, which is what one 적용하기 under the
        /// whole panel means.
        /// </summary>
        private void Apply()
        {
            general.Apply(generalDraft);
            graphics.Apply(graphicsDraft);
            ui.Apply(uiDraft);
            sound.Apply(soundDraft);
            controls.Apply(controlDraft);
            notifications.Apply(noticeDraft);
            generalApplied = general.Current;
            generalDraft = generalApplied;
            graphicsApplied = graphics.Current;
            graphicsDraft = graphicsApplied;
            uiApplied = ui.Current;
            uiDraft = uiApplied;
            soundApplied = sound.Current;
            soundDraft = soundApplied;
            controlApplied = controls.Current;
            controlDraft = controlApplied;
            noticeApplied = notifications.Current;
            noticeDraft = noticeApplied;
            ShowDraft();
        }

        /// <summary>
        /// Puts one tab's defaults into the draft, leaving the other tabs'
        /// drafts alone: 초기화 under the panel names the tab being looked at.
        /// </summary>
        /// <remarks>
        /// The four tabs without rows yet are named so the column is complete,
        /// and resetting one of them is nothing.
        /// </remarks>
        private void ResetTab(SettingsTab tab)
        {
            switch (tab)
            {
                case SettingsTab.General:
                    generalDraft = general.Current;
                    break;
                case SettingsTab.Graphics:
                    graphicsDraft = graphics.Current;
                    break;
                case SettingsTab.Interface:
                    uiDraft = ui.Current;
                    break;
                case SettingsTab.Sound:
                    soundDraft = sound.Current;
                    sound.Preview(soundDraft);
                    break;
                case SettingsTab.Controls:
                    // A row left waiting for a press would still be lit over
                    // a key it no longer holds.
                    StopListening();
                    controlDraft = controls.Current;
                    break;
                case SettingsTab.Notifications:
                    noticeDraft = notifications.Current;
                    break;
                default:
                    return;
            }

            ShowDraft();
        }

        private void ShowDraft()
        {
            var language = general.Languages.TryFind(generalDraft.LanguageCode, out var listed)
                ? listed
                : general.Languages.Default;
            view.ShowLanguage(language.Label, general.Languages.CanStep);

            foreach (GraphicsOption option in Enum.GetValues(typeof(GraphicsOption)))
            {
                view.ShowGraphics(
                    option,
                    graphics.Catalog.Label(option, graphicsDraft.Get(option)),
                    graphics.Catalog.For(option).CanStep);
            }

            foreach (InterfaceOption option in Enum.GetValues(typeof(InterfaceOption)))
            {
                view.ShowInterface(
                    option,
                    ui.Catalog.Label(option, uiDraft.Get(option)),
                    ui.Catalog.For(option).CanStep);
            }

            foreach (SoundVolume volume in Enum.GetValues(typeof(SoundVolume)))
            {
                view.ShowVolume(volume, soundDraft.Get(volume));
            }

            var devices = sound.DeviceChoices;
            view.ShowMicrophoneDevice(
                devices.TryFind(soundDraft.DeviceName, out var device)
                    ? device.Label
                    : devices.Default.Label,
                devices.CanStep);

            var modes = SoundCatalog.InputModes;
            view.ShowInputMode(
                modes.TryFind(soundDraft.InputMode, out var mode) ? mode.Label : modes.Default.Label,
                modes.CanStep);

            view.ShowMicrophoneTest(microphoneTest.IsRunning);

            ShowBindings();
            foreach (ControlSensitivity sensitivity in Enum.GetValues(typeof(ControlSensitivity)))
            {
                view.ShowSensitivity(sensitivity, controlDraft.Get(sensitivity));
            }

            foreach (NotificationOption option in Enum.GetValues(typeof(NotificationOption)))
            {
                view.ShowNotification(
                    option,
                    notifications.Catalog.Label(option, noticeDraft.Get(option)),
                    notifications.Catalog.For(option).CanStep);
            }

            foreach (ControlToggle toggle in Enum.GetValues(typeof(ControlToggle)))
            {
                view.ShowReversal(
                    toggle,
                    ControlCatalog.Reversals.TryFind(controlDraft.Get(toggle), out var reversal)
                        ? reversal.Label
                        : ControlCatalog.Reversals.Default.Label,
                    ControlCatalog.Reversals.CanStep);
            }

            view.SetActionsEnabled(IsChanged);
        }

        /// <summary>
        /// Goes back to Home, and says so to the flow before going.
        /// </summary>
        /// <remarks>
        /// Leaving the scene is not enough. Home gates the buttons that change
        /// screen on the flow state, so a screen that walks out without moving
        /// the state back leaves Home with half its menu dead. A refusal is
        /// reported rather than swallowed: a dead back button and a hung screen
        /// look identical from the outside.
        /// </remarks>
        /// <summary>
        /// Every key plate, and which of them is waiting. Kept apart from
        /// <see cref="ShowDraft"/> so that starting and abandoning a wait can
        /// redraw the plates without redrawing every other tab.
        /// </summary>
        private void ShowBindings()
        {
            foreach (ControlAction action in Enum.GetValues(typeof(ControlAction)))
            {
                view.ShowBinding(action, ControlCatalog.KeyLabel(controlDraft.Get(action)));
            }

            view.ShowRebinding(listening);
        }

        private void Leave()
        {
            sound.ApplyToAudio();
            StopMicrophoneTest();
            StopListening();

            // Discard the draft before the cached screen is hidden.
            Open();
            if (closeSettings != null)
            {
                closeSettings();
                return;
            }

            if (appFlow.CurrentState != AppFlowState.Home &&
                !appFlow.TryTransitionTo(AppFlowState.Home))
            {
                Debug.LogError(
                    "[Settings] Cannot leave the settings screen for the home screen " +
                    $"from {appFlow.CurrentState}.");
                return;
            }

            applicationHost.OpenHome();
        }
    }
}
