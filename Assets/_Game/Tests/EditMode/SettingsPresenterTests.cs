using System;
using System.Collections.Generic;
using Game.Client.Home;
using Game.Client.Settings;
using Game.Core.Flow;
using Game.Core.Settings;
using Game.Core.Ports;
using VContainer;
using NUnit.Framework;

namespace Game.Architecture.Tests
{
    /// <summary>
    /// What the settings screen shows when it opens, what changing a value
    /// does, and how the three confirmations are answered.
    /// </summary>
    /// <remarks>
    /// Two languages rather than the shipped one, so the picker has somewhere
    /// to go; with Korean alone there would be no way to make a change to
    /// apply, reset or throw away.
    /// </remarks>
    public sealed class SettingsPresenterTests
    {
        private static readonly LanguageCatalog TwoLanguages = new LanguageCatalog(
            new Language("ko", "한국어"),
            new Language("en", "English"));

        private FakeSettingsView view;
        private FakeApplicationHost host;
        private AppFlowSystem flow;
        private InMemoryGeneralSettingsStore store;
        private GeneralSettingsSystem general;
        private InMemoryGraphicsSettingsStore graphicsStore;
        private NullGraphicsSettingsApplier applier;
        private GraphicsSettingsSystem graphics;
        private InMemoryInterfaceSettingsStore uiStore;
        private InterfaceSettingsSystem ui;
        private InMemorySoundSettingsStore soundStore;
        private NullSoundSettingsApplier soundApplier;
        private SoundSettingsSystem sound;
        private NullMicrophoneTest microphoneTest;
        private InMemoryControlSettingsStore controlStore;
        private ControlSettingsSystem controls;
        private FakeKeyCapture keyCapture;
        private InMemoryNotificationSettingsStore noticeStore;
        private NotificationSettingsSystem notifications;

        [SetUp]
        public void SetUp()
        {
            view = new FakeSettingsView();
            host = new FakeApplicationHost();
            store = new InMemoryGeneralSettingsStore();
            general = new GeneralSettingsSystem(store, TwoLanguages);
            graphicsStore = new InMemoryGraphicsSettingsStore();
            applier = new NullGraphicsSettingsApplier();
            graphics = new GraphicsSettingsSystem(graphicsStore, applier);
            uiStore = new InMemoryInterfaceSettingsStore();
            ui = new InterfaceSettingsSystem(uiStore);
            soundStore = new InMemorySoundSettingsStore();
            soundApplier = new NullSoundSettingsApplier();
            sound = new SoundSettingsSystem(
                soundStore, soundApplier, new FixedMicrophoneDevices("Headset", "Webcam"));
            microphoneTest = new NullMicrophoneTest();
            controlStore = new InMemoryControlSettingsStore();
            controls = new ControlSettingsSystem(controlStore);
            keyCapture = new FakeKeyCapture();
            noticeStore = new InMemoryNotificationSettingsStore();
            notifications = new NotificationSettingsSystem(noticeStore);

            // Where the application is while this screen is up: Home opened it
            // and moved the flow on the way in.
            flow = new AppFlowSystem();
            flow.TryTransitionTo(AppFlowState.Settings);
        }

        [Test]
        public void Opening_ShowsTheGeneralTab_WithTheAppliedLanguage_AndActionsOff()
        {
            store.Save(new GeneralSettings("en"));
            general = new GeneralSettingsSystem(store, TwoLanguages);

            using var presenter = Started();

            Assert.That(view.ShownTab, Is.EqualTo(SettingsTab.General));
            Assert.That(view.LanguageLabel, Is.EqualTo("English"));
            Assert.That(view.CanStep, Is.True);
            Assert.That(view.ActionsEnabled, Is.False);
            Assert.That(view.ConfirmVisible, Is.False);
            Assert.That(view.FeedbackVisible, Is.False);
        }

        [Test]
        public void Opening_WithOneLanguage_DrawsTheArrowsUnavailable()
        {
            general = new GeneralSettingsSystem(store, LanguageCatalog.Shipped);

            using var presenter = Started();

            Assert.That(view.LanguageLabel, Is.EqualTo("한국어"));
            Assert.That(view.CanStep, Is.False);
        }

        [Test]
        public void SelectingATab_ShowsIt()
        {
            using var presenter = Started();

            view.SelectTab(SettingsTab.Sound);

            Assert.That(view.ShownTab, Is.EqualTo(SettingsTab.Sound));
        }

        [Test]
        public void SteppingTheLanguage_MovesTheDraft_AndLightsTheButtons_WithoutApplying()
        {
            using var presenter = Started();

            view.StepLanguage(1);

            Assert.That(presenter.GeneralDraft.LanguageCode, Is.EqualTo("en"));
            Assert.That(view.LanguageLabel, Is.EqualTo("English"));
            Assert.That(view.ActionsEnabled, Is.True);
            Assert.That(general.Current.LanguageCode, Is.EqualTo("ko"), "Nothing is settled until apply.");
            Assert.That(store.Saved, Is.Null);
        }

        [Test]
        public void SteppingBack_ToTheAppliedLanguage_DarkensTheButtonsAgain()
        {
            using var presenter = Started();
            view.StepLanguage(1);

            view.StepLanguage(-1);

            Assert.That(presenter.GeneralDraft.LanguageCode, Is.EqualTo("ko"));
            Assert.That(view.ActionsEnabled, Is.False);
        }

        [Test]
        public void Apply_SettlesTheDraft_SavesIt_AndDarkensTheButtons()
        {
            using var presenter = Started();
            view.StepLanguage(1);

            view.Apply();

            Assert.That(general.Current.LanguageCode, Is.EqualTo("en"));
            Assert.That(store.Saved, Is.EqualTo(new GeneralSettings("en")));
            Assert.That(view.ActionsEnabled, Is.False);
            Assert.That(view.ConfirmVisible, Is.False, "Applying asks nothing.");
        }

        [Test]
        public void Apply_WithoutChanges_DoesNothing()
        {
            using var presenter = Started();

            view.Apply();

            Assert.That(store.Saved, Is.Null);
        }

        [Test]
        public void Reset_WithoutChanges_AsksNothing()
        {
            using var presenter = Started();

            view.Reset();

            Assert.That(view.ConfirmVisible, Is.False);
        }

