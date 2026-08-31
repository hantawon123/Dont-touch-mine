using System;
using Game.Client.Match;
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
            if (view == null) throw new InvalidOperationException("ResultLifetimeScope: ResultView를 연결하세요.");
            view.Initialize();
            builder.RegisterComponent(view).As<IResultView>();
            builder.RegisterEntryPoint<ResultPresenter>();
        }
    }

    public sealed class ResultPresenter : IStartable, IDisposable
    {
        private readonly NetworkResultLobbyReturnController result;
        private readonly IResultView view;
        private IDisposable subscription;

        public ResultPresenter(NetworkResultLobbyReturnController result, IResultView view)
        {
            this.result = result;
            this.view = view;
        }

        public void Start() => subscription = result.ResultText.Subscribe(view.SetText);
        public void Dispose() => subscription?.Dispose();
    }
}
