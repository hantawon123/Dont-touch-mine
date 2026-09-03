using System;
using System.Collections.Generic;
using Game.Client.Match;
using Game.Client.Players;
using Game.Client.Voice;
using Game.Core.Home;
using Game.Core.Lobby;
using Game.Core.Match;
using Game.Core.Players;
using Game.Core.Ports;
using Game.Network.Players;
using Game.Network.Session;
using Game.Server.Match;
using Game.Server.Players;
using Game.SOAP.Config;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
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
        private GameObject[] sceneRoots = Array.Empty<GameObject>();

        internal IReadOnlyList<GameObject> SceneRoots => sceneRoots;

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
            sceneRoots = gameObject.scene.GetRootGameObjects();
            if (gameObject.scene.isLoaded)
            {
                EnsureGameplayEventSystem();
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

            var chatCanvas = matchHudView == null
                ? null
                : matchHudView.GetComponentInParent<Canvas>();
            var chatView = MatchChatView.Create(chatCanvas == null ? null : chatCanvas.transform);
            var chatBubbleView = MatchChatBubbleView.Create(transform);
            builder.RegisterComponent(chatView).As<IMatchChatView>();
            builder.RegisterComponent(chatBubbleView).As<IMatchChatBubbleView>();
            builder.Register(
                    c => CreateChatLog(
                        c.Resolve<RoomBrowserSystem>(),
                        c.Resolve<PlayerProfile>()),
                    Lifetime.Scoped)
                .As<ILobbyChatLog>();
            builder.RegisterEntryPoint<MatchChatPresenter>();
            builder.RegisterEntryPoint<InGameChatBubbleBinder>();

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
            EnsureGameplayEventSystem();
            base.Awake();
        }

        private void EnsureGameplayEventSystem()
        {
            // The project scope's EventSystem belongs to the frontend and is
            // intentionally disabled while Fusion owns a gameplay scene.
            // Keep a scene-local module alive for the in-game chat input.
            var current = EventSystem.current;
            if (current != null && current.gameObject.scene == gameObject.scene)
            {
                return;
            }

            var eventSystemObject = new GameObject(
                "Playground UI EventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule));
            eventSystemObject.transform.SetParent(transform, false);
        }

        private static LobbyChatLog CreateChatLog(
            RoomBrowserSystem room,
            PlayerProfile profile)
        {
            var localPlayerId = room.LocalPlayerId.CurrentValue;
            if (string.IsNullOrWhiteSpace(localPlayerId))
            {
                localPlayerId = "local";
            }

            return new LobbyChatLog(localPlayerId, profile.Nickname);
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

    internal sealed class InGameChatBubbleBinder : ITickable, IDisposable
    {
        private readonly NetworkRunnerService network;
        private readonly IMatchChatBubbleView bubbles;

        public InGameChatBubbleBinder(
            NetworkRunnerService network,
            IMatchChatBubbleView bubbles)
        {
            this.network = network ?? throw new ArgumentNullException(nameof(network));
            this.bubbles = bubbles ?? throw new ArgumentNullException(nameof(bubbles));
        }

        public void Tick()
        {
            var avatars = network.PlayerAvatars;
            for (var index = 0; index < avatars.Count; index++)
            {
                var avatar = avatars[index];
                if (avatar == null || !avatar.isActiveAndEnabled || string.IsNullOrEmpty(avatar.PlayerId))
                {
                    continue;
                }

                bubbles.BindPlayer(avatar.PlayerId, avatar.transform);
            }
        }

        public void Dispose() => bubbles.Clear();
    }
}
