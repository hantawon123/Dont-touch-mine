using System;
using System.Collections.Generic;
using Game.Client.Home;
using Game.Core.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Game.Client.Settings
{
    [DisallowMultipleComponent]
    public sealed class SettingsView : MonoBehaviour, ISettingsView
    {
        private static readonly Color RowColor = new Color(0.92f, 0.92f, 0.92f, 1f);
        private static readonly Color TrackColor = new Color(0.82f, 0.82f, 0.82f, 1f);
        private static readonly Color FillColor = new Color(0.31f, 0.62f, 0.91f, 1f);
        private static readonly Color TabIdle = new Color(0.45f, 0.45f, 0.45f, 1f);
        private static readonly Color MenuHover = new Color(0.18f, 0.47f, 0.98f, 1f);
        private static readonly Color MenuPressed = new Color(0.10f, 0.32f, 0.78f, 1f);
        private static readonly Color PlaceholderColor = new Color(0.55f, 0.55f, 0.55f, 1f);

        private static readonly (SettingsTab Tab, string Label)[] Tabs =
        {
            (SettingsTab.Graphics, "그래픽"),
            (SettingsTab.Audio, "오디오"),
            (SettingsTab.Controls, "조작"),
            (SettingsTab.Accessibility, "접근성"),
            (SettingsTab.Notifications, "알림")
        };

        [SerializeField]
        private TMP_FontAsset fontAsset;

        private readonly List<Button> buttons = new List<Button>();
        private readonly Dictionary<SettingsTab, TabButton> tabButtons = new Dictionary<SettingsTab, TabButton>();
        private readonly Dictionary<SettingsTab, GameObject> panels = new Dictionary<SettingsTab, GameObject>();
        private TMP_FontAsset koreanFont;
        private SettingsTab activeTab = SettingsTab.Graphics;

        public event Action BackRequested;

        public event Action<SettingsTab> TabSelected;

        private void Awake()
        {
            EnsureEventSystem();
            BuildLayout();
            SetActiveTab(SettingsTab.Graphics);
        }

        private void OnDestroy()
        {
            for (var index = 0; index < buttons.Count; index++)
            {
                if (buttons[index] != null)
                {
                    buttons[index].onClick.RemoveAllListeners();
                }
            }
        }

        public void SetActiveTab(SettingsTab tab)
        {
            activeTab = tab;
            foreach (var pair in panels)
            {
                if (pair.Value != null)
                {
                    pair.Value.SetActive(pair.Key == tab);
                }
            }

            foreach (var pair in tabButtons)
            {
                var selected = pair.Key == tab;
                pair.Value.Label.color = selected ? Color.black : TabIdle;
                pair.Value.Label.fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
                pair.Value.Underline.SetActive(selected);
            }
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            var eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.transform.SetParent(transform, false);
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
        }

        private void BuildLayout()
        {
            koreanFont = HomeUiFonts.Apply(fontAsset);
            var canvas = CreateCanvas();
            CreateHeader(canvas);
            CreateTabBar(canvas);
            CreatePanels(canvas);
        }

        private RectTransform CreateCanvas()
        {
            var canvasObject = new GameObject("SettingsCanvas", typeof(RectTransform));
            canvasObject.layer = LayerMask.NameToLayer("UI");
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvas.additionalShaderChannels =
                AdditionalCanvasShaderChannels.TexCoord1
                | AdditionalCanvasShaderChannels.Normal
                | AdditionalCanvasShaderChannels.Tangent;

            var canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.offsetMin = Vector2.zero;
            canvasRect.offsetMax = Vector2.zero;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            AddImage(canvasRect, Color.white);
            return canvasRect;
        }

        private void CreateHeader(RectTransform canvas)
        {
            var header = CreateRect("Header", canvas);
            SetAnchor(header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            header.anchoredPosition = Vector2.zero;
            header.sizeDelta = new Vector2(0f, 96f);

            var layout = header.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(40, 40, 24, 12);
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            CreateTextButton(header, "Back", "<", 36f, FontStyles.Bold, 48f, () => BackRequested?.Invoke());

            var titleRect = CreateRect("Title", header);
            var titleLayout = titleRect.gameObject.AddComponent<LayoutElement>();
            titleLayout.preferredWidth = 280f;
            titleLayout.minWidth = 200f;
            titleLayout.flexibleWidth = 1f;
            AddText(titleRect, "환경 설정", 36f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        }

        private void CreateTabBar(RectTransform canvas)
        {
            var tabBar = CreateRect("Tabs", canvas);
            SetAnchor(tabBar, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            tabBar.anchoredPosition = new Vector2(0f, -96f);
            tabBar.sizeDelta = new Vector2(0f, 64f);

            var layout = tabBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(48, 48, 0, 0);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            for (var index = 0; index < Tabs.Length; index++)
            {
                var tab = Tabs[index];
                tabButtons[tab.Tab] = CreateTabButton(tabBar, tab.Tab, tab.Label);
            }
        }

        private TabButton CreateTabButton(RectTransform parent, SettingsTab tab, string label)
        {
            var rect = CreateRect(tab.ToString(), parent);
            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 64f;
            layout.minHeight = 48f;

            var column = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            column.childAlignment = TextAnchor.MiddleCenter;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
            column.spacing = 4f;

            var labelRect = CreateRect("Label", rect);
            var labelLayout = labelRect.gameObject.AddComponent<LayoutElement>();
            labelLayout.preferredHeight = 36f;
            var text = AddText(
                labelRect,
                label,
                24f,
                FontStyles.Normal,
                TextAlignmentOptions.Center,
                raycastTarget: true);
            text.color = TabIdle;

            var underline = CreateRect("Underline", rect);
            var underlineLayout = underline.gameObject.AddComponent<LayoutElement>();
            underlineLayout.preferredHeight = 4f;
            underlineLayout.minHeight = 4f;
            AddImage(underline, FillColor);
            underline.gameObject.SetActive(false);

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = text;
            button.transition = Selectable.Transition.ColorTint;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.85f, 0.9f, 1f, 1f);
            colors.pressedColor = new Color(0.75f, 0.82f, 1f, 1f);
            colors.selectedColor = Color.white;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(() => TabSelected?.Invoke(tab));
            buttons.Add(button);

            return new TabButton
            {
                Label = text,
                Underline = underline.gameObject
            };
        }

        private void CreatePanels(RectTransform canvas)
        {
            var body = CreateRect("Body", canvas);
            SetAnchor(body, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            body.offsetMin = new Vector2(48f, 40f);
            body.offsetMax = new Vector2(-48f, -176f);

            panels[SettingsTab.Graphics] = CreateGraphicsPanel(body).gameObject;
            panels[SettingsTab.Audio] = CreatePlaceholder(body, "Audio", "오디오 설정은 준비 중입니다").gameObject;
            panels[SettingsTab.Controls] = CreatePlaceholder(body, "Controls", "조작 설정은 준비 중입니다").gameObject;
            panels[SettingsTab.Accessibility] = CreatePlaceholder(
                body,
                "Accessibility",
                "접근성 설정은 준비 중입니다").gameObject;
            panels[SettingsTab.Notifications] = CreatePlaceholder(
                body,
                "Notifications",
                "알림 설정은 준비 중입니다").gameObject;
        }

        private RectTransform CreateGraphicsPanel(RectTransform parent)
        {
            var panel = CreateRect("Graphics", parent);
            SetAnchor(panel, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;

            var scroll = panel.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;

            var viewport = CreateRect("Viewport", panel);
            SetAnchor(viewport, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;
            viewport.gameObject.AddComponent<RectMask2D>();
            AddImage(viewport, Color.clear, raycastTarget: true);

            var content = CreateRect("Content", viewport);
            SetAnchor(content, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 12f;
            contentLayout.padding = new RectOffset(8, 8, 8, 8);
            contentLayout.childAlignment = TextAnchor.UpperCenter;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = content;

            CreateCycleRow(
                content,
                "그래픽 품질",
                new[] { "매우 낮음", "낮음", "중간", "높음", "매우 높음", "사용자 설정" },
                3);
            CreateCycleRow(
                content,
                "해상도",
                new[] { "1280x720", "1920x1080", "2560x1440", "3840x2160" },
                1);
            CreateCycleRow(
                content,
                "화면 모드",
                new[] { "전체 화면", "창 모드", "무테 창 모드" },
                0);
            CreateCycleRow(
                content,
                "프레임 제한",
                new[] { "30", "60", "90", "120", "144", "165", "240", "제한 없음" },
                1);
            CreateCycleRow(
                content,
                "그림자 품질",
                new[] { "끄기", "낮음", "중간", "높음", "매우 높음" },
                3);
            CreateCycleRow(
                content,
                "이펙트 품질",
                new[] { "낮음", "중간", "높음", "매우 높음" },
                2);
            CreateCycleRow(
                content,
                "안티앨리어싱",
                new[] { "끄기", "FXAA", "SMAA", "TAA" },
                3);
            CreateSliderRow(content, "밝기 / 감마", 50);
            return panel;
        }

        private RectTransform CreatePlaceholder(RectTransform parent, string name, string message)
        {
            var panel = CreateRect(name, parent);
            SetAnchor(panel, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            panel.gameObject.SetActive(false);

            var label = CreateRect("Hint", panel);
            SetAnchor(label, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            label.offsetMin = Vector2.zero;
            label.offsetMax = Vector2.zero;
            var text = AddText(label, message, 24f, FontStyles.Normal, TextAlignmentOptions.Center);
            text.color = PlaceholderColor;
            return panel;
        }

        private void CreateCycleRow(RectTransform parent, string label, string[] options, int defaultIndex)
        {
            var row = CreateSettingRow(parent);
            AddRowLabel(row, label);

            var control = CreateRect("Control", row);
            var controlLayout = control.gameObject.AddComponent<LayoutElement>();
            controlLayout.preferredWidth = 420f;
            controlLayout.minWidth = 320f;
            controlLayout.preferredHeight = 48f;
            var background = AddImage(control, RowColor, HomeUiFonts.PillSprite);
            background.type = Image.Type.Sliced;

            var layout = control.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 4, 4);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            var state = new CycleState
            {
                Options = options,
                Index = Mathf.Clamp(defaultIndex, 0, options.Length - 1)
            };

            CreateCycleArrow(control, "<", () =>
            {
                state.Index = (state.Index - 1 + state.Options.Length) % state.Options.Length;
                state.ValueLabel.text = state.Options[state.Index];
            });

            var valueRect = CreateRect("Value", control);
            var valueLayout = valueRect.gameObject.AddComponent<LayoutElement>();
            valueLayout.flexibleWidth = 1f;
            valueLayout.minWidth = 120f;
            state.ValueLabel = AddText(
                valueRect,
                options[state.Index],
                22f,
                FontStyles.Bold,
                TextAlignmentOptions.Center);

            CreateCycleArrow(control, ">", () =>
            {
                state.Index = (state.Index + 1) % state.Options.Length;
                state.ValueLabel.text = state.Options[state.Index];
            });
        }

        private void CreateCycleArrow(RectTransform parent, string label, Action onClicked)
        {
            var rect = CreateRect(label == "<" ? "Prev" : "Next", parent);
            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = 48f;
            layout.minWidth = 48f;
            layout.preferredHeight = 40f;
            CreateTextButton(rect, label, label, 24f, FontStyles.Bold, 48f, onClicked, stretch: true);
        }

        private void CreateSliderRow(RectTransform parent, string label, int defaultValue)
        {
            var row = CreateSettingRow(parent);
            AddRowLabel(row, label);

            var control = CreateRect("Control", row);
            var controlLayout = control.gameObject.AddComponent<LayoutElement>();
            controlLayout.preferredWidth = 420f;
            controlLayout.minWidth = 320f;
            controlLayout.preferredHeight = 48f;

            var layout = control.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            var sliderRect = CreateRect("Slider", control);
            var sliderLayout = sliderRect.gameObject.AddComponent<LayoutElement>();
            sliderLayout.flexibleWidth = 1f;
            sliderLayout.minWidth = 220f;
            sliderLayout.preferredHeight = 40f;

            var percentRect = CreateRect("Percent", control);
            var percentLayout = percentRect.gameObject.AddComponent<LayoutElement>();
            percentLayout.preferredWidth = 72f;
            percentLayout.minWidth = 72f;
            var percent = AddText(
                percentRect,
                $"{defaultValue}%",
                20f,
                FontStyles.Bold,
                TextAlignmentOptions.MidlineRight);

            var slider = CreateBrightnessSlider(sliderRect, defaultValue);
            slider.onValueChanged.AddListener(value => percent.text = $"{Mathf.RoundToInt(value)}%");
        }

        private Slider CreateBrightnessSlider(RectTransform parent, int defaultValue)
        {
            const float trackHeight = 6f;
            const float handleSize = 36f;

            var background = CreateRect("Background", parent);
            SetAnchor(background, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f));
            background.sizeDelta = new Vector2(0f, trackHeight);
            background.anchoredPosition = Vector2.zero;
            var backgroundImage = AddImage(background, TrackColor, HomeUiFonts.PillSprite, raycastTarget: true);
            backgroundImage.type = Image.Type.Sliced;

            var fillArea = CreateRect("Fill Area", parent);
            SetAnchor(fillArea, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f));
            fillArea.sizeDelta = new Vector2(-handleSize, trackHeight);
            fillArea.anchoredPosition = Vector2.zero;

            var fill = CreateRect("Fill", fillArea);
            SetAnchor(fill, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            var fillImage = AddImage(fill, FillColor, HomeUiFonts.PillSprite);
            fillImage.type = Image.Type.Sliced;

            var handleArea = CreateRect("Handle Slide Area", parent);
            SetAnchor(handleArea, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f));
            handleArea.sizeDelta = new Vector2(-handleSize, handleSize);
            handleArea.anchoredPosition = Vector2.zero;

            var handle = CreateRect("Handle", handleArea);
            handle.sizeDelta = new Vector2(handleSize, 0f);
            var handleImage = AddImage(handle, FillColor, HomeUiFonts.CircleSprite, raycastTarget: true);
            handleImage.preserveAspect = true;

            var slider = parent.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 100f;
            slider.wholeNumbers = true;
            slider.value = defaultValue;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            return slider;
        }

        private static RectTransform CreateSettingRow(RectTransform parent)
        {
            var row = CreateRect("Row", parent);
            var layout = row.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 72f;
            layout.minHeight = 72f;
            var group = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.padding = new RectOffset(24, 24, 12, 12);
            group.spacing = 24f;
            group.childAlignment = TextAnchor.MiddleLeft;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = true;
            var background = AddImage(row, new Color(0.96f, 0.96f, 0.96f, 1f), HomeUiFonts.RoundedSprite);
            background.type = Image.Type.Sliced;
            return row;
        }

        private void AddRowLabel(RectTransform row, string label)
        {
            var labelRect = CreateRect("Label", row);
            var layout = labelRect.gameObject.AddComponent<LayoutElement>();
            layout.flexibleWidth = 1f;
            layout.minWidth = 180f;
            AddText(labelRect, label, 24f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        }

        private void CreateTextButton(
            RectTransform parent,
            string name,
            string label,
            float fontSize,
            FontStyles style,
            float preferredWidth,
            Action onClicked,
            bool stretch = false)
        {
            RectTransform buttonRect = parent;
            if (!stretch)
            {
                buttonRect = CreateRect(name, parent);
                buttonRect.sizeDelta = new Vector2(preferredWidth, 56f);
                var layoutElement = buttonRect.gameObject.AddComponent<LayoutElement>();
                layoutElement.preferredWidth = preferredWidth;
                layoutElement.minWidth = preferredWidth;
                layoutElement.preferredHeight = 56f;
                layoutElement.minHeight = 48f;
                layoutElement.flexibleWidth = 0f;
            }

            var text = AddText(buttonRect, label, fontSize, style, TextAlignmentOptions.Center, raycastTarget: true);
            text.color = Color.white;
            var button = buttonRect.gameObject.AddComponent<Button>();
            button.targetGraphic = text;
            button.transition = Selectable.Transition.ColorTint;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.black;
            colors.highlightedColor = MenuHover;
            colors.pressedColor = MenuPressed;
            colors.selectedColor = Color.black;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            text.CrossFadeColor(colors.normalColor, 0f, true, true);
            button.onClick.AddListener(() => onClicked?.Invoke());
            buttons.Add(button);
        }

        private TMP_Text AddText(
            RectTransform target,
            string content,
            float fontSize,
            FontStyles style,
            TextAlignmentOptions alignment,
            bool raycastTarget = false)
        {
            if (koreanFont == null)
            {
                throw new InvalidOperationException("Cafe24 Ssurround TMP font is missing.");
            }

            target.gameObject.SetActive(false);
            var tmp = target.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = koreanFont;
            tmp.fontSharedMaterial = koreanFont.material;
            tmp.text = content;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.alignment = alignment;
            tmp.color = Color.black;
            tmp.raycastTarget = raycastTarget;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            target.gameObject.SetActive(true);
            return tmp;
        }

        private static Image AddImage(
            RectTransform rect,
            Color color,
            Sprite sprite = null,
            bool raycastTarget = false)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite != null ? sprite : HomeUiFonts.WhiteSprite;
            image.type = Image.Type.Simple;
            image.color = color;
            image.raycastTarget = raycastTarget;
            return image;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var rectObject = new GameObject(name, typeof(RectTransform));
            rectObject.layer = LayerMask.NameToLayer("UI");
            var rect = rectObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void SetAnchor(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
        }

        private sealed class TabButton
        {
            public TMP_Text Label;
            public GameObject Underline;
        }

        private sealed class CycleState
        {
            public string[] Options;
            public int Index;
            public TMP_Text ValueLabel;
        }
    }
}
