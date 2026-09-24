using Game.BotRuntime.Policy;

namespace Game.Training
{
    /// <summary>
    /// What PickSelectAgent needs from the world it plays in. The training arena
    /// (PickEpisodeEnvironment) and the mansion sandbox (MansionSandboxEnvironment)
    /// both implement it, so one agent and one ONNX model run in both without change.
    /// </summary>
    public interface IPickEnvironment
    {
        bool IsReady { get; }
        PickBotExecutor Executor { get; }
        float DecisionIntervalSeconds { get; }
        int EpisodeIndex { get; }

        /// <summary>Remaining share of the current episode (or pickup cycle), 0..1.</summary>
        float TimeLeftRatio { get; }

        /// <summary>
        /// Whether this candidate matches the current goal. The rule is the environment's:
        /// the arena compares kind stickers, the mansion treats every carryable prop as a match
        /// (real-game goal 1). The policy only ever sees the resulting 0/1.
        /// </summary>
        bool IsGoalMatch(string targetId, string kindKey);

        bool TryGetSize(string targetId, out PickSizeClass size);

        void ResetEpisode();

        void ReportFirstCandidateCount(int count);

        void RecordEpisode(PickEpisodeOutcome outcome, float seconds, int[] actionCounts);
    }
}
