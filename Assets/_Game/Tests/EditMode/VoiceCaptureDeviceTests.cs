using Game.Core.Settings;
using Game.Core.Voice;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 사운드 탭이 고른 마이크가 녹음기까지 갈 수 있는 이름으로 나오는지.
    /// </summary>
    /// <remarks>
    /// 이게 없던 동안 장치 선택은 설정 화면의 마이크 테스트에만 반영되고
    /// 실제 통화는 계속 기본 장치로 나갔다.
    /// </remarks>
    public sealed class VoiceCaptureDeviceTests
    {
        [TestCase("머리에 거는 수화기 (2- 태원의 Buds4)")]
        [TestCase("Headset (AirPods Pro 2)")]
        public void ReconnectedDeviceIsSelectedAgainWithoutChangingSavedChoice(string saved)
        {
            Assert.That(VoiceCaptureDevice.ResolveAvailable(saved, new[] { saved }), Is.EqualTo(saved));
            Assert.That(VoiceCaptureDevice.ResolveAvailable(saved, new[] { "USB Mic" }), Is.Null);
            Assert.That(VoiceCaptureDevice.ResolveAvailable(saved, new[] { "USB Mic", saved }), Is.EqualTo(saved));
        }

        [Test]
        public void ChosenDevice_IsPassedThroughByName()
        {
            Assert.That(
                VoiceCaptureDevice.Requested("마이크(Realtek(R) Audio)"),
                Is.EqualTo("마이크(Realtek(R) Audio)"));
        }

        [TestCase(SoundCatalog.DefaultDevice)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        public void NoRealChoice_AsksForNothing(string saved)
        {
            // "기본 장치"는 장치가 아니라 코드다. 그대로 넘기면 녹음기가
            // default 라는 이름의 마이크를 찾다가 못 찾는다.
            Assert.That(VoiceCaptureDevice.Requested(saved), Is.Empty);
        }

        [Test]
        public void ReadsTheNameOffTheSavedSettings()
        {
            var settings = SoundSettings.Empty.WithDevice("Headset Mic");

            Assert.That(VoiceCaptureDevice.Requested(settings), Is.EqualTo("Headset Mic"));
        }

        [Test]
        public void SettingsLeftOnTheDefault_AskForNothing()
        {
            var settings = SoundSettings.Empty.WithDevice(SoundCatalog.DefaultDevice);

            Assert.That(VoiceCaptureDevice.Requested(settings), Is.Empty);
        }
    }
}
