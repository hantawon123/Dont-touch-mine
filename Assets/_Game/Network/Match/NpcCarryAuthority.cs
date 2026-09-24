using System.Collections.Generic;
using Game.Core.Players;
using Game.Server.Items;
using UnityEngine;

namespace Game.Network.Match
{
    /// <summary>
    /// 학습 씬처럼 경기 상태 네트워크 오브젝트가 없는 곳에서 NPC의 집기·놓기를 판정하는 순수 권위.
    /// 로비의 <see cref="LobbyObjectAuthority"/>와 같은 구조로, 실제 경기와 같은
    /// <see cref="InteractionAuthorityRules"/>(잡기 2 m, 놓기 3 m, 정상 회전)를 쓴다.
    /// 화면 반영은 하지 않는다. 호출자가 확정 결과를 받아 실제 게임과 같은 확정 함수로 반영한다.
    /// 거절 이유를 문자열로 돌려주어 학습 실패 사유를 기록할 수 있게 한다.
    /// </summary>
    public sealed class NpcCarryAuthority
    {
        private readonly IReadOnlyList<WorldObjectState> initialStates;
        private readonly InteractionAuthorityRules rules;
        private readonly Dictionary<string, string> heldByNpc = new(System.StringComparer.Ordinal);
        private readonly HashSet<string> occupiedObjects = new(System.StringComparer.Ordinal);
        private WorldObjectStateSystem objects;

        public NpcCarryAuthority(
            IReadOnlyList<WorldObjectState> initialStates,
            InteractionAuthorityRules rules = null)
        {
            this.initialStates = initialStates ?? throw new System.ArgumentNullException(nameof(initialStates));
            this.rules = rules ?? new InteractionAuthorityRules();
            objects = new WorldObjectStateSystem(initialStates);
        }

        /// <summary>현재 무언가를 들고 있는 NPC 수. 에피소드 초기화 뒤에는 0이어야 한다.</summary>
        public int HeldCount => heldByNpc.Count;

        public bool TryHold(string npcId, string objectId, Pose npcPose, out string reason)
        {
            reason = null;
            if (!MatchNpcBotProfile.IsNpcId(npcId))
            {
                reason = "not an NPC id";
                return false;
            }

            if (string.IsNullOrWhiteSpace(objectId))
            {
                reason = "empty object id";
                return false;
            }

            var id = objectId.Trim();
            if (heldByNpc.ContainsKey(npcId))
            {
                reason = "NPC hand is already occupied";
                return false;
            }

            if (occupiedObjects.Contains(id))
            {
                reason = $"object '{id}' is already held";
                return false;
            }

            if (!objects.TryGetState(id, out var state))
            {
                reason = $"object '{id}' is unknown to the authority";
                return false;
            }

            if (!rules.IsWithinInteractionDistance(npcPose.position, state.Pose.position))
            {
                reason = $"object '{id}' is outside the interaction distance";
                return false;
            }

            heldByNpc.Add(npcId, id);
            occupiedObjects.Add(id);
            return true;
        }

        public bool TryGetHeld(string npcId, out string objectId) =>
            heldByNpc.TryGetValue(npcId ?? string.Empty, out objectId);

        /// <summary>속도 없는 놓기(내려놓기·배치). 던지기는 이 첫 버전의 범위 밖이다.</summary>
        public bool TryRelease(string npcId, Pose npcPose, Pose releasePose, out string reason)
        {
            reason = null;
            if (!heldByNpc.TryGetValue(npcId ?? string.Empty, out var objectId))
            {
                reason = "NPC hand is empty";
                return false;
            }

            if (!rules.IsValidRelease(npcPose, releasePose))
            {
                reason = "release pose is outside the release distance or has an invalid rotation";
                return false;
            }

            heldByNpc.Remove(npcId);
            occupiedObjects.Remove(objectId);
            objects.TrySetPose(objectId, releasePose);
            return true;
        }

        /// <summary>물리로 굴러간 뒤의 실제 위치를 권위에 알려 다음 거리 판정이 맞게 한다.</summary>
        public bool TrySetPose(string objectId, Pose pose) => objects.TrySetPose(objectId, pose);

        public bool TryGetPose(string objectId, out Pose pose)
        {
            if (objects.TryGetState(objectId, out var state))
            {
                pose = state.Pose;
                return true;
            }

            pose = default;
            return false;
        }

        /// <summary>에피소드 초기화: 모든 손을 비우고 물건 위치를 처음 상태로 되돌린다.</summary>
        public void Reset()
        {
            heldByNpc.Clear();
            occupiedObjects.Clear();
            objects = new WorldObjectStateSystem(initialStates);
        }
    }
}
