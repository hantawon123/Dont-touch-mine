using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using R3;
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
    }
}
