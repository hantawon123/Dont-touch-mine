using Game.Core.Match;
using UnityEngine;

namespace Game.SOAP.Config
{
    [CreateAssetMenu(fileName = "MatchRules", menuName = "Game/Match Rules")]
    public sealed class MatchRulesSO : ScriptableObject
    {
        public const int PlayerCount = 6;
        public const int MaxHighlightCount = 3;

        [SerializeField, Min(1f)]
        private float hidingTurnDurationSeconds = 30f;

        [SerializeField, Min(1f)]
        private float searchingDurationSeconds = 360f;

        [SerializeField, Min(1f)]
        private float finalWarningSeconds = 30f;

        [SerializeField, Min(1f)]
        private float highlightMaxDurationSeconds = 30f;

        [SerializeField, Min(1)]
        private int destructionUsesPerPlayer = 5;

        [SerializeField, Min(1)]
        private int hitsRequiredToStun = 3;

        [SerializeField, Min(0f)]
        private float stunDurationSeconds = 2f;

        [SerializeField, Min(0f)]
        private float invulnerabilityDurationSeconds;

        public float HidingTurnDurationSeconds => hidingTurnDurationSeconds;
        public float HidingDurationSeconds => hidingTurnDurationSeconds * PlayerCount;
        public float SearchingDurationSeconds => searchingDurationSeconds;
        public float FinalWarningSeconds => finalWarningSeconds;
        public float HighlightMaxDurationSeconds => highlightMaxDurationSeconds;
        public float HighlightClipDurationSeconds =>
            highlightMaxDurationSeconds / MaxHighlightCount;
        public int DestructionUsesPerPlayer => destructionUsesPerPlayer;
        public int HitsRequiredToStun => hitsRequiredToStun;
        public float StunDurationSeconds => stunDurationSeconds;
        public float InvulnerabilityDurationSeconds => invulnerabilityDurationSeconds;

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
