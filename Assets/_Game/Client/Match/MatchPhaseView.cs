using Game.Core.Match;
using TMPro;
using UnityEngine;

namespace Game.Client.Match
{
    public sealed class MatchPhaseView : MonoBehaviour, IMatchPhaseView
    {
        [SerializeField]
        private TMP_Text phaseText;

        public void SetPhase(MatchPhase phase)
        {
            phaseText.text = phase switch
            {
                MatchPhase.Waiting => "대기 중",
                MatchPhase.Hiding => "숨기는 중",
                MatchPhase.Searching => "찾는 중",
                MatchPhase.Result => "결과",
                _ => phase.ToString()
            };
        }
    }
}
