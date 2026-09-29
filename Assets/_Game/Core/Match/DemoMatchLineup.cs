using System;
using System.Collections.Generic;
using Game.Core.Items;

namespace Game.Core.Match
{
    /// <summary>
    /// 최종 발표 시연용 고정 라인업. 숨기기 차례는 들어온 역순이고,
    /// 배정 물건은 들어온 순서마다 정해 둔 할로윈 물건이다.
    /// </summary>
    /// <remarks>
    /// 들어온 순서는 좌석 순서다. 좌석은 들어온 차례로 비어 있는 가장 낮은 번호를
    /// 받으므로, 매치 시작 전에 누가 나갔다가 다른 사람이 들어오면 새로 온 사람이
    /// 그 빈 순서를 이어받는다. 로비에서 고른 카테고리는 배정에 쓰이지 않는다.
    /// <para>
    /// 시연이 끝나면 이 파일과 호출부 두 곳(<c>MatchStarter</c>의 라인업 확정,
    /// <c>NetworkMatchRuntimeCoordinator</c>의 세션 생성)을 되돌린다.
    /// </para>
    /// </remarks>
    public static class DemoMatchLineup
    {
        /// <summary>들어온 순서(0부터)별 배정 물건 id. 전부 할로윈 카테고리다.</summary>
        public static IReadOnlyList<string> ItemIdsByJoinOrder { get; } = Array.AsReadOnly(new[]
        {
            "ica6cc0e88c", // 1번째: 양초
            "i9ee4e4e378", // 2번째: 호박
            "i53ecdeed12", // 3번째: 마녀 모자
            "i70c079a8b7", // 4번째: 해골 양초
            "ibddd25527c", // 5번째: 가마솥
            "if760667936", // 6번째: 랜턴
        });

        /// <summary>
        /// 좌석 순서 라인업을 뒤집어 마지막에 들어온 사람이 먼저 숨기게 한다.
        /// 플레이어 id와 계정 id의 짝은 그대로 유지된다.
        /// </summary>
        public static void ReverseJoinOrder(string[] playerIds, string[] userIds)
        {
            if (playerIds == null)
            {
                throw new ArgumentNullException(nameof(playerIds));
            }

            if (userIds == null)
            {
                throw new ArgumentNullException(nameof(userIds));
            }

            if (playerIds.Length != userIds.Length)
            {
                throw new ArgumentException(
                    "Player ids and user ids must be the same length.",
                    nameof(userIds));
            }

            Array.Reverse(playerIds);
            Array.Reverse(userIds);
        }

        /// <summary>
        /// <see cref="ReverseJoinOrder"/>로 확정한 라인업에 고정 물건을 배정한다.
        /// </summary>
        /// <returns>
        /// 물건 하나라도 <paramref name="definitions"/>에 없으면 false. 호출부는 이때
        /// 무작위 배정으로 돌아가 경기가 시작되지 못하는 일을 막는다.
        /// </returns>
        public static bool TryAssign(
            IReadOnlyList<ItemDefinition> definitions,
            int playerCount,
            out PlayerItemAssignment[] assignments)
        {
            assignments = null;
            if (definitions == null || playerCount <= 0 || playerCount > ItemIdsByJoinOrder.Count)
            {
                return false;
            }

            var result = new PlayerItemAssignment[playerCount];
            for (var playerIndex = 0; playerIndex < playerCount; playerIndex++)
            {
                // 라인업이 들어온 역순이라 첫 숨기기 차례(0)가 마지막에 들어온 사람이다.
                var joinOrder = playerCount - 1 - playerIndex;
                if (!TryFind(definitions, ItemIdsByJoinOrder[joinOrder], out var item))
                {
                    return false;
                }

                result[playerIndex] = new PlayerItemAssignment(playerIndex, item);
            }

            assignments = result;
            return true;
        }

        private static bool TryFind(
            IReadOnlyList<ItemDefinition> definitions,
            string itemId,
            out ItemDefinition item)
        {
            foreach (var definition in definitions)
            {
                if (string.Equals(definition.ItemId, itemId, StringComparison.Ordinal))
                {
                    item = definition;
                    return true;
                }
            }

            item = default;
            return false;
        }
    }
}
