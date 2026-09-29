using Game.Bootstrap;
using Game.Core.Settings;
using NUnit.Framework;
using UnityEngine;

namespace Game.Architecture.Tests
{
    /// <summary>
    /// What happens to a player who answered 내 닉네임 표시 before it became
    /// 스트리머 모드.
    /// </summary>
    /// <remarks>
    /// Touches the machine's own preferences, so every key it uses is put back
    /// afterwards. Runs in the Unity editor rather than the standalone runner,
    /// which has no <see cref="PlayerPrefs"/>.
    /// </remarks>
    public sealed class InterfaceSettingsMigrationTests
    {
        private const string Retired = "game.settings.interface.OwnNickname";
        private const string Current = "game.settings.interface.StreamerMode";

        private bool hadRetired, hadCurrent;
        private string retired, current;

        [SetUp]
        public void RememberWhatWasThere()
        {
            hadRetired = PlayerPrefs.HasKey(Retired);
            hadCurrent = PlayerPrefs.HasKey(Current);
            retired = hadRetired ? PlayerPrefs.GetString(Retired) : null;
            current = hadCurrent ? PlayerPrefs.GetString(Current) : null;
            PlayerPrefs.DeleteKey(Retired);
            PlayerPrefs.DeleteKey(Current);
        }

        [TearDown]
        public void PutItBack()
        {
            PlayerPrefs.DeleteKey(Retired);
            PlayerPrefs.DeleteKey(Current);
            if (hadRetired) PlayerPrefs.SetString(Retired, retired);
            if (hadCurrent) PlayerPrefs.SetString(Current, current);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// The one that matters: a name that was hidden stays hidden, as a
        /// pseudonym now. Arriving as 실명 would publish it without the player
        /// doing anything.
        /// </summary>
        [TestCase("off")]
        [TestCase("friends")]
        public void AHiddenName_ComesBackAsAPseudonym(string saved)
        {
            PlayerPrefs.SetString(Retired, saved);

            Assert.That(new PlayerPrefsInterfaceSettingsStore().TryLoad(out var settings), Is.True);
            Assert.That(settings.Get(InterfaceOption.StreamerMode), Is.EqualTo(InterfaceCatalog.On));
        }

        [Test]
        public void ANameThatWasPublic_StaysPublic()
        {
            PlayerPrefs.SetString(Retired, InterfaceCatalog.On);

            Assert.That(new PlayerPrefsInterfaceSettingsStore().TryLoad(out var settings), Is.True);
            Assert.That(settings.Get(InterfaceOption.StreamerMode), Is.EqualTo(InterfaceCatalog.Off));
        }

        /// <summary>
        /// Once the player has answered the new row, the old one has nothing
        /// left to say — or turning 스트리머 모드 off would not survive a
        /// restart.
        /// </summary>
        [Test]
        public void AnAnsweredRow_IsNotOverruledByTheOldOne()
        {
            PlayerPrefs.SetString(Retired, "off");
            PlayerPrefs.SetString(Current, InterfaceCatalog.Off);

            Assert.That(new PlayerPrefsInterfaceSettingsStore().TryLoad(out var settings), Is.True);
            Assert.That(settings.Get(InterfaceOption.StreamerMode), Is.EqualTo(InterfaceCatalog.Off));
        }

        /// <summary>
        /// With neither row saved there is nothing to carry over, and the
        /// system falls back to the catalogue's own answer.
        /// </summary>
        /// <remarks>
        /// Asks only about this row. Whether the store found anything at all
        /// depends on the other eight rows, which belong to whoever last used
        /// this machine — an earlier version of this test asserted on that and
        /// failed as soon as somebody opened the 인터페이스 tab.
        /// </remarks>
        [Test]
        public void NeitherRowSaved_LeavesTheRowToItsDefault()
        {
            new PlayerPrefsInterfaceSettingsStore().TryLoad(out var settings);

            Assert.That(
                settings.Get(InterfaceOption.StreamerMode),
                Is.Empty,
                "Nothing was carried over, so the catalogue decides.");
        }
    }
}
