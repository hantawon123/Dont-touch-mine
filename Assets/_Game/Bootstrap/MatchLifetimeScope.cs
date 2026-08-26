using System;
using Game.Client.Match;
using Game.Core.Match;
using Game.Server.Match;
using Game.Server.Players;
using Game.SOAP.Config;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Bootstrap
{
    public sealed class MatchLifetimeScope : LifetimeScope
    {
        [SerializeField]
        private MatchRulesSO matchRules;

        [SerializeField]
        private MatchPhaseView matchPhaseView;

        [SerializeField]
        private MatchRuntimeContext matchRuntimeContext;

        protected override void Configure(IContainerBuilder builder)
        {
            if (matchRules == null)
            {
                throw new InvalidOperationException("MatchRulesSO must be assigned.");
            }

            if (matchPhaseView == null)
            {
                throw new InvalidOperationException("MatchPhaseView must be assigned.");
            }

            if (matchRuntimeContext == null)
            {
                throw new InvalidOperationException("MatchRuntimeContext must be assigned.");
            }

            builder.RegisterInstance(matchRules);
            builder.Register<MatchState>(Lifetime.Scoped).AsSelf().As<IMatchState>();
            builder.Register<MatchFlow>(Lifetime.Scoped);
            builder.Register<PlayerInteractionSystem>(Lifetime.Scoped);
            builder.Register<MatchRuntimeFactory>(Lifetime.Scoped);
            builder.Register<HighlightRuntimeFactory>(Lifetime.Scoped);
            builder.RegisterComponent(matchRuntimeContext)
                .As<IMatchRuntimeContext>()
                .As<IMatchClock>();
            builder.RegisterComponent(matchPhaseView).As<IMatchPhaseView>();
            builder.RegisterEntryPoint<MatchPhasePresenter>();
        }
    }
}
