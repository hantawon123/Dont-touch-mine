using System;
using Game.Client.Players;
using Game.Core.Ports;
using R3;
using UnityEngine.InputSystem;
using VContainer.Unity;

namespace Game.Client.Voice
{
    /// <summary>
    /// Turns the two talk keys and the microphone button into what the voice
    /// layer hears.
    /// </summary>
    /// <remarks>
    /// Two ways to be heard, because they answer different moments. Holding a
    /// key suits a sentence thrown across the room and cannot be left on by
    /// accident. Latching it open suits a conversation, where holding a key for
    /// a minute is its own kind of tiring.
    /// <para>
    /// The button is neither: it is the way to be certain of silence, and it
    /// overrules both keys.
    /// </para>
    /// </remarks>
    public sealed class VoicePresenter : IStartable, ITickable, IDisposable
    {
        private readonly IVoiceView view;
        private readonly IVoiceControl voice;
        private readonly InputActionAsset inputActions;
        private IDisposable stateSubscription;
        private InputAction holdAction;
        private InputAction toggleAction;

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

            var player = inputActions.FindActionMap("Player", throwIfNotFound: true);
            holdAction = player.FindAction("PushToTalk", throwIfNotFound: true);
            toggleAction = player.FindAction("VoiceToggle", throwIfNotFound: true);

            stateSubscription = voice.IsAvailable
                .CombineLatest(
                    voice.IsMuted,
                    voice.IsTransmitting,
                    (available, muted, transmitting) =>
                        (available, muted, transmitting))
                .Subscribe(state => Paint(
                    state.available, state.muted, state.transmitting));
        }

        public void Dispose()
        {
            view.MuteToggleRequested -= ToggleMute;
            stateSubscription?.Dispose();

            // A key could be down, or the latch on, as the screen goes away, and
            // the microphone would stay open into whatever comes next.
            latched = false;
            voice.SetTalking(false);
        }

        public void Tick()
        {
            if (holdAction == null || toggleAction == null)
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
                latched = !latched;
                Repaint();
            }

            voice.SetTalking(latched || holdAction.IsPressed());
        }

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

        private void Repaint() => Paint(
            voice.IsAvailable.CurrentValue,
            voice.IsMuted.CurrentValue,
            voice.IsTransmitting.CurrentValue);

        private void Paint(bool available, bool muted, bool transmitting) =>
            view.SetState(available, muted, latched, transmitting);
    }
}