        [Test]
        public void Reset_AsksAboutTheShownTab_AndAcceptingPutsTheDefaultsInTheDraft()
        {
            using var presenter = Started();
            view.StepLanguage(1);

            view.Reset();

            Assert.That(view.ConfirmVisible, Is.True);
            Assert.That(view.ConfirmKind, Is.EqualTo(SettingsConfirmKind.ResetTab));
            Assert.That(view.ConfirmTab, Is.EqualTo(SettingsTab.General));

            view.Accept();

            Assert.That(view.ConfirmVisible, Is.False);
            Assert.That(presenter.GeneralDraft.LanguageCode, Is.EqualTo("ko"));
            Assert.That(view.ActionsEnabled, Is.False);
        }

        [Test]
        public void CancelChanges_RestoresAppliedValues_NotDefaults()
        {
            store.Save(new GeneralSettings("en"));
            general = new GeneralSettingsSystem(store, TwoLanguages);
            using var presenter = Started();
            view.StepLanguage(1);
            Assert.That(presenter.GeneralDraft.LanguageCode, Is.EqualTo("ko"));

            // Cancel restores saved English rather than factory-default Korean.
            view.Reset();
            view.Accept();

            Assert.That(presenter.GeneralDraft.LanguageCode, Is.EqualTo("en"));
            Assert.That(view.ActionsEnabled, Is.False);
            Assert.That(general.Current.LanguageCode, Is.EqualTo("en"), "Reset applies nothing by itself.");
        }

        [Test]
        public void Reset_Cancelled_KeepsTheDraft()
        {
            using var presenter = Started();
            view.StepLanguage(1);
            view.Reset();

            view.Decline();

            Assert.That(view.ConfirmVisible, Is.False);
            Assert.That(presenter.GeneralDraft.LanguageCode, Is.EqualTo("en"));
            Assert.That(view.ActionsEnabled, Is.True);
        }

        [Test]
        public void CancelAll_RestoresSavedSettings()
        {
            store.Save(new GeneralSettings("en"));
            general = new GeneralSettingsSystem(store, TwoLanguages);
            using var presenter = Started();
            Assert.That(view.ActionsEnabled, Is.False);

            view.ResetAll();

            Assert.That(view.ConfirmKind, Is.EqualTo(SettingsConfirmKind.ResetAll));

            view.Accept();

            Assert.That(presenter.GeneralDraft.LanguageCode, Is.EqualTo("en"));
            Assert.That(view.ActionsEnabled, Is.False);
            Assert.That(general.Current.LanguageCode, Is.EqualTo("en"));
        }

        [Test]
        public void LeaveGame_AsksFirst_AndOnlyAcceptingConfirms()
        {
            using var presenter = Started();
            var confirmed = 0;
            presenter.LeaveGameConfirmed += () => confirmed++;

            view.LeaveGame();

            Assert.That(view.ConfirmVisible, Is.True);
            Assert.That(view.ConfirmKind, Is.EqualTo(SettingsConfirmKind.LeaveGame));
            Assert.That(confirmed, Is.Zero);

            view.Dismiss();
            Assert.That(view.ConfirmVisible, Is.False);
            Assert.That(confirmed, Is.Zero);

            view.LeaveGame();
            view.Decline();
            Assert.That(view.ConfirmVisible, Is.False);
            Assert.That(confirmed, Is.Zero);

            view.LeaveGame();
            view.Accept();
            Assert.That(view.ConfirmVisible, Is.False);
            Assert.That(confirmed, Is.EqualTo(1));
            Assert.That(host.HomeOpenCount, Is.Zero);
        }

        [Test]
        public void Back_WithoutChanges_LeavesForHome()
        {
            using var presenter = Started();

            view.Back();

            Assert.That(view.ConfirmVisible, Is.False);
            Assert.That(host.HomeOpenCount, Is.EqualTo(1));
            Assert.That(flow.CurrentState, Is.EqualTo(AppFlowState.Home));
        }

        [Test]
        public void Back_WithChanges_AsksFirst()
        {
            using var presenter = Started();
            view.StepLanguage(1);

            view.Back();

            Assert.That(view.ConfirmVisible, Is.True);
            Assert.That(view.ConfirmKind, Is.EqualTo(SettingsConfirmKind.Discard));
            Assert.That(host.HomeOpenCount, Is.EqualTo(0));
            Assert.That(flow.CurrentState, Is.EqualTo(AppFlowState.Settings));
        }

        [Test]
        public void Back_SaveAndLeave_AppliesThenLeaves()
        {
            using var presenter = Started();
            view.StepLanguage(1);
            view.Back();

            view.Accept();

            Assert.That(general.Current.LanguageCode, Is.EqualTo("en"));
            Assert.That(store.Saved, Is.EqualTo(new GeneralSettings("en")));
            Assert.That(host.HomeOpenCount, Is.EqualTo(1));
            Assert.That(flow.CurrentState, Is.EqualTo(AppFlowState.Home));
        }

        [Test]
        public void Back_LeaveWithoutSaving_LeavesAndAppliesNothing()
        {
            using var presenter = Started();
            view.StepLanguage(1);
            view.Back();

            view.Decline();

            Assert.That(general.Current.LanguageCode, Is.EqualTo("ko"));
            Assert.That(store.Saved, Is.Null);
            Assert.That(host.HomeOpenCount, Is.EqualTo(1));
            Assert.That(flow.CurrentState, Is.EqualTo(AppFlowState.Home));
        }

        [Test]
        public void Back_Dismissed_Stays()
        {
            using var presenter = Started();
            view.StepLanguage(1);
            view.Back();

            view.Dismiss();

            Assert.That(view.ConfirmVisible, Is.False);
            Assert.That(host.HomeOpenCount, Is.EqualTo(0));
            Assert.That(presenter.GeneralDraft.LanguageCode, Is.EqualTo("en"));
            Assert.That(flow.CurrentState, Is.EqualTo(AppFlowState.Settings));
        }

        [Test]
        public void WhileAConfirmationIsUp_OtherInputIsIgnored()
        {
            using var presenter = Started();
            view.StepLanguage(1);
            view.Back();

            view.StepLanguage(1);
            view.SelectTab(SettingsTab.Sound);
            view.Apply();
            view.Back();

            Assert.That(presenter.GeneralDraft.LanguageCode, Is.EqualTo("en"));
            Assert.That(view.ShownTab, Is.EqualTo(SettingsTab.General));
            Assert.That(store.Saved, Is.Null);
            Assert.That(view.ConfirmKind, Is.EqualTo(SettingsConfirmKind.Discard));
        }

