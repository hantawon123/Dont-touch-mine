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
                Debug.LogError("HomeMenuView must be assigned on HomeLifetimeScope.", this);
                return;
            }

            var nickname = string.IsNullOrWhiteSpace(defaultNickname) ? "Player" : defaultNickname;
            var level = defaultLevel < 1 ? 1 : defaultLevel;

            builder.RegisterInstance(new PlayerProfile(nickname, level));
            builder.Register<UnityHomeApplicationHost>(Lifetime.Scoped).As<IHomeApplicationHost>();
            builder.RegisterComponent(homeMenuView).As<IHomeMenuView>();
            builder.RegisterEntryPoint<HomeMenuPresenter>();
        }
    }
}
