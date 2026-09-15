using System;
using Game.Client.Players;
using Game.Core.Ports;
using R3;
using UnityEngine.InputSystem;
using VContainer.Unity;

namespace Game.Client.Voice
{
    /// <summary>
    /// Turns the talk keys, the microphone button, and the speaker button into
    /// what the voice layer hears and plays.
    /// </summary>
    /// <remarks>
    /// Holding 마이크 송출 suits a sentence thrown across the room. 마이크 고정
    /// and the HUD mic button are the same on/off. The speaker button and T
    /// decide whether this machine hears the room, without leaving voice.
    /// Closing the speaker also mutes the microphone so a silent room is
    /// not still sending.
    /// </remarks>
    public sealed class VoicePresenter : IStartable, ITickable, IDisposable
    {
        private readonly IVoiceView view;
        private readonly IVoiceControl voice;
        private readonly InputActionAsset inputActions;
        private IDisposable stateSubscription;
        private InputAction holdAction;
        private InputAction toggleAction;
        private InputAction listenAction;

        /// <summary>
        /// Whether the microphone was latched open, as opposed to held open.
        /// </summary>
        private bool latched;

        public VoicePresenter(
            IVoiceView view,
            IVoiceControl voice,
            InputActionAsset inputActions)
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.voice = voice ?? throw new ArgumentNullException(nameof(voice));
            this.inputActions = inputActions
                ?? throw new ArgumentNullException(nameof(inputActions));
        }

        public void Start()
        {
            view.MuteToggleRequested += ToggleMute;
            view.SpeakerToggleRequested += ToggleListen;

            var player = inputActions.FindActionMap("Player", throwIfNotFound: true);
            holdAction = player.FindAction("PushToTalk", throwIfNotFound: true);
            toggleAction = player.FindAction("VoiceToggle", throwIfNotFound: true);
            listenAction = player.FindAction("VoiceListen", throwIfNotFound: true);

            stateSubscription = voice.IsAvailable
                .CombineLatest(
                    voice.IsMuted,
                    voice.IsTransmitting,
                    (available, muted, transmitting) =>
                        (available, muted, transmitting))
                .CombineLatest(
                    voice.IsListening,
                    (state, listening) =>
                        (state.available, state.muted, state.transmitting, listening))
                .Subscribe(state => Paint(
                    state.available, state.muted, state.transmitting, state.listening));
        }

        public void Dispose()
        {
            view.MuteToggleRequested -= ToggleMute;
            view.SpeakerToggleRequested -= ToggleListen;
            stateSubscription?.Dispose();

            // A key could be down, or the latch on, as the screen goes away, and
            // the microphone would stay open into whatever comes next.
            latched = false;
            voice.SetTalking(false);
        }

        public void Tick()
        {
            if (holdAction == null || toggleAction == null || listenAction == null)
            {
                return;
            }

            // Chat takes the keyboard while it is focused, and these keys belong
            // to the message being typed then, not to the microphone.
            if (PlayerMovement.IsTextInputFocused())
            {
                voice.SetTalking(latched);
                return;
            }

            if (toggleAction.WasPressedThisFrame())
            {
                HandleVoiceToggle();
            }

            if (listenAction.WasPressedThisFrame())
            {
                HandleSpeakerToggle();
            }

            voice.SetTalking(latched || holdAction.IsPressed());
        }

        /// <summary>
        /// 마이크 고정 (VoiceToggle) is the same on/off as the HUD button, so
        /// the white mic and the grey slash follow the key as well as the click.
        /// </summary>
        internal void HandleVoiceToggle() => ToggleMute();

        internal void HandleSpeakerToggle() => ToggleListen();

        /// <remarks>
        /// Muting drops the latch rather than overruling it. Unmuting would
        /// otherwise reopen a microphone the player had muted a conversation
        /// ago, which is the surprise the button exists to prevent.
        /// </remarks>
        private void ToggleMute()
        {
            var muted = !voice.IsMuted.CurrentValue;
            if (muted)
            {
                latched = false;
                voice.SetTalking(false);
            }

            voice.SetMuted(muted);
        }

        private void ToggleListen()
        {
            var next = !voice.IsListening.CurrentValue;
            if (!next)
            {
                // Leaving the room's playback also stops talking. The latch
                // drops the same way a mute does, so turning the speaker back
                // on does not reopen a microphone the player just closed.
                latched = false;
                voice.SetTalking(false);
                voice.SetMuted(true);
            }

            voice.SetListening(next);
        }

        private void Paint(bool available, bool muted, bool transmitting, bool listening) =>
            view.SetState(available, muted, latched, transmitting, listening);
    }
}
