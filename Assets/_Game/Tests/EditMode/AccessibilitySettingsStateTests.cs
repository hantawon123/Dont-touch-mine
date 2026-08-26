using Game.Core.Settings;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class AccessibilitySettingsStateTests
    {
        [Test]
        public void Defaults_AreMidScaleWithHighContrastOff()
        {
            var settings = new AccessibilitySettingsState();

            Assert.That(settings.UiScale, Is.EqualTo(AccessibilitySettingsState.DefaultScale));
            Assert.That(settings.TextScale, Is.EqualTo(AccessibilitySettingsState.DefaultScale));
            Assert.That(settings.HighContrastEnabled, Is.False);
            Assert.That(settings.GetUiScaleMultiplier(), Is.EqualTo(1f));
            Assert.That(settings.GetTextScaleMultiplier(), Is.EqualTo(1f));
        }

        [Test]
        public void Constructor_ClampsOutOfRangeScales()
        {
            var settings = new AccessibilitySettingsState(-20, 140, true);

            Assert.That(settings.UiScale, Is.EqualTo(0));
            Assert.That(settings.TextScale, Is.EqualTo(100));
            Assert.That(settings.HighContrastEnabled, Is.True);
        }

        [Test]
        public void TrySetScale_RejectsOutOfRange()
        {
            var settings = new AccessibilitySettingsState();

            Assert.That(settings.TrySetUiScale(-1, out var error), Is.False);
            Assert.That(error, Is.EqualTo(AccessibilitySettingsError.InvalidScale));
            Assert.That(settings.UiScale, Is.EqualTo(AccessibilitySettingsState.DefaultScale));

            Assert.That(settings.TrySetTextScale(101, out error), Is.False);
            Assert.That(error, Is.EqualTo(AccessibilitySettingsError.InvalidScale));
            Assert.That(settings.TextScale, Is.EqualTo(AccessibilitySettingsState.DefaultScale));
        }

        [Test]
        public void Multipliers_MapSmallMediumAndLarge()
        {
            var settings = new AccessibilitySettingsState();

            Assert.That(settings.TrySetUiScale(0, out _), Is.True);
            Assert.That(
                settings.GetUiScaleMultiplier(),
                Is.EqualTo(AccessibilitySettingsState.MinUiMultiplier).Within(0.0001f));

            Assert.That(settings.TrySetUiScale(100, out _), Is.True);
            Assert.That(
                settings.GetUiScaleMultiplier(),
                Is.EqualTo(AccessibilitySettingsState.MaxUiMultiplier).Within(0.0001f));

            Assert.That(settings.TrySetTextScale(0, out _), Is.True);
            Assert.That(
                settings.GetTextScaleMultiplier(),
                Is.EqualTo(AccessibilitySettingsState.MinTextMultiplier).Within(0.0001f));

            Assert.That(settings.TrySetTextScale(100, out _), Is.True);
            Assert.That(
                settings.GetTextScaleMultiplier(),
                Is.EqualTo(AccessibilitySettingsState.MaxTextMultiplier).Within(0.0001f));
        }
    }
}
