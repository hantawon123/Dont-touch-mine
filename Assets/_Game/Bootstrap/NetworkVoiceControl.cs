using System;
using Game.Core.Ports;
using Game.Core.Settings;
using Game.Core.Voice;
using Game.Network.Session;
using R3;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>
    /// Hands the player's microphone choices to whichever voice rig is current,
    /// and reports back what that rig is doing.
    /// </summary>
    /// <remarks>
    /// The rig that actually opens the microphone is a component on the runner
    /// object, so it is built when a session starts and destroyed when one ends.
    /// A screen given that rig directly would be holding a destroyed component
    /// after the first room. This stands between them.
    /// <para>
    /// Scoped to the screens that offer a microphone rather than to the app. On
    /// the home and room-list screens there is no rig to mirror and no button to
    /// paint, and something that ticks where it has no work is one more thing
    /// the next reader has to rule out. What has to outlive the screen is the
    /// mute choice alone, and that lives in <see cref="VoicePreferences"/>.
    /// </para>
    /// </remarks>
    public sealed class NetworkVoiceControl : IVoiceControl, ITickable, IDisposable
    {
        private readonly NetworkRunnerService network;
        private readonly VoicePreferences preferences;
        private readonly SoundSettingsSystem sound;
        private readonly ReactiveProperty<bool> available = new(false);
        private readonly ReactiveProperty<bool> muted;
        private readonly ReactiveProperty<bool> transmitting = new(false);
        private readonly ReactiveProperty<bool> listening;

        /// <summary>
        /// The rig these choices were last handed to, so a replacement can be
        /// told about them.
        /// </summary>
        private IVoiceControl current;

        private bool talking;
        private bool disposed;

        public NetworkVoiceControl(
            NetworkRunnerService network,
            VoicePreferences preferences,
            SoundSettingsSystem sound)
        {
            this.network = network ?? throw new ArgumentNullException(nameof(network));
            this.preferences = preferences
                ?? throw new ArgumentNullException(nameof(preferences));
            this.sound = sound ?? throw new ArgumentNullException(nameof(sound));

            // Opens on whatever the player last decided, which is how a mute set
            // in the lobby survives the walk into the match. 입력 모드 끄기 is
            // the same silence, from the sound tab.
            muted = new ReactiveProperty<bool>(EffectiveMute);
            listening = new ReactiveProperty<bool>(preferences.Listening);
            this.sound.Changed += OnSoundChanged;
        }

        public ReadOnlyReactiveProperty<bool> IsAvailable => available;
        public ReadOnlyReactiveProperty<bool> IsMuted => muted;
        public ReadOnlyReactiveProperty<bool> IsTransmitting => transmitting;
        public ReadOnlyReactiveProperty<bool> IsListening => listening;

        public void SetMuted(bool muted)
        {
            if (disposed) return;
            preferences.Muted = muted;
            PublishEffectiveMute();
        }

        public void SetTalking(bool talking)
        {
            if (disposed) return;
            this.talking = talking;
            PublishTalking();
        }

        public void SetListening(bool listening)
        {
            if (disposed) return;
            preferences.Listening = listening;
            PublishListening();
            if (!listening)
            {
                SetMuted(true);
            }
        }

        /// <remarks>
        /// Mirrors rather than forwards the rig's own properties: they belong to
        /// the rig and go away with it, and a screen that subscribed to them
        /// would have to resubscribe on every room change.
        /// </remarks>
        public void Tick()
        {
            if (disposed) return;
            var voice = network.Voice;
            if (voice == null)
            {
                current = null;
                available.Value = false;
                transmitting.Value = false;
                return;
            }

            if (!ReferenceEquals(voice, current))
            {
                // A session just started. The rig comes up silent and knowing
                // nothing, so it hears what the player already decided.
                current = voice;
                voice.SetMuted(EffectiveMute);
                voice.SetTalking(EffectiveTalking);
                voice.SetListening(preferences.Listening);
            }

            available.Value = voice.IsAvailable.CurrentValue;
            transmitting.Value = voice.IsTransmitting.CurrentValue;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            sound.Changed -= OnSoundChanged;
            available.Dispose();
            muted.Dispose();
            transmitting.Dispose();
            listening.Dispose();
        }

        private bool EffectiveMute =>
            VoiceMutePolicy.IsMuted(preferences.Muted, sound.Current.InputMode);

        private bool EffectiveTalking =>
            VoiceMutePolicy.IsTalking(talking, sound.Current.InputMode);

        private void OnSoundChanged(SoundSettings _)
        {
            if (disposed)
            {
                return;
            }

            PublishEffectiveMute();
            PublishTalking();
        }

        private void PublishEffectiveMute()
        {
            var next = EffectiveMute;
            muted.Value = next;
            network.Voice?.SetMuted(next);
        }

        private void PublishTalking()
        {
            network.Voice?.SetTalking(EffectiveTalking);
        }

        private void PublishListening()
        {
            var next = preferences.Listening;
            listening.Value = next;
            network.Voice?.SetListening(next);
        }
    }
}
