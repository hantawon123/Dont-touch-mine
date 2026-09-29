using Fusion.Addons.KCC;
using UnityEngine;

namespace Game.Network.Players
{
    /// <summary>
    /// Applies this player's configured speed to the standard KCC environment.
    /// The processor lives on each avatar, so players never mutate a shared
    /// processor prefab.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerKCCMovementProcessor : EnvironmentProcessor
    {
        private NetworkPlayerMotor motor;

        private void Awake()
        {
            motor = GetComponent<NetworkPlayerMotor>();
        }

        public override void Execute(
            ISetKinematicSpeed stage,
            KCC kcc,
            KCCData data)
        {
            motor ??= GetComponent<NetworkPlayerMotor>();
            data.KinematicSpeed = motor != null
                ? motor.DesiredMoveSpeed
                : KinematicSpeed;
            kcc.SuppressProcessors<EnvironmentProcessor>();
        }

        internal void ConfigureGravity(float multiplier)
        {
            Gravity = Physics.gravity * multiplier;
        }
    }
}
