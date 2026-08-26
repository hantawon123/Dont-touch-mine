using System;
using Game.Core.Players;
using Game.Server.Players;
using Game.SOAP.Config;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>
    /// Playground 테스트 씬 전용 조립: 전투 판정 규칙(서버 시스템)을
    /// Client 컴포넌트들이 인터페이스로 쓸 수 있게 등록한다.
    /// </summary>
    public sealed class PlaygroundLifetimeScope : LifetimeScope
    {
        [SerializeField]
        private MatchRulesSO matchRules;

        protected override void Configure(IContainerBuilder builder)
        {
            if (matchRules == null)
            {
                throw new InvalidOperationException("PlaygroundLifetimeScope: MatchRulesSO를 연결하세요.");
            }

            builder.RegisterInstance(matchRules);
            builder.Register<PlayerInteractionSystem>(Lifetime.Scoped)
                .AsSelf()
                .As<IPlayerCombatRules>();
        }
    }
}
