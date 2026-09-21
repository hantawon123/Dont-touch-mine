using Game.Bootstrap;
using Game.Core.Settings;
using NUnit.Framework;
using UnityEngine;

namespace Game.Architecture.Tests
{
    /// <summary>
    /// First person versus third person survives a new scene the way the
    /// player left it.
    /// </summary>
    public sealed class CameraViewPreferenceTests
    {
        [Test]
        public void EmptyStore_StartsInThirdPerson()
        {
            var prefs = new CameraViewPreference(new InMemoryCameraViewStore());

            Assert.That(prefs.FirstPerson, Is.False);
        }

        [Test]
        public void SavedView_ComesBackOnAFreshInstance()
        {
            var store = new InMemoryCameraViewStore();
            var first = new CameraViewPreference(store);
            first.FirstPerson = true;

            var nextScene = new CameraViewPreference(store);

            Assert.That(nextScene.FirstPerson, Is.True);
        }

        [Test]
        public void UnchangedView_DoesNotWriteAgain()
        {
            var store = new InMemoryCameraViewStore();
            var prefs = new CameraViewPreference(store);

            prefs.FirstPerson = false;

            Assert.That(store.Saved, Is.Null);
        }
    }

    /// <summary>
    /// Touches the machine's own preferences, so every key it uses is put back
    /// afterwards.
    /// </summary>
    public sealed class PlayerPrefsCameraViewStoreTests
    {
        private const string FirstPersonKey = "game.camera.firstPerson";

        private bool hadFirstPerson;
        private int firstPerson;

        [SetUp]
        public void RememberWhatWasThere()
        {
            hadFirstPerson = PlayerPrefs.HasKey(FirstPersonKey);
            firstPerson = hadFirstPerson ? PlayerPrefs.GetInt(FirstPersonKey) : 0;
            PlayerPrefs.DeleteKey(FirstPersonKey);
        }

        [TearDown]
        public void PutItBack()
        {
            PlayerPrefs.DeleteKey(FirstPersonKey);
            if (hadFirstPerson)
            {
                PlayerPrefs.SetInt(FirstPersonKey, firstPerson);
            }

            PlayerPrefs.Save();
        }

        [Test]
        public void Save_ThenLoad_KeepsTheView()
        {
            var store = new PlayerPrefsCameraViewStore();
            store.Save(firstPerson: true);

            var other = new PlayerPrefsCameraViewStore();
            Assert.That(other.TryLoad(out var saved), Is.True);
            Assert.That(saved, Is.True);
        }

        [Test]
        public void EmptyKey_LeavesThirdPerson()
        {
            var store = new PlayerPrefsCameraViewStore();

            Assert.That(store.TryLoad(out var saved), Is.False);
            Assert.That(saved, Is.False);
        }
    }
}
