using UnityEngine;

namespace Game.Client.Intro
{
    /// <summary>
    /// 인트로 믹스의 기본 음량을 유지하다가 화면 페이드와 같은 구간에서 소리를 줄인다.
    /// 영상과 오디오는 둘 다 씬 진입 시 시작하므로 발 착지 타이밍이 그대로 맞는다.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class IntroAudioFade : MonoBehaviour
    {
        [Range(0f, 1f)] public float baseVolume = 0.8f;
        [Min(0f)] public float fadeDuration = 0.7f;

        AudioSource _source;

        void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.volume = baseVolume;
        }

        void LateUpdate()
        {
            var ctrl = IntroLoopController.Instance;
            if (ctrl == null || !ctrl.autoFinish || fadeDuration <= 0f)
            {
                _source.volume = baseVolume;
                return;
            }

            float end = ctrl.loopDuration * ctrl.loopCount;
            float fade01 = Mathf.Clamp01((ctrl.Elapsed - (end - fadeDuration)) / fadeDuration);
            _source.volume = baseVolume * (1f - Mathf.SmoothStep(0f, 1f, fade01));
        }
    }
}
