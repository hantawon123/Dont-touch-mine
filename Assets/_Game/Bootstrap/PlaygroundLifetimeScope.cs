using System;
using System.Collections.Generic;
using Game.Client.Match;
using Game.Client.Players;
using Game.Core.Match;
using Game.Core.Players;
using Game.Network.Players;
using Game.Network.Session;
using Game.Server.Match;
using Game.Server.Players;
using Game.SOAP.Config;
using UnityEngine;
using UnityEngine.SceneManagement;
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
        private bool waitingForSceneLoad;

        [SerializeField]
        private MatchRulesSO matchRules;

        [SerializeField]
        [Tooltip("Optional HUD root. Leave empty until the scene UI is laid out.")]
        private NetworkMatchHudView matchHudView;

        protected override void Awake()
        {
            if (gameObject.scene.isLoaded)
            {
                base.Awake();
                return;
            }

            waitingForSceneLoad = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        protected override void OnDestroy()
        {
            if (waitingForSceneLoad)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
            }

            base.OnDestroy();
        }

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

            var matchScene = PlaygroundMatchScene.Capture(gameObject.scene);
            builder.RegisterInstance(matchScene.RuntimeContext)
                .As<IMatchRuntimeContext>();
            builder.RegisterInstance(matchScene.NetworkConfiguration);
            builder.Register<MatchRuntimeFactory>(Lifetime.Scoped);
            builder.RegisterEntryPoint<NetworkMatchRuntimeCoordinator>();
            builder.RegisterEntryPoint<NetworkInteractionSceneBridge>();
            builder.RegisterEntryPoint<NetworkHighlightPlaybackController>();
            builder.RegisterEntryPoint<NetworkResultLobbyReturnController>();
            builder.RegisterEntryPoint<InGamePlayerNameplatePresenter>();

            if (matchHudView != null)
            {
                builder.RegisterComponent(matchHudView).As<INetworkMatchHudView>();
                builder.RegisterEntryPoint<NetworkMatchHudPresenter>();
            }

        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!waitingForSceneLoad || scene != gameObject.scene)
            {
                return;
            }

            waitingForSceneLoad = false;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            base.Awake();
        }
    }

    internal sealed class InGamePlayerNameplatePresenter : ITickable, IDisposable
    {
        private readonly NetworkRunnerService network;
        private readonly Dictionary<PlayerAvatar, PlayerNameplateView> views = new();

        public InGamePlayerNameplatePresenter(NetworkRunnerService network)
        {
            this.network = network ?? throw new ArgumentNullException(nameof(network));
        }

        public void Tick()
        {
            var avatars = network.PlayerAvatars;
            for (var index = 0; index < avatars.Count; index++)
            {
                var avatar = avatars[index];
                if (avatar == null)
                {
                    continue;
                }

                if (!views.TryGetValue(avatar, out var view) || view == null)
                {
                    view = PlayerNameplateView.Attach(avatar.transform);
                    views[avatar] = view;
                }

                if (!view.HasNickname)
                {
                    view.SetNickname(avatar.Nickname.ToString());
                }
            }
        }

        public void Dispose()
        {
            foreach (var view in views.Values)
            {
                if (view != null)
                {
                    UnityEngine.Object.Destroy(view.gameObject);
                }
            }

            views.Clear();
        }
    }
}
