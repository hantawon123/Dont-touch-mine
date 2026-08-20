using Game.Core.Match;
using Game.Server.Match;
using Game.SOAP.Config;
using NUnit.Framework;
using UnityEngine;
using VContainer;

namespace Game.Tests.EditMode
{
    public sealed class MatchCompositionTests
    {
        [Test]
        public void ScopedRegistrations_ResolveOneSharedMatchState()
        {
            var rules = ScriptableObject.CreateInstance<MatchRulesSO>();

            try
            {
                var builder = new ContainerBuilder();
                builder.RegisterInstance(rules);
                builder.Register<MatchState>(Lifetime.Scoped).AsSelf().As<IMatchState>();
                builder.Register<MatchFlow>(Lifetime.Scoped);

                using var container = builder.Build();
                var flow = container.Resolve<MatchFlow>();
                var state = container.Resolve<MatchState>();

                flow.Start(10d);

                Assert.That(state.CurrentPhase.CurrentValue, Is.EqualTo(MatchPhase.Hiding));
                Assert.That(container.Resolve<MatchState>(), Is.SameAs(state));
            }
            finally
            {
                Object.DestroyImmediate(rules);
            }
        }
    }
}
