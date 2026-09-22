using Game.Client.Emotes;
using Game.Core.Emotes;
using Game.Core.Settings;
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace Game.Architecture.Tests
{
    public sealed class EmoteWheelSelectionTests
    {
        [Test]
        public void Digits_MapClockwiseFromOne()
        {
            Assert.That(EmoteWheelSelection.TrySliceFromDigit(1, out var first), Is.True);
            Assert.That(first, Is.EqualTo(0));
            Assert.That(EmoteWheelSelection.TrySliceFromDigit(6, out var last), Is.True);
            Assert.That(last, Is.EqualTo(5));
            Assert.That(EmoteWheelSelection.TrySliceFromDigit(0, out _), Is.False);
            Assert.That(EmoteWheelSelection.TrySliceFromDigit(7, out _), Is.False);
        }

        [Test]
        public void SliceKeys_AreOneThroughSix()
        {
            Assert.That(EmoteWheelSelection.IsSliceKey(Key.Digit1), Is.True);
            Assert.That(EmoteWheelSelection.IsSliceKey(Key.Numpad3), Is.True);
            Assert.That(EmoteWheelSelection.IsSliceKey(Key.Digit7), Is.False);
            Assert.That(EmoteWheelSelection.IsSliceKey(Key.W), Is.False);
            Assert.That(EmoteWheelSelection.IsSliceKey(Key.Escape), Is.False);
        }

        [Test]
        public void Catalog_HasSixNamedSlices()
        {
            Assert.That(EmoteCatalog.All.Length, Is.EqualTo(6));
            Assert.That(EmoteCatalog.All[0].Label, Is.EqualTo("인사"));
            Assert.That(EmoteCatalog.All[1].Label, Is.EqualTo("도발"));
            Assert.That(EmoteCatalog.All[2].Label, Is.EqualTo("모욕"));
            Assert.That(EmoteCatalog.All[3].Label, Is.EqualTo("닭춤"));
            Assert.That(EmoteCatalog.All[4].Label, Is.EqualTo("힙합"));
            Assert.That(EmoteCatalog.All[5].Label, Is.EqualTo("스핀"));
            Assert.That(EmoteCatalog.All[0].Loop, Is.False);
            Assert.That(EmoteCatalog.All[4].Loop, Is.True);
        }

        [Test]
        public void Catalog_LabelsHaveEnglishWords()
        {
            var english = new[] { "Wave", "Taunt", "Insult", "Chicken", "Hip Hop", "Spin" };
            for (var i = 0; i < EmoteCatalog.All.Length; i++)
            {
                Assert.That(
                    UiTextCatalog.Shipped.Get(EmoteCatalog.All[i].LabelKey, "en"),
                    Is.EqualTo(english[i]));
            }

            Assert.That(UiTextCatalog.Shipped.Knows(UiText.Emote.Hint), Is.True);
            Assert.That(UiTextCatalog.Shipped.Knows(UiText.Guide.Emote), Is.True);
            Assert.That(UiTextCatalog.Shipped.Knows(UiText.Settings.ActionEmoteWheel), Is.True);
        }

        [Test]
        public void OneShotEmotes_AreTheGesturesNotTheDances()
        {
            Assert.That(EmoteCatalog.IsOneShotEmoteState("Emote_Wave"), Is.True);
            Assert.That(EmoteCatalog.IsOneShotEmoteState("Emote_Taunt"), Is.True);
            Assert.That(EmoteCatalog.IsOneShotEmoteState("Emote_Insult"), Is.True);
            Assert.That(EmoteCatalog.IsOneShotEmoteState("Emote_Chicken"), Is.False);
            Assert.That(EmoteCatalog.IsOneShotEmoteState("Emote_HipHop"), Is.False);
            Assert.That(EmoteCatalog.IsOneShotEmoteState("Emote_Spin"), Is.False);
            Assert.That(EmoteCatalog.IsOneShotEmoteState("Walk_Forward"), Is.False);
            Assert.That(EmoteCatalog.IsOneShotEmoteState(null), Is.False);
        }
    }
}