        [Test]
        public void Opening_ShowsEveryGraphicsRow()
        {
            using var presenter = Started();

            Assert.That(view.GraphicsLabels.Count, Is.EqualTo(9));
            Assert.That(view.GraphicsLabels[GraphicsOption.Resolution], Is.EqualTo("1920x1080"));
            Assert.That(view.GraphicsLabels[GraphicsOption.ShadowQuality], Is.EqualTo("높음"));
        }

        [Test]
        public void SteppingAGraphicsRow_MovesTheDraft_AndLightsTheButtons_WithoutApplying()
        {
            using var presenter = Started();

            view.StepGraphics(GraphicsOption.ShadowQuality, 1);

            Assert.That(
                presenter.GraphicsDraft.Get(GraphicsOption.ShadowQuality),
                Is.EqualTo("medium"));
            Assert.That(view.GraphicsLabels[GraphicsOption.ShadowQuality], Is.EqualTo("중간"));
            Assert.That(view.ActionsEnabled, Is.True);
            Assert.That(graphics.Current, Is.EqualTo(graphics.Defaults), "Nothing is settled until apply.");
            Assert.That(graphicsStore.Saved, Is.Null);
            Assert.That(applier.ApplyCount, Is.EqualTo(0), "And nothing reaches the renderer.");
        }

        [Test]
        public void SteppingAGraphicsRow_LeavesTheOtherRowsAlone()
        {
            using var presenter = Started();

            view.StepGraphics(GraphicsOption.Resolution, 1);

            Assert.That(
                presenter.GraphicsDraft.Get(GraphicsOption.Resolution),
                Is.EqualTo("2560x1440"));
            Assert.That(
                presenter.GraphicsDraft.Get(GraphicsOption.ShadowQuality),
                Is.EqualTo("high"));
        }

        [Test]
        public void SteppingBack_ToTheAppliedValue_DarkensTheButtonsAgain()
        {
            using var presenter = Started();
            view.StepGraphics(GraphicsOption.Hbao, 1);

            view.StepGraphics(GraphicsOption.Hbao, -1);

            Assert.That(view.ActionsEnabled, Is.False);
        }

        [Test]
        public void Apply_SettlesEveryTabAtOnce_AndReachesTheRenderer()
        {
            using var presenter = Started();
            view.StepLanguage(1);
            view.StepGraphics(GraphicsOption.FpsLimit, 1);

            view.Apply();

            Assert.That(general.Current.LanguageCode, Is.EqualTo("en"));
            Assert.That(graphics.Current.Get(GraphicsOption.FpsLimit), Is.EqualTo("60"));
            Assert.That(graphicsStore.Saved, Is.EqualTo(graphics.Current));
            Assert.That(applier.Applied, Is.EqualTo(graphics.Current));
            Assert.That(view.ActionsEnabled, Is.False);
        }

        [Test]
        public void Reset_OnTheGraphicsTab_LeavesTheGeneralDraftAlone()
        {
            using var presenter = Started();
            view.StepLanguage(1);
            view.StepGraphics(GraphicsOption.TextureQuality, 1);
            view.SelectTab(SettingsTab.Graphics);

            view.Reset();

            Assert.That(view.ConfirmKind, Is.EqualTo(SettingsConfirmKind.ResetTab));
            Assert.That(view.ConfirmTab, Is.EqualTo(SettingsTab.Graphics));

            view.Accept();

            Assert.That(
                presenter.GraphicsDraft.Get(GraphicsOption.TextureQuality),
                Is.EqualTo("medium"),
                "The graphics tab goes back to its defaults.");
            Assert.That(
                presenter.GeneralDraft.LanguageCode,
                Is.EqualTo("en"),
                "And the language typed on the other tab is still waiting.");
            Assert.That(view.ActionsEnabled, Is.True);
        }

        [Test]
        public void ResetAll_PutsEveryTabBack()
        {
            using var presenter = Started();
            view.StepLanguage(1);
            view.StepGraphics(GraphicsOption.ShadowQuality, 1);

            view.ResetAll();
            view.Accept();

            Assert.That(presenter.GeneralDraft.LanguageCode, Is.EqualTo("ko"));
            Assert.That(presenter.GraphicsDraft, Is.EqualTo(graphics.Defaults));
            Assert.That(view.ActionsEnabled, Is.False, "The defaults are what is applied.");
        }

        [Test]
        public void Back_WithOnlyAGraphicsChange_AsksFirst()
        {
            using var presenter = Started();
            view.StepGraphics(GraphicsOption.Volumetrics, 1);

            view.Back();

            Assert.That(view.ConfirmKind, Is.EqualTo(SettingsConfirmKind.Discard));
            Assert.That(host.HomeOpenCount, Is.EqualTo(0));
        }

        [Test]
        public void Back_SaveAndLeave_SettlesTheGraphicsToo()
        {
            using var presenter = Started();
            view.StepGraphics(GraphicsOption.Volumetrics, 1);
            view.Back();

            view.Accept();

            Assert.That(graphics.Current.Get(GraphicsOption.Volumetrics), Is.EqualTo("low"));
            Assert.That(host.HomeOpenCount, Is.EqualTo(1));
        }

        [Test]
        public void Opening_ShowsEveryInterfaceRow()
        {
            using var presenter = Started();

            Assert.That(view.InterfaceLabels.Count, Is.EqualTo(9));
            Assert.That(view.InterfaceLabels[InterfaceOption.UiScale], Is.EqualTo("중간"));
            Assert.That(view.InterfaceLabels[InterfaceOption.FpsCounter], Is.EqualTo("켜기"));
            Assert.That(view.InterfaceLabels[InterfaceOption.ChatScope], Is.EqualTo("끄기(모두)"));
        }

        [Test]
        public void SteppingAnInterfaceRow_MovesOnlyThatTabsDraft()
        {
            using var presenter = Started();

            view.StepInterface(InterfaceOption.PingCounter, 1);

            Assert.That(
                presenter.InterfaceDraft.Get(InterfaceOption.PingCounter), Is.EqualTo("off"));
            Assert.That(view.InterfaceLabels[InterfaceOption.PingCounter], Is.EqualTo("끄기"));
            Assert.That(view.ActionsEnabled, Is.True);
            Assert.That(presenter.GraphicsDraft, Is.EqualTo(graphics.Defaults));
            Assert.That(presenter.GeneralDraft.LanguageCode, Is.EqualTo("ko"));
            Assert.That(uiStore.Saved, Is.Null, "Nothing is settled until apply.");
        }

