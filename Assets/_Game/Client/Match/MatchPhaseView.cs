using Game.Core.Match;
using Game.Core.Settings;
using TMPro;
using UnityEngine;

namespace Game.Client.Match
{
    public sealed class MatchPhaseView : MonoBehaviour, IMatchPhaseView
    {
        [SerializeField]
        private TMP_Text phaseText;

        private UiLocale chromeLocale;
        private MatchPhase lastPhase;
        private string lastHidingPlayerName = string.Empty;
        private bool hasPhase;

        private void Awake()
        {
            EnsureLayout();
        }

        public void ShowChrome(UiLocale locale)
        {
            chromeLocale = locale;
            if (hasPhase)
            {
                SetPhase(lastPhase, lastHidingPlayerName);
            }
        }

        public void SetPhase(MatchPhase phase, string hidingPlayerName)
        {
            lastPhase = phase;
            lastHidingPlayerName = hidingPlayerName ?? string.Empty;
            hasPhase = true;
            EnsureLayout();
            if (phaseText == null)
            {
                return;
            }

            phaseText.text = phase switch
            {
                MatchPhase.Waiting => Copy(UiText.Match.PhaseWaiting),
                MatchPhase.Hiding => DescribeHiding(hidingPlayerName, Language),
                MatchPhase.Searching => string.Empty,
                MatchPhase.Highlight => Copy(UiText.Match.PhaseHighlight),
                MatchPhase.Result => Copy(UiText.Match.PhaseResult),
                _ => phase.ToString()
            };
        }

        private string Copy(string key) =>
            chromeLocale != null
                ? chromeLocale.Get(key)
                : UiLocale.Applied(key);

        private string Language =>
            chromeLocale != null ? chromeLocale.LanguageCode : UiLocale.AppliedLanguage;

        private void EnsureLayout()
        {
            if (phaseText == null)
            {
                phaseText = GetComponent<TMP_Text>();
            }

            if (transform is not RectTransform rect)
            {
                return;
            }

            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(
                0f,
                -(HidingActiveHudView.TopPadding + MatchTimerView.TimerHeight + MatchTimerView.HintHeight));
            rect.sizeDelta = new Vector2(620f, 40f);
        }

        public static string DescribeHiding(string hidingPlayerName) =>
            DescribeHiding(hidingPlayerName, "ko");

        public static string DescribeHiding(string hidingPlayerName, string language)
        {
            if (string.IsNullOrWhiteSpace(hidingPlayerName))
            {
                return UiTextCatalog.Shipped.Get(UiText.Match.PhaseHiding, language);
            }

            return string.Format(
                UiTextCatalog.Shipped.Get(UiText.Match.PhaseHidingNamed, language),
                hidingPlayerName,
                SubjectParticle(hidingPlayerName));
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
