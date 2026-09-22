using Game.Bootstrap;
using Game.Client.Cameras;
using Game.Core.Settings;
using NUnit.Framework;
using UnityEditor;
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

        [Test]
        public void LobbyRig_Toggle_IsWhatTheMatchRigOpensWith()
        {
            var preference = new CameraViewPreference(new InMemoryCameraViewStore());
            var settings = new ControlSettingsSystem(new InMemoryControlSettingsStore());
            var lobby = InstantiateRig();
            var match = InstantiateRig();
            try
            {
                lobby.BindSettings(settings, preference);
                lobby.SetPreferredView(true);

                match.BindSettings(settings, preference);

                Assert.That(preference.FirstPerson, Is.True);
                Assert.That(lobby.IsFirstPerson, Is.True);
                Assert.That(match.IsFirstPerson, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(lobby.gameObject);
                Object.DestroyImmediate(match.gameObject);
            }
        }

        [Test]
        public void MatchRig_Toggle_IsWhatTheLobbyRigOpensWith()
        {
            var preference = new CameraViewPreference(new InMemoryCameraViewStore());
            var settings = new ControlSettingsSystem(new InMemoryControlSettingsStore());
            var match = InstantiateRig();
            var lobby = InstantiateRig();
            try
            {
                match.BindSettings(settings, preference);
                match.SetPreferredView(true);

                lobby.BindSettings(settings, preference);

                Assert.That(preference.FirstPerson, Is.True);
                Assert.That(match.IsFirstPerson, Is.True);
                Assert.That(lobby.IsFirstPerson, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(match.gameObject);
                Object.DestroyImmediate(lobby.gameObject);
            }
        }

        [Test]
        public void MatchRig_ToggleBackToThirdPerson_IsWhatTheLobbyRigOpensWith()
        {
            var preference = new CameraViewPreference(new InMemoryCameraViewStore());
            var settings = new ControlSettingsSystem(new InMemoryControlSettingsStore());
            var match = InstantiateRig();
            var lobby = InstantiateRig();
            try
            {
                match.BindSettings(settings, preference);
                match.SetPreferredView(true);
                match.SetPreferredView(false);

                lobby.BindSettings(settings, preference);

                Assert.That(preference.FirstPerson, Is.False);
                Assert.That(lobby.IsFirstPerson, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(match.gameObject);
                Object.DestroyImmediate(lobby.gameObject);
            }
        }

        private static PlayerCameraController InstantiateRig()
        {
            return Object.Instantiate(LoadRigPrefab()).GetComponent<PlayerCameraController>();
        }

        private static GameObject LoadRigPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Content/Prefabs/PlayerCameraRig.prefab");
            Assert.That(prefab, Is.Not.Null);
            return prefab;
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
