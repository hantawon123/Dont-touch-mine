using System.Reflection;
using System.Threading;
using Fusion;
using Game.Core.Home;
using Game.Core.Lobby;
using Game.Network.Session;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

namespace Game.Architecture.Tests
{
    public sealed class SessionCloudRecoveryTests
    {
        [TestCase(GameMode.Host)]
        [TestCase(GameMode.Client)]
        [TestCase(GameMode.Server)]
        public void RoomConnectionOwnsRecoverySettings_AndPreservesAdmissionAndAuthentication(GameMode mode)
        {
            var settings = Fusion.Photon.Realtime.PhotonAppSettings.Global.AppSettings;
            var region = settings.FixedRegion;
            var version = settings.AppVersion;
            var config = NetworkProjectConfig.Global;
            var migration = config.HostMigration.EnableAutoUpdate;
            var webHost = config.AllowClientServerModesInWebGL;
            var profile = new PlayerProfile("test player");
            profile.AdoptUserId("test-user", "test-token");
            var service = new NetworkRunnerService(null, null, null, null, null, profile);
            var browser = new Photon.Realtime.RealtimeClient();
            typeof(NetworkRunnerService).GetField("_matchmakingClient", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(service, browser);
            using var cancellation = new CancellationTokenSource();
            var request = mode == GameMode.Client ? SessionRequest.Join("TEST01", "password") :
                mode == GameMode.Server ? SessionRequest.CreateServer("TEST01", "test room", "supermarket", 3) :
                SessionRequest.Create("TEST01", "test room", "supermarket", 3, "password", isPrivate: true);
            try
            {
                var args = service.BuildSessionStartArgs(request, null, cancellation.Token);
                Assert.That(args.RealtimeClient, Is.Null, "A ready browser client skips Fusion recovery initialization.");
                Assert.That(args.CustomPhotonAppSettings, Is.Not.Null);
                Assert.That(args.CustomPhotonAppSettings.AppVersion, Is.EqualTo(settings.AppVersion));
                Assert.That(args.GameMode, Is.EqualTo(mode));
                Assert.That(args.SessionName, Is.EqualTo("TEST01"));
                Assert.That(args.EnableClientSessionCreation, Is.EqualTo(mode != GameMode.Client));
                Assert.That(args.IsVisible, Is.EqualTo(mode == GameMode.Client ? (bool?)null : false));
                Assert.That(args.PlayerCount, Is.EqualTo(mode == GameMode.Client ? (int?)null : RoomSettings.MaxPlayerCount));
                Assert.That(args.AuthValues.UserId, Is.EqualTo("test-user"));
                Assert.That(args.AuthValues.AuthType, Is.EqualTo(Photon.Realtime.CustomAuthenticationType.Custom));
                StringAssert.Contains("token=test-token", args.AuthValues.AuthGetParameters);
                SessionConnectionTokenCodec.Decode(args.ConnectionToken, out var password, out var nickname, out var userId);
                Assert.That(password, Is.EqualTo(mode == GameMode.Server ? "" : "password"));
                Assert.That(nickname, Is.EqualTo("test player"));
                Assert.That(userId, Is.EqualTo("test-user"));
                Assert.That(args.StartGameCancellationToken, Is.EqualTo(cancellation.Token));
                cancellation.Cancel();
                Assert.That(args.StartGameCancellationToken.IsCancellationRequested, Is.True);
            }
            finally
            {
                settings.FixedRegion = region; settings.AppVersion = version;
                config.HostMigration.EnableAutoUpdate = migration;
                config.AllowClientServerModesInWebGL = webHost;
            }
        }
    }
}
