using Game.Bootstrap;
using Game.Core.Voice;
using NUnit.Framework;
using UnityEngine;

namespace Game.Architecture.Tests
{
    /// <summary>
    /// The microphone and speaker switches survive a new room the way the
    /// player left them.
    /// </summary>
    public sealed class VoicePreferencesTests
    {
        [Test]
        public void EmptyStore_StartsUnmutedAndListening()
        {
            var prefs = new VoicePreferences(new InMemoryVoicePreferencesStore());

            Assert.That(prefs.Muted, Is.False);
            Assert.That(prefs.Listening, Is.True);
        }

        [Test]
        public void SavedSwitches_ComeBackOnAFreshInstance()
        {
            var store = new InMemoryVoicePreferencesStore();
            var first = new VoicePreferences(store);
            first.Muted = true;
            first.Listening = false;

            var nextRoom = new VoicePreferences(store);

            Assert.That(nextRoom.Muted, Is.True);
            Assert.That(nextRoom.Listening, Is.False);
        }

        [Test]
        public void UnchangedSwitch_DoesNotWriteAgain()
        {
            var store = new InMemoryVoicePreferencesStore();
            var prefs = new VoicePreferences(store);

            prefs.Muted = false;

            Assert.That(store.Saved, Is.Null);
        }

        [Test]
        public void BothSwitches_AreRememberedTogether()
        {
            var store = new InMemoryVoicePreferencesStore();
            var prefs = new VoicePreferences(store);
            prefs.Muted = true;

            Assert.That(store.Saved, Is.EqualTo((true, true)));

            prefs.Listening = false;

            Assert.That(store.Saved, Is.EqualTo((true, false)));
        }
    }

    /// <summary>
    /// Touches the machine's own preferences, so every key it uses is put back
    /// afterwards.
    /// </summary>
    public sealed class PlayerPrefsVoicePreferencesStoreTests
    {
        private const string MutedKey = "game.voice.muted";
        private const string ListeningKey = "game.voice.listening";

        private bool hadMuted;
        private bool hadListening;
        private int muted;
        private int listening;

        [SetUp]
        public void RememberWhatWasThere()
        {
            hadMuted = PlayerPrefs.HasKey(MutedKey);
            hadListening = PlayerPrefs.HasKey(ListeningKey);
            muted = hadMuted ? PlayerPrefs.GetInt(MutedKey) : 0;
            listening = hadListening ? PlayerPrefs.GetInt(ListeningKey) : 0;
            PlayerPrefs.DeleteKey(MutedKey);
            PlayerPrefs.DeleteKey(ListeningKey);
        }

        [TearDown]
        public void PutItBack()
        {
            PlayerPrefs.DeleteKey(MutedKey);
            PlayerPrefs.DeleteKey(ListeningKey);
            if (hadMuted)
            {
                PlayerPrefs.SetInt(MutedKey, muted);
            }

            if (hadListening)
            {
                PlayerPrefs.SetInt(ListeningKey, listening);
            }

            PlayerPrefs.Save();
        }

        [Test]
        public void Save_ThenLoad_KeepsBothSwitches()
        {
            var store = new PlayerPrefsVoicePreferencesStore();
            store.Save(muted: true, listening: false);

            var other = new PlayerPrefsVoicePreferencesStore();
            Assert.That(other.TryLoad(out var savedMuted, out var savedListening), Is.True);
            Assert.That(savedMuted, Is.True);
            Assert.That(savedListening, Is.False);
        }

        [Test]
        public void EmptyKeys_LeaveTheDefaults()
        {
            var store = new PlayerPrefsVoicePreferencesStore();

            Assert.That(store.TryLoad(out var savedMuted, out var savedListening), Is.False);
            Assert.That(savedMuted, Is.False);
            Assert.That(savedListening, Is.True);
        }
    }
}
