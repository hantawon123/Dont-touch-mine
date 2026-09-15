using UnityEngine;

namespace Game.Client.Intro
{
    /// <summary>
    /// Animator/AnimationClip 없이 스프라이트 시트를 프레임 단위로 재생한다.
    /// 인트로처럼 클립 1개만 도는 경우 이쪽이 세팅이 훨씬 가볍고,
    /// phaseOffset 으로 캐릭터마다 발을 어긋나게 만들기도 쉽다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteSheetAnimator : MonoBehaviour
    {
        [Tooltip("Sliced 로 자른 8프레임을 순서대로 넣는다. (에디터 빌더가 자동으로 채워줌)")]
        public Sprite[] frames;

        [Tooltip("초당 프레임 수. 5초 동안 12사이클 x 8프레임 = 19.2fps")]
        public float fps = 19.2f;

        [Tooltip("시작 프레임을 몇 칸 밀지. 도둑마다 다르게 주면 동작이 겹치지 않는다.")]
        public int phaseOffset = 0;

        SpriteRenderer _sr;
        int _last = -1;

        void Awake() => _sr = GetComponent<SpriteRenderer>();

        void LateUpdate()
        {
            if (frames == null || frames.Length == 0) return;

            float t = IntroLoopController.Now();
            int i = (Mathf.FloorToInt(t * fps) + phaseOffset) % frames.Length;
            if (i < 0) i += frames.Length;

            if (i != _last)
            {
                _last = i;
                _sr.sprite = frames[i];
            }
        }
    }
}
