using Game.Client.Home;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Match
{
    public interface IMatchVitalsHudView
    {
        void Show(float stamina, float maxStamina, int hits, int maxHits, bool exhausted = false);
        void Hide();
        void SetValues(float stamina, float maxStamina, int hits, int maxHits, bool exhausted = false);
    }

    /// <summary>
    /// Bottom-center stamina and hit bars. Stamina fill follows the local motor each frame.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MatchVitalsHudView : MonoBehaviour, IMatchVitalsHudView
    {
        public const float DefaultStamina = 100f;
        public const float LowStaminaThreshold = 20f;
        public const float ShakeAmplitude = 2.5f;
        public const float ShakeCyclesPerSecond = 18f;
        public const int DefaultHits = 3;
        public const float IconSize = 22f;
        public const float IconPadding = 12f;
        public const float BarIconGap = 8f;
        public const float RowInset = 16f;
        public const float BarHeight = 14f;
        public const float BarSlant = 10f;
        public const float SegmentGap = 10f;
        public const float PanelWidth = 380f;
        public const float PanelHeight = 88f;
        public const float BottomPadding = MatchChatView.Margin;

        public static float BarStart => IconPadding + IconSize + BarIconGap;
        public static float BarRightInset => IconPadding;
        public static float TrackWidth =>
            PanelWidth - (RowInset * 2f) - BarStart - BarRightInset;
        public static float SegmentWidth =>
            (TrackWidth - (SegmentGap * (DefaultHits - 1))) / DefaultHits;
        public static float BarWidth =>
            (SegmentWidth * DefaultHits) + (SegmentGap * (DefaultHits - 1));
        public const string FlashIconResource = "UI/ic_flash";
        public const string HeartIconResource = "UI/ic_heart";

        public static readonly Color PanelColor = new Color(11f / 255f, 16f / 255f, 24f / 255f, 0.7f);
        public static readonly Color StaminaColor = new Color(245f / 255f, 243f / 255f, 241f / 255f, 1f);
        public static readonly Color StaminaDisabledColor = new Color(136f / 255f, 136f / 255f, 136f / 255f, 1f);
        public static readonly Color StaminaLowColor = new Color(1f, 51f / 255f, 51f / 255f, 1f);
        public static readonly Color HealthStartColor = new Color(1f, 154f / 255f, 106f / 255f, 1f);
        public static readonly Color HealthEndColor = new Color(1f, 112f / 255f, 50f / 255f, 1f);

        [SerializeField]
        private GameObject panel;

        [SerializeField]
        private Image staminaIcon;

        [SerializeField]
        private RectTransform staminaRow;

        [SerializeField]
        private RectTransform staminaFill;

        [SerializeField]
        private RectTransform[] healthSegments;

        [SerializeField]
        [Tooltip("Shows the bars in the editor Game view without entering Play.")]
        private bool previewOnAwake;

        private bool shown;
        private bool shakeStamina;
        private float shakeElapsed;
        private float lastStamina = float.NaN;
        private Vector2 staminaRowRest;

        public static string FormatValue(int current, int max)
        {
            return $"{Mathf.Max(0, current)}/{Mathf.Max(0, max)}";
        }

        public static int RemainingHits(int hitCount, int maxHits)
        {
            return Mathf.Max(0, maxHits - Mathf.Max(0, hitCount));
        }

        public static string FormatStamina(float current)
        {
            return Mathf.RoundToInt(Mathf.Max(0f, current)).ToString();
        }

        public static bool IsLowStamina(float current)
        {
            return current <= LowStaminaThreshold;
        }

        public static bool ShouldShakeStamina(float current, float previous, bool exhausted)
        {
            if (exhausted || !IsLowStamina(current) || !float.IsFinite(previous))
            {
                return false;
            }

            return current < previous;
        }

        public static Color StaminaColorFor(bool exhausted)
        {
            return StaminaColorFor(DefaultStamina, exhausted);
        }

        public static Color StaminaColorFor(float current, bool exhausted)
        {
            if (exhausted)
            {
                return StaminaDisabledColor;
            }

            return IsLowStamina(current) ? StaminaLowColor : StaminaColor;
        }

        public static Color StaminaAccentFor(float current, bool exhausted)
        {
            if (exhausted)
            {
                return StaminaDisabledColor;
            }

            return IsLowStamina(current) ? StaminaLowColor : Color.white;
        }

        public static Vector2 ShakeOffset(float elapsedSeconds)
        {
            var radians = elapsedSeconds * ShakeCyclesPerSecond * Mathf.PI * 2f;
            return new Vector2(
                Mathf.Sin(radians) * ShakeAmplitude,
                Mathf.Cos(radians * 1.3f) * (ShakeAmplitude * 0.7f));
        }

        public static float FillAmount(float current, float max)
        {
            return max <= 0f ? 0f : Mathf.Clamp01(current / max);
        }

        public static Color HealthColorAt(int index, int count)
        {
            if (count <= 1)
            {
                return HealthStartColor;
            }

            return Color.Lerp(HealthStartColor, HealthEndColor, index / (float)(count - 1));
        }

        public static MatchVitalsHudView Create(Transform parent)
        {
            var rootObject = new GameObject("MatchVitals", typeof(RectTransform));
            rootObject.transform.SetParent(parent, false);
            Stretch((RectTransform)rootObject.transform);
            return rootObject.AddComponent<MatchVitalsHudView>();
        }

        private void Awake()
        {
            EnsureLayout();
            if (previewOnAwake && !shown)
            {
                Show(DefaultStamina, DefaultStamina, DefaultHits, DefaultHits);
                return;
            }

            if (!shown)
            {
                Hide();
            }
        }

        public void Show(float stamina, float maxStamina, int hits, int maxHits, bool exhausted = false)
        {
            shown = true;
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            EnsureLayout();
            SetValues(stamina, maxStamina, hits, maxHits, exhausted);
            if (panel != null)
            {
                panel.SetActive(true);
            }
        }

        public void Hide()
        {
            shown = false;
            lastStamina = float.NaN;
            StopStaminaShake();
            if (panel != null)
            {
                panel.SetActive(false);
            }
        }

        public void SetValues(float stamina, float maxStamina, int hits, int maxHits, bool exhausted = false)
        {
            EnsureLayout();
            var staminaColor = StaminaColorFor(stamina, exhausted);
            var staminaAccent = StaminaAccentFor(stamina, exhausted);
            if (staminaIcon != null)
            {
                staminaIcon.color = staminaAccent;
            }

            if (staminaFill != null)
            {
                var fill = staminaFill.GetComponent<Image>();
                if (fill != null)
                {
                    fill.type = Image.Type.Filled;
                    fill.fillMethod = Image.FillMethod.Horizontal;
                    fill.fillAmount = FillAmount(stamina, maxStamina);
                    fill.color = staminaColor;
                }
            }

            if (shown && ShouldShakeStamina(stamina, lastStamina, exhausted))
            {
                shakeStamina = true;
            }
            else
            {
                StopStaminaShake();
            }

            lastStamina = stamina;

            if (healthSegments == null)
            {
                return;
            }

            for (var index = 0; index < healthSegments.Length; index++)
            {
                var segment = healthSegments[index];
                if (segment != null)
                {
                    segment.gameObject.SetActive(index < hits);
                }
            }
        }

        private void LateUpdate()
        {
            if (staminaRow == null)
            {
                return;
            }

            if (!shown || !shakeStamina)
            {
                ResetStaminaRowPosition();
                return;
            }

            shakeElapsed += Time.unscaledDeltaTime;
            staminaRow.anchoredPosition = staminaRowRest + ShakeOffset(shakeElapsed);
        }

        private void StopStaminaShake()
        {
            shakeStamina = false;
            shakeElapsed = 0f;
            ResetStaminaRowPosition();
        }

        private void ResetStaminaRowPosition()
        {
            if (staminaRow != null)
            {
                staminaRow.anchoredPosition = staminaRowRest;
            }
        }

        private void EnsureLayout()
        {
            var rect = transform as RectTransform;
            if (rect != null)
            {
                Stretch(rect);
            }

            if (!HasCurrentLayout())
            {
                DestroyChild("Panel");
                panel = null;
                staminaIcon = null;
                staminaRow = null;
                staminaFill = null;
                healthSegments = null;
                BuildLayout();
            }

            if (panel == null)
            {
                panel = transform.Find("Panel")?.gameObject;
            }

            if (staminaIcon == null)
            {
                staminaIcon = transform.Find("Panel/Stamina/Icon")?.GetComponent<Image>();
            }

            if (staminaRow == null)
            {
                staminaRow = transform.Find("Panel/Stamina") as RectTransform;
            }

            StripValueLabels();

            if (staminaFill == null)
            {
                staminaFill = transform.Find("Panel/Stamina/Bar") as RectTransform;
            }

            if (healthSegments == null || healthSegments.Length == 0)
            {
                healthSegments = new RectTransform[DefaultHits];
                for (var index = 0; index < DefaultHits; index++)
                {
                    healthSegments[index] =
                        transform.Find($"Panel/Health/BarTrack/Segment{index}") as RectTransform;
                }
            }

            ApplyBarMetrics();
        }

        private bool HasCurrentLayout()
        {
            var panelRect = transform.Find("Panel") as RectTransform;
            var segment = transform.Find("Panel/Health/BarTrack/Segment0")
                ?.GetComponent<LayoutElement>();
            var staminaBar = transform.Find("Panel/Stamina/Bar") as RectTransform;
            return panelRect != null &&
                   Mathf.Approximately(panelRect.anchorMin.x, 0.5f) &&
                   Mathf.Approximately(panelRect.anchorMax.x, 0.5f) &&
                   Mathf.Approximately(panelRect.sizeDelta.x, PanelWidth) &&
                   transform.Find("Panel/Health/BarTrack") != null &&
                   staminaBar != null &&
                   staminaBar.GetComponent<ParallelogramShear>() != null &&
                   Mathf.Approximately(staminaBar.sizeDelta.x, BarWidth) &&
                   transform.Find("Panel/Stamina/Value") == null &&
                   transform.Find("Panel/Health/Value") == null &&
                   segment != null &&
                   Mathf.Approximately(segment.preferredWidth, SegmentWidth) &&
                   Mathf.Approximately(segment.flexibleWidth, 0f);
        }

        private void DestroyChild(string childName)
        {
            var child = transform.Find(childName);
            if (child == null)
            {
                return;
            }

            DestroyImmediate(child.gameObject);
        }

        private void ApplyBarMetrics()
        {
            if (panel != null && panel.transform is RectTransform panelRect)
            {
                Place(
                    panelRect,
                    new Vector2(0.5f, 0f),
                    new Vector2(0f, BottomPadding),
                    new Vector2(PanelWidth, PanelHeight),
                    new Vector2(0.5f, 0f));
            }

            var staminaRowTransform = transform.Find("Panel/Stamina") as RectTransform;
            if (staminaRowTransform != null)
            {
                StretchRow(staminaRowTransform, 16f);
                staminaRow = staminaRowTransform;
                staminaRowRest = staminaRowTransform.anchoredPosition;
            }

            var healthRow = transform.Find("Panel/Health") as RectTransform;
            if (healthRow != null)
            {
                StretchRow(healthRow, -16f);
            }

            if (staminaFill != null)
            {
                PlaceBar(staminaFill, BarStart, BarWidth, BarHeight);
            }

            var track = transform.Find("Panel/Health/BarTrack") as RectTransform;
            if (track != null)
            {
                PlaceBar(track, BarStart, BarWidth, BarHeight);
            }
        }

        private void BuildLayout()
        {
            var panelRect = CreateImage(
                transform,
                "Panel",
                PanelColor,
                HomeUiFonts.RoundedSprite).rectTransform;
            panelRect.GetComponent<Image>().type = Image.Type.Sliced;
            Place(
                panelRect,
                new Vector2(0.5f, 0f),
                new Vector2(0f, BottomPadding),
                new Vector2(PanelWidth, PanelHeight),
                new Vector2(0.5f, 0f));
            panel = panelRect.gameObject;

            var stamina = CreateRow(panelRect, "Stamina", FlashIconResource, Color.white);
            StretchRow(stamina, 16f);
            staminaRow = stamina;
            staminaRowRest = stamina.anchoredPosition;
            staminaIcon = stamina.Find("Icon")?.GetComponent<Image>();
            staminaFill = CreateFillBar(stamina, "Bar", StaminaColor);

            var health = CreateRow(panelRect, "Health", HeartIconResource, Color.white);
            StretchRow(health, -16f);
            healthSegments = CreateHealthSegments(health);
        }

        private static RectTransform CreateRow(
            Transform parent,
            string name,
            string iconResource,
            Color iconColor)
        {
            var row = CreateRect(parent, name);
            var icon = CreateImage(row, "Icon", iconColor, Resources.Load<Sprite>(iconResource));
            icon.preserveAspect = true;
            Place(
                icon.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(IconPadding, 0f),
                new Vector2(IconSize, IconSize),
                new Vector2(0f, 0.5f));
            return row;
        }

        private static RectTransform CreateFillBar(Transform parent, string name, Color color)
        {
            var bar = CreateImage(parent, name, color, HomeUiFonts.WhiteSprite);
            bar.preserveAspect = false;
            bar.type = Image.Type.Filled;
            bar.fillMethod = Image.FillMethod.Horizontal;
            bar.fillAmount = 1f;
            bar.gameObject.AddComponent<ParallelogramShear>();
            PlaceBar(bar.rectTransform, BarStart, BarWidth, BarHeight);
            return bar.rectTransform;
        }

        private static RectTransform[] CreateHealthSegments(Transform parent)
        {
            var track = CreateRect(parent, "BarTrack");
            PlaceBar(track, BarStart, BarWidth, BarHeight);
            var layout = track.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = SegmentGap;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            layout.padding = new RectOffset(0, 0, 0, 0);

            var segments = new RectTransform[DefaultHits];
            for (var index = 0; index < DefaultHits; index++)
            {
                var bar = CreateImage(
                    track,
                    $"Segment{index}",
                    HealthColorAt(index, DefaultHits),
                    HomeUiFonts.WhiteSprite);
                bar.preserveAspect = false;
                bar.gameObject.AddComponent<ParallelogramShear>();
                var element = bar.gameObject.AddComponent<LayoutElement>();
                element.minWidth = SegmentWidth;
                element.preferredWidth = SegmentWidth;
                element.flexibleWidth = 0f;
                element.preferredHeight = BarHeight;
                segments[index] = bar.rectTransform;
            }

            return segments;
        }

        private void StripValueLabels()
        {
            DestroyChildAt("Panel/Stamina/Value");
            DestroyChildAt("Panel/Health/Value");
        }

        private void DestroyChildAt(string path)
        {
            var child = transform.Find(path);
            if (child != null)
            {
                DestroyImmediate(child.gameObject);
            }
        }

        private static RectTransform CreateRect(Transform parent, string name)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject.GetComponent<RectTransform>();
        }

        private static Image CreateImage(Transform parent, string name, Color color, Sprite sprite)
        {
            var gameObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            gameObject.transform.SetParent(parent, false);
            var image = gameObject.GetComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            image.raycastTarget = false;
            image.preserveAspect = sprite != null;
            return image;
        }

        private static void Place(
            RectTransform rect,
            Vector2 anchor,
            Vector2 anchoredPosition,
            Vector2 size,
            Vector2? pivot = null)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void StretchRow(RectTransform rect, float y)
        {
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(RowInset, y - 14f);
            rect.offsetMax = new Vector2(-RowInset, y + 14f);
        }

        private static void PlaceBar(
            RectTransform rect,
            float left,
            float width,
            float height)
        {
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(left, 0f);
            rect.sizeDelta = new Vector2(width, height);
        }
    }
}
