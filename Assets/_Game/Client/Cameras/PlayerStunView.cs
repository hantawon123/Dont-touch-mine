using UnityEngine;

namespace Game.Client.Cameras
{
    /// <summary>
    /// Maps a falling head bone to a first-person look so knockout tilts the
    /// view toward the ceiling instead of leaving the camera at standing height.
    /// </summary>
    public static class PlayerStunView
    {
        public static Quaternion Calibrate(Quaternion headRotation, Quaternion bodyRotation) =>
            Quaternion.Inverse(headRotation) * bodyRotation;

        public static Pose Pose(Transform head, Quaternion headToBody)
        {
            if (head == null)
            {
                return default;
            }

            return new Pose(head.position, head.rotation * headToBody);
        }

        /// <summary>
        /// 위를 올려다보는 각도만 뽑아낸다. 천장을 보는 순간 요(yaw)와 롤은 같은 축이 되어
        /// 오일러로 풀면 값이 튀고, 그대로 카메라에 넣으면 쓰러졌다 일어나는 동안 화면이
        /// 한 바퀴 돈다. 앞벡터의 높이만 쓰면 그 축 뒤틀림이 끼어들 자리가 없다.
        /// </summary>
        public static float Pitch(Quaternion view)
        {
            var forward = view * Vector3.forward;
            return -Mathf.Asin(Mathf.Clamp(forward.y, -1f, 1f)) * Mathf.Rad2Deg;
        }

        /// <summary>올려다보는 각도는 머리에서, 좌우 방향은 고정한 값에서 가져온 시점.</summary>
        public static Quaternion Stabilize(Quaternion view, float lockedYaw) =>
            Quaternion.Euler(Pitch(view), lockedYaw, 0f);

        public static bool LooksSkyward(Quaternion view, float threshold = 0.35f) =>
            (view * Vector3.forward).y > threshold;
    }
}
