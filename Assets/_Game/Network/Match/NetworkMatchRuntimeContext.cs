using System;
using System.Collections.Generic;
using Game.Core.Lobby;
using Game.Core.Match;
using Game.Core.Players;
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

    public readonly struct NetworkPlayerReplayState
    {
        public NetworkPlayerReplayState(
            PlayerPosture posture,
            bool grounded,
            int attackSequence)
        {
            if (!Enum.IsDefined(typeof(PlayerPosture), posture) || attackSequence < 0)
                throw new ArgumentOutOfRangeException(nameof(posture));
            Posture = posture;
            Grounded = grounded;
            AttackSequence = attackSequence;
        }

        public PlayerPosture Posture { get; }
        public bool Grounded { get; }
        public int AttackSequence { get; }
    }

    public interface INetworkPlayerReplayStateSource
    {
        bool TryGetPlayerReplayState(string playerId, out NetworkPlayerReplayState state);
    }

    public sealed class NetworkMatchRuntimeContext :
        IMatchRuntimeContext,
        IHighlightReplayActionSource
    {
        private readonly INetworkMatchRuntimeSource source;
        private readonly IMatchRuntimeContext sceneContext;
        private readonly MatchParticipant[] participantsByIndex;

        private Vector3[] positions;
        private Pose[] poses;
        private bool[] hasPose;
        private readonly HighlightPlayerAction[] replayActions;
        private readonly int[] attackSequences;
        private readonly bool[] hasAttackSequence;
        private readonly double[] punchEndsAt;

        public NetworkMatchRuntimeContext(
            INetworkMatchRuntimeSource source,
            IMatchRuntimeContext sceneContext,
            IReadOnlyList<MatchParticipant> participants,
            IReadOnlyList<Pose> restoredPoses = null)
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
            replayActions = new HighlightPlayerAction[participantsByIndex.Length];
            attackSequences = new int[participantsByIndex.Length];
            hasAttackSequence = new bool[participantsByIndex.Length];
            punchEndsAt = new double[participantsByIndex.Length];
            if (restoredPoses != null)
            {
                if (restoredPoses.Count != poses.Length) throw new ArgumentException("Migration pose count mismatch.");
                for (var i = 0; i < poses.Length; i++)
                {
                    poses[i] = restoredPoses[i];
                    positions[i] = poses[i].position;
                    hasPose[i] = true;
                }
            }
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
        public IReadOnlyList<HighlightPlayerAction> PlayerReplayActions
        {
            get
            {
                CapturePlayers();
                return replayActions;
            }
        }

        private void CapturePlayers()
        {
            for (var playerIndex = 0;
                 playerIndex < participantsByIndex.Length;
                 playerIndex++)
            {
                var playerId = participantsByIndex[playerIndex].PlayerId;
                replayActions[playerIndex] = HighlightPlayerAction.None;
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
                CaptureReplayAction(playerId, playerIndex);
            }
        }

        private void CaptureReplayAction(string playerId, int playerIndex)
        {
            replayActions[playerIndex] = HighlightPlayerAction.None;
            if (source is not INetworkPlayerReplayStateSource replaySource ||
                !replaySource.TryGetPlayerReplayState(playerId, out var state))
            {
                return;
            }

            var action = state.Posture switch
            {
                PlayerPosture.Crouching => HighlightPlayerAction.Crouching,
                PlayerPosture.Prone => HighlightPlayerAction.Prone,
                _ => HighlightPlayerAction.None,
            };
            if (!state.Grounded) action |= HighlightPlayerAction.Airborne;
            if (hasAttackSequence[playerIndex] &&
                attackSequences[playerIndex] != state.AttackSequence)
            {
                punchEndsAt[playerIndex] = source.ServerTime + 0.5d;
            }

            attackSequences[playerIndex] = state.AttackSequence;
            hasAttackSequence[playerIndex] = true;
            if (source.ServerTime < punchEndsAt[playerIndex])
                action |= HighlightPlayerAction.Punching;
            replayActions[playerIndex] = action;
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
