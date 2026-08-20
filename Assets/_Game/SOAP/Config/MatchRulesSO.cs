using Game.Core.Match;
using UnityEngine;

namespace Game.SOAP.Config
{
    [CreateAssetMenu(fileName = "MatchRules", menuName = "Game/Match Rules")]
    public sealed class MatchRulesSO : ScriptableObject
    {
        [SerializeField, Min(1f)]
        private float hidingDurationSeconds = 180f;

        [SerializeField, Min(1f)]
        private float searchingDurationSeconds = 300f;

        public float HidingDurationSeconds => hidingDurationSeconds;
        public float SearchingDurationSeconds => searchingDurationSeconds;

        public float GetDurationSeconds(MatchPhase phase)
        {
            return phase switch
            {
                MatchPhase.Hiding => hidingDurationSeconds,
                MatchPhase.Searching => searchingDurationSeconds,
                _ => 0f
            };
        }
    }
}
