using Game.Client;
using Game.Client.Character;
using Game.Client.Combat;
using Game.Client.Tutorial;
using Game.Core.Players;
using Game.Core.Settings;
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

        protected override void Awake()
        {
            // The scene is loaded Single from Home, and this scope used to be
            // a root of its own. Nothing then received the applied language,
            // so the radio, checklist and key guide stayed Korean.
            if (parentReference.Type == null)
            {
                parentReference = ParentReference.Create<ProjectLifetimeScope>();
            }

            base.Awake();
        }

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterInstance(matchRules);
            builder.Register<PlayerInteractionSystem>(Lifetime.Scoped).As<IPlayerCombatRules>();
            builder.RegisterComponentInHierarchy<PlayerCombatant>();
            builder.RegisterComponentInHierarchy<TutorialRadioView>();
            builder.RegisterComponentInHierarchy<TutorialPauseController>();
            builder.RegisterComponentInHierarchy<TutorialItemCourse>();
            builder.RegisterComponentInHierarchy<KeySettingGuideView>();
            builder.RegisterComponentInHierarchy<AvatarAppearanceApplier>();
            builder.RegisterEntryPoint<TutorialChrome>();
            builder.RegisterEntryPoint<TutorialAvatarDresser>();
        }

        /// <summary>
        /// Pushes the applied language onto the tutorial chrome after inject,
        /// in case a view built itself in Awake before BindLocale arrived.
        /// </summary>
        private sealed class TutorialChrome : IStartable
        {
            private readonly UiLocale locale;
            private readonly TutorialRadioView radio;
            private readonly TutorialPauseController pause;
            private readonly TutorialItemCourse items;
            private readonly KeySettingGuideView keyGuide;

            public TutorialChrome(
                UiLocale locale,
                TutorialRadioView radio,
                TutorialPauseController pause,
                TutorialItemCourse items,
                KeySettingGuideView keyGuide)
            {
                this.locale = locale;
                this.radio = radio;
                this.pause = pause;
                this.items = items;
                this.keyGuide = keyGuide;
            }

            public void Start()
            {
                radio.BindLocale(locale);
                pause.BindLocale(locale);
                items.BindLocale(locale);
                keyGuide.ShowChrome(locale);
            }
        }
    }
}
