using System;
using Game.Core.Home;
using Game.Core.Lobby;
using Game.Core.Rooms;
using Game.Core.Settings;
using Game.Client.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Architecture.Tests
{
    public sealed class InterfacePresentationTests
    {
        [Test]
        public void APeersName_IsWhatTheyPublished_AndChatScopeStillUsesAccountIds()
        {
            using var room = new RoomBrowserSystem();
            room.SetLocalPlayer("P1");
            room.SetParticipants(new[] { new RoomParticipant("P1", 0, true, "same", "user1"), new RoomParticipant("P2", 1, false, "same", "user2") });
            var store = new InMemoryInterfaceSettingsStore();
            var settings = new InterfaceSettingsSystem(store);
            var friends = new FriendListSystem();
            using var policy = new InterfacePresentation(settings, friends, room);

            Assert.That(policy.Name("P2", "same"), Is.Empty, "unpublished names fail closed");

            policy.SetPublishedName("P2", real: true, pseudonym: null);
            Assert.That(policy.Name("P2", "same"), Is.EqualTo("same"));

            // 스트리머 모드 on the other side: a name, but not their own.
            policy.SetPublishedName("P2", real: false, "익명나그네12");
            Assert.That(policy.Name("P2", "same"), Is.EqualTo("익명나그네12"));
            Assert.That(policy.Name("P2", "same"), Is.Not.EqualTo("same"));

            // The chat row is a separate restriction and still goes by account id.
            settings.Apply(settings.Current.With(InterfaceOption.ChatScope, InterfaceCatalog.On));
            Assert.That(policy.ShowsChat("P2"), Is.False);
            Assert.That(policy.ShowsChat("P1"), Is.True, "Never your own messages.");
            friends.ReplaceFriends(new[] { new FriendSummary("user2", "different nickname", FriendPresence.Offline) });
            Assert.That(policy.ShowsChat("P2"), Is.True);
            Assert.That(
                policy.Name("P2", "same"),
                Is.EqualTo("익명나그네12"),
                "Being a friend does not undo 스트리머 모드.");

            var restored = new InterfaceSettingsSystem(store);
            Assert.That(restored.Current, Is.EqualTo(settings.Current));

            policy.ClearPermissions();
            Assert.That(policy.Name("P2", "same"), Is.Empty);
            Assert.That(policy.InitialVisibilityReady, Is.False);
        }

        [Test]
        public void InitialVisibilityReady_OpensOnceUntilTheRoomIsLeft()
        {
            using var room = new RoomBrowserSystem();
            var settings = new InterfaceSettingsSystem(new InMemoryInterfaceSettingsStore());
            using var policy = new InterfacePresentation(settings, new FriendListSystem(), room);

            Assert.That(policy.InitialVisibilityReady, Is.False);
            Assert.That(policy.HasPublishedName("P2"), Is.False);

            policy.SetPublishedName("P2", real: true, pseudonym: null);
            Assert.That(policy.HasPublishedName("P2"), Is.True);
            Assert.That(policy.InitialVisibilityReady, Is.False);

            policy.MarkInitialVisibilityReady();
            Assert.That(policy.InitialVisibilityReady, Is.True);

            policy.MarkInitialVisibilityReady();
            Assert.That(policy.InitialVisibilityReady, Is.True);

            policy.ClearPermissions();
            Assert.That(policy.InitialVisibilityReady, Is.False);
            Assert.That(policy.HasPublishedName("P2"), Is.False);
        }

        /// <summary>
        /// A player's own name is never replaced, however they have set the
        /// mode: they have to be able to find themselves in the chat.
        /// </summary>
        [Test]
        public void YourOwnName_IsAlwaysYourOwn()
        {
            using var room = new RoomBrowserSystem();
            room.SetLocalPlayer("P1");
            room.SetParticipants(new[] { new RoomParticipant("P1", 0, true, "mine", "user1") });
            var settings = new InterfaceSettingsSystem(new InMemoryInterfaceSettingsStore());
            using var policy = new InterfacePresentation(settings, new FriendListSystem(), room);

            settings.Apply(settings.Current.With(InterfaceOption.StreamerMode, InterfaceCatalog.On));

            Assert.That(policy.Name("P1", "mine"), Is.EqualTo("mine"));
            Assert.That(policy.NameplateName("P1", "mine"), Is.EqualTo("mine"));
        }

        /// <summary>
        /// 다른 플레이어 이름 표시 is about the nameplates over characters and
        /// nothing else. A chat line with no sender would read as nobody's.
        /// </summary>
        [Test]
        public void TurningOffPlayerNames_TakesTheNameplateOnly()
        {
            using var room = new RoomBrowserSystem();
            room.SetLocalPlayer("P1");
            room.SetParticipants(new[] { new RoomParticipant("P1", 0, true, "mine", "user1"), new RoomParticipant("P2", 1, false, "theirs", "user2") });
            var settings = new InterfaceSettingsSystem(new InMemoryInterfaceSettingsStore());
            using var policy = new InterfacePresentation(settings, new FriendListSystem(), room);
            policy.SetPublishedName("P2", real: true, pseudonym: null);

            Assert.That(policy.NameplateName("P2", "theirs"), Is.EqualTo("theirs"));

            settings.Apply(settings.Current.With(InterfaceOption.PlayerNames, InterfaceCatalog.Off));

            Assert.That(policy.NameplateName("P2", "theirs"), Is.Empty);
            Assert.That(
                policy.Name("P2", "theirs"),
                Is.EqualTo("theirs"),
                "The chat, the participant list and the rest still say who it is.");
        }

        [Test]
        public void TwoIndependentViews_NeverRenderAStreamersRealName()
        {
            using var roomA = new RoomBrowserSystem();
            using var roomB = new RoomBrowserSystem();
            roomA.SetLocalPlayer("A"); roomB.SetLocalPlayer("B");
            var peers = new[] { new RoomParticipant("A", 0, true, "SecretA", "accountA"), new RoomParticipant("B", 1, false, "SecretB", "accountB") };
            roomA.SetParticipants(peers); roomB.SetParticipants(peers);
            var settingsA = new InterfaceSettingsSystem(new InMemoryInterfaceSettingsStore());
            var settingsB = new InterfaceSettingsSystem(new InMemoryInterfaceSettingsStore());
            using var policyA = new InterfacePresentation(settingsA, new FriendListSystem(), roomA);
            using var policyB = new InterfacePresentation(settingsB, new FriendListSystem(), roomB);

            // B is happy to be named; A is in 스트리머 모드.
            policyA.SetPublishedName("B", real: true, pseudonym: null);
            policyB.SetPublishedName("A", real: false, "익명파수꾼07");

            var a = MatchChatView.Create(null); var b = MatchChatView.Create(null);
            try
            {
                a.BindPresentation(policyA); b.BindPresentation(policyB);

                b.SetMessages(new[] { new LobbyChatMessage("A", "SecretA", "hello") });
                Assert.That(Texts(b, "SecretA"), Is.False, "A's real name never reaches B.");
                Assert.That(Texts(b, "익명파수꾼07"), Is.True, "But B can still tell who spoke.");

                a.SetMessages(new[] { new LobbyChatMessage("B", "SecretB", "hello") });
                Assert.That(Texts(a, "SecretB"), Is.True);

                // Turning the nameplates off does not empty the chat.
                settingsA.Apply(settingsA.Current.With(InterfaceOption.PlayerNames, InterfaceCatalog.Off));
                a.SetMessages(new[] { new LobbyChatMessage("B", "SecretB", "hello") });
                Assert.That(Texts(a, "SecretB"), Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(a.gameObject); UnityEngine.Object.DestroyImmediate(b.gameObject); }
        }

        private static bool Texts(MatchChatView view, string wanted) =>
            Array.Exists(
                view.GetComponentsInChildren<TMPro.TMP_Text>(true), t => t.text.Contains(wanted));
    }
}