        [Test]
        public void Apply_SettlesTheInterfaceTabToo()
        {
            using var presenter = Started();
            view.StepInterface(InterfaceOption.UiScale, 1);

            view.Apply();

            Assert.That(ui.Current.Get(InterfaceOption.UiScale), Is.EqualTo("small"));
            Assert.That(uiStore.Saved, Is.EqualTo(ui.Current));
            Assert.That(view.ActionsEnabled, Is.False);
        }

        [Test]
        public void Reset_OnTheInterfaceTab_LeavesTheOtherTabsAlone()
        {
            using var presenter = Started();
            view.StepGraphics(GraphicsOption.Hbao, 1);
            view.StepInterface(InterfaceOption.InGameUi, 1);
            view.SelectTab(SettingsTab.Interface);

            view.Reset();
            Assert.That(view.ConfirmTab, Is.EqualTo(SettingsTab.Interface));
            view.Accept();

            Assert.That(presenter.InterfaceDraft, Is.EqualTo(ui.Defaults));
            Assert.That(
                presenter.GraphicsDraft.Get(GraphicsOption.Hbao),
                Is.EqualTo("low"),
                "The graphics change is still waiting.");
            Assert.That(view.ActionsEnabled, Is.True);
        }

        [Test]
        public void ResetAll_PutsTheInterfaceTabBackToo()
        {
            using var presenter = Started();
            view.StepInterface(InterfaceOption.BeginnerGuide, 1);

            view.ResetAll();
            view.Accept();

            Assert.That(presenter.InterfaceDraft, Is.EqualTo(ui.Defaults));
            Assert.That(view.ActionsEnabled, Is.False);
        }

        [Test]
        public void Opening_ShowsEveryVolume_TheMicrophone_AndTheInputMode()
        {
            using var presenter = Started();

            Assert.That(view.Volumes.Count, Is.EqualTo(5));
            Assert.That(view.Volumes[SoundVolume.Master], Is.EqualTo(50));
            Assert.That(view.DeviceLabel, Is.EqualTo("기본 장치"));
            Assert.That(view.DeviceCanStep, Is.True, "Two microphones to choose between.");
            Assert.That(view.InputModeLabel, Is.EqualTo("눌러서 말하기"));
            Assert.That(view.TestRunning, Is.False);
        }

        [Test]
        public void DraggingAVolume_PreviewsTheDraft_WithoutSaving()
        {
            using var presenter = Started();

            view.DragVolume(SoundVolume.Music, 20);

            Assert.That(presenter.SoundDraft.Get(SoundVolume.Music), Is.EqualTo(20));
            Assert.That(view.Volumes[SoundVolume.Music], Is.EqualTo(20));
            Assert.That(view.Volumes[SoundVolume.Master], Is.EqualTo(50), "The other sliders stay.");
            Assert.That(view.ActionsEnabled, Is.True);
            Assert.That(soundApplier.ApplyCount, Is.EqualTo(1), "Dragging previews the draft.");
            Assert.That(soundApplier.Applied.Value.Get(SoundVolume.Music), Is.EqualTo(20));
            Assert.That(sound.Current.Get(SoundVolume.Music), Is.EqualTo(50), "Preview does not commit settings.");
            Assert.That(soundStore.Saved, Is.Null);
        }

        [Test]
        public void DraggingAVolume_PastTheEnd_IsHeldAtTheEnd()
        {
            using var presenter = Started();

            view.DragVolume(SoundVolume.Effects, 130);

            Assert.That(presenter.SoundDraft.Get(SoundVolume.Effects), Is.EqualTo(100));
        }

        [Test]
        public void SteppingTheMicrophone_WalksTheMachinesDevices()
        {
            using var presenter = Started();

            view.StepDevice(1);
            Assert.That(view.DeviceLabel, Is.EqualTo("Headset"));
            Assert.That(presenter.SoundDraft.DeviceName, Is.EqualTo("Headset"));

            view.StepDevice(1);
            Assert.That(view.DeviceLabel, Is.EqualTo("Webcam"));

            view.StepDevice(1);
            Assert.That(view.DeviceLabel, Is.EqualTo("기본 장치"), "And wraps.");
        }

        [Test]
        public void SteppingTheInputMode_WalksTheThreeModes()
        {
            using var presenter = Started();

            view.StepInputMode(1);
            Assert.That(view.InputModeLabel, Is.EqualTo("오픈 마이크"));
            Assert.That(presenter.SoundDraft.InputMode, Is.EqualTo(SoundCatalog.OpenMic));

            view.StepInputMode(1);
            Assert.That(view.InputModeLabel, Is.EqualTo("끄기"));
        }

        [Test]
        public void Apply_SettlesTheSoundTab_AndReachesTheAudio()
        {
            using var presenter = Started();
            view.DragVolume(SoundVolume.Master, 75);
            view.StepInputMode(1);

            view.Apply();

            Assert.That(sound.Current.Get(SoundVolume.Master), Is.EqualTo(75));
            Assert.That(sound.Current.InputMode, Is.EqualTo(SoundCatalog.OpenMic));
            Assert.That(soundStore.Saved, Is.EqualTo(sound.Current));
            Assert.That(soundApplier.Applied, Is.EqualTo(sound.Current));
            Assert.That(view.ActionsEnabled, Is.False);
        }

        [Test]
        public void Reset_OnTheSoundTab_PutsEverySliderBackToHalf()
        {
            using var presenter = Started();
            view.DragVolume(SoundVolume.Ambience, 5);
            view.StepDevice(1);
            view.SelectTab(SettingsTab.Sound);

            view.Reset();
            Assert.That(view.ConfirmTab, Is.EqualTo(SettingsTab.Sound));
            view.Accept();

            Assert.That(presenter.SoundDraft, Is.EqualTo(sound.Defaults));
            Assert.That(view.Volumes[SoundVolume.Ambience], Is.EqualTo(50));
            Assert.That(view.DeviceLabel, Is.EqualTo("기본 장치"));
        }

        [Test]
        public void MicrophoneTest_StartsOnTheDeviceBeingConsidered_AndStopsWhenPressedAgain()
        {
            using var presenter = Started();
            view.StepDevice(1);

            view.ToggleTest();

            Assert.That(microphoneTest.IsRunning, Is.True);
            Assert.That(microphoneTest.StartedOn, Is.EqualTo("Headset"), "The draft's device, not the applied one.");
            Assert.That(view.TestRunning, Is.True);
            Assert.That(view.ActionsEnabled, Is.True, "Testing is not a change; the device step is.");

            view.ToggleTest();

            Assert.That(microphoneTest.IsRunning, Is.False);
            Assert.That(view.TestRunning, Is.False);
        }

