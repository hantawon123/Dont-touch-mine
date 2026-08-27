using Game.Core.Match;
using TMPro;
using UnityEngine;

namespace Game.Client.Match
{
    public sealed class MatchPhaseView : MonoBehaviour, IMatchPhaseView
    {
        [SerializeField]
        private TMP_Text phaseText;

        public void SetPhase(MatchPhase phase, string hidingPlayerName)
        {
            phaseText.text = phase switch
            {
                MatchPhase.Waiting => "대기 중",
                MatchPhase.Hiding => DescribeHiding(hidingPlayerName),
                MatchPhase.Searching => "찾는 중",
                MatchPhase.Highlight => "하이라이트",
                MatchPhase.Result => "결과",
                _ => phase.ToString()
            };
        }

        private static string DescribeHiding(string hidingPlayerName)
        {
            return string.IsNullOrWhiteSpace(hidingPlayerName)
                ? "숨기는 중"
                : $"{hidingPlayerName}{SubjectParticle(hidingPlayerName)} 숨기는 중";
        }

        /// <summary>
        /// 이름 뒤에 붙일 주격 조사. 받침이 있으면 "이", 없으면 "가".
        /// </summary>
        /// <remarks>
        /// 닉네임은 사람이 정하므로 한글·영문·숫자가 섞여 들어온다. 마지막
        /// 글자가 한글 음절일 때만 받침을 따지고, 그 밖에는 "이"로 둔다.
        /// 조사를 고정하면 절반의 이름에서 어색해지므로 값을 보고 고른다.
        /// </remarks>
        private static string SubjectParticle(string name)
        {
            var last = name.TrimEnd()[^1];

            if (last < '가' || last > '힣')
            {
                return "이";
            }

            // 한글 음절은 (초성, 중성, 종성) 순서로 배열되어 있어, 종성 개수
            // 28로 나눈 나머지가 0이면 받침이 없다.
            return (last - '가') % 28 == 0 ? "가" : "이";
        }
    }
}
