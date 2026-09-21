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

        public static bool LooksSkyward(Quaternion view, float threshold = 0.35f) =>
            (view * Vector3.forward).y > threshold;
    }
}
