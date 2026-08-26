using System;
using Game.Client.Home;
using Game.Core.Home;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Bootstrap
{
    public sealed class HomeLifetimeScope : LifetimeScope
    {
        [SerializeField]
        private HomeMenuView homeMenuView;

        [SerializeField]
        private string defaultNickname = "사용자닉네임";

        [SerializeField]
        private int defaultLevel = 1;

        protected override void Configure(IContainerBuilder builder)
        {
            if (homeMenuView == null)
            {
                throw new InvalidOperationException("HomeMenuView must be assigned.");
            }

            if (string.IsNullOrWhiteSpace(defaultNickname))
            {
                throw new InvalidOperationException("Default nickname must be assigned.");
            }

            if (defaultLevel < 1)
            {
                throw new InvalidOperationException("Default level must be 1 or greater.");
            }

            builder.RegisterInstance(new PlayerProfile(defaultNickname, defaultLevel));
            builder.Register<HomeMenuSystem>(Lifetime.Scoped);
            builder.Register<UnityHomeApplicationHost>(Lifetime.Scoped).As<IHomeApplicationHost>();
            builder.RegisterComponent(homeMenuView).As<IHomeMenuView>();
            builder.RegisterEntryPoint<HomeMenuPresenter>();
        }
    }
}
