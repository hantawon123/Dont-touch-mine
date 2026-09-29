using Game.Client.Home;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Match
{
    /// <summary>
    /// Bottom-center stamina and hit bars. Stamina fill follows the local motor each frame.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MatchVitalsHudView : MonoBehaviour
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
        public static float SegmentWidth => SegmentWidthFor(DefaultHits);
        public static float BarWidth =>
            (SegmentWidth * DefaultHits) + (SegmentGap * (DefaultHits - 1));

        public static float SegmentWidthFor(int hits)
        {
            var count = Mathf.Max(1, hits);
            return (TrackWidth - (SegmentGap * (count - 1))) / count;
        }

        /// <summary>무지개 램프의 가로 해상도. 바는 막대 하나라 스프라이트가 그라데이션을 진다.</summary>
        public const int RainbowRampWidth = 128;

        /// <summary>바 한 변에 걸리는 색상환 바퀴 수. 1이면 빨→빨 한 바퀴가 딱 들어간다.</summary>
        public const float RainbowTurnsAcrossBar = 1f;

        /// <summary>무지개가 흘러가는 속도(초당 바퀴 수).</summary>
        public const float RainbowTurnsPerSecond = 0.35f;

        public const float RainbowSaturation = 0.82f;
        public const float RainbowValue = 1f;

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
        private bool finalSprintActive;
        private float rainbowElapsed;
        private Texture2D rainbowTexture;
        private Sprite rainbowSprite;
        private Sprite plainBarSprite;
        private Color32[] rainbowPixels;
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
            return StaminaAccentFor(current, exhausted, false);
        }

        public static Color StaminaAccentFor(float current, bool exhausted, bool finalSprint)
        {
            if (finalSprint)
            {
                return RainbowColorAt(0f, 0f);
            }

            if (exhausted)
            {
                return StaminaDisabledColor;
            }

            return IsLowStamina(current) ? StaminaLowColor : Color.white;
        }

        /// <summary>
        /// 무지개가 지금 얼만큼 흘렀는지. 바퀴 단위라 0.5는 반바퀴 돌아간 상태다.
        /// </summary>
        public static float RainbowPhase(float elapsedSeconds)
        {
            return Mathf.Repeat(elapsedSeconds * RainbowTurnsPerSecond, 1f);
        }

        /// <summary>
        /// 바의 왼쪽 끝을 0, 오른쪽 끝을 1로 본 <paramref name="t"/> 지점의 색.
        /// </summary>
        public static Color RainbowColorAt(float t, float phase)
        {
            var hue = Mathf.Repeat(phase + Mathf.Clamp01(t) * RainbowTurnsAcrossBar, 1f);
            var color = Color.HSVToRGB(hue, RainbowSaturation, RainbowValue);
            color.a = 1f;
            return color;
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

        public void Show(float stamina, float maxStamina, int hits, int maxHits, bool exhausted = false,
            bool finalSprint = false)
        {
            shown = true;
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            EnsureLayout();
            SetValues(stamina, maxStamina, hits, maxHits, exhausted, finalSprint);
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
            ApplyFinalSprint(false);
            if (panel != null)
            {
                panel.SetActive(false);
            }
        }

        public void SetValues(float stamina, float maxStamina, int hits, int maxHits, bool exhausted = false,
            bool finalSprint = false)
        {
            EnsureLayout();
            ApplyFinalSprint(finalSprint);
            // 무지개는 스프라이트가 지고 Image.color는 그 위에 곱해지므로, 그때는 흰색으로 둔다.
            var staminaColor = finalSprint ? Color.white : StaminaColorFor(stamina, exhausted);
            var staminaAccent = StaminaAccentFor(stamina, exhausted, finalSprint);
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

            if (shown && !finalSprint && ShouldShakeStamina(stamina, lastStamina, exhausted))
            {
                shakeStamina = true;
            }
            else
            {
                StopStaminaShake();
            }

            lastStamina = stamina;

            EnsureHealthSegments(Mathf.Max(1, maxHits));
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
            if (shown && finalSprintActive)
            {
                rainbowElapsed += Time.unscaledDeltaTime;
                PaintRainbow(RainbowPhase(rainbowElapsed));
            }

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

        private void OnDestroy()
        {
            DestroyGenerated(rainbowSprite);
            DestroyGenerated(rainbowTexture);
            rainbowSprite = null;
            rainbowTexture = null;
            rainbowPixels = null;
        }

        private static void DestroyGenerated(Object generated)
        {
            if (generated == null) return;
            if (Application.isPlaying) Destroy(generated);
            else DestroyImmediate(generated);
        }

        /// <summary>
        /// 바를 무지개 램프로 갈아끼우거나, 원래의 단색 막대로 되돌린다.
        /// </summary>
        private void ApplyFinalSprint(bool finalSprint)
        {
            finalSprintActive = finalSprint;
            var fill = staminaFill != null ? staminaFill.GetComponent<Image>() : null;
            if (fill == null) return;
            // 기준은 플래그가 아니라 지금 바가 지고 있는 스프라이트다. 그래야 중간에 레이아웃을
            // 다시 세워도 무지개가 눈에 띄게 사라지거나 매 프레임 다시 출발하지 않는다.
            if (!finalSprint)
            {
                if (rainbowSprite != null && fill.sprite == rainbowSprite && plainBarSprite != null)
                    fill.sprite = plainBarSprite;
                return;
            }

            if (rainbowSprite != null && fill.sprite == rainbowSprite) return;
            plainBarSprite = fill.sprite != null ? fill.sprite : HomeUiFonts.WhiteSprite;
            rainbowElapsed = 0f;
            EnsureRainbowSprite();
            PaintRainbow(0f);
            fill.sprite = rainbowSprite;
        }

        private void EnsureRainbowSprite()
        {
            if (rainbowSprite != null) return;
            rainbowTexture = new Texture2D(RainbowRampWidth, 1, TextureFormat.RGBA32, false)
            {
                name = "StaminaRainbowRamp",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            rainbowPixels = new Color32[RainbowRampWidth];
            rainbowSprite = Sprite.Create(
                rainbowTexture,
                new Rect(0f, 0f, RainbowRampWidth, 1f),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect);
            rainbowSprite.name = "StaminaRainbow";
            rainbowSprite.hideFlags = HideFlags.HideAndDontSave;
        }

        private void PaintRainbow(float phase)
        {
            if (rainbowTexture == null || rainbowPixels == null) return;
            for (var index = 0; index < rainbowPixels.Length; index++)
            {
                var t = rainbowPixels.Length <= 1
                    ? 0f
                    : index / (float)(rainbowPixels.Length - 1);
                rainbowPixels[index] = RainbowColorAt(t, phase);
            }

            rainbowTexture.SetPixels32(rainbowPixels);
            rainbowTexture.Apply(false);
            if (staminaIcon != null) staminaIcon.color = RainbowColorAt(0f, phase);
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
                healthSegments = CollectHealthSegments();
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

        private void EnsureHealthSegments(int count)
        {
            var track = transform.Find("Panel/Health/BarTrack") as RectTransform;
            if (track == null)
            {
                return;
            }

            if (healthSegments == null || healthSegments.Length != count)
            {
                RebuildHealthSegments(track, count);
            }

            ApplyHealthSegmentMetrics(count);
        }

        private void RebuildHealthSegments(RectTransform track, int count)
        {
            for (var index = track.childCount - 1; index >= 0; index--)
            {
                DestroyImmediate(track.GetChild(index).gameObject);
            }

            healthSegments = CreateHealthSegmentsOn(track, count);
        }

        private RectTransform[] CollectHealthSegments()
        {
            var track = transform.Find("Panel/Health/BarTrack");
            if (track == null)
            {
                return null;
            }

            var count = 0;
            while (track.Find($"Segment{count}") != null)
            {
                count++;
            }

            if (count == 0)
            {
                return null;
            }

            var segments = new RectTransform[count];
            for (var index = 0; index < count; index++)
            {
                segments[index] = track.Find($"Segment{index}") as RectTransform;
            }

            return segments;
        }

        private void ApplyHealthSegmentMetrics(int count)
        {
            if (healthSegments == null)
            {
                return;
            }

            var width = SegmentWidthFor(count);
            for (var index = 0; index < healthSegments.Length; index++)
            {
                var segment = healthSegments[index];
                if (segment == null)
                {
                    continue;
                }

                var element = segment.GetComponent<LayoutElement>();
                if (element != null)
                {
                    element.minWidth = width;
                    element.preferredWidth = width;
                    element.flexibleWidth = 0f;
                    element.preferredHeight = BarHeight;
                }

                var image = segment.GetComponent<Image>();
                if (image != null)
                {
                    image.color = HealthColorAt(index, count);
                }
            }
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
            return CreateHealthSegmentsOn(track, DefaultHits);
        }

        private static RectTransform[] CreateHealthSegmentsOn(RectTransform track, int count)
        {
            var width = SegmentWidthFor(count);
            var segments = new RectTransform[count];
            for (var index = 0; index < count; index++)
            {
                var bar = CreateImage(
                    track,
                    $"Segment{index}",
                    HealthColorAt(index, count),
                    HomeUiFonts.WhiteSprite);
                bar.preserveAspect = false;
                bar.gameObject.AddComponent<ParallelogramShear>();
                var element = bar.gameObject.AddComponent<LayoutElement>();
                element.minWidth = width;
                element.preferredWidth = width;
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
