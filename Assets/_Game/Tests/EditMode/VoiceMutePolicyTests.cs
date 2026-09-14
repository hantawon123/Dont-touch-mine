using Game.Core.Settings;
using Game.Core.Voice;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class VoiceMutePolicyTests
    {
        [Test]
        public void IsMuted_WhenEitherSwitchIsOff()
        {
            Assert.That(VoiceMutePolicy.IsMuted(false, SoundCatalog.PushToTalk), Is.False);
            Assert.That(VoiceMutePolicy.IsMuted(true, SoundCatalog.PushToTalk), Is.True);
            Assert.That(VoiceMutePolicy.IsMuted(false, SoundCatalog.MicOff), Is.True);
            Assert.That(VoiceMutePolicy.IsMuted(true, SoundCatalog.MicOff), Is.True);
            Assert.That(VoiceMutePolicy.IsMuted(false, SoundCatalog.OpenMic), Is.False);
        }
    }
}
