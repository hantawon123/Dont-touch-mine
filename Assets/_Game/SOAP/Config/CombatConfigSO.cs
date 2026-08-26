using UnityEngine;

namespace Game.SOAP.Config
{
    [CreateAssetMenu(fileName = "CombatConfig", menuName = "Game/Combat Config")]
    public sealed class CombatConfigSO : ScriptableObject
    {
        [SerializeField, Min(0.05f)]
        private float attackCooldownSeconds = 0.5f;

        [SerializeField, Min(0.1f)]
        private float attackForwardOffset = 0.8f;

        [SerializeField, Min(0.1f)]
        private float attackRadius = 0.7f;

        [SerializeField, Min(0f)]
        private float hitKnockbackImpulse = 4f;

        public float AttackCooldownSeconds => attackCooldownSeconds;
        public float AttackForwardOffset => attackForwardOffset;
        public float AttackRadius => attackRadius;
        public float HitKnockbackImpulse => hitKnockbackImpulse;
    }
}