        [Test]
        public void MicrophoneTest_IsStoppedByLeaving()
        {
            using var presenter = Started();
            view.ToggleTest();

            view.Back();

            Assert.That(microphoneTest.IsRunning, Is.False);
            Assert.That(host.HomeOpenCount, Is.EqualTo(1));
        }

        [Test]
        public void MicrophoneTest_IsStoppedByDisposal()
        {
            var presenter = Started();
            view.ToggleTest();

            presenter.Dispose();

            Assert.That(microphoneTest.IsRunning, Is.False);
        }

        [Test]
        public void MicrophoneTest_ShowsNoticeWhenTheDeviceCannotOpen()
        {
            using var presenter = new SettingsPresenter(
                view, general, graphics, ui, sound, new ClosedMicrophoneTest(),
                controls, keyCapture, notifications, host, flow);
            presenter.Start();

            view.ToggleTest();

            Assert.That(view.TestRunning, Is.False);
            Assert.That(view.Notices, Does.Contain(SettingsStyle.MicrophoneTest.UnavailableMessage));
        }

        [Test]
        public void Opening_ShowsEveryKey_SensitivityAndReversal()
        {
            using var presenter = Started();

            Assert.That(view.Bindings.Count, Is.EqualTo(20));
            Assert.That(view.Bindings[ControlAction.MoveForward], Is.EqualTo("W"));
            Assert.That(view.Bindings[ControlAction.Interact], Is.EqualTo("F"));
            Assert.That(view.Bindings[ControlAction.PrimaryAction], Is.EqualTo("좌클릭"));
            Assert.That(view.Bindings[ControlAction.VoiceToggle], Is.EqualTo("B"));
            Assert.That(view.Bindings[ControlAction.ToggleSpeaker], Is.EqualTo("T"));
            Assert.That(view.Bindings[ControlAction.Jump], Is.EqualTo("SPACE"));
            Assert.That(view.Bindings[ControlAction.RaiseObject], Is.EqualTo("스크롤 ↑"));
            Assert.That(view.Bindings[ControlAction.LowerObject], Is.EqualTo("스크롤 ↓"));
            Assert.That(view.Bindings[ControlAction.ToggleKeyGuide], Is.EqualTo("L"));
            Assert.That(view.Sensitivities.Count, Is.EqualTo(3));
            Assert.That(view.Sensitivities[ControlSensitivity.FirstPersonMouse], Is.EqualTo(50));
            Assert.That(view.Reversals.Count, Is.EqualTo(4));
            Assert.That(view.Reversals[ControlToggle.FirstPersonInvertX], Is.EqualTo("끄기"));
            Assert.That(view.Listening, Is.Null);
        }

        [Test]
        public void ClickingAKey_WaitsForAPress_ThenPutsItOnTheAction()
        {
            using var presenter = Started();

            view.ClickKey(ControlAction.Jump);

            Assert.That(keyCapture.IsCapturing, Is.True);
            Assert.That(view.Listening, Is.EqualTo(ControlAction.Jump));
            Assert.That(view.ActionsEnabled, Is.False, "Waiting is not yet a change.");

            Assert.That(view.ClicksBlocked, Is.True, "Or the press would work whatever it lands on.");

            keyCapture.Press("k");

            Assert.That(presenter.ControlDraft.Get(ControlAction.Jump), Is.EqualTo("k"));
            Assert.That(view.Bindings[ControlAction.Jump], Is.EqualTo("K"));
            Assert.That(view.Listening, Is.Null);
            Assert.That(view.ClicksBlocked, Is.False);
            Assert.That(view.ActionsEnabled, Is.True);
            Assert.That(controlStore.Saved, Is.Null, "Nothing is settled until apply.");
        }

        /// <summary>
        /// The design puts mouse buttons on several actions, so one has to be
        /// as bindable as a key. The screen stops taking clicks while it waits,
        /// which is what makes that possible.
        /// </summary>
        /// <remarks>
        /// The wheel button rather than the left one, which the 주 조작 group
        /// already holds and will not give up.
        /// </remarks>
        [Test]
        public void AMouseButton_CanBePutOnAnAction()
        {
            using var presenter = Started();

            view.ClickKey(ControlAction.Jump);
            keyCapture.Press(ControlCatalog.MouseMiddle);

            Assert.That(
                presenter.ControlDraft.Get(ControlAction.Jump), Is.EqualTo(ControlCatalog.MouseMiddle));
            Assert.That(view.Bindings[ControlAction.Jump], Is.EqualTo("휠클릭"));
            Assert.That(view.ClicksBlocked, Is.False, "And the screen answers again afterwards.");
        }

        [Test]
        public void TheLeftButton_IsRefusedToAnActionThatDoesNotHaveIt()
        {
            using var presenter = Started();

            view.ClickKey(ControlAction.Jump);
            keyCapture.Press(ControlCatalog.MouseLeft);

            Assert.That(presenter.ControlDraft.Get(ControlAction.Jump), Is.EqualTo("space"));
            Assert.That(
                presenter.ControlDraft.Get(ControlAction.PrimaryAction),
                Is.EqualTo(ControlCatalog.MouseLeft));
            Assert.That(view.Notices.Count, Is.EqualTo(1));
            Assert.That(
                view.Notices[0],
                Does.Contain("공격/던지기/배치"),
                "The refusal names who has it, so the player knows what to move first.");
        }

        [Test]
        public void TheScreen_TakesClicksAgainAfterAWaitIsAbandoned()
        {
            using var presenter = Started();
            view.ClickKey(ControlAction.Jump);

            keyCapture.PressEscape();

            Assert.That(view.ClicksBlocked, Is.False);
        }

        [Test]
        public void Escape_DuringACapture_LeavesTheKeyAsItWas()
        {
            using var presenter = Started();
            view.ClickKey(ControlAction.Jump);

            keyCapture.PressEscape();

            Assert.That(presenter.ControlDraft.Get(ControlAction.Jump), Is.EqualTo("space"));
            Assert.That(view.Listening, Is.Null);
            Assert.That(view.ActionsEnabled, Is.False);
        }

