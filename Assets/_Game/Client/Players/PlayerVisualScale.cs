namespace Game.Client.Players
{
    /// <summary>
    /// 팀이 정한 인게임 캐릭터(PlayerCharacter 프리팹의 Visual) 크기. 값의 원본은 프리팹이지만,
    /// 에디터 세팅 도구와 회귀 테스트가 같은 숫자를 보게 하려고 여기 한 곳에 둔다.
    /// </summary>
    /// <remarks>
    /// 크기를 바꿀 때는 프리팹 Visual 스케일과 이 값을 함께 바꾸고, 충돌 캡슐·자세 높이·눈높이도
    /// 비례해서 맞춘다(2026-09-16 기준 0.85: 캡슐 1.67/0.27, 키 1.67/1.10/0.56, 눈높이 1.42/0.85/0.46).
    /// 머지 뒤 이 값과 프리팹이 어긋나면 EditMode 테스트가 실패해 바로 드러난다.
    /// </remarks>
    public static class PlayerVisualScale
    {
        public const float Value = 0.85f;
    }
}
