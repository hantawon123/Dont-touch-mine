using UnityEngine;

namespace Game.Client.Intro
{
    /// <summary>
    /// 도둑 한 명을 화면 **오른쪽 -> 왼쪽**으로 등속 이동시키고,
    /// 왼쪽 밖으로 나가면 다시 오른쪽에서 등장시킨다.
    /// Mathf.Repeat 로 위치를 계산하므로 프레임레이트와 무관하게 루프가 정확히 이어진다.
    /// (스프라이트 자체가 왼쪽을 보도록 그려져 있으므로 flipX 는 건드리지 않는다.)
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class ThiefRunner : MonoBehaviour
    {
        [Header("이동 (월드 단위)")]
        [Tooltip("가로로 한 바퀴 도는 거리. 화면 폭(19.2) + 좌우 여유. 5초 루프면 speed = loopWidth / 5")]
        public float loopWidth = 27.2f;

        [Tooltip("초당 이동 거리. loopWidth / loopDuration 과 같아야 루프가 맞는다.")]
        public float speed = 5.44f;

        [Tooltip("무리 안에서의 위치. 값이 클수록 앞(왼쪽)에 선다.")]
        public float startOffset = 0f;

        [Tooltip("등장하는 쪽 끝 x. 오른쪽에서 나오므로 화면 오른쪽(+9.6)보다 바깥.")]
        public float spawnEdge = 13.4f;

        [Tooltip("진행 방향. -1 = 오른쪽에서 왼쪽(기본), +1 = 왼쪽에서 오른쪽")]
        public int direction = -1;

        [Header("바닥")]
        [Tooltip("발이 닿는 높이. 스프라이트 피벗을 발밑(0.5, 0.059)으로 잡아둬야 정확히 맞는다.")]
        public float groundY = -3.6f;

        [Tooltip("달릴 때 위아래로 미세하게 흔들리는 폭. 0 이면 스프라이트 애니메이션만 사용.")]
        public float extraBob = 0f;

        void LateUpdate()
        {
            float t = IntroLoopController.Now();

            float x = spawnEdge + direction * Mathf.Repeat(startOffset + speed * t, loopWidth);

            float y = groundY;
            if (extraBob > 0f)
            {
                // 러닝 사이클 1회당 2번 튀는 리듬
                float cyclesPerSecond = speed / 2.27f;
                y += Mathf.Abs(Mathf.Sin(Mathf.PI * 2f * cyclesPerSecond * t)) * extraBob;
            }

            transform.position = new Vector3(x, y, transform.position.z);
        }
    }
}
