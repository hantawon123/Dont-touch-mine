using Game.Client.Home;
using TMPro;
using UnityEngine;

namespace Game.Client.Match
{
    public interface IMatchTimerView
    {
        void SetRemainingSeconds(double remainingSeconds);
        void SetHintVisible(bool visible);
        void SetResult(string headline, string subtitle);
        void ClearResult();
    }

    /// <summary>
    /// Top-of-screen searching clock. Matches the hiding timer until the last
    /// thirty seconds, then grows to orange 64 Black type, shows an orange
    /// prompt, and pulses both lines.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MatchTimerView : MonoBehaviour, IMatchTimerView
    {
        public const float TimerFontSize = 64f;
        public const float HintFontSize = 36f;
        public const float WarningSeconds = 30f;
        public const float TimerHeight = 80f;
        public const float HintHeight = 48f;
        public const string HintText = "서둘러 자신의 물건을 확보하세요 !";
        public const string WinHeadline = "YOU WIN!";
        public const string LoseHeadline = "YOU LOSE..";
        public const string WinSubtitle = "숨겼던 물건을 끝까지 지켜냈어요!";
        public const string LoseSubtitle = "아쉽게도 물건을 지키지 못했어요!";
        public static readonly Color TimerColor = HidingActiveHudView.WarningColor;
        public static readonly Color WarningColor = HidingActiveHudView.WarningColor;
        public static readonly Color ResultSubtitleColor = Color.white;

        [SerializeField]
        private TMP_Text timerText;

        [SerializeField]
        private TMP_Text hintText;

        private int lastTotalSeconds = -1;
        private bool warningActive;
        private bool hintAllowed = true;
        private bool resultActive;
        private string resultHeadline = string.Empty;
        private string resultSubtitle = string.Empty;

        public static bool IsWarning(double remainingSeconds)
        {
            return Mathf.Max(0, Mathf.CeilToInt((float)remainingSeconds)) <= WarningSeconds;
        }

        public static MatchTimerView Create(Transform parent)
        {
            var root = new GameObject(
                "TimerText",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            if (parent != null)
            {
                root.transform.SetParent(parent, false);
            }

            var text = root.GetComponent<TextMeshProUGUI>();
            text.text = "00:00";
            text.fontSize = HidingActiveHudView.TimerFontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            return root.AddComponent<MatchTimerView>();
        }

        private void Awake()
        {
            EnsureLayout();
        }

        private void OnDisable()
        {
            warningActive = false;
            ResetPulseScale();
        }

        private void Update()
        {
            if (resultActive || !warningActive)
            {
                ResetPulseScale();
                return;
            }

            ApplyPulseScale(Vector3.one * HidingActiveHudView.HeartbeatScale(Time.unscaledTime));
        }

        public void SetResult(string headline, string subtitle)
        {
            resultActive = true;
            resultHeadline = headline ?? string.Empty;
            resultSubtitle = subtitle ?? string.Empty;
            warningActive = false;
            lastTotalSeconds = -1;
            ResetPulseScale();
            EnsureLayout();
            ApplyResult();
        }

        public void ClearResult()
        {
            if (!resultActive)
            {
                return;
            }

            resultActive = false;
            resultHeadline = string.Empty;
            resultSubtitle = string.Empty;
            lastTotalSeconds = -1;
            EnsureLayout();
            ResetPulseScale();
        }

        public void SetRemainingSeconds(double remainingSeconds)
        {
            EnsureLayout();
            if (timerText == null)
            {
                return;
            }

            if (resultActive)
            {
                ApplyResult();
                return;
            }

            var totalSeconds = Mathf.Max(0, Mathf.CeilToInt((float)remainingSeconds));
            if (totalSeconds != lastTotalSeconds)
            {
                timerText.text = HidingTurnStartView.FormatTimer(remainingSeconds);
                lastTotalSeconds = totalSeconds;
            }

            ApplyUrgency(remainingSeconds);
        }

        public void SetHintVisible(bool visible)
        {
            hintAllowed = visible;
            EnsureLayout();
            ApplyHintVisibility();
        }

        private void EnsureLayout()
        {
            if (timerText == null)
            {
                timerText = transform.Find("Timer")?.GetComponent<TMP_Text>() ??
                            GetComponent<TMP_Text>();
            }

            if (hintText == null)
            {
                hintText = transform.Find("Hint")?.GetComponent<TMP_Text>();
            }

            if (hintText == null)
            {
                hintText = CreateText(transform, "Hint", HintText, HintFontSize);
            }

            ApplyTimerStyle();
            ApplyHintStyle();
            ApplyPlacement();
        }

        private void ApplyTimerStyle()
        {
            if (timerText == null)
            {
                return;
            }

            ApplyTimerTypeface();
            timerText.fontStyle = FontStyles.Normal;
            timerText.alignment = TextAlignmentOptions.Center;
            timerText.textWrappingMode = TextWrappingModes.NoWrap;
            timerText.overflowMode = TextOverflowModes.Overflow;
            timerText.raycastTarget = false;
        }

        private void ApplyHintStyle()
        {
            if (hintText == null)
            {
                return;
            }

            hintText.font = HomeUiFonts.Apply();
            hintText.fontSize = HintFontSize;
            hintText.fontStyle = FontStyles.Normal;
            hintText.alignment = TextAlignmentOptions.Center;
            hintText.color = resultActive ? ResultSubtitleColor : WarningColor;
            hintText.textWrappingMode = TextWrappingModes.NoWrap;
            hintText.overflowMode = TextOverflowModes.Overflow;
            hintText.raycastTarget = false;
            hintText.text = resultActive ? resultSubtitle : HintText;
            ApplyHintVisibility();
        }

        private void ApplyPlacement()
        {
            if (transform is not RectTransform viewRect)
            {
                return;
            }

            var timerOnSelf = timerText != null && timerText.transform == transform;
            viewRect.SetAsFirstSibling();
            if (timerOnSelf)
            {
                Place(
                    viewRect,
                    new Vector2(0.5f, 1f),
                    new Vector2(0f, -HidingActiveHudView.TopPadding),
                    new Vector2(resultActive ? 980f : 420f, TimerHeight),
                    new Vector2(0.5f, 1f));
            }
            else
            {
                Place(
                    viewRect,
                    new Vector2(0.5f, 1f),
                    new Vector2(0f, -HidingActiveHudView.TopPadding),
                    new Vector2(1200f, TimerHeight + HintHeight),
                    new Vector2(0.5f, 1f));
                if (timerText != null)
                {
                    Place(
                        timerText.rectTransform,
                        new Vector2(0.5f, 1f),
                        Vector2.zero,
                        new Vector2(resultActive ? 980f : 420f, TimerHeight),
                        new Vector2(0.5f, 1f));
                }
            }

            if (hintText != null)
            {
                Place(
                    hintText.rectTransform,
                    new Vector2(0.5f, 1f),
                    new Vector2(0f, -TimerHeight),
                    new Vector2(1200f, HintHeight),
                    new Vector2(0.5f, 1f));
            }
        }

        private void ApplyUrgency(double remainingSeconds)
        {
            if (resultActive)
            {
                ApplyResult();
                return;
            }

            warningActive = IsWarning(remainingSeconds);
            if (timerText != null)
            {
                timerText.fontSize = warningActive
                    ? TimerFontSize
                    : HidingActiveHudView.TimerFontSize;
                timerText.color = warningActive ? TimerColor : Color.white;
                ApplyTimerTypeface();
            }

            if (!warningActive)
            {
                ResetPulseScale();
            }

            ApplyHintVisibility();
        }

        private void ApplyResult()
        {
            warningActive = false;
            if (timerText != null)
            {
                timerText.text = resultHeadline;
                timerText.fontSize = TimerFontSize;
                timerText.color = TimerColor;
                timerText.font = HomeUiFonts.ApplyBlack();
                timerText.textWrappingMode = TextWrappingModes.NoWrap;
                timerText.overflowMode = TextOverflowModes.Overflow;
            }

            if (hintText != null)
            {
                hintText.text = resultSubtitle;
                hintText.color = ResultSubtitleColor;
                hintText.font = HomeUiFonts.Apply();
                hintText.gameObject.SetActive(true);
            }

            ResetPulseScale();
        }

        private void ApplyTimerTypeface()
        {
            if (timerText == null)
            {
                return;
            }

            timerText.font = warningActive || resultActive
                ? HomeUiFonts.ApplyBlack()
                : HomeUiFonts.Apply();
        }

        private void ApplyPulseScale(Vector3 scale)
        {
            var timerOnSelf = timerText != null && timerText.transform == transform;
            if (timerOnSelf)
            {
                transform.localScale = scale;
                return;
            }

            if (timerText != null)
            {
                timerText.transform.localScale = scale;
            }

            if (hintText != null && hintText.gameObject.activeSelf)
            {
                hintText.transform.localScale = scale;
            }
        }

        private void ResetPulseScale()
        {
            ApplyPulseScale(Vector3.one);
        }

        private void ApplyHintVisibility()
        {
            if (hintText != null)
            {
                hintText.gameObject.SetActive(resultActive || (hintAllowed && warningActive));
            }
        }

        private static TMP_Text CreateText(
            Transform parent,
            string name,
            string content,
            float fontSize)
        {
            var gameObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            gameObject.transform.SetParent(parent, false);

            var text = gameObject.GetComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = WarningColor;
            text.raycastTarget = false;
            text.font = HomeUiFonts.Apply();
            text.fontStyle = FontStyles.Normal;
            return text;
        }

        private static void Place(
            RectTransform rect,
            Vector2 anchor,
            Vector2 anchoredPosition,
            Vector2 size,
            Vector2 pivot)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition3D = new Vector3(anchoredPosition.x, anchoredPosition.y, 0f);
            rect.sizeDelta = size;
        }
    }
}
