using Game.Client.Home;
using Game.Client.Settings;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Bootstrap
{
    public sealed class SettingsLifetimeScope : LifetimeScope
    {
        [SerializeField]
        private SettingsView settingsView;

        protected override void Configure(IContainerBuilder builder)
        {
            if (settingsView == null)
            {
                Debug.LogError("SettingsView must be assigned on SettingsLifetimeScope.", this);
                return;
            }

            builder.Register<UnityHomeApplicationHost>(Lifetime.Scoped).As<IHomeApplicationHost>();
            builder.RegisterComponent(settingsView).As<ISettingsView>();
            builder.RegisterEntryPoint<SettingsPresenter>();
        }
    }
}
