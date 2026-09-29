using System;
using System.Collections.Generic;
using Game.Core.Emotes;
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
        MatchRuleSettings MatchRules { get; }
        bool TryGetPlayerPose(string playerId, out Pose pose);
        bool TryGetLocalStamina(out float current, out float max, out bool exhausted);
    }

    public readonly struct NetworkPlayerReplayState
    {
        public NetworkPlayerReplayState(
            PlayerPosture posture,
            bool grounded,
            int attackSequence,
            int emoteSequence = 0,
            int emoteId = 0)
        {
            if (!Enum.IsDefined(typeof(PlayerPosture), posture) ||
                attackSequence < 0 ||
                emoteSequence < 0)
                throw new ArgumentOutOfRangeException(nameof(posture));
            Posture = posture;
            Grounded = grounded;
            AttackSequence = attackSequence;
            EmoteSequence = emoteSequence;
            EmoteId = emoteId;
        }

        public PlayerPosture Posture { get; }
        public bool Grounded { get; }
        public int AttackSequence { get; }

        /// <summary>감정 표현이 시작될 때마다 오르는 번호. 값이 바뀐 순간이 표현의 시작이다.</summary>
        public int EmoteSequence { get; }

        /// <summary>마지막으로 시작한 감정 표현의 카탈로그 ID.</summary>
        public int EmoteId { get; }
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
        private readonly int[] emoteSequences;
        private readonly bool[] hasEmoteSequence;
        private readonly int[] emoteIds;
        private readonly double[] emoteEndsAt;
        private double capturedAt = double.NaN;

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
            emoteSequences = new int[participantsByIndex.Length];
            hasEmoteSequence = new bool[participantsByIndex.Length];
            emoteIds = new int[participantsByIndex.Length];
            emoteEndsAt = new double[participantsByIndex.Length];
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
            // The runtime reads actions, poses and positions in the same server
            // tick. Keep them consistent without querying every avatar three times.
            var now = source.ServerTime;
            if (capturedAt == now) return;

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

            // A failed initial capture must remain retryable at the same tick.
            capturedAt = now;
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
            var punched = hasAttackSequence[playerIndex] &&
                          attackSequences[playerIndex] != state.AttackSequence;
            if (punched)
            {
                punchEndsAt[playerIndex] = source.ServerTime + 0.5d;
            }

            attackSequences[playerIndex] = state.AttackSequence;
            hasAttackSequence[playerIndex] = true;
            if (source.ServerTime < punchEndsAt[playerIndex])
                action |= HighlightPlayerAction.Punching;
            replayActions[playerIndex] = CaptureEmote(playerIndex, state, punched, action);
        }

        /// <summary>
        /// 감정 표현이 재생 중인 동안 동작에 표현 ID를 함께 담는다.
        /// </summary>
        /// <remarks>
        /// 번호가 바뀐 순간이 시작이고, 카탈로그가 알려주는 길이만큼 이어진다. 반복 표현은
        /// 다른 동작이 끊을 때까지 이어지므로, 실제 플레이가 표현을 끊는 것들(주먹질·점프·자세
        /// 바꾸기)을 여기서도 본다. 맞기·기절은 서버가 아는 일이라
        /// <c>MatchSessionCoordinator</c> 가 끊는다.
        /// </remarks>
        private HighlightPlayerAction CaptureEmote(
            int playerIndex,
            NetworkPlayerReplayState state,
            bool punched,
            HighlightPlayerAction action)
        {
            var now = source.ServerTime;
            if (hasEmoteSequence[playerIndex] &&
                emoteSequences[playerIndex] != state.EmoteSequence)
            {
                emoteIds[playerIndex] = state.EmoteId;
                emoteEndsAt[playerIndex] = now + EmoteCatalog.PlaybackSeconds(state.EmoteId);
            }

            emoteSequences[playerIndex] = state.EmoteSequence;
            hasEmoteSequence[playerIndex] = true;
            if (punched || !state.Grounded || state.Posture != PlayerPosture.Standing)
            {
                emoteEndsAt[playerIndex] = 0d;
            }

            return now < emoteEndsAt[playerIndex]
                ? action.WithEmote(emoteIds[playerIndex])
                : action;
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