        [Test]
        public void ClickingASecondKey_MovesTheWaitToIt()
        {
            using var presenter = Started();
            view.ClickKey(ControlAction.Jump);

            view.ClickKey(ControlAction.Crouch);

            Assert.That(view.Listening, Is.EqualTo(ControlAction.Crouch));
            Assert.That(keyCapture.BeginCount, Is.EqualTo(2));

            keyCapture.Press("n");

            Assert.That(presenter.ControlDraft.Get(ControlAction.Crouch), Is.EqualTo("n"));
            Assert.That(
                presenter.ControlDraft.Get(ControlAction.Jump),
                Is.EqualTo("space"),
                "The first row was left alone.");
        }

        /// <summary>
        /// Nobody shares a key any more. The two groups that used to are one
        /// row each now, so the screen's rule has no exception left to make.
        /// </summary>
        [Test]
        public void NoTwoActions_ShipOnTheSameKey()
        {
            var seen = new Dictionary<string, ControlAction>();

            foreach (ControlAction action in Enum.GetValues(typeof(ControlAction)))
            {
                var code = ControlCatalog.Defaults.Get(action);
                Assert.That(code, Is.Not.Empty, $"{action} ships with no key.");
                Assert.That(
                    seen.ContainsKey(code),
                    Is.False,
                    $"{action} ships on {code}, which {(seen.TryGetValue(code, out var held) ? held.ToString() : string.Empty)} already has.");
                seen[code] = action;
            }
        }

        /// <summary>
        /// 물건 상호작용 is 들기·놓기·파괴장치 상호작용 and 공격/던지기/배치 is the
        /// three the left button did, so the key table's single lines stay
        /// single lines however the player moves them.
        /// </summary>
        [TestCase(ControlAction.Interact, "f", "k")]
        [TestCase(ControlAction.PrimaryAction, ControlCatalog.MouseLeft, "k")]
        public void AMergedRow_CarriesItsWholeGroupsKey(
            ControlAction action, string shipped, string moved)
        {
            using var presenter = Started();
            Assert.That(presenter.ControlDraft.Get(action), Is.EqualTo(shipped));

            view.ClickKey(action);
            keyCapture.Press(moved);

            Assert.That(presenter.ControlDraft.Get(action), Is.EqualTo(moved));
            Assert.That(view.Notices, Is.Empty, "Nobody else was on that key to be disturbed.");
        }

        /// <summary>
        /// Y ends the player's hiding turn, read straight off the keyboard by
        /// the match HUD. No row may take it, and the refusal says who has it.
        /// </summary>
        [TestCase("y", "숨기기 완료")]
        [TestCase("1", "캐릭터 단축키")]
        [TestCase("numpad1", "캐릭터 단축키")]
        [TestCase("2", "참가자 목록 단축키")]
        [TestCase("numpad2", "참가자 목록 단축키")]
        [TestCase("escape", "환경설정 메뉴")]
        [TestCase("enter", "채팅")]
        [TestCase("numpadEnter", "채팅")]
        public void AReservedKey_IsRefused_AndWhatHoldsItIsNamed(string code, string holder)
        {
            using var presenter = Started();

            view.ClickKey(ControlAction.Jump);
            keyCapture.Press(code);

            Assert.That(presenter.ControlDraft.Get(ControlAction.Jump), Is.EqualTo("space"));
            Assert.That(view.Notices.Count, Is.EqualTo(1));
            Assert.That(view.Notices[0], Does.Contain(holder));
            Assert.That(view.Listening, Is.Null);
        }

        [Test]
        public void NoRow_ShipsOnAReservedKey()
        {
            foreach (ControlAction action in Enum.GetValues(typeof(ControlAction)))
            {
                Assert.That(
                    ControlCatalog.IsReserved(ControlCatalog.Defaults.Get(action), out var holder),
                    Is.False,
                    $"{action} ships on a key that belongs to {holder}.");
            }
        }

        /// <summary>
        /// Nothing is taken from anybody: the key stays where it was and the
        /// row that asked for it keeps what it had.
        /// </summary>
        [Test]
        public void AKeyInUseElsewhere_IsRefused_AndWhoHasItIsNamed()
        {
            using var presenter = Started();

            view.ClickKey(ControlAction.Jump);
            keyCapture.Press("v");

            Assert.That(
                presenter.ControlDraft.Get(ControlAction.Jump),
                Is.EqualTo("space"),
                "Jump kept the key it had.");
            Assert.That(
                presenter.ControlDraft.Get(ControlAction.ToggleView),
                Is.EqualTo("v"),
                "And the holder kept its own.");
            Assert.That(view.Notices.Count, Is.EqualTo(1));
            Assert.That(view.Notices[0], Does.Contain("시점 변경"));
            Assert.That(view.ActionsEnabled, Is.False, "Nothing changed, so there is nothing to apply.");
            Assert.That(view.Listening, Is.Null, "And the wait is over either way.");
        }

        /// <summary>
        /// The way through, once a key is refused: move the holder first.
        /// </summary>
        [Test]
        public void AKeyFreedByMovingItsHolder_CanThenBeTaken()
        {
            using var presenter = Started();

            view.ClickKey(ControlAction.ToggleView);
            keyCapture.Press("k");

            view.ClickKey(ControlAction.Jump);
            keyCapture.Press("v");

            Assert.That(presenter.ControlDraft.Get(ControlAction.Jump), Is.EqualTo("v"));
            Assert.That(presenter.ControlDraft.Get(ControlAction.ToggleView), Is.EqualTo("k"));
            Assert.That(view.Notices, Is.Empty);
        }

        [Test]
        public void ACapture_IsAbandonedByLeaving()
        {
            using var presenter = Started();
            view.ClickKey(ControlAction.Jump);

            view.Back();

            Assert.That(keyCapture.IsCapturing, Is.False);
            Assert.That(view.Listening, Is.Null);
        }

        [Test]
        public void ACapture_IsAbandonedByDisposal()
        {
            var presenter = Started();
            view.ClickKey(ControlAction.Jump);

            presenter.Dispose();

            Assert.That(keyCapture.IsCapturing, Is.False);
        }

        [Test]
        public void DraggingASensitivity_MovesOnlyThatRow()
        {
            using var presenter = Started();

            view.DragSensitivity(ControlSensitivity.ThirdPersonCamera, 90);

            Assert.That(
                presenter.ControlDraft.Get(ControlSensitivity.ThirdPersonCamera), Is.EqualTo(90));
            Assert.That(
                presenter.ControlDraft.Get(ControlSensitivity.FirstPersonMouse), Is.EqualTo(50));
            Assert.That(view.Sensitivities[ControlSensitivity.ThirdPersonCamera], Is.EqualTo(90));
            Assert.That(view.ActionsEnabled, Is.True);
        }

