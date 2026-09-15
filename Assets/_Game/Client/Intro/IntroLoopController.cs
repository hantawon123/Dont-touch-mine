using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Game.Client.Intro
{
    /// <summary>
    /// 인트로 전체의 "마스터 시계".
    /// 도둑 이동/애니메이션이 전부 이 시간을 참조하므로 5초 루프가 정확히 맞아떨어진다.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class IntroLoopController : MonoBehaviour
    {
        public static IntroLoopController Instance { get; private set; }

        [Header("루프")]
        [Tooltip("한 루프의 길이(초). 요구사항 = 5초")]
        public float loopDuration = 5f;

        [Tooltip("Time.timeScale 영향을 받지 않게 할지 (로딩 중 정지 방지)")]
        public bool useUnscaledTime = true;

        [Header("종료 처리")]
        [Tooltip("켜면 loopCount 바퀴를 돌고 onIntroFinished 를 호출. 끄면 무한 루프.")]
        public bool autoFinish = false;

        [Min(1)] public int loopCount = 1;

        [Tooltip("아무 키/클릭으로 스킵 허용. 기본 OFF — Play 버튼 클릭이 첫 프레임에 스킵으로 잡히는 것도 막는다.")]
        public bool allowSkip = false;

        public UnityEvent onIntroFinished;

        /// <summary>인트로 시작 후 누적 경과 시간(초).</summary>
        public float Elapsed { get; private set; }

        /// <summary>현재 루프 안에서의 시간 [0, loopDuration).</summary>
        public float LoopTime => Mathf.Repeat(Elapsed, loopDuration);

        /// <summary>현재 루프 진행도 [0,1).</summary>
        public float Loop01 => LoopTime / loopDuration;

        bool _finished;
        bool _firstFrame = true;

        /// <summary>
        /// 한 프레임에 더할 수 있는 최대 시간. 에디터에서 Play 직후 첫 프레임이나 렉이 걸린 프레임은
        /// unscaledDeltaTime 이 몇 초씩 나올 수 있어서(컴파일·씬 로딩 시간 포함), 그대로 더하면
        /// 인트로가 시작하자마자 5초를 넘겨 바로 끝나 버린다. Time.maximumDeltaTime 은 unscaled 에는
        /// 적용되지 않으므로 여기서 직접 막는다.
        /// </summary>
        const float MaxFrameDelta = 0.1f;

        void Awake()
        {
            Instance = this;
            Elapsed = 0f;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (_firstFrame) { _firstFrame = false; dt = 0f; }   // 첫 프레임은 로딩 시간이 섞여 있어 버린다
            Elapsed += Mathf.Min(dt, MaxFrameDelta);

            if (_finished) return;

            if (allowSkip && SkipPressed())
            {
                Finish();
                return;
            }

            if (autoFinish && Elapsed >= loopDuration * loopCount)
                Finish();
        }

        /// <summary>
        /// 아무 키 / 마우스 왼쪽 클릭. 이 프로젝트는 새 Input System 만 활성화되어 있어
        /// 옛 UnityEngine.Input 을 읽으면 예외가 나므로 컴파일 심볼로 분기한다.
        /// </summary>
        static bool SkipPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            var ms = Mouse.current;
            return (kb != null && kb.anyKey.wasPressedThisFrame)
                || (ms != null && ms.leftButton.wasPressedThisFrame);
#else
            return Input.anyKeyDown || Input.GetMouseButtonDown(0);
#endif
        }

        public void Finish()
        {
            if (_finished) return;
            _finished = true;
            Debug.Log($"[Intro] 인트로 종료 (경과 {Elapsed:F2}초, 프레임 {Time.frameCount})");
            onIntroFinished?.Invoke();
        }

        /// <summary>
        /// 인트로 공통 시계. 컨트롤러가 씬에 없어도 스크립트들이 동작하도록 폴백을 둔다.
        /// </summary>
        public static float Now()
        {
            return Instance != null ? Instance.Elapsed : Time.unscaledTime;
        }
    }
}
