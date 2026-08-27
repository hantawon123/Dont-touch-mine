using System;
using System.Collections.Generic;
using Game.Core.Items;
using Game.Core.Match;
using Game.Server.Match;
using UnityEngine;

namespace Game.Network.Match
{
    /// <summary>
    /// Match-wide messages already confirmed by the network authority.
    /// Presentation and application flow observe these without depending on
    /// Photon or the runner that delivered them.
    /// </summary>
    public interface INetworkMatchEvents
    {
        event Action<MatchStateSnapshot> MatchStateReceived;
        event Action<PlayerItemDestroyedEvent> ItemDestroyedReceived;
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
        event Action<IReadOnlyList<MatchParticipant>> LineUpReceived;
        event Action SimulationTick;
        bool BindMatchSession(MatchSessionCoordinator session, Pose shredderEjectionPose);
        bool UnbindMatchSession(MatchSessionCoordinator session);
        bool TryPublishMatchState(MatchStateSnapshot snapshot);
        bool TryPublishItemAssignments(IReadOnlyList<PlayerItemAssignment> assignments);
        bool TryPublishHighlightReplay(IReadOnlyList<HighlightReplayData> replay);
        bool TrySetPlayerControls(int playerIndex, bool enabled);
        bool TryTeleportPlayer(int playerIndex, Pose pose);
    }
}
