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
        [SerializeField] private EndingStage endingStage;
        private bool waitingForSceneLoad;
        private GameObject[] sceneRoots = Array.Empty<GameObject>();

        protected override void Awake()
        {
            sceneRoots = gameObject.scene.GetRootGameObjects();
            DisableAdditiveSceneOutputs();
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
            DisableAdditiveSceneOutputs();
            base.Awake();
        }

        protected override void OnDestroy()
        {
            if (waitingForSceneLoad) SceneManager.sceneLoaded -= OnSceneLoaded;
            base.OnDestroy();
        }

        protected override void Configure(IContainerBuilder builder)
        {
            if (endingStage != null)
            {
                builder.RegisterComponent(endingStage);
                builder.RegisterEntryPoint<EndingStagePlacementController>();
            }
            if (DedicatedServerStartup.IsRequested) return;
            var configureStartedAt = Time.realtimeSinceStartupAsDouble;
            if (view == null) throw new InvalidOperationException("ResultLifetimeScope: ResultView를 연결하세요.");
            view.Initialize();
            builder.RegisterComponent(view).As<IResultView>();
            builder.RegisterEntryPoint<ResultPresenter>();
            if (endingStage != null)
            {
                // 클라이언트는 무대 카메라와 로컬 입력·표시만 담당한다.
                builder.RegisterEntryPoint<EndingStagePresenter>();
            }
            builder.RegisterBuildCallback(_ => Debug.Log(
                $"[SceneTiming] Result scope ready, " +
                $"elapsed={Time.realtimeSinceStartupAsDouble - configureStartedAt:F3}s."));
        }

        private void DisableAdditiveSceneOutputs()
        {
            // Result is a screen-space overlay. Fusion can merge network-loaded
            // content into one Unity scene, so SceneManager.sceneCount cannot
            // tell whether its camera would compete with Playground's output.
            // The ending stage is the exception: its camera and lights are the
            // point of the scene. Its presenter turns the camera on when it runs.
            foreach (var root in sceneRoots)
            {
                foreach (var camera in root.GetComponentsInChildren<Camera>(true))
                    if (!IsOnEndingStage(camera.transform)) camera.enabled = false;
                foreach (var listener in root.GetComponentsInChildren<AudioListener>(true))
                    listener.enabled = false;
                foreach (var light in root.GetComponentsInChildren<Light>(true))
                    if (!IsOnEndingStage(light.transform)) light.enabled = false;
            }
        }

        private bool IsOnEndingStage(Transform target) =>
            endingStage != null && target.IsChildOf(endingStage.transform);
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
            subscription = result.ResultText.Subscribe(_ =>
            {
                if (string.IsNullOrEmpty(result.ResultHeadline))
                {
                    view.SetText(result.ResultSubtitle);
                    return;
                }

                view.SetOutcome(result.ResultHeadline, result.ResultSubtitle);
            });
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
            // Result unload is followed by replay preparation, which must stay
            // covered until every peer is ready.
            transition.SetOpacity(1f);
        }
    }
}
