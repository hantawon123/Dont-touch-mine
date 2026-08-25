using TMPro;
using UnityEngine;

namespace Game.Client.Match
{
    [DisallowMultipleComponent]
    public sealed class MatchTimerView : MonoBehaviour, IMatchTimerView
    {
        [SerializeField]
        private TMP_Text timerText;

        private int lastTotalSeconds = -1;

        public void SetRemainingSeconds(double remainingSeconds)
        {
            var totalSeconds = Mathf.Max(0, Mathf.CeilToInt((float)remainingSeconds));
            if (totalSeconds == lastTotalSeconds)
            {
                return;
            }

            timerText.text = $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
            lastTotalSeconds = totalSeconds;
        }
    }
}
