using Game.Client.Match;
using Game.Client.Voice;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.Architecture.Tests
{
    public sealed class VoiceViewTests
    {
        [Test]
        public void SetState_UsesOnlyWhiteMicAndGreySlash()
        {
            var root = new GameObject("Voice", typeof(RectTransform));
            try
            {
                root.SetActive(false);
                var view = root.AddComponent<VoiceView>();
                var button = CreateMuteButton(root.transform);
                var icon = button.transform.Find(VoiceView.IconName).GetComponent<Image>();
                view.BindMuteControl(button, button.GetComponent<Image>(), icon);
                root.SetActive(true);

                Assert.That(VoiceView.MicOnSprite, Is.Not.Null);
                Assert.That(VoiceView.MicOffSprite, Is.Not.Null);
                Assert.That(VoiceView.MicOnResource, Is.EqualTo("UI/Icon_Mic_White"));
                Assert.That(VoiceView.MicOffResource, Is.EqualTo("UI/Icon_Mic_Off_Gray"));

                view.SetState(available: true, muted: false, latched: true, transmitting: true, listening: true);
                Assert.That(icon.sprite, Is.EqualTo(VoiceView.MicOnSprite));

                view.SetState(available: true, muted: true, latched: false, transmitting: false, listening: true);
                Assert.That(icon.sprite, Is.EqualTo(VoiceView.MicOffSprite));
                Assert.That(button.interactable, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SpeakerButton_RaisesToggle()
        {
            var root = new GameObject("Voice", typeof(RectTransform));
            try
            {
                root.SetActive(false);
                var view = root.AddComponent<VoiceView>();
                var bar = VoiceView.EnsureBar(root.transform);
                view.BindBar(bar);
                root.SetActive(true);

                var speaker = bar.Find(VoiceView.SpeakerButtonName).GetComponent<Button>();
                var raised = 0;
                view.SpeakerToggleRequested += () => raised++;
                speaker.onClick.Invoke();
                Assert.That(raised, Is.EqualTo(1));

                view.SetState(true, false, false, false, false);
                Assert.That(
                    speaker.transform.Find(VoiceView.IconName).GetComponent<Image>().sprite,
                    Is.EqualTo(VoiceView.SpeakerOffSprite));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MuteButton_RaisesToggle()
        {
            var root = new GameObject("Voice", typeof(RectTransform));
            try
            {
                root.SetActive(false);
                var view = root.AddComponent<VoiceView>();
                var button = CreateMuteButton(root.transform);
                view.BindMuteControl(button, button.GetComponent<Image>(), null);
                root.SetActive(true);

                var raised = 0;
                view.MuteToggleRequested += () => raised++;
                button.onClick.Invoke();

                Assert.That(raised, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MatchHud_ShowsTheSameIconButton()
        {
            var canvas = new GameObject(
                "MatchHud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var hud = canvas.AddComponent<NetworkMatchHudView>();
                var voice = hud.EnsureVoiceControl();
                var bar = canvas.transform.Find(VoiceView.BarName) as RectTransform;
                var slot = bar.Find(VoiceView.ButtonName) as RectTransform;
                var speaker = bar.Find(VoiceView.SpeakerButtonName) as RectTransform;
                var icon = slot.Find(VoiceView.IconName).GetComponent<Image>();

                Assert.That(voice, Is.Not.Null);
                Assert.That(bar.gameObject.activeSelf, Is.True);
                Assert.That(slot.GetComponent<Image>().color, Is.EqualTo(VoiceView.PlateColor));
                Assert.That(icon.sprite, Is.EqualTo(VoiceView.MicOnSprite));
                Assert.That(speaker, Is.Not.Null);
                Assert.That(speaker.GetSiblingIndex(), Is.GreaterThan(slot.GetSiblingIndex()));
                Assert.That(
                    bar.anchoredPosition,
                    Is.EqualTo(new Vector2(
                        -VoiceView.CornerMarginRight,
                        VoiceView.CornerMarginBottom)));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        private static Button CreateMuteButton(Transform parent)
        {
            var slot = new GameObject(
                VoiceView.ButtonName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            slot.transform.SetParent(parent, false);
            var icon = new GameObject(
                VoiceView.IconName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            icon.transform.SetParent(slot.transform, false);
            return slot.GetComponent<Button>();
        }
    }

    public sealed class VoicePresenterTests
    {
        [Test]
        public void VoiceToggle_FlipsMuteLikeTheHudButton()
        {
            var view = new FakeVoiceView();
            var voice = new FakeVoiceControl(muted: false);
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            try
            {
                var presenter = new VoicePresenter(view, voice, asset);
                presenter.HandleVoiceToggle();
                Assert.That(voice.IsMuted.CurrentValue, Is.True);
                view.PaintedMuted = false;

                presenter.HandleVoiceToggle();
                Assert.That(voice.IsMuted.CurrentValue, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void SpeakerOff_AlsoMutesTheMicrophone()
        {
            var view = new FakeVoiceView();
            var voice = new FakeVoiceControl(muted: false);
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            try
            {
                var presenter = new VoicePresenter(view, voice, asset);
                presenter.HandleSpeakerToggle();
                Assert.That(voice.IsListening.CurrentValue, Is.False);
                Assert.That(voice.IsMuted.CurrentValue, Is.True);

                presenter.HandleSpeakerToggle();
                Assert.That(voice.IsListening.CurrentValue, Is.True);
                Assert.That(voice.IsMuted.CurrentValue, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void SpeakerOff_IgnoresMicrophoneUnmute()
        {
            var view = new FakeVoiceView();
            var voice = new FakeVoiceControl(muted: false);
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            try
            {
                var presenter = new VoicePresenter(view, voice, asset);
                presenter.HandleSpeakerToggle();
                presenter.HandleVoiceToggle();

                Assert.That(voice.IsListening.CurrentValue, Is.False);
                Assert.That(voice.IsMuted.CurrentValue, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        private sealed class FakeVoiceView : IVoiceView
        {
            public event System.Action MuteToggleRequested;
            public event System.Action SpeakerToggleRequested;
            public bool PaintedMuted { get; set; }
            public bool PaintedListening { get; set; }

            public void SetState(bool available, bool muted, bool latched, bool transmitting, bool listening)
            {
                PaintedMuted = muted;
                PaintedListening = listening;
            }

            public void Raise() => MuteToggleRequested?.Invoke();
        }

        private sealed class FakeVoiceControl : Game.Core.Ports.IVoiceControl
        {
            private readonly R3.ReactiveProperty<bool> muted;
            private readonly R3.ReactiveProperty<bool> available = new(true);
            private readonly R3.ReactiveProperty<bool> transmitting = new(false);
            private readonly R3.ReactiveProperty<bool> listening = new(true);

            public FakeVoiceControl(bool muted)
            {
                this.muted = new R3.ReactiveProperty<bool>(muted);
            }

            public R3.ReadOnlyReactiveProperty<bool> IsAvailable => available;
            public R3.ReadOnlyReactiveProperty<bool> IsMuted => muted;
            public R3.ReadOnlyReactiveProperty<bool> IsTransmitting => transmitting;
            public R3.ReadOnlyReactiveProperty<bool> IsListening => listening;

            public void SetMuted(bool value)
            {
                if (!value && !listening.Value)
                {
                    return;
                }

                muted.Value = value;
            }

            public void SetTalking(bool talking)
            {
            }

            public void SetListening(bool value)
            {
                listening.Value = value;
                if (!value)
                {
                    muted.Value = true;
                }
            }
        }
    }
}
