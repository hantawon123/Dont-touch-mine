using System;
using Game.Core.Ports;
using Game.Core.Settings;
using UnityEngine;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>
    /// Plays the chosen microphone back through the speakers so the player
    /// can hear whether it works before applying it.
    /// </summary>
    public sealed class UnityMicrophoneTest : IMicrophoneTest, ITickable, IDisposable
    {
        private const int ClipSeconds = 1;
        internal const int FallbackSampleRate = 44100;

        private GameObject host;
        private AudioSource source;
        private string openedDevice;
        private bool recording;
        private bool waitingForBuffer;

        public bool IsRunning { get; private set; }

        /// <summary>
        /// The name Unity's <c>Microphone.Start</c> wants, or null for the
        /// machine default. Empty, <see cref="SoundCatalog.DefaultDevice"/>,
        /// and a name the machine no longer has all become null.
        /// </summary>
        public static string ResolveDevice(string deviceName, string[] devices)
        {
            if (string.IsNullOrWhiteSpace(deviceName)
                || string.Equals(deviceName, SoundCatalog.DefaultDevice, StringComparison.Ordinal))
            {
                return null;
            }

            if (devices == null)
            {
                return null;
            }

            for (var i = 0; i < devices.Length; i++)
            {
                if (string.Equals(devices[i], deviceName, StringComparison.Ordinal))
                {
                    return deviceName;
                }
            }

            return null;
        }

        /// <summary>
        /// A rate the device accepts. Unity reports 0–0 when any rate is
        /// fine; otherwise 44100 if it sits in range, else the device maximum.
        /// </summary>
        public static int ChooseSampleRate(int minFreq, int maxFreq)
        {
            if (minFreq <= 0 && maxFreq <= 0)
            {
                return FallbackSampleRate;
            }

            if (minFreq <= FallbackSampleRate && (maxFreq <= 0 || FallbackSampleRate <= maxFreq))
            {
                return FallbackSampleRate;
            }

            return maxFreq > 0 ? maxFreq : minFreq;
        }

        public void Start(string deviceName)
        {
            Stop();

#if UNITY_WEBGL && !UNITY_EDITOR
            return;
#else
            try
            {
                var device = ResolveDevice(deviceName, Microphone.devices);
                Microphone.GetDeviceCaps(device, out var minFreq, out var maxFreq);
                var clip = Microphone.Start(device, true, ClipSeconds, ChooseSampleRate(minFreq, maxFreq));
                if (clip == null)
                {
                    return;
                }

                openedDevice = device;
                recording = true;

                host = new GameObject("Microphone Test");
                host.hideFlags = HideFlags.HideAndDontSave;
                if (Application.isPlaying)
                {
                    UnityEngine.Object.DontDestroyOnLoad(host);
                }

                source = host.AddComponent<AudioSource>();
                source.clip = clip;
                source.loop = true;
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.volume = 1f;
                waitingForBuffer = true;
                IsRunning = true;
            }
            catch (Exception)
            {
                TearDown();
            }
#endif
        }

        public void Tick()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return;
#else
            if (!waitingForBuffer || source == null)
            {
                return;
            }

            if (Microphone.GetPosition(openedDevice) > 0)
            {
                source.Play();
                waitingForBuffer = false;
            }
#endif
        }

        public void Stop() => TearDown();

        public void Dispose() => Stop();

        private void TearDown()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (source != null)
            {
                source.volume = 0f;
                source.Stop();
                source.clip = null;
            }

            if (recording)
            {
                try
                {
                    if (Microphone.IsRecording(openedDevice))
                    {
                        Microphone.End(openedDevice);
                    }
                }
                catch (Exception)
                {
                    // The device was unplugged or already closed.
                }
            }
#endif
            if (host != null)
            {
                // Immediate: a deferred Destroy leaves the loopback in the
                // mixer for the rest of the frame, which is heard as noise
                // on the BGM as it fades back in.
                UnityEngine.Object.DestroyImmediate(host);
                host = null;
            }

            source = null;
            openedDevice = null;
            recording = false;
            waitingForBuffer = false;
            IsRunning = false;
        }
    }
}