        [Test]
        public void SteppingAReversal_TurnsItOn()
        {
            using var presenter = Started();

            view.StepReversal(ControlToggle.FirstPersonInvertY, 1);

            Assert.That(presenter.ControlDraft.IsOn(ControlToggle.FirstPersonInvertY), Is.True);
            Assert.That(view.Reversals[ControlToggle.FirstPersonInvertY], Is.EqualTo("켜기"));
        }

        [Test]
        public void Apply_SettlesTheControlsTab()
        {
            using var presenter = Started();
            view.ClickKey(ControlAction.Jump);
            keyCapture.Press("k");
            view.DragSensitivity(ControlSensitivity.FirstPersonMouse, 30);

            view.Apply();

            Assert.That(controls.Current.Get(ControlAction.Jump), Is.EqualTo("k"));
            Assert.That(controls.Current.Get(ControlSensitivity.FirstPersonMouse), Is.EqualTo(30));
            Assert.That(controlStore.Saved, Is.EqualTo(controls.Current));
            Assert.That(view.ActionsEnabled, Is.False);
        }

        [Test]
        public void Reset_OnTheControlsTab_PutsEveryKeyBack_AndAbandonsAnyWait()
        {
            using var presenter = Started();
            view.ClickKey(ControlAction.Jump);
            keyCapture.Press("k");
            view.SelectTab(SettingsTab.Controls);
            view.ClickKey(ControlAction.Crouch);

            view.Reset();
            Assert.That(view.ConfirmTab, Is.EqualTo(SettingsTab.Controls));
            view.Accept();

            Assert.That(presenter.ControlDraft, Is.EqualTo(controls.Defaults));
            Assert.That(view.Bindings[ControlAction.Jump], Is.EqualTo("SPACE"));
            Assert.That(keyCapture.IsCapturing, Is.False);
            Assert.That(view.Listening, Is.Null);
        }

        [Test]
        public void Rebinding_IsRefusedWhileAConfirmationIsUp()
        {
            using var presenter = Started();
            view.DragSensitivity(ControlSensitivity.FirstPersonMouse, 10);
            view.Back();

            view.ClickKey(ControlAction.Jump);

            Assert.That(keyCapture.IsCapturing, Is.False);
            Assert.That(view.Listening, Is.Null);
        }

        [Test]
        public void Opening_ShowsTheInviteNoticeOn()
        {
            using var presenter = Started();

            Assert.That(view.NotificationLabels.Count, Is.EqualTo(1));
            Assert.That(
                view.NotificationLabels[NotificationOption.GameInvite],
                Is.EqualTo("켜기"),
                "A player who has never opened the tab has not asked to stop hearing.");
        }

        [Test]
        public void SteppingTheInviteNotice_TurnsItOff_WithoutApplying()
        {
            using var presenter = Started();

            view.StepNotification(NotificationOption.GameInvite, 1);

            Assert.That(presenter.NotificationDraft.IsOn(NotificationOption.GameInvite), Is.False);
            Assert.That(view.NotificationLabels[NotificationOption.GameInvite], Is.EqualTo("끄기"));
            Assert.That(view.ActionsEnabled, Is.True);
            Assert.That(noticeStore.Saved, Is.Null);
        }

        [Test]
        public void Apply_SettlesTheNotificationsTab()
        {
            using var presenter = Started();
            view.StepNotification(NotificationOption.GameInvite, 1);

            view.Apply();

            Assert.That(notifications.Current.IsOn(NotificationOption.GameInvite), Is.False);
            Assert.That(noticeStore.Saved, Is.EqualTo(notifications.Current));
            Assert.That(view.ActionsEnabled, Is.False);
        }

        [Test]
        public void Reset_OnTheNotificationsTab_LeavesTheOtherTabsAlone()
        {
            using var presenter = Started();
            view.StepLanguage(1);
            view.StepNotification(NotificationOption.GameInvite, 1);
            view.SelectTab(SettingsTab.Notifications);

            view.Reset();
            Assert.That(view.ConfirmTab, Is.EqualTo(SettingsTab.Notifications));
            view.Accept();

            Assert.That(presenter.NotificationDraft, Is.EqualTo(notifications.Defaults));
            Assert.That(presenter.GeneralDraft.LanguageCode, Is.EqualTo("en"));
        }

        [Test]
        public void ResetAll_PutsTheNotificationsTabBackToo()
        {
            using var presenter = Started();
            view.StepNotification(NotificationOption.GameInvite, 1);

            view.ResetAll();
            view.Accept();

            Assert.That(presenter.NotificationDraft, Is.EqualTo(notifications.Defaults));
            Assert.That(view.ActionsEnabled, Is.False);
        }

        [Test]
        public void Feedback_OpensTheWritingPanel_WithSendUnavailable()
        {
            using var presenter = Started();

            view.Feedback();

            Assert.That(view.FeedbackVisible, Is.True);
            Assert.That(view.SubmitEnabled, Is.False, "Nothing written yet.");
            Assert.That(view.Notices, Is.Empty, "Opening the panel says nothing.");
        }

        [Test]
        public void Feedback_Send_ComesAliveOnlyOnceSomethingIsWritten()
        {
            using var presenter = Started();
            view.Feedback();

            view.TypeFeedback("   ");
            Assert.That(view.SubmitEnabled, Is.False, "Whitespace alone is not feedback.");

            view.TypeFeedback("소리가 너무 작아요");
            Assert.That(view.SubmitEnabled, Is.True);

            view.TypeFeedback(string.Empty);
            Assert.That(view.SubmitEnabled, Is.False);
        }

        [Test]
        public void Feedback_Send_DarkensTheButtonAndSaysNothingItself()
        {
            // The sending is SettingsFeedbackBridge's, and only the answer can
            // decide what the screen says. This presenter's whole part is
            // stopping a second press from starting a second send.
            using var presenter = Started();
            view.Feedback();
            view.TypeFeedback("소리가 너무 작아요");

            view.SubmitFeedback("소리가 너무 작아요");

            Assert.That(view.SubmitEnabled, Is.False, "A second press must not send again.");
            Assert.That(view.Notices, Is.Empty, "Only the answer has something to say.");
            Assert.That(
                view.FeedbackVisible,
                Is.True,
                "Closing it before the answer would throw away what was written.");
        }

