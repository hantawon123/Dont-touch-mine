#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using Fusion;
using Game.Network.Session;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    public sealed class NetworkSceneCatchUpTests
    {
        [UnityTest]
        public IEnumerator SceneReady_WaitsForBacklogAcrossMultipleRenderedFrames()
        {
            var runner = new GameObject("Scene catch-up runner").AddComponent<NetworkRunner>();
            var network = new NetworkRunnerService(null, null, null, null, null, null);
            typeof(NetworkRunnerService).GetField("_runner", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(network, runner);
            try
            {
                var config = NetworkProjectConfig.Deserialize(NetworkProjectConfig.Serialize(NetworkProjectConfig.Global));
                config.Simulation.SimulationUpdateTimeMode = SimulationConfig.SimulationTimeMode.UnscaledDeltaTime;
                var start = runner.StartGame(new StartGameArgs { GameMode = GameMode.Single, Config = config });
                var timeout = Time.realtimeSinceStartupAsDouble + 30;
                while (!start.IsCompleted && Time.realtimeSinceStartupAsDouble < timeout) yield return null;
                Assert.That(start.IsCompleted && start.Result.Ok, Is.True);
                for (var i = 0; i < 5; i++) yield return null;

                // Real Fusion accumulates this scene-activation stall and processes
                // at most a bounded number of ticks per render frame.
                System.Threading.Thread.Sleep(3250);
                var waitingFrames = 0;
                timeout = Time.realtimeSinceStartupAsDouble + 10;
                do
                {
                    yield return null;
                    if (!network.IsSimulationCaughtUp)
                    {
                        waitingFrames++;
                        Assert.That(network.IsFinalForwardTick, Is.False,
                            "The last tick of one frame must not start the intro while later catch-up frames remain.");
                    }
                } while ((!network.IsSimulationCaughtUp || waitingFrames == 0) && Time.realtimeSinceStartupAsDouble < timeout);
                Assert.That(waitingFrames, Is.GreaterThanOrEqualTo(2));
                Assert.That(network.IsSimulationCaughtUp, Is.True);
            }
            finally
            {
                var shutdown = runner.Shutdown();
            }
            while (runner != null && runner.IsRunning) yield return null;
        }
    }
}
#endif
