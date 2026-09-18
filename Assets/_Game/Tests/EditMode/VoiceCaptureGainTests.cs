using Game.Core.Settings;
using Game.Core.Voice;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 마이크 볼륨 슬라이더가 실제 배율로 바뀌는지.
    /// </summary>
    /// <remarks>
    /// 이게 없던 동안 슬라이더는 저장만 되고 읽는 코드가 한 줄도 없었다.
    /// </remarks>
    public sealed class VoiceCaptureGainTests
    {
        [Test]
        public void TheMiddle_LeavesTheMicrophoneAlone()
        {
            // 모두가 기본값에 앉아 있으므로, 여기가 1배가 아니면 읽기 시작한
            // 날 전원의 마이크가 작아진다.
            Assert.That(
                VoiceCaptureGain.From(SoundCatalog.DefaultVolume),
                Is.EqualTo(VoiceCaptureGain.Neutral).Within(0.0001f));
        }

        [Test]
        public void Zero_IsSilence()
        {
            Assert.That(VoiceCaptureGain.From(SoundCatalog.MinVolume), Is.EqualTo(0f));
        }

        [Test]
        public void TheTop_IsTwiceAsLoud()
        {
            Assert.That(
                VoiceCaptureGain.From(SoundCatalog.MaxVolume),
                Is.EqualTo(VoiceCaptureGain.Max).Within(0.0001f));
        }

        [Test]
        public void Unset_ReadsAsTheDefault_NotAsSilence()
        {
            // 한 번도 건드리지 않은 슬라이더는 기본값을 뜻한다. 0 으로 읽으면
            // 설정을 연 적 없는 사람이 음소거된다.
            Assert.That(
                VoiceCaptureGain.From(SoundCatalog.Unset),
                Is.EqualTo(VoiceCaptureGain.Neutral).Within(0.0001f));
        }

        [TestCase(-40)]
        [TestCase(400)]
        public void OutOfRange_IsClampedIntoTheSliderRange(int percent)
        {
            var gain = VoiceCaptureGain.From(percent);

            Assert.That(gain, Is.InRange(0f, VoiceCaptureGain.Max));
        }

        [Test]
        public void ReadsTheSliderOffTheSavedSettings()
        {
            var settings = SoundSettings.Empty.With(SoundVolume.Microphone, 100);

            Assert.That(
                VoiceCaptureGain.From(settings),
                Is.EqualTo(VoiceCaptureGain.Max).Within(0.0001f));
        }

        [Test]
        public void TheOtherSliders_DoNotMoveIt()
        {
            var settings = SoundSettings.Empty
                .With(SoundVolume.Microphone, SoundCatalog.DefaultVolume)
                .With(SoundVolume.Master, 0)
                .With(SoundVolume.Effects, 100);

            Assert.That(
                VoiceCaptureGain.From(settings),
                Is.EqualTo(VoiceCaptureGain.Neutral).Within(0.0001f));
        }
    }
}