        [Test]
        public void Feedback_Sent_TakesThePanelDownAndFreesTheScreen()
        {
            // What the bridge calls when the server confirmed it. The panel is
            // down and the settings behind it answer again — that second part
            // is why FeedbackSent exists instead of a bare HideFeedback.
            using var presenter = Started();
            view.Feedback();
            view.TypeFeedback("소리가 너무 작아요");
            view.SubmitFeedback("소리가 너무 작아요");

            view.FeedbackSent();

            Assert.That(view.FeedbackVisible, Is.False);
            view.SelectTab(SettingsTab.Sound);
            Assert.That(view.ShownTab, Is.EqualTo(SettingsTab.Sound));
        }

        [Test]
        public void Feedback_Send_WithNothingWritten_DoesNothing()
        {
            using var presenter = Started();
            view.Feedback();

            view.SubmitFeedback("   ");

            Assert.That(view.Notices, Is.Empty);
        }

        [Test]
        public void Feedback_Dismissed_ClosesThePanel()
        {
            using var presenter = Started();
            view.Feedback();

            view.DismissFeedback();

            Assert.That(view.FeedbackVisible, Is.False);

            // And the screen behind it answers again.
            view.SelectTab(SettingsTab.Sound);
            Assert.That(view.ShownTab, Is.EqualTo(SettingsTab.Sound));
        }

        [Test]
        public void WhileTheWritingPanelIsUp_TheSettingsBehindItAreDeaf()
        {
            using var presenter = Started();
            view.StepLanguage(1);
            view.Feedback();

            view.StepLanguage(1);
            view.SelectTab(SettingsTab.Sound);
            view.Apply();
            view.Reset();
            view.ResetAll();
            view.Back();

            Assert.That(presenter.GeneralDraft.LanguageCode, Is.EqualTo("en"));
            Assert.That(view.ShownTab, Is.EqualTo(SettingsTab.General));
            Assert.That(store.Saved, Is.Null);
            Assert.That(view.ConfirmVisible, Is.False);
            Assert.That(host.HomeOpenCount, Is.EqualTo(0));
        }

        [Test]
        public void Feedback_IsRefusedWhileAConfirmationIsUp()
        {
            using var presenter = Started();
            view.StepLanguage(1);
            view.Back();

            view.Feedback();

            Assert.That(view.FeedbackVisible, Is.False);
            Assert.That(view.ConfirmKind, Is.EqualTo(SettingsConfirmKind.Discard));
        }

        [Test]
        public void AChangeAppliedElsewhere_IsReflectedInTheButtons()
        {
            using var presenter = Started();
            view.StepLanguage(1);
            Assert.That(view.ActionsEnabled, Is.True);

            // Something else settles on the very language the draft holds.
            general.Apply(new GeneralSettings("en"));

            Assert.That(view.ActionsEnabled, Is.False);
        }

        [Test]
        public void Dispose_StopsListening()
        {
            var presenter = Started();
            presenter.Dispose();

            view.StepLanguage(1);

            Assert.That(view.LanguageLabel, Is.EqualTo("한국어"));
        }

        [Test]
        public void DiscardAndReopen_DoesNotRestoreUnappliedSensitivity()
        {
            using var presenter = Started();
            view.DragSensitivity(ControlSensitivity.FirstPersonMouse, 90);
            view.Back();
            view.Decline();
            view.Reopen();
            Assert.That(presenter.ControlDraft.Get(ControlSensitivity.FirstPersonMouse), Is.EqualTo(50));
            Assert.That(view.ActionsEnabled, Is.False);
        }

        [Test]
        public void LobbyClose_KeepsNetworkFlowAndAppliesOnlyConfirmedDraft()
        {
            flow.TryTransitionTo(AppFlowState.Home);
            flow.TryTransitionTo(AppFlowState.Lobby);
            var closed = 0;
            using var presenter = new SettingsPresenter(view, general, graphics, ui, sound,
                microphoneTest, controls, keyCapture, notifications, host, flow, () => closed++);
            presenter.Start();
            view.DragSensitivity(ControlSensitivity.FirstPersonMouse, 80);
            view.Back();
            Assert.That(closed, Is.Zero);
            view.Accept();
            Assert.That(closed, Is.EqualTo(1));
            Assert.That(flow.CurrentState, Is.EqualTo(AppFlowState.Lobby));
            Assert.That(new ControlSettingsSystem(controlStore).Current.Get(ControlSensitivity.FirstPersonMouse), Is.EqualTo(80));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Container_ResolvesHomeAndLobbySettings(bool lobby)
        {
            var builder = new ContainerBuilder();
            builder.RegisterInstance<ISettingsView>(view);
            builder.RegisterInstance(general);
            builder.RegisterInstance(graphics);
            builder.RegisterInstance(ui);
            builder.RegisterInstance(sound);
            builder.RegisterInstance<IMicrophoneTest>(microphoneTest);
            builder.RegisterInstance(controls);
            builder.RegisterInstance<IKeyCapture>(keyCapture);
            builder.RegisterInstance(notifications);
            builder.RegisterInstance<IHomeApplicationHost>(host);
            builder.RegisterInstance(flow);
            var registration = builder.Register<SettingsPresenter>(Lifetime.Scoped);
            var closed = false;
            if (lobby) registration.WithParameter<Action>(() => closed = true);
            else registration.WithParameter<Action>((Action)null);
            using var container = builder.Build();
            var presenter = container.Resolve<SettingsPresenter>();
            presenter.Start();
            view.Back();
            Assert.That(closed, Is.EqualTo(lobby));
            Assert.That(flow.CurrentState, Is.EqualTo(lobby ? AppFlowState.Settings : AppFlowState.Home));
        }

        [Test]
        public void ReopenedCachedScreen_DiscardsDraftAfterExternalNavigation()
        {
            using var presenter = Started();
            view.DragSensitivity(ControlSensitivity.ThirdPersonMouse, 10);
            view.Reopen();
            Assert.That(presenter.ControlDraft.Get(ControlSensitivity.ThirdPersonMouse), Is.EqualTo(50));
            Assert.That(view.ActionsEnabled, Is.False);
        }

        private SettingsPresenter Started()
        {
            var presenter = new SettingsPresenter(
                view, general, graphics, ui, sound, microphoneTest, controls, keyCapture,
                notifications, host, flow);
            presenter.Start();
            return presenter;
        }

        private sealed class ClosedMicrophoneTest : IMicrophoneTest
        {
            public bool IsRunning => false;

            public void Start(string deviceName)
            {
            }

            public void Stop()
            {
            }
        }
    }
}
