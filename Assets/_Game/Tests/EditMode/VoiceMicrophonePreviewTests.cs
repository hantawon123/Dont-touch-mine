using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Network.Voice;
using NUnit.Framework;
using Photon.Voice;
using Photon.Voice.Unity;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.EditMode
{
    public sealed class VoiceMicrophonePreviewTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator PreviewProcessesOpusLocallyAndRestoresCaptureAcrossRestartAndFailure()
        {
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                UnityEditor.SceneManagement.NewSceneMode.Single);
            yield return new EnterPlayMode();
            var avatar = new GameObject("Live microphone state");
            var recorder = avatar.AddComponent<Recorder>();
            recorder.RecordWhenJoined = true;
            recorder.TransmitEnabled = false;
            var rig = avatar.AddComponent<VoiceRig>();
            typeof(VoiceRig).GetField("boundRecorder", Private).SetValue(rig, recorder);
            var preview = new VoiceMicrophonePreview();
            try
            {
                for (var attempt = 0; attempt < 2; attempt++)
                {
                    var input = new TestInput();
                    preview.StartInput(() => input, 2f);
                    Assert.That(preview.IsRunning, Is.True);
                    Assert.That(recorder.RecordingEnabled, Is.False);
                    Assert.That(recorder.RecordWhenJoined, Is.False);
                    var host = (GameObject)typeof(VoiceMicrophonePreview).GetField("host", Private).GetValue(preview);
                    var voice = (LocalVoiceAudioShort)typeof(VoiceMicrophonePreview).GetField("voice", Private).GetValue(preview);
                    var encoder = (OpusCodec.Encoder<short>)typeof(VoiceMicrophonePreview).GetField("encoder", Private).GetValue(preview);
                    var client = (Realtime5Transport)typeof(VoiceMicrophonePreview).GetField("transport", Private).GetValue(preview);
                    var packets = 0;
                    var decode = encoder.Output;
                    encoder.Output = (data, flags) =>
                    {
                        if (data.Count > 0) System.Threading.Interlocked.Increment(ref packets);
                        decode(data, flags);
                    };
                    for (var i = 0; i < 40; i++)
                    {
                        input.Ready = true;
                        preview.Tick();
                        yield return new WaitForSecondsRealtime(.01f);
                    }
                    preview.Tick();
                    Assert.That(packets, Is.GreaterThan(0), "Actual Opus packets must reach the local decoder.");
                    Assert.That(client.IsConnected, Is.False, "The test must never open a server connection.");
                    var clip = host.GetComponent<AudioSource>().clip;
                    var samples = new float[clip.samples * clip.channels];
                    Assert.That(clip.GetData(samples, 0), Is.True);
                    Assert.That(samples.Any(x => Math.Abs(x) > .001f), Is.True, "Decoded PCM must reach playback.");
                    Assert.That(voice.Info.SamplingRate, Is.EqualTo(48000));
                    Assert.That(voice.Info.Bitrate, Is.EqualTo(32000));

                    // Reproduce the SDK reattaching DSP on output-device changes.
                    typeof(WebRtcAudioDsp).GetMethod("Restart", Private).Invoke(host.GetComponent<WebRtcAudioDsp>(), null);
                    typeof(VoiceCaptureLimiter).GetMethod("OnAudioConfigurationChanged", Private)
                        .Invoke(host.GetComponent<VoiceCaptureLimiter>(), new object[] { true });
                    var chain = (List<IProcessor<short>>)typeof(LocalVoiceFramed<short>).GetField("processors", Private).GetValue(voice);
                    Assert.That(chain[chain.Count - 1], Is.SameAs(voice.VoiceDetector));
                    Assert.That(chain[chain.Count - 2], Is.TypeOf<VoiceCaptureLimiter.Processor>());
                    Assert.That(chain.Count(x => x is WebRTCAudioProcessor), Is.EqualTo(1));
                    Assert.That(chain.Count(x => x is VoiceCaptureLimiter.Processor), Is.EqualTo(1));

                    // Same-device gain changes update the running chain, not the microphone.
                    preview.Start(null, 0f);
                    Assert.That(typeof(VoiceMicrophonePreview).GetField("host", Private).GetValue(preview), Is.SameAs(host));
                    Assert.That(host.GetComponent<VoiceCaptureLimiter>().AmplificationFactor, Is.Zero);
                    preview.Stop();
                    preview.Stop();
                    Assert.That(input.Disposed, Is.True);
                    Assert.That(preview.IsRunning, Is.False);
                    Assert.That(host == null, Is.True);
                    Assert.That(recorder.RecordingEnabled, Is.True);
                    Assert.That(recorder.RecordWhenJoined, Is.True);
                    Assert.That(recorder.TransmitEnabled, Is.False, "Testing cannot unmute actual chat.");
                }
                preview.StartInput(() => throw new InvalidOperationException("Expected unavailable device"), 1f);
                Assert.That(preview.IsRunning, Is.False);
                Assert.That(recorder.RecordingEnabled && recorder.RecordWhenJoined, Is.True);
                Assert.That(recorder.TransmitEnabled, Is.False);
            }
            finally { preview.Dispose(); Object.DestroyImmediate(avatar); }
            yield return new ExitPlayMode();
        }

        private sealed class TestInput : IAudioReader<short>
        {
            public int SamplingRate => 48000;
            public int Channels => 1;
            public string Error => null;
            public bool Ready, Disposed;
            private int position;
            public bool Read(short[] buffer)
            {
                if (!Ready) return false;
                Ready = false;
                for (var i = 0; i < buffer.Length; i++)
                    buffer[i] = (short)(5000 * Math.Sin(2 * Math.PI * 300 * position++ / SamplingRate));
                return true;
            }
            public void Dispose() => Disposed = true;
        }
    }
}
