using System;
using System.Reflection;
using Game.Bootstrap;
using Game.Network.Session;
using NUnit.Framework;

namespace Game.Architecture.Tests
{
    public sealed class EditorDevelopmentSessionTests
    {
        private EditorDevelopmentSession.PeerRole previousRole;
        private string previousCode;
        [SetUp] public void Save()
        {
            previousRole = EditorDevelopmentSession.Role;
            previousCode = EditorDevelopmentSession.Code;
        }
        [TearDown] public void Restore() => EditorDevelopmentSession.Configure(previousRole, previousCode);

        [Test]
        public void ServerAndClientSharePartitionButOnlyServerSelectsAuthority()
        {
            EditorDevelopmentSession.Configure(EditorDevelopmentSession.PeerRole.Server, "dev988");
            var serverVersion = EditorDevelopmentSession.AppVersion;
            Assert.That(DedicatedServerStartup.IsRequested, Is.True);
            Assert.That(EditorDevelopmentSession.Code, Is.EqualTo("DEV988"));
            EditorDevelopmentSession.Configure(EditorDevelopmentSession.PeerRole.Client, "DEV988");
            Assert.That(DedicatedServerStartup.IsRequested, Is.False);
            Assert.That(EditorDevelopmentSession.AppVersion, Is.EqualTo(serverVersion));
            EditorDevelopmentSession.Configure(EditorDevelopmentSession.PeerRole.Client, "DEV989");
            Assert.That(EditorDevelopmentSession.AppVersion, Is.Not.EqualTo(serverVersion));
        }

        [Test]
        public void ActualPhotonSettingsUseDevelopmentPartitionAndRegion()
        {
            var shared = Fusion.Photon.Realtime.PhotonAppSettings.Global.AppSettings;
            var oldVersion = shared.AppVersion;
            var oldRegion = shared.FixedRegion;
            var network = new NetworkRunnerService(null, null, null, null, null, null);
            try
            {
                EditorDevelopmentSession.Configure(EditorDevelopmentSession.PeerRole.Client, "DEV988");
                typeof(NetworkRunnerService).GetMethod("GetPhotonSettings", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(network, null);
                Assert.That(shared.AppVersion, Is.EqualTo(EditorDevelopmentSession.AppVersion));
                Assert.That(shared.AppVersion, Does.StartWith("editor-dev-v1-"));
                Assert.That(shared.FixedRegion, Is.EqualTo("kr"));
            }
            finally { network.Dispose(); shared.AppVersion = oldVersion; shared.FixedRegion = oldRegion; }
        }

        [Test]
        public void InvalidTestCodeCannotSelectServer()
        {
            EditorDevelopmentSession.Configure(EditorDevelopmentSession.PeerRole.Normal, "DEV988");
            Assert.Throws<ArgumentException>(() => EditorDevelopmentSession.Configure(EditorDevelopmentSession.PeerRole.Server, "bad"));
            Assert.That(EditorDevelopmentSession.Enabled, Is.False);
        }
    }
}
