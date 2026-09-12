using UnityEngine;

namespace Game.Client.Interactions
{
    /// <summary>
    /// 배치 부피(콜라이더 기준 상자) 계산을 클라이언트 미리보기와 권한자 판정이 함께 쓴다.
    /// 두 곳의 식이 달라지면 "초록인데 서버가 거부"가 생기므로 여기 한 곳에만 둔다.
    /// </summary>
    public static class PlacementVolumeMath
    {
        /// <summary>
        /// 회전한 배치 상자의 중심에서 가장 낮은 점까지의 세로 거리(회전한 AABB 반높이).
        /// 받침면 검사 광선은 이 길이만큼 내려야 기울인 물건도 바닥을 찾는다.
        /// </summary>
        public static float RotatedVerticalExtent(Quaternion rotation, Vector3 halfExtents)
        {
            return Mathf.Abs((rotation * new Vector3(halfExtents.x, 0f, 0f)).y) +
                   Mathf.Abs((rotation * new Vector3(0f, halfExtents.y, 0f)).y) +
                   Mathf.Abs((rotation * new Vector3(0f, 0f, halfExtents.z)).y);
        }
    }
}
