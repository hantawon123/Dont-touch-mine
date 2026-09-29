using System.Collections.Generic;
using Game.Core.Players;
using NUnit.Framework;

namespace Game.Architecture.Tests
{
    public sealed class AvatarAppearanceBoardTests
    {
        [TearDown]
        public void TearDown() => AvatarAppearanceBoard.Clear();

        [Test]
        public void Replace_IsReadableByPlayerIdAndUserId()
        {
            var worn = new AvatarAppearance("body_a", "hood_a", "shoes_a", "face_a");

            AvatarAppearanceBoard.Replace(new List<(string, string, AvatarAppearance)>
            {
                ("p1", "u1", worn)
            });

            Assert.That(AvatarAppearanceBoard.TryGet("p1", out var byPlayer), Is.True);
            Assert.That(byPlayer, Is.EqualTo(worn));
            Assert.That(AvatarAppearanceBoard.TryGet("u1", out var byUser), Is.True);
            Assert.That(byUser, Is.EqualTo(worn));
        }

        [Test]
        public void UnknownId_IsNotFound()
        {
            Assert.That(AvatarAppearanceBoard.TryGet("missing", out _), Is.False);
            Assert.That(AvatarAppearanceBoard.TryGet(" ", out _), Is.False);
            Assert.That(AvatarAppearanceBoard.TryGet(null, out _), Is.False);
        }

        [Test]
        public void SetLocal_SurvivesReplaceClearingTheRoster()
        {
            var local = new AvatarAppearance("body_b", "hood_b", "shoes_b", "face_b");
            var other = new AvatarAppearance("body_a", "hood_a", "shoes_a", "face_a");
            AvatarAppearanceBoard.SetLocal(local);
            AvatarAppearanceBoard.Replace(new List<(string, string, AvatarAppearance)>
            {
                ("p1", "u1", other)
            });
            AvatarAppearanceBoard.Replace(null);

            Assert.That(AvatarAppearanceBoard.HasLocal, Is.True);
            Assert.That(AvatarAppearanceBoard.Local, Is.EqualTo(local));
            Assert.That(AvatarAppearanceBoard.TryGet("p1", out _), Is.False);
        }

        [Test]
        public void Remember_SurvivesReplaceClearingTheRoster()
        {
            var saved = new AvatarAppearance("body_c", "hood_c", "shoes_c", "face_c");
            AvatarAppearanceBoard.Remember("offline-1", saved);
            AvatarAppearanceBoard.Replace(new List<(string, string, AvatarAppearance)>
            {
                ("p1", "u1", new AvatarAppearance("body_a", "hood_a", "shoes_a", "face_a"))
            });
            AvatarAppearanceBoard.Replace(null);

            Assert.That(AvatarAppearanceBoard.TryGet("offline-1", out var found), Is.True);
            Assert.That(found, Is.EqualTo(saved));
            Assert.That(AvatarAppearanceBoard.TryGet("p1", out _), Is.False);
        }

        [Test]
        public void LiveRoster_BeatsRememberedLook()
        {
            var saved = new AvatarAppearance("body_c", "hood_c", "shoes_c", "face_c");
            var live = new AvatarAppearance("body_a", "hood_a", "shoes_a", "face_a");
            AvatarAppearanceBoard.Remember("p1", saved);
            AvatarAppearanceBoard.Replace(new List<(string, string, AvatarAppearance)>
            {
                ("p1", "u1", live)
            });

            Assert.That(AvatarAppearanceBoard.TryGet("p1", out var found), Is.True);
            Assert.That(found, Is.EqualTo(live));
        }

        [Test]
        public void Clear_DropsLocalAndRoster()
        {
            AvatarAppearanceBoard.SetLocal(new AvatarAppearance("body_a", "hood_a", "shoes_a", "face_a"));
            AvatarAppearanceBoard.Replace(new List<(string, string, AvatarAppearance)>
            {
                ("p1", "u1", new AvatarAppearance("body_b", "hood_b", "shoes_b", "face_b"))
            });

            AvatarAppearanceBoard.Remember("offline-1", new AvatarAppearance("body_c", "hood_c", "shoes_c", "face_c"));
            AvatarAppearanceBoard.Clear();

            Assert.That(AvatarAppearanceBoard.HasLocal, Is.False);
            Assert.That(AvatarAppearanceBoard.TryGet("p1", out _), Is.False);
            Assert.That(AvatarAppearanceBoard.TryGet("offline-1", out _), Is.False);
        }
    }
}
