using System;
using Game.Server.Match;
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

        protected override void Configure(IContainerBuilder builder)
        {
            if (matchRules == null)
            {
                throw new InvalidOperationException("MatchRulesSO must be assigned.");
            }

            builder.RegisterInstance(matchRules);
            builder.Register<MatchState>(Lifetime.Scoped);
            builder.Register<MatchFlow>(Lifetime.Scoped);
        }
    }
}
