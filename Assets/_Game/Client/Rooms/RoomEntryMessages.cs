using Game.Core.Rooms;

namespace Game.Client.Rooms
{
    /// <summary>
    /// What the player is told when a room refuses them.
    /// </summary>
    /// <remarks>
    /// One wording for every refusal, which is what the mock-up draws. From the
    /// player's side these are the same event — they picked a room and did not
    /// get in — and the answer to all of them is to try again.
    /// <para>
    /// Written as a lookup on the reason rather than a constant, because the
    /// reasons genuinely differ: a full room and an unreachable server want
    /// different next steps from the player. When those get wording of their
    /// own, this is the only file that changes.
    /// </para>
    /// </remarks>
    public static class RoomEntryMessages
    {
        public const string Title = "게임 접속 오류";

        public const string Body = "잠시 문제가 발생했어요. 다시 접속 시도 해주세요.";

        public static string Describe(RoomEntryFailure failure) => Body;
    }
}
