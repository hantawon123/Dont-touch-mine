using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Bootstrap;
using Game.Client.Audio;
using Game.Core.Flow;
using Game.Core.Home;
using Game.Core.Lobby;
using Game.Core.Ports;
using Game.Network.Session;
using NUnit.Framework;
using R3;
using UnityEngine;
using VContainer;

namespace Game.Architecture.Tests
{
    public sealed class ArchitectureStackTests
    {
        [Test]
        public async Task VContainer_R3_And_UniTask_Work_Together()
        {
            using var state = new ReactiveProperty<int>(0);
            var builder = new ContainerBuilder();
            builder.RegisterInstance(state);

            using var container = builder.Build();
            var resolvedState = container.Resolve<ReactiveProperty<int>>();
            var observedValue = -1;
            using var subscription = resolvedState.Subscribe(value => observedValue = value);

            await UniTask.Yield();
            resolvedState.Value = 6;

            Assert.That(observedValue, Is.EqualTo(6));
        }

        [Test]
        public void ProjectServices_ResolveAsOneRuntimeGraph()
        {
            var builder = new ContainerBuilder();
            ProjectLifetimeScope.RegisterServices(builder);

            using var container = builder.Build();
            var roomState = container.Resolve<RoomBrowserSystem>();

            Assert.That(container.Resolve<IRoomListSink>(), Is.SameAs(roomState));
            Assert.That(container.Resolve<IRoomSessionSink>(), Is.SameAs(roomState));
            Assert.That(container.Resolve<RoomUiCommands>(), Is.Not.Null);
            Assert.That(container.Resolve<NetworkRunnerService>(), Is.Not.Null);
            Assert.That(container.Resolve<AppFlowSystem>(), Is.Not.Null);
            Assert.That(container.Resolve<HomeMenuSystem>(), Is.Not.Null);
            Assert.That(container.Resolve<FriendListSystem>(), Is.Not.Null);
            Assert.That(container.Resolve<FriendSearchSystem>(), Is.Not.Null);
            Assert.That(container.Resolve<PlayerProfile>(), Is.Not.Null);

            var audio = container.Resolve<IAudioSettings>();
            Assert.That(audio, Is.Not.Null);
            Assert.That(audio.Current, Is.Not.Null);
            Assert.That(
                AudioListener.volume,
                Is.EqualTo(audio.Current.GetListenerVolume()).Within(0.0001f));
        }
    }
}
