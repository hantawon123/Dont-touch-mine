using Game.Network.Voice;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Photon.Voice.Unity;

namespace Game.Tests.EditMode
{
    public sealed class VoiceCaptureLimiterTests
    {
        [Test]
        public void QuietSignalPreservesSliderGainAndZeroMutes()
        {
            var processor = new VoiceCaptureLimiter.Processor(48000) { Gain = 2f };
            var samples = new[] { .1f, -.2f, 0f };
            Assert.That(processor.Process(samples), Is.SameAs(samples));
            Assert.That(samples, Is.EqualTo(new[] { .2f, -.4f, 0f }));
            processor.Gain = 0f;
            processor.Process(samples);
            Assert.That(samples, Is.All.Zero);
        }

        [Test]
        public void BoostedPeakIsBoundedWithoutFlatteningWaveform_AndRecovers()
        {
            var processor = new VoiceCaptureLimiter.Processor(48000) { Gain = 2f };
            var samples = new[] { 1f, .5f, -1f, -.5f };
            processor.Process(samples);
            Assert.That(samples[0], Is.EqualTo(.98f).Within(.00001f));
            Assert.That(samples[1], Is.EqualTo(.49f).Within(.00001f));
            Assert.That(samples[2], Is.EqualTo(-.98f).Within(.00001f));
            var quiet = new float[4800];
            quiet[0] = .1f;
            processor.Process(quiet);
            Assert.That(quiet[0], Is.InRange(.1f, .2f));
            processor.Process(new float[48000]);
            var recovered = new[] { .1f };
            processor.Process(recovered);
            Assert.That(recovered[0], Is.EqualTo(.2f).Within(.0001f));
        }

        [Test]
        public void ShortSamplesDoNotOverflowOrReversePolarity()
        {
            var processor = new VoiceCaptureLimiter.Processor(48000) { Gain = 2f };
            var samples = new[] { short.MaxValue, short.MinValue, (short)16000, (short)-16000 };
            Assert.That(processor.Process(samples), Is.SameAs(samples));
            Assert.That(samples[0], Is.InRange(1, 32113));
            Assert.That(samples[1], Is.InRange(-32113, -1));
            Assert.That(samples[2], Is.GreaterThan(0));
            Assert.That(samples[3], Is.LessThan(0));
        }

        [Test]
        public void QuietInputIsDetectedAfterCaptureGain_AndMuteStillStopsSending()
        {
            using var transport = new Photon.Voice.Realtime5Transport();
            var info = Photon.Voice.VoiceInfo.CreateAudioOpus(
                POpusCodec.Enums.SamplingRate.Sampling48000, 1,
                Photon.Voice.OpusCodec.FrameDuration.Frame20ms, 32000);
            var voice = transport.VoiceClient.CreateLocalVoiceAudio<float>(
                info, new Photon.Voice.AudioDesc(48000, 1, null), 0,
                new Photon.Voice.VoiceCreateOptions { Encoder = Photon.Voice.OpusCodec.Factory.CreateEncoder<float[]>(info, new Photon.Voice.Unity.Logger(Photon.Voice.LogLevel.Error)) });
            voice.TransmitEnabled = true;
            voice.VoiceDetector.On = true;
            voice.VoiceDetector.Threshold = .01f;
            voice.VoiceDetector.ActivityDelayMs = 0;
            var host = new GameObject("Voice detection order test");
            try
            {
                host.AddComponent<VoiceCaptureLimiter>().AmplificationFactor = 2f;
                typeof(VoiceCaptureLimiter).GetMethod("PhotonVoiceCreated",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(host.GetComponent<VoiceCaptureLimiter>(), new object[] { new PhotonVoiceCreatedParams { Voice = voice } });
                var quiet = new float[960];
                System.Array.Fill(quiet, .006f);
                voice.PushData(quiet);
                Assert.That(voice.VoiceDetector.Detected, Is.True,
                    "Quiet audio must reach capture gain before the detector can discard it.");
                voice.PushData(new float[960]);
                Assert.That(voice.VoiceDetector.Detected, Is.False, "Silence stays gated.");
                voice.TransmitEnabled = false;
                System.Array.Fill(quiet, .5f);
                voice.PushData(quiet);
                Assert.That(voice.VoiceDetector.Detected, Is.False, "Mute still stops processing.");
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void CaptureConfigurationKeepsOneDetectorAndOneDspAcrossRepeatedSetup()
        {
            var host = new GameObject("Capture configuration test");
            try
            {
                var recorder = host.AddComponent<Recorder>();
                recorder.RecordingEnabled = false;
                VoiceCaptureLimiter.Configure(recorder);
                VoiceCaptureLimiter.Configure(recorder);
                var dsp = host.GetComponent<WebRtcAudioDsp>();
                Assert.That(dsp.AGC && dsp.NoiseSuppression, Is.True);
                Assert.That(dsp.VAD || dsp.AEC, Is.False);
                Assert.That(dsp.AgcCompressionGain, Is.EqualTo(9));
                Assert.That(dsp.AgcTargetLevel, Is.EqualTo(3));
                Assert.That(host.GetComponents<WebRtcAudioDsp>().Length, Is.EqualTo(1));
                Assert.That(host.GetComponents<VoiceCaptureLimiter>().Length, Is.EqualTo(1));
                Assert.That(recorder.RecordingEnabled, Is.False);
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void NetworkPrefabUsesFullBandVoiceWithExistingPacketDuration()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Content/Prefabs/NetworkedPlayer.prefab");
            var recorder = prefab.GetComponentInChildren<Recorder>(true);
            var settings = new SerializedObject(recorder);
            Assert.That(settings.FindProperty("samplingRate").intValue, Is.EqualTo(48000));
            Assert.That(recorder.Bitrate, Is.EqualTo(32000));
            Assert.That(recorder.MicrophoneType, Is.EqualTo(Recorder.MicType.Unity),
                "Settings, microphone test and voice capture must use the same device names.");
            Assert.That(settings.FindProperty("frameDuration").intValue, Is.EqualTo(20000));
            Assert.That(prefab.GetComponentInChildren<VoiceCaptureLimiter>(true), Is.Null,
                "The capture chain is attached only to the local recorder at runtime.");
        }
    }
}
