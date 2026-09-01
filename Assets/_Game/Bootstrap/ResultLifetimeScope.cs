using System;
using Game.Client.Match;
using Game.Core.Match;
using R3;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

namespace Game.Bootstrap
{
    public sealed class ResultLifetimeScope : LifetimeScope
    {
        [SerializeField] private ResultView view;
        private bool waitingForSceneLoad;

        protected override void Awake()
        {
            if (gameObject.scene.isLoaded) base.Awake();
            else
            {
                waitingForSceneLoad = true;
                SceneManager.sceneLoaded += OnSceneLoaded;
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene != gameObject.scene) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            waitingForSceneLoad = false;
            base.Awake();
        }

        protected override void OnDestroy()
        {
            if (waitingForSceneLoad) SceneManager.sceneLoaded -= OnSceneLoaded;
            base.OnDestroy();
        }

        protected override void Configure(IContainerBuilder builder)
        {
            var configureStartedAt = Time.realtimeSinceStartupAsDouble;
            if (view == null) throw new InvalidOperationException("ResultLifetimeScope: ResultView를 연결하세요.");
            view.Initialize();
            builder.RegisterComponent(view).As<IResultView>();
            builder.RegisterEntryPoint<ResultPresenter>();
            builder.RegisterBuildCallback(_ => Debug.Log(
                $"[SceneTiming] Result scope ready, " +
                $"elapsed={Time.realtimeSinceStartupAsDouble - configureStartedAt:F3}s."));
        }
    }

    public sealed class ResultPresenter : IStartable, ITickable, IDisposable
    {
        private readonly NetworkResultLobbyReturnController result;
        private readonly IResultView view;
        private readonly IHighlightTransitionView transition;
        private float fadeElapsed;
        private IDisposable subscription;

        public ResultPresenter(NetworkResultLobbyReturnController result, IResultView view,
            IHighlightTransitionView transition)
        {
            this.result = result;
            this.view = view;
            this.transition = transition;
        }

        public void Start()
        {
            transition.SetOpacity(1f);
            subscription = result.ResultText.Subscribe(view.SetText);
        }

        public void Tick() => Tick(Time.unscaledDeltaTime);

        internal void Tick(float deltaSeconds)
        {
            fadeElapsed += deltaSeconds;
            transition.SetOpacity(1f - Mathf.Clamp01(fadeElapsed / (float)HighlightPresentationTiming.FadeSeconds));
        }

        public void Dispose()
        {
            subscription?.Dispose();
            transition.SetOpacity(0f);
        }
    }
}
