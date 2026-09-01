using System;
using System.Collections.Generic;
using Game.Client.Match;
using Game.Client.Players;
using Game.Client.Voice;
using Game.Core.Match;
using Game.Core.Players;
using Game.Core.Ports;
using Game.Network.Players;
using Game.Network.Session;
using Game.Server.Match;
using Game.Server.Players;
using Game.SOAP.Config;
using UnityEngine;
using UnityEngine.InputSystem;
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

        [SerializeField]
        [Tooltip("Microphone button on the match HUD.")]
        private VoiceView voiceView;

        /// <summary>
        /// Read for the talk keys. Held here rather than reached through a
        /// character, which is spawned by Fusion after this scope is built.
        /// </summary>
        [SerializeField]
        private InputActionAsset inputActions;

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
            var configureStartedAt = Time.realtimeSinceStartupAsDouble;
            if (matchRules == null)
            {
                throw new InvalidOperationException("PlaygroundLifetimeScope: MatchRulesSO를 연결하세요.");
            }

            builder.RegisterInstance(matchRules);
            builder.Register<PlayerInteractionSystem>(Lifetime.Scoped)
                .AsSelf()
                .As<IPlayerCombatRules>();

            var captureStartedAt = Time.realtimeSinceStartupAsDouble;
            var matchScene = PlaygroundMatchScene.Capture(gameObject.scene);
            Debug.Log(
                $"[SceneTiming] Playground scene capture completed, " +
                $"elapsed={Time.realtimeSinceStartupAsDouble - captureStartedAt:F3}s.");
            builder.RegisterInstance(matchScene.RuntimeContext)
                .As<IMatchRuntimeContext>();
            builder.RegisterInstance(matchScene.NetworkConfiguration);
            builder.Register<MatchRuntimeFactory>(Lifetime.Scoped);
            builder.RegisterEntryPoint<NetworkMatchRuntimeCoordinator>();
            builder.RegisterEntryPoint<NetworkInteractionSceneBridge>();
            builder.RegisterEntryPoint<NetworkHighlightPlaybackController>().AsSelf();
            builder.RegisterEntryPoint<InGamePlayerNameplatePresenter>();

            if (matchHudView != null)
            {
                builder.RegisterComponent(matchHudView).As<INetworkMatchHudView>();
                builder.RegisterEntryPoint<NetworkMatchHudPresenter>();
            }

            // The rig on the runner keeps carrying voice through the match on
            // its own. What the match lacks is a way to speak to it, so the
            // control and the button are what get registered here. The mute
            // choice itself comes from the project scope and is already set.
            if (voiceView != null && inputActions != null)
            {
                builder.RegisterComponent(voiceView).As<IVoiceView>();
                builder.RegisterInstance(inputActions);
                builder.RegisterEntryPoint<NetworkVoiceControl>().As<IVoiceControl>();
                builder.RegisterEntryPoint<VoicePresenter>();
            }

            builder.RegisterBuildCallback(_ => Debug.Log(
                $"[SceneTiming] Playground scope ready, " +
                $"elapsed={Time.realtimeSinceStartupAsDouble - configureStartedAt:F3}s."));
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
