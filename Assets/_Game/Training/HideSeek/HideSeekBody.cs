using UnityEngine;
using UnityEngine.AI;

namespace Game.Training.HideSeek
{
    /// <summary>
    /// Kinematic stand-in for a player body in the hide-seek simulator: follows NavMesh path corners at player
    /// speeds (walk / sprint with stamina / crouch walk), turns at 720 deg/s, has stand and crouch eye heights.
    /// No collider, so bodies of different matches in the same scene never meet.
    /// </summary>
    public sealed class HideSeekBody
    {
        public Vector3 Position;
        public float Yaw;
        public float Pitch;
        public bool Crouched;
        public bool WantsSprint;
        public float Stamina = HideSeekRules.MaxStamina;

        /// <summary>When set, the body keeps its gaze on this point while walking (a player turning the camera toward
        /// what it walks to), instead of facing the path.</summary>
        public bool HasLookTarget;
        public Vector3 LookTarget;

        private readonly NavMeshPath path = new();
        private Vector3[] corners = System.Array.Empty<Vector3>();
        private int corner;

        public bool Moving { get; private set; }
        public float LastPathLength { get; private set; }

        public float EyeHeight => Crouched ? HideSeekRules.CrouchEyeHeight : HideSeekRules.StandEyeHeight;
        public Vector3 Eye => Position + Vector3.up * EyeHeight;

        public void Teleport(Vector3 position, float yaw)
        {
            Position = position;
            Yaw = yaw;
            Pitch = HideSeekRules.WalkPitch;
            Crouched = false;
            Stamina = HideSeekRules.MaxStamina;
            HasLookTarget = false;
            Stop();
        }

        public bool TrySetDestination(Vector3 destination, float sampleRadius = 1.5f)
        {
            Stop();
            if (!NavMesh.SamplePosition(destination, out var hit, sampleRadius, NavMesh.AllAreas) ||
                !NavMesh.CalculatePath(Position, hit.position, NavMesh.AllAreas, path) ||
                path.status != NavMeshPathStatus.PathComplete)
            {
                return false;
            }

            corners = path.corners;
            corner = corners.Length > 1 ? 1 : 0;
            LastPathLength = 0f;
            for (var i = 1; i < corners.Length; i++)
            {
                LastPathLength += Vector3.Distance(corners[i - 1], corners[i]);
            }

            Moving = corners.Length > 0;
            Pitch = HideSeekRules.WalkPitch;
            return Moving;
        }

        public void Stop()
        {
            Moving = false;
            corners = System.Array.Empty<Vector3>();
            corner = 0;
        }

        public void Step(float dt)
        {
            var sprinting = false;
            if (Moving)
            {
                var speed = Crouched ? HideSeekRules.CrouchSpeed : HideSeekRules.WalkSpeed;
                if (HideSeekRules.BotsCanSprint && !Crouched && WantsSprint && Stamina > 0f)
                {
                    speed = HideSeekRules.SprintSpeed;
                    sprinting = true;
                }

                var travel = speed * dt;
                while (travel > 0f && corner < corners.Length)
                {
                    var target = corners[corner];
                    var offset = target - Position;
                    var flat = new Vector3(offset.x, 0f, offset.z);
                    if (!HasLookTarget && flat.sqrMagnitude > 0.0001f)
                    {
                        TurnToward(Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg, dt);
                    }

                    var distance = offset.magnitude;
                    if (distance <= travel)
                    {
                        Position = target;
                        travel -= distance;
                        corner++;
                    }
                    else
                    {
                        Position += offset / distance * travel;
                        travel = 0f;
                    }
                }

                if (corner >= corners.Length)
                {
                    Moving = false;
                }
            }

            if (HasLookTarget)
            {
                LookAt(LookTarget, dt);
            }

            Stamina = sprinting
                ? Mathf.Max(0f, Stamina - HideSeekRules.StaminaDrainPerSecond * dt)
                : Mathf.Min(HideSeekRules.MaxStamina, Stamina + HideSeekRules.StaminaRecoveryPerSecond * dt);
        }

        public void LookAt(Vector3 point, float dt)
        {
            var offset = point - Eye;
            var flat = new Vector3(offset.x, 0f, offset.z);
            if (flat.sqrMagnitude > 0.0001f)
            {
                TurnToward(Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg, dt);
            }

            Pitch = Mathf.Clamp(Mathf.Atan2(offset.y, Mathf.Max(0.01f, flat.magnitude)) * Mathf.Rad2Deg, -70f, 30f);
        }

        /// <summary>Turn toward a yaw at the player turn rate. Returns true once facing it.</summary>
        public bool TurnToward(float targetYaw, float dt)
        {
            var delta = Mathf.DeltaAngle(Yaw, targetYaw);
            var step = HideSeekRules.TurnDegreesPerSecond * dt;
            if (Mathf.Abs(delta) <= step)
            {
                Yaw = targetYaw;
                return true;
            }

            Yaw += Mathf.Sign(delta) * step;
            return false;
        }
    }
}
