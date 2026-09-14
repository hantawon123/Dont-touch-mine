using Game.Core.Lobby;
using Game.Network.Session;
using NUnit.Framework;

namespace Game.Architecture.Tests
{
    public sealed class SessionPropertyMapperTests
    {
        private sealed class PendingAccount : Game.Core.Ports.IAccountReady
        {
            public readonly Cysharp.Threading.Tasks.UniTaskCompletionSource<bool> Completion = new();
            public bool Requested;
            public Cysharp.Threading.Tasks.UniTask<bool> Ready
            {
                get { Requested = true; return Completion.Task; }
            }
        }

        [UnityEngine.TestTools.UnityTest]
        public System.Collections.IEnumerator LobbyJoin_RechecksConnectionAfterSharedSignIn() =>
            Cysharp.Threading.Tasks.UniTask.ToCoroutine(async () =>
            {
                var account = new PendingAccount();
                var network = new NetworkRunnerService(null, null, null, null, null, null, accountReady: account);
                try
                {
                    var joining = network.JoinLobbyAsync(System.Threading.CancellationToken.None);
                    await Cysharp.Threading.Tasks.UniTask.WaitUntil(() => account.Requested);
                    typeof(NetworkRunnerService).GetField("_browsingLobby",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(network, true);
                    account.Completion.TrySetResult(true);
                    Assert.That((await joining).Ok, Is.True);
                }
                finally { network.Dispose(); }
            });

        [UnityEngine.TestTools.UnityTest]
        public System.Collections.IEnumerator LobbyJoin_CancelDuringSignInDoesNotCreateConnection() =>
            Cysharp.Threading.Tasks.UniTask.ToCoroutine(async () =>
            {
                var account = new PendingAccount();
                var network = new NetworkRunnerService(null, null, null, null, null, null, accountReady: account);
                using var cancellation = new System.Threading.CancellationTokenSource();
                try
                {
                    var joining = network.JoinLobbyAsync(cancellation.Token);
                    await Cysharp.Threading.Tasks.UniTask.WaitUntil(() => account.Requested);
                    cancellation.Cancel();
                    var cancelled = false;
                    try { await joining; }
                    catch (System.OperationCanceledException) { cancelled = true; }
                    Assert.That(cancelled, Is.True);
                    Assert.That(network.IsBrowsingLobby, Is.False);
                }
                finally { network.Dispose(); }
            });

        private sealed class ListedServer : Photon.Realtime.RoomInfo
        {
            public ListedServer(bool available, int peers) : base("988ABC", new Photon.Client.PhotonHashtable
            {
                [Photon.Realtime.GamePropertyKey.IsOpen] = true,
                [Photon.Realtime.GamePropertyKey.IsVisible] = true,
                [Photon.Realtime.GamePropertyKey.PlayerCount] = (byte)peers,
                [SessionPropertyKeys.AvailableServer] = available,
                [SessionPropertyKeys.MapId] = "supermarket",
                [SessionPropertyKeys.MaxPlayers] = 6
            }) { }
        }

        [UnityEngine.TestTools.UnityTest]
        public System.Collections.IEnumerator AvailableServer_ReusesWarmLobbyAndHidesUnclaimedRoom() =>
            Cysharp.Threading.Tasks.UniTask.ToCoroutine(async () =>
            {
                var network = new NetworkRunnerService(null, null, null, null, null, null);
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var type = typeof(NetworkRunnerService);
                type.GetField("_browsingLobby", flags).SetValue(network, true);
                type.GetField("_receivedLobbySnapshot", flags).SetValue(network, true);
                var rooms = (System.Collections.Generic.IDictionary<string, Photon.Realtime.RoomInfo>)
                    type.GetField("_realtimeRooms", flags).GetValue(network);
                var available = new ListedServer(true, 1);
                rooms.Add(available.Name, available);
                try
                {
                    Assert.That(await network.FindAvailableServerAsync(System.Threading.CancellationToken.None), Is.EqualTo("988ABC"));
                    Assert.That(Game.Network.Lobby.RoomSummaryMapper.TryToSummary(available, out _), Is.False);
                    Assert.That(Game.Network.Lobby.RoomSummaryMapper.TryToSummary(new ListedServer(false, 7), out var room), Is.True);
                    Assert.That(room.PlayerCount, Is.EqualTo(6));
                }
                finally { network.Dispose(); }
            });

        [TestCase(1, true, 0)]
        [TestCase(7, true, 6)]
        [TestCase(6, false, 6)]
        [TestCase(0, true, 0)]
        public void RoomListing_DoesNotCountDedicatedServerAsPlayer(int peers, bool dedicated, int expected) =>
            Assert.That(Game.Network.Lobby.RoomSummaryMapper.CountPlayers(peers, dedicated), Is.EqualTo(expected));
        [Test]
        public void PrivateSession_IsInvisibleWithoutPassword_AndCodeJoinCannotCreate()
        {
            var request = SessionRequest.Create("ROOM01", "비공개", "Playground", 6, null, isPrivate: true);
            Assert.That(request.IsVisible, Is.False);
            Assert.That(request.Password, Is.Null);
            Assert.That(SessionRequest.Create("ROOM02", "공개", "Playground", 6, null).IsVisible, Is.True);
            Assert.That(SessionRequest.Join("ROOM01", null).AllowCreate, Is.False);
            var properties = SessionPropertyMapper.BuildForStart(request, "방장");
            Assert.That((bool)properties[SessionPropertyKeys.Locked], Is.False);
            Assert.That(properties.Count, Is.LessThanOrEqualTo(10));
        }

        [Test]
        public void JoinRequest_DoesNotWriteSessionProperties()
        {
            var request = SessionRequest.Join("ROOM01", "secret");

            var properties = SessionPropertyMapper.BuildForStart(request, "player");

            Assert.That(properties, Is.Null);
        }

        [Test]
        public void CreateRequest_WritesPublicSettingsWithoutPassword()
        {
            var request = SessionRequest.Create(
                "ROOM01",
                "테스트 방",
                "Playground",
                6,
                "secret");

            var properties = SessionPropertyMapper.BuildForStart(request, "태원");

            Assert.That((string)properties[SessionPropertyKeys.DisplayName],
                Is.EqualTo("테스트 방"));
            Assert.That((string)properties[SessionPropertyKeys.MapId],
                Is.EqualTo("Playground"));
            Assert.That((int)properties[SessionPropertyKeys.MaxPlayers], Is.EqualTo(6));
            Assert.That(
                (int)properties[SessionPropertyKeys.DestructionLimit],
                Is.EqualTo(PlaySettingsDraft.DefaultDestructionLimit));
            Assert.That(properties.ContainsKey(SessionPropertyKeys.HidingDurationSeconds), Is.False);
            Assert.That(properties.ContainsKey(SessionPropertyKeys.SearchingDurationMinutes), Is.False);
            Assert.That(properties.ContainsKey(SessionPropertyKeys.SprintMultiplierPercent), Is.False);
            Assert.That(properties.ContainsKey(SessionPropertyKeys.StunHitCount), Is.False);
            Assert.That(properties.ContainsKey(SessionPropertyKeys.CategoryId), Is.False);
            Assert.That(SessionPropertyMapper.ReadPackedMatchRules(
                (string)properties[SessionPropertyKeys.MatchRules], default),
                Is.EqualTo(MatchRuleSettings.Default));
            Assert.That(properties.Count, Is.LessThanOrEqualTo(10));
            Assert.That((string)properties[SessionPropertyKeys.HostNickname],
                Is.EqualTo("태원"));
            Assert.That((bool)properties[SessionPropertyKeys.Locked], Is.True);
            foreach (var property in properties.Values)
            {
                if (property.IsString)
                {
                    Assert.That((string)property, Is.Not.EqualTo(request.Password));
                }
            }
        }

        [Test]
        public void LobbySettings_TrimMapIdAndPreserveLimits()
        {
            Assert.That(
                MatchRuleSettings.TryCreate(
                    60,
                    10,
                    1.5f,
                    5,
                    " fruit ",
                    out var matchRules,
                    out _),
                Is.True);
            var properties = SessionPropertyMapper.BuildLobbySettings(
                4,
                3,
                " Playground ",
                matchRules);

            Assert.That((int)properties[SessionPropertyKeys.MaxPlayers], Is.EqualTo(4));
            Assert.That((int)properties[SessionPropertyKeys.DestructionLimit], Is.EqualTo(3));
            Assert.That((string)properties[SessionPropertyKeys.MapId],
                Is.EqualTo("Playground"));
            Assert.That(SessionPropertyMapper.ReadPackedMatchRules(
                (string)properties[SessionPropertyKeys.MatchRules], default), Is.EqualTo(matchRules));
        }

        [Test]
        public void CreateAndRepeatedSettingsUpdates_StayWithinTenProperties()
        {
            var all = SessionPropertyMapper.BuildForStart(
                SessionRequest.Create("ROOM01", "방", "playground", 6, "secret"), "host");
            for (var count = 2; count <= 6; count++)
            {
                foreach (var pair in SessionPropertyMapper.BuildLobbySettings(
                             count, count, "playground", MatchRuleSettings.Default))
                    all[pair.Key] = pair.Value;
                Assert.That(all.Count, Is.LessThanOrEqualTo(10));
            }
        }

        [Test]
        public void AvailableServer_AdvertisesCapacityWithoutExceedingPhotonPropertyLimit()
        {
            var request = SessionRequest.AvailableServer("988ABC", "supermarket");
            Assert.That(request.Mode, Is.EqualTo(Fusion.GameMode.Server));
            Assert.That(request.IsVisible, Is.True);
            var properties = SessionPropertyMapper.BuildForStart(request, "Server");
            Assert.That((bool)properties[SessionPropertyKeys.AvailableServer], Is.True);
            Assert.That(properties.Count, Is.LessThanOrEqualTo(10));
            Assert.That(properties.ContainsKey("password"), Is.False);
            var client = SessionPropertyMapper.BuildForStart(SessionRequest.Join("988ABC", "secret"), "client");
            Assert.That(client, Is.Null, "A joining client must never publish authority properties.");
        }

        [Test]
        public void PackedRules_Version1ReadsMinutes_Version2ReadsSeconds()
        {
            var v1 = SessionPropertyMapper.ReadPackedMatchRules(
                "{\"version\":1,\"hiding\":30,\"searching\":5,\"sprint\":1,\"stun\":3}",
                default);
            Assert.That(v1.HidingDurationSeconds, Is.EqualTo(30));
            Assert.That(v1.SearchingDurationSeconds, Is.EqualTo(300));

            Assert.That(MatchRuleSettings.TryCreateSeconds(45, 90, 1f, 3, "food", out var created, out _), Is.True);
            var properties = SessionPropertyMapper.BuildLobbySettings(6, 5, "Playground", created);
            var read = SessionPropertyMapper.ReadPackedMatchRules(
                (string)properties[SessionPropertyKeys.MatchRules], default);
            Assert.That(read, Is.EqualTo(created));
            Assert.That(read.SearchingDurationSeconds, Is.EqualTo(90));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("broken")]
        [TestCase("{}")]
        [TestCase("{\"version\":2}")]
        [TestCase("{\"version\":1,\"hiding\":0,\"searching\":5,\"sprint\":1,\"stun\":3}")]
        public void InvalidPackedRules_KeepLastValidSettings(string payload)
        {
            MatchRuleSettings.TryCreate(60, 10, 1.5f, 5, "food", out var previous, out _);
            Assert.That(SessionPropertyMapper.ReadPackedMatchRules(payload, previous), Is.EqualTo(previous));
        }

        /// <summary>
        /// The lobby only ever sees the keys a room was created with, so the
        /// status key has to exist before any match starts or the room list
        /// can never say a room is playing.
        /// </summary>
        [Test]
        public void CreateRequest_ListsPlayingAsFalse_SoTheLobbyCanWatchIt()
        {
            var properties = SessionPropertyMapper.BuildForStart(
                SessionRequest.Create("ROOM01", "방", "Playground", 6, null), "host");

            Assert.That(properties.ContainsKey(SessionPropertyKeys.Playing), Is.True);
            Assert.That((bool)properties[SessionPropertyKeys.Playing], Is.False);
            Assert.That(properties.Count, Is.LessThanOrEqualTo(10));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void RoomStatus_TouchesOnlyThePlayingKey(bool playing)
        {
            var properties = SessionPropertyMapper.BuildRoomStatus(playing);

            Assert.That(properties.Count, Is.EqualTo(1));
            Assert.That((bool)properties[SessionPropertyKeys.Playing], Is.EqualTo(playing));
        }

        [Test]
        public void LobbySettings_SerializesUnlimitedDestructionDistinctly()
        {
            var properties = SessionPropertyMapper.BuildLobbySettings(
                6,
                PlaySettingsDraft.UnlimitedDestructionLimit,
                "Playground",
                MatchRuleSettings.Default);

            Assert.That(
                (int)properties[SessionPropertyKeys.DestructionLimit],
                Is.EqualTo(PlaySettingsDraft.UnlimitedDestructionLimit));
            Assert.That(
                PlaySettingsDraft.UnlimitedDestructionLimit,
                Is.LessThan(PlaySettingsDraft.MinDestructionLimit));
        }
    }
}
