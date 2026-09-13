using Game.Client.Match;
using Game.Core.Match;
using NUnit.Framework;

namespace Game.Architecture.Tests
{
    public sealed class DestroyedItemsHudLayoutTests
    {
        [Test]
        public void Build_PutsLocalItemFirstWithOrangeSlotAndOthersInDestroyOrder()
        {
            var slots = DestroyedItemsHudLayout.Build(
                4,
                new[]
                {
                    new PlayerItemStatusSnapshot("Soda_01", false),
                    new PlayerItemStatusSnapshot("Burger_01", true),
                    new PlayerItemStatusSnapshot("Pineapple_01", true),
                    new PlayerItemStatusSnapshot("Cup1_C3", false),
                },
                "Soda_01",
                new[] { "Pineapple_01", "Burger_01" });

            Assert.That(slots.Length, Is.EqualTo(4));
            Assert.That(slots[0].ItemId, Is.EqualTo("Soda_01"));
            Assert.That(slots[0].IsOwn, Is.True);
            Assert.That(slots[0].ShowPreview, Is.True);
            Assert.That(slots[0].Grayscale, Is.False);
            Assert.That(slots[1].ItemId, Is.EqualTo("Pineapple_01"));
            Assert.That(slots[1].IsOwn, Is.False);
            Assert.That(slots[1].ShowPreview, Is.True);
            Assert.That(slots[2].ItemId, Is.EqualTo("Burger_01"));
            Assert.That(slots[3].ShowPreview, Is.False);
            Assert.That(slots[3].IsOwn, Is.False);
        }

        [Test]
        public void Build_GrayscalesLocalItemWhenDestroyed_AndKeepsItLeftmost()
        {
            var slots = DestroyedItemsHudLayout.Build(
                3,
                new[]
                {
                    new PlayerItemStatusSnapshot("Soda_01", true),
                    new PlayerItemStatusSnapshot("Burger_01", true),
                    new PlayerItemStatusSnapshot("Pineapple_01", false),
                },
                "Soda_01",
                new[] { "Burger_01", "Soda_01" });

            Assert.That(slots.Length, Is.EqualTo(3));
            Assert.That(slots[0].ItemId, Is.EqualTo("Soda_01"));
            Assert.That(slots[0].IsOwn, Is.True);
            Assert.That(slots[0].ShowPreview, Is.True);
            Assert.That(slots[0].Grayscale, Is.True);
            Assert.That(slots[1].ItemId, Is.EqualTo("Burger_01"));
            Assert.That(slots[1].Grayscale, Is.False);
            Assert.That(slots[2].ShowPreview, Is.False);
        }

        [Test]
        public void Build_FallsBackToStatusOrderWhenDestroyEventsAreMissing()
        {
            var slots = DestroyedItemsHudLayout.Build(
                3,
                new[]
                {
                    new PlayerItemStatusSnapshot("Soda_01", true),
                    new PlayerItemStatusSnapshot("Burger_01", false),
                    new PlayerItemStatusSnapshot("Pineapple_01", true),
                },
                null,
                null);

            Assert.That(slots.Length, Is.EqualTo(3));
            Assert.That(slots[0].ItemId, Is.EqualTo("Soda_01"));
            Assert.That(slots[0].IsOwn, Is.False);
            Assert.That(slots[1].ItemId, Is.EqualTo("Pineapple_01"));
            Assert.That(slots[2].ShowPreview, Is.False);
        }

        [Test]
        public void Build_KeepsLocalItemLeftmostWhenItIsDestroyedLast()
        {
            var slots = DestroyedItemsHudLayout.Build(
                3,
                new[]
                {
                    new PlayerItemStatusSnapshot("Burger_01", true),
                    new PlayerItemStatusSnapshot("Pineapple_01", true),
                    new PlayerItemStatusSnapshot("Soda_01", true),
                },
                "Soda_01",
                new[] { "Burger_01", "Pineapple_01", "Soda_01" });

            Assert.That(slots.Length, Is.EqualTo(3));
            Assert.That(slots[0].ItemId, Is.EqualTo("Soda_01"));
            Assert.That(slots[0].IsOwn, Is.True);
            Assert.That(slots[0].Grayscale, Is.True);
            Assert.That(slots[1].ItemId, Is.EqualTo("Burger_01"));
            Assert.That(slots[2].ItemId, Is.EqualTo("Pineapple_01"));
        }

        [Test]
        public void Build_KeepsEmptyCirclesForUndestroyedItems()
        {
            var slots = DestroyedItemsHudLayout.Build(
                4,
                new[]
                {
                    new PlayerItemStatusSnapshot("Soda_01", false),
                    new PlayerItemStatusSnapshot("Burger_01", false),
                    new PlayerItemStatusSnapshot("Pineapple_01", false),
                    new PlayerItemStatusSnapshot("Cup1_C3", false),
                },
                "Soda_01",
                System.Array.Empty<string>());

            Assert.That(slots.Length, Is.EqualTo(4));
            Assert.That(slots[0].ItemId, Is.EqualTo("Soda_01"));
            Assert.That(slots[0].IsOwn, Is.True);
            Assert.That(slots[0].ShowPreview, Is.True);
            Assert.That(slots[0].Grayscale, Is.False);
            Assert.That(slots[1].ShowPreview, Is.False);
            Assert.That(slots[2].ShowPreview, Is.False);
            Assert.That(slots[3].ShowPreview, Is.False);
        }
    }
}
