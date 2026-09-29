using UnityEngine;

namespace Game.Client.Intro
{
    /// <summary>
    /// 인트로 마지막 fadeDuration 초 동안 화면 전체를 배경색으로 서서히 덮는다.
    /// 종료 시점(loopDuration x loopCount)에 정확히 완전 불투명이 되고, 그 뒤 Home 이 로드된다.
    /// 도둑들은 페이드 중에도 계속 달리므로 "밤이 삼키는" 느낌으로 끝난다.
    ///
    /// 별도 텍스처 없이 Texture2D.whiteTexture 로 스프라이트를 만들어 쓰고,
    /// 카메라 화면보다 훨씬 크게 늘려 두므로 해상도/비율과 무관하게 전체를 덮는다.
    /// autoFinish 가 꺼진(무한 루프) 컨트롤러에서는 아무 것도 하지 않는다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class IntroFadeOut : MonoBehaviour
    {
        [Tooltip("페이드 길이(초). 인트로 총 길이 안에 포함된다 (뒤에 덧붙이지 않음).")]
        [Min(0f)] public float fadeDuration = 0.7f;

        [Tooltip("덮는 색. 알파는 무시하고 스크립트가 0 -> 1 로 올린다. 기본 = 배경색 #030813")]
        public Color color = new Color32(0x03, 0x08, 0x13, 0xFF);

        SpriteRenderer _sr;

        void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            if (_sr.sprite == null)
                _sr.sprite = Sprite.Create(Texture2D.whiteTexture,
                                           new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
                                           new Vector2(0.5f, 0.5f), 1f);   // 1 px = 1 유닛 -> 4x4 유닛
            transform.localScale = Vector3.one * 20f;                      // 80x80 유닛, 화면(19.2x10.8)을 넉넉히 덮음
            SetAlpha(0f);
        }

        void LateUpdate()
        {
            var ctrl = IntroLoopController.Instance;
            if (ctrl == null || !ctrl.autoFinish || fadeDuration <= 0f) { SetAlpha(0f); return; }

            float end   = ctrl.loopDuration * ctrl.loopCount;
            float start = end - fadeDuration;
            float k     = Mathf.Clamp01((ctrl.Elapsed - start) / fadeDuration);
            SetAlpha(Mathf.SmoothStep(0f, 1f, k));
        }

        void SetAlpha(float a)
        {
            var c = color; c.a = a;
            _sr.color = c;
        }
    }
}
