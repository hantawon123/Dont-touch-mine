using System;
using System.Collections.Generic;
using Game.Core.Lobby;
using Game.Core.Match;
using Game.Server.Match;
using UnityEngine;

namespace Game.Network.Match
{
    /// <summary>
    /// Everything the match analytics recorder reads from the network runner,
    /// and nothing else.
    /// </summary>
    /// <remarks>
    /// Narrow on purpose. The recorder's gating decides whether a whole match is
    /// collected at all, so it has to be reachable from a test without a Fusion
    /// runner: a dedicated server that never enters through the room browser once
    /// silently dropped every match (S15P21D205-1021).
    /// </remarks>
    public interface IMatchAnalyticsSource
    {
        event Action<MatchStateSnapshot> MatchStateReceived;
        event Action<MatchResult> MatchResultReceived;
        event Action<IReadOnlyList<MatchObjectStateSnapshot>> ObjectStatesReceived;

        bool IsRuntimeReady { get; }
        bool IsServer { get; }

        /// <summary>True for a headless server run, which has no local player.</summary>
        bool IsDedicatedServer { get; }

        /// <summary>The session name, which is the room code on either host shape.</summary>
        string RoomCode { get; }

        double ServerTime { get; }
        string AnalyticsMapId { get; }
        MatchRuleSettings MatchRules { get; }
        int DestructionLimit { get; }
        MatchMigrationState MatchMigration { get; }
        IReadOnlyList<PlayerItemStatusSnapshot> LatestPlayerItemStatuses { get; }

        bool TryGetPlayerPose(string playerId, out Pose pose);
        bool TryGetPlayerReplayState(string playerId, out NetworkPlayerReplayState state);
        (int HitsReceived, int Stuns) GetCombatTotals(int playerIndex);
    }
}
