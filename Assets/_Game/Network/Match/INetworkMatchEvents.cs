using System;
using System.Collections.Generic;
using Game.Core.Items;
using Game.Core.Lobby;
using Game.Core.Match;
using Game.Server.Match;
using UnityEngine;

namespace Game.Network.Match
{
    public interface INetworkResultNavigation
    {
        bool IsServer { get; }
        bool IsRuntimeReady { get; }
        bool IsResultSceneLoaded { get; }
        bool EnterResultScene();
        bool PrepareLobbyForHighlights();
        bool CompleteLocalHighlightViewing();
        bool RequestReturnToLobby();
    }

    public interface INetworkHighlightReady
    {
        bool TryConfirmHighlightReady();
    }

    /// <summary>
    /// Match-wide messages already confirmed by the network authority.
    /// Presentation and application flow observe these without depending on
    /// Photon or the runner that delivered them.
    /// </summary>
    public interface INetworkMatchEvents
    {
        event Action<MatchStateSnapshot> MatchStateReceived;
        event Action<LobbyChatMessage> MatchChatReceived;
        event Action<string> ItemAssignmentReceived;
        event Action<PlayerItemDestroyedEvent> ItemDestroyedReceived;
        event Action<IReadOnlyList<PlayerItemStatusSnapshot>> PlayerItemStatusesReceived;
        IReadOnlyList<PlayerItemStatusSnapshot> LatestPlayerItemStatuses { get; }
        event Action<IReadOnlyList<PlayerInteractionStateSnapshot>>
            PlayerInteractionStatesReceived;
        event Action<IReadOnlyList<HighlightReplayData>> HighlightReplayReceived;
        event Action<MatchResult> MatchResultReceived;
    }

    /// <summary>
    /// Authority-side bridge used by the scene runtime without exposing Fusion types.
    /// </summary>
    public interface INetworkMatchAuthority : INetworkMatchRuntimeSource
    {
        bool IsServer { get; }
        MatchMigrationState MatchMigration { get; }
        bool IsMatchRuntimeRestorePending { get; }
        void ReportMatchRuntimeRestored(Exception failure);
        int DestructionLimit { get; }
        event Action<IReadOnlyList<MatchParticipant>> LineUpReceived;
        event Action SimulationTick;

        /// <summary>
        /// 씬 로드가 끝났을 때. 스포너가 이 직후 전원을 씬 스폰 위치로 재배치하므로,
        /// 경기 배치(숨기기 초기 배치 등)는 이 신호 이후 다시 적용해야 한다.
        /// </summary>
        event Action SceneLoaded;
        bool BindMatchSession(MatchSessionCoordinator session, Pose shredderEjectionPose);
        bool UnbindMatchSession(MatchSessionCoordinator session);
        bool TryInitializeAssignedItems(IReadOnlyList<PlayerItemAssignment> assignments);
        bool TryPublishMatchState(MatchStateSnapshot snapshot);
        bool TryPublishItemAssignments(IReadOnlyList<PlayerItemAssignment> assignments);
        bool TryPublishPlayerItemStatuses(
            IReadOnlyList<PlayerItemStatusSnapshot> statuses);
        bool TryPublishHighlightReplay(IReadOnlyList<HighlightReplayData> replay);
        bool IsHighlightReplayReady { get; }
        bool TrySetPlayerSprintMultiplier(int playerIndex, float multiplier);
        bool TryResetPlayerStamina(int playerIndex);
        bool TrySetPlayerControls(int playerIndex, bool enabled);
        bool TryTeleportPlayer(int playerIndex, Pose pose);
    }
}
