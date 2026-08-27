using UnityEngine;

namespace Game.SOAP.Config
{
    [CreateAssetMenu(fileName = "CombatConfig", menuName = "Game/Combat Config")]
    public sealed class CombatConfigSO : ScriptableObject
    {
        [SerializeField, Min(0.05f)]
        private float attackCooldownSeconds = 0.5f;

        [SerializeField, Min(0f), Tooltip("공격 입력 후 실제 타격 판정까지의 지연 (모션 임팩트 타이밍에 맞춤)")]
        private float attackHitDelaySeconds = 0.35f;

        [SerializeField, Min(0.1f)]
        private float attackForwardOffset = 0.8f;

        [SerializeField, Min(0.1f)]
        private float attackRadius = 0.7f;

        [SerializeField, Min(0f)]
        private float hitKnockbackImpulse = 4f;

        public float AttackCooldownSeconds => attackCooldownSeconds;
        public float AttackHitDelaySeconds => attackHitDelaySeconds;
        public float AttackForwardOffset => attackForwardOffset;
        public float AttackRadius => attackRadius;
        public float HitKnockbackImpulse => hitKnockbackImpulse;
    }
}
