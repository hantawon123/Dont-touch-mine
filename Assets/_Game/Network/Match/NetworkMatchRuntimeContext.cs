using System;
using System.Collections.Generic;
using Game.Core.Lobby;
using Game.Core.Match;
using Game.Server.Items;
using Game.Server.Match;
using UnityEngine;

namespace Game.Network.Match
{
    public interface INetworkMatchRuntimeSource
    {
        bool IsRuntimeReady { get; }
        double ServerTime { get; }
        bool TryGetPlayerPose(string playerId, out Pose pose);
    }

    public sealed class NetworkMatchRuntimeContext : IMatchRuntimeContext
    {
        private readonly INetworkMatchRuntimeSource source;
        private readonly IMatchRuntimeContext sceneContext;
        private readonly MatchParticipant[] participantsByIndex;

        private Vector3[] positions;
        private Pose[] poses;
        private bool[] hasPose;

        public NetworkMatchRuntimeContext(
            INetworkMatchRuntimeSource source,
            IMatchRuntimeContext sceneContext,
            IReadOnlyList<MatchParticipant> participants)
        {
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            this.sceneContext = sceneContext ??
                throw new ArgumentNullException(nameof(sceneContext));

            if (participants == null)
            {
                throw new ArgumentNullException(nameof(participants));
            }

            if (participants.Count < RoomSettings.MinMatchPlayerCount ||
                participants.Count > RoomSettings.MaxPlayerCount)
            {
                throw new ArgumentOutOfRangeException(nameof(participants));
            }

            participantsByIndex = OrderByPlayerIndex(participants);
            positions = new Vector3[participantsByIndex.Length];
            poses = new Pose[participantsByIndex.Length];
            hasPose = new bool[participantsByIndex.Length];
        }

        public double ServerTime => source.ServerTime;

        public IReadOnlyList<Vector3> PlayerPositions
        {
            get
            {
                CapturePlayers();
                return positions;
            }
        }

        public IReadOnlyList<Pose> PlayerPoses
        {
            get
            {
                CapturePlayers();
                return poses;
            }
        }

        public IReadOnlyList<WorldObjectState> ReplayObjects => sceneContext.ReplayObjects;

        private void CapturePlayers()
        {
            for (var playerIndex = 0;
                 playerIndex < participantsByIndex.Length;
                 playerIndex++)
            {
                var playerId = participantsByIndex[playerIndex].PlayerId;
                if (!source.TryGetPlayerPose(playerId, out var pose))
                {
                    if (!hasPose[playerIndex])
                    {
                        throw new InvalidOperationException(
                            $"No spawned avatar exists for player '{playerId}'.");
                    }

                    continue;
                }

                poses[playerIndex] = pose;
                positions[playerIndex] = pose.position;
                hasPose[playerIndex] = true;
            }
        }

        private static MatchParticipant[] OrderByPlayerIndex(
            IReadOnlyList<MatchParticipant> participants)
        {
            var ordered = new MatchParticipant[participants.Count];
            var assigned = new bool[participants.Count];

            for (var index = 0; index < participants.Count; index++)
            {
                var participant = participants[index];
                var playerIndex = participant.PlayerIndex;
                if (playerIndex < 0 ||
                    playerIndex >= ordered.Length ||
                    assigned[playerIndex])
                {
                    throw new ArgumentException(
                        "Player indices must be unique and contiguous from zero.",
                        nameof(participants));
                }

                ordered[playerIndex] = participant;
                assigned[playerIndex] = true;
            }

            return ordered;
        }
    }
}
