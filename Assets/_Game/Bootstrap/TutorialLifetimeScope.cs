using Game.Client.Combat;
using Game.Core.Players;
using Game.Server.Players;
using Game.SOAP.Config;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Bootstrap
{
    public sealed class TutorialLifetimeScope : LifetimeScope
    {
        [SerializeField] private MatchRulesSO matchRules;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterInstance(matchRules);
            builder.Register<PlayerInteractionSystem>(Lifetime.Scoped).As<IPlayerCombatRules>();
            builder.RegisterComponentInHierarchy<PlayerCombatant>();
        }
    }
}
