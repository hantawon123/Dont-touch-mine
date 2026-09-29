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

        [SerializeField, Min(0.1f), Tooltip("펀치 모션 재생 시간. 이 시간 동안은 다음 공격이 불가능하다 (모션 캔슬 방지)")]
        private float punchMotionSeconds = 0.9f;

        [SerializeField, Min(0.1f)]
        private float attackForwardOffset = 0.8f;

        [SerializeField, Min(0.1f)]
        private float attackRadius = 0.7f;

        [SerializeField, Min(0f)]
        private float hitKnockbackImpulse = 4f;

        public float AttackCooldownSeconds => attackCooldownSeconds;
        public float AttackHitDelaySeconds => attackHitDelaySeconds;
        public float PunchMotionSeconds => punchMotionSeconds;

        /// <summary>실효 쿨다운: 모션이 끝나기 전에는 다음 공격을 시작할 수 없다.</summary>
        public float EffectiveAttackCooldownSeconds =>
            Mathf.Max(attackCooldownSeconds, punchMotionSeconds);
        public float AttackForwardOffset => attackForwardOffset;
        public float AttackRadius => attackRadius;
        public float HitKnockbackImpulse => hitKnockbackImpulse;
    }
}
