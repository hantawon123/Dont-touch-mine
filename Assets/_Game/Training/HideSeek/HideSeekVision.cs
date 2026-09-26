using UnityEngine;

namespace Game.Training.HideSeek
{
    /// <summary>
    /// Player-equivalent numbers (docs/planning/hide-seek-v1.md, H5/H7). Movement and eye heights come from
    /// MovementConfig.asset, the field of view from PlayerCameraRig (vertical 60 deg, 16:9), the body from
    /// NetworkedPlayer (r 0.31, h 1.67), grab distance from InteractionAuthorityRules (2 m).
    /// </summary>
    public static class HideSeekRules
    {
        public const float WalkSpeed = 3.5f;
        public const float SprintSpeed = 6.3f;

        /// <summary>User decision (H7, 2026-09-25): bots never sprint. Jumping is allowed in the game but the
        /// NavMesh walking in this simulator never needs it.</summary>
        public const bool BotsCanSprint = false;
        public const float CrouchSpeed = 1.5f;
        public const float MaxStamina = 100f;
        public const float StaminaDrainPerSecond = 20f;
        public const float StaminaRecoveryPerSecond = 25f;
        public const float TurnDegreesPerSecond = 720f;

        public const float StandEyeHeight = 1.42f;
        public const float CrouchEyeHeight = 0.85f;
        public const float ProneEyeHeight = 0.46f;

        public const float BodyRadius = 0.31f;
        public const float BodyHeight = 1.67f;

        public const float VerticalFov = 60f;
        public static readonly float HorizontalFov = 2f * Mathf.Atan(Mathf.Tan(VerticalFov * 0.5f * Mathf.Deg2Rad) * 16f / 9f) * Mathf.Rad2Deg;
        public const float SightRange = 20f;
        public const int MinVisiblePoints = 3;
        public const float MinAngularDegrees = 1f;

        public const float GrabDistance = 2f;
        public const float HideDeadlineSeconds = 20f;
        public const float EpisodeSeconds = 150f;
        public const float VisionIntervalSeconds = 0.1f;

        /// <summary>Gaze pitch while walking and while scanning (players look down a little to search).</summary>
        public const float WalkPitch = 0f;
        public const float ScanPitch = -20f;
        public const float CrouchScanPitch = -25f;
    }

    /// <summary>
    /// What a player could see: inside the camera cone (level-ish gaze, vertical 60 x horizontal ~91 deg), within
    /// 20 m, with a clear line (every collider occludes: walls, furniture and props). Never through walls.
    /// </summary>
    public static class HideSeekVision
    {
        private static readonly Vector3[] Samples = new Vector3[HideScoreV3.SamplesPerBox];

        public static bool InCone(Vector3 eye, float yawDegrees, float pitchDegrees, Vector3 point)
        {
            var offset = point - eye;
            var distance = offset.magnitude;
            if (distance < 0.05f)
            {
                return true;
            }

            if (distance > HideSeekRules.SightRange)
            {
                return false;
            }

            // Camera-space frustum test (like a real camera): rotate the offset into the view frame, then compare
            // the horizontal and vertical angles on the image plane. Measuring yaw and pitch separately in world
            // space broke for points almost straight below (the prop at one's feet): their yaw is arbitrary.
            var view = Quaternion.Inverse(Quaternion.Euler(-pitchDegrees, yawDegrees, 0f)) * offset;
            if (view.z <= 0.01f)
            {
                return false;
            }

            var horizontal = Mathf.Atan2(Mathf.Abs(view.x), view.z) * Mathf.Rad2Deg;
            var vertical = Mathf.Atan2(Mathf.Abs(view.y), view.z) * Mathf.Rad2Deg;
            return horizontal <= HideSeekRules.HorizontalFov * 0.5f && vertical <= HideSeekRules.VerticalFov * 0.5f;
        }

        /// <summary>
        /// In the real match (ThiefNpcDirector) the avatars and the players' items have colliders that the simulator
        /// never had; they are skipped so that sight lines match training. Null during training.
        /// </summary>
        public static System.Func<Collider, bool> IgnoreCollider;

        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        public static bool Clear(Vector3 eye, Vector3 point, int occluders)
        {
            var offset = point - eye;
            var distance = offset.magnitude;
            if (distance < 0.05f)
            {
                return true;
            }

            if (IgnoreCollider == null)
            {
                return !Physics.Raycast(eye, offset / distance, distance - 0.03f, occluders, QueryTriggerInteraction.Ignore);
            }

            return !Raycast(eye, offset / distance, distance - 0.03f, occluders, out _);
        }

        /// <summary>Nearest hit that is not ignored (see <see cref="IgnoreCollider"/>).</summary>
        public static bool Raycast(Vector3 origin, Vector3 direction, float distance, int occluders, out float hitDistance)
        {
            hitDistance = distance;
            if (IgnoreCollider == null)
            {
                if (!Physics.Raycast(origin, direction, out var hit, distance, occluders, QueryTriggerInteraction.Ignore)) return false;
                hitDistance = hit.distance;
                return true;
            }

            var n = Physics.RaycastNonAlloc(origin, direction, Hits, distance, occluders, QueryTriggerInteraction.Ignore);
            var found = false;
            for (var i = 0; i < n; i++)
            {
                if (IgnoreCollider(Hits[i].collider) || Hits[i].distance >= hitDistance) continue;
                hitDistance = Hits[i].distance;
                found = true;
            }

            return found;
        }

        /// <summary>A placed or held box is seen when at least 3 of its 10 points are in the cone and unblocked,
        /// and it is not smaller than 1 degree on screen.</summary>
        public static bool CanSeeBox(Vector3 eye, float yaw, float pitch, Vector3 bottom, Vector3 half, int occluders)
        {
            var centre = bottom + Vector3.up * half.y;
            var distance = Vector3.Distance(eye, centre);
            if (distance > HideSeekRules.SightRange)
            {
                return false;
            }

            var angular = 2f * Mathf.Atan(Mathf.Max(half.x, half.y, half.z) / Mathf.Max(0.01f, distance)) * Mathf.Rad2Deg;
            if (angular < HideSeekRules.MinAngularDegrees)
            {
                return false;
            }

            HideScoreV3.SamplePoints(bottom, half, Samples);
            var seen = 0;
            for (var i = 0; i < Samples.Length; i++)
            {
                if (InCone(eye, yaw, pitch, Samples[i]) && Clear(eye, Samples[i], occluders))
                {
                    seen++;
                    if (seen >= HideSeekRules.MinVisiblePoints)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>A body is seen when its head, chest or hips is in the cone and unblocked.</summary>
        public static bool CanSeeBody(Vector3 eye, float yaw, float pitch, Vector3 feet, float bodyHeight, int occluders)
        {
            for (var i = 0; i < 3; i++)
            {
                var point = feet + Vector3.up * (bodyHeight * (i == 0 ? 0.9f : i == 1 ? 0.65f : 0.4f));
                if (InCone(eye, yaw, pitch, point) && Clear(eye, point, occluders))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
