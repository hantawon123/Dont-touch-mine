using System;
using Game.Core.Ports;
using Game.Network.Voice;
using VContainer.Unity;

namespace Game.Bootstrap
{
    public sealed class UnityMicrophoneTest : IMicrophoneTest, ITickable, IDisposable
    {
        private readonly VoiceMicrophonePreview preview = new();
        public bool IsRunning => preview.IsRunning;
        public static string ResolveDevice(string name, string[] devices) =>
            Game.Core.Voice.VoiceCaptureDevice.ResolveAvailable(name, devices);
        public void Start(string deviceName, float gain = 1f) => preview.Start(deviceName, gain);
        public void Tick() => preview.Tick();
        public void Stop() => preview.Stop();
        public void Dispose() => Stop();
    }
}
