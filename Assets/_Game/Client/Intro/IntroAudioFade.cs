using UnityEngine;

namespace Game.Client.Intro
{
    /// <summary>
    /// 인트로 믹스의 기본 음량을 유지하다가 짧아진 화면 종료 시점에 앞서 소리를 줄인다.
    /// 영상과 오디오는 둘 다 씬 진입 시 시작하므로 발 착지 타이밍이 그대로 맞는다.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class IntroAudioFade : MonoBehaviour
    {
        [Range(0f, 1f)] public float baseVolume = 0.8f;
        [Min(0f)] public float fadeDuration = 0.3f;

        [Tooltip("화면 종료보다 먼저 소리가 끝나는 시간(초). 홈 전환 전에 발소리를 정리한다.")]
        [Min(0f)] public float fadeEndLeadTime = 0.5f;

        AudioSource _source;

        void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.volume = baseVolume;
        }

        void LateUpdate()
        {
            var ctrl = IntroLoopController.Instance;
            if (ctrl == null || !ctrl.autoFinish)
            {
                _source.volume = baseVolume;
                return;
            }

            float end = Mathf.Max(0f, ctrl.loopDuration * ctrl.loopCount - fadeEndLeadTime);
            float fade01 = fadeDuration > 0f
                ? Mathf.Clamp01((ctrl.Elapsed - (end - fadeDuration)) / fadeDuration)
                : (ctrl.Elapsed >= end ? 1f : 0f);
            _source.volume = baseVolume * (1f - Mathf.SmoothStep(0f, 1f, fade01));
        }
    }
}
