using Fusion;
using Game.Network.Session;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;
using UnityEngine;

namespace Game.Architecture.Tests
{
    public sealed class DedicatedServerFrameRateTests
    {
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
