using Fusion;
using Game.Network.Session;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;
using UnityEngine;

namespace Game.Architecture.Tests
{
    public sealed class DedicatedServerFrameRateTests
    {
        [TestCase(GameMode.Server, 64)]
        [TestCase(GameMode.Host, 256)]
        [TestCase(GameMode.Client, 256)]
        public void HeapReservation_OnlyChangesDedicatedServerPageCount(GameMode mode, int expectedPages)
        {
            var source = new NetworkProjectConfig();
            source.Heap.PageCount = 256;
            source.Heap.PageShift = (PageSizes)16;
            source.HostMigration.EnableAutoUpdate = true;
            var tickRate = source.Simulation.TickRateSelection;
            var prefabTable = source.PrefabTable;
            var configured = NetworkRunnerService.ConfigureSession(source, mode);
            Assert.That(configured.Heap.PageCount, Is.EqualTo(expectedPages));
            Assert.That(configured.Heap.PageShift, Is.EqualTo((PageSizes)16));
            Assert.That(configured.Simulation.TickRateSelection, Is.EqualTo(tickRate));
            Assert.That(configured.PrefabTable, Is.SameAs(prefabTable));
            Assert.That(configured.HostMigration.EnableAutoUpdate, Is.False);
            Assert.That(configured, Is.SameAs(source), "Preserve Fusion's loaded execution-order metadata.");
        }

        [TestCase(GameMode.Server, 32, 0)]
        [TestCase(GameMode.Client, 144, 1)]
        [TestCase(GameMode.Host, 144, 1)]
        public void OnlyDedicatedServerUsesConfiguredSimulationRate(GameMode mode, int expected, int sync)
        {
            var previousRate = Application.targetFrameRate;
            var previousSync = QualitySettings.vSyncCount;
            try
            {
                Application.targetFrameRate = 144;
                QualitySettings.vSyncCount = 1;
                NetworkRunnerService.ApplyServerFrameRate(mode, 32);
                Assert.That(Application.targetFrameRate, Is.EqualTo(expected));
                Assert.That(QualitySettings.vSyncCount, Is.EqualTo(sync));
            }
            finally
            {
                Application.targetFrameRate = previousRate;
                QualitySettings.vSyncCount = previousSync;
            }
        }
    }
}
