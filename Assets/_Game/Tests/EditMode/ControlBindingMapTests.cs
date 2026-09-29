using System;
using System.Collections.Generic;
using Game.Core.Settings;
using NUnit.Framework;

namespace Game.Architecture.Tests
{
    /// <summary>
    /// What the 컨트롤 rows promise the input asset, checked without Unity.
    /// The other half — that the asset keeps its side — is
    /// <see cref="ControlBindingContractTests"/>.
    /// </summary>
    public sealed class ControlBindingMapTests
    {
        [Test]
        public void EveryRow_HasSomewhereToWriteItsKey()
        {
            foreach (ControlAction action in Enum.GetValues(typeof(ControlAction)))
            {
                Assert.That(
                    ControlBindingMap.TryTarget(action, out var target),
                    Is.True,
                    $"{action} is a row on the screen with nowhere in the input asset to go.");
                Assert.That(target.ActionName, Is.Not.Empty);
            }
        }

        /// <summary>
        /// Two rows writing to one binding would mean the second silently
        /// undoing the first, which is the failure the 공격/던지기/배치 merge
        /// exists to prevent.
        /// </summary>
        [Test]
        public void NoTwoRows_WriteToTheSameBinding()
        {
            var seen = new Dictionary<string, ControlAction>();

            foreach (ControlAction action in Enum.GetValues(typeof(ControlAction)))
            {
                Assert.That(ControlBindingMap.TryTarget(action, out var target), Is.True);

                var key = target.ToString();
                Assert.That(
                    seen.ContainsKey(key),
                    Is.False,
                    $"{action} writes to {key}, which {(seen.TryGetValue(key, out var held) ? held.ToString() : string.Empty)} already writes to.");
                seen[key] = action;
            }
        }

        [TestCase("w", "<Keyboard>/w")]
        [TestCase("leftShift", "<Keyboard>/leftShift")]
        [TestCase("space", "<Keyboard>/space")]
        [TestCase(ControlCatalog.MouseLeft, "<Mouse>/leftButton")]
        [TestCase(ControlCatalog.MouseRight, "<Mouse>/rightButton")]
        [TestCase(ControlCatalog.MouseMiddle, "<Mouse>/middleButton")]
        [TestCase(ControlCatalog.ScrollUp, "<Mouse>/scroll/up")]
        [TestCase(ControlCatalog.ScrollDown, "<Mouse>/scroll/down")]
        public void ACode_IsSpelledAsTheInputSystemSpellsIt(string code, string path)
        {
            Assert.That(ControlBindingMap.PathOf(code), Is.EqualTo(path));
        }

        /// <summary>
        /// An action with no key is a binding that is off, and the Input System
        /// is told so with an empty path.
        /// </summary>
        [Test]
        public void NoKey_IsAnEmptyPath()
        {
            Assert.That(ControlBindingMap.PathOf(ControlCatalog.Unbound), Is.Empty);
            Assert.That(ControlBindingMap.PathOf(null), Is.Empty);
        }

        /// <summary>
        /// The codes come from <c>UnityKeyCapture</c> and the paths go to the
        /// input asset, so the two directions have to agree or a key would be
        /// saved as one thing and bound as another.
        /// </summary>
        [Test]
        public void EveryShippedKey_SurvivesTheRoundTrip()
        {
            foreach (ControlAction action in Enum.GetValues(typeof(ControlAction)))
            {
                var code = ControlCatalog.Defaults.Get(action);
                var path = ControlBindingMap.PathOf(code);

                Assert.That(
                    ControlBindingMap.TryCodeOf(path, out var back),
                    Is.True,
                    $"{action}'s {path} is not a path this map can read back.");
                Assert.That(back, Is.EqualTo(code), $"{action} came back as something else.");
            }
        }
    }
}
