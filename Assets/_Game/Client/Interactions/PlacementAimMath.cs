using UnityEngine;

namespace Game.Client.Interactions
{
    /// <summary>
    /// 배치 모드의 순수 계산. Unity 물리·컴포넌트 없이 검증할 수 있게 떼어 두었다.
    /// </summary>
    public static class PlacementAimMath
    {
        /// <summary>
        /// 시선 방향으로 뻗을 수 있는 최대 손 거리: 카메라에서 시선을 따라 나갈 때, 발 위치에서 <paramref name="reach"/> 안에
        /// 들어오는 가장 먼 점까지의 거리. 발 쪽으로 끌어당기지 않고 시선 위에서만 거리를 줄이므로, 어느 방향을 봐도 물건은
        /// 크로스헤어 위에 남는다. 발이 이미 한도 밖(예외 상황)이면 <paramref name="minDistance"/>.
        /// </summary>
        public static float MaxDistanceAlongView(
            Vector3 cameraPosition, Vector3 viewForward, Vector3 feetPosition, float reach, float minDistance)
        {
            var e = cameraPosition - feetPosition;           // 발 → 카메라(눈높이)
            var b = Vector3.Dot(e, viewForward);
            var c = e.sqrMagnitude - reach * reach;
            var disc = b * b - c;                            // |e + f·d|² = reach² 의 판별식
            if (disc <= 0f) return minDistance;
            return Mathf.Max(minDistance, -b + Mathf.Sqrt(disc));
        }

        /// <summary>
        /// 기즈모 원이 색으로 차는 비율: 돌린 각도 / 360, 0..1. 180도면 반, 360도 이상이면 전부.
        /// </summary>
        public static float FillFraction(float rotatedDegrees) =>
            Mathf.Clamp01(Mathf.Abs(rotatedDegrees) / 360f);

        /// <summary>
        /// 우클릭 드래그 입력을 한 축으로 정리한다: 한 축이 다른 축보다 <paramref name="dominantRatio"/>배 이상 크면 작은 축을
        /// 0으로 만든다(손떨림이 다른 축 회전으로 새지 않게). 대각선 의도는 그대로 둔다.
        /// </summary>
        public static Vector2 FilterDominantAxis(Vector2 pixels, float dominantRatio)
        {
            if (Mathf.Abs(pixels.x) > Mathf.Abs(pixels.y) * dominantRatio) pixels.y = 0f;
            else if (Mathf.Abs(pixels.y) > Mathf.Abs(pixels.x) * dominantRatio) pixels.x = 0f;
            return pixels;
        }
    }
}
