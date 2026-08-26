namespace Game.Core.Players
{
    public enum HitResult
    {
        Ignored,
        Registered,
        Stunned
    }

    /// <summary>
    /// 전투 판정 규칙(맞은 횟수 누적, 기절, 무적)의 공통 계약.
    /// 현재는 Server의 PlayerInteractionSystem이 구현하며,
    /// Client는 이 인터페이스만 참조한다. (Client -> Server 직접 참조 금지 규칙)
    /// </summary>
    public interface IPlayerCombatRules
    {
        int GetHitCount(int playerIndex);

        HitResult RegisterHit(int playerIndex, double now);

        bool IsStunned(int playerIndex, double now);

        bool IsInvulnerable(int playerIndex, double now);
    }
}
