using Game.Core.Match;
using UnityEngine;

namespace Game.SOAP.Config
{
    [CreateAssetMenu(fileName = "MatchRules", menuName = "Game/Match Rules")]
    public sealed class MatchRulesSO : ScriptableObject
    {
        public const int PlayerCount = 6;

        [SerializeField, Min(1f)]
        private float hidingTurnDurationSeconds = 30f;

        [SerializeField, Min(1f)]
        private float searchingDurationSeconds = 360f;

        [SerializeField, Min(1f)]
        private float finalWarningSeconds = 30f;

        [SerializeField, Min(1f)]
        private float highlightMaxDurationSeconds = 30f;

        public float HidingTurnDurationSeconds => hidingTurnDurationSeconds;
        public float HidingDurationSeconds => hidingTurnDurationSeconds * PlayerCount;
        public float SearchingDurationSeconds => searchingDurationSeconds;
        public float FinalWarningSeconds => finalWarningSeconds;
        public float HighlightMaxDurationSeconds => highlightMaxDurationSeconds;

        public float GetDurationSeconds(MatchPhase phase)
        {
            return phase switch
            {
                MatchPhase.Hiding => HidingDurationSeconds,
                MatchPhase.Searching => searchingDurationSeconds,
                MatchPhase.Highlight => highlightMaxDurationSeconds,
                _ => 0f
            };
        }
    }
}
