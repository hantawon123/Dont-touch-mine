using System;
using System.Collections;
using System.Collections.Generic;
using Game.Client.Home;
using Game.Core.Lobby;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.Client.Match
{
    public enum MatchChatHudMode
    {
        Hidden,
        Full,
        Searching
    }

    public interface IChatView
    {
        event Action<string> SendRequested;

        void SetMessages(IReadOnlyList<LobbyChatMessage> messages);
        void ClearInput();
        void Deactivate();
    }

    /// <summary>한 줄 입력과 최근 메시지만 표시하는 인게임 채팅 View.</summary>
    public sealed class MatchChatView : MonoBehaviour, IChatView, ICancelHandler
    {
        public const int VisibleMessageCount = 4;
        public const float NameFontSize = 14f;
        public const float BodyFontSize = 20f;
        public const float InputFontSize = 16f;
        public const string PlaceholderText = "채팅 입력..";
        public const float InputWidth = 320f;
        public const int PanelRadius = 10;
        public const float ContentPadding = 16f;
        public const float SendIconGap = 8f;
        public const float OpenCooldownSeconds = 0.12f;
        public static readonly Color NameColor = new Color32(0xC1, 0xC1, 0xC1, 0xFF);
        public static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.62f);
        private const float HistoryHeight = 248f;
        private const float InputHeight = 48f;
        private const float PanelGap = 10f;
        public const float Margin = 24f;
        public const float SendIconSize = 24f;
        private const string SendOrangeResource = "UI/ic_send_orange";
        private const string SendGrayResource = "UI/ic_send_gray";

        private TMP_InputField inputField;
        private Image sendImage;
        private Sprite sendOrange;
        private Sprite sendGray;
        private Button sendButton;
        private Transform itemRoot;
        private RectTransform historyRect;
        private readonly RectTransform[] rows = new RectTransform[VisibleMessageCount];
        private readonly TMP_Text[] nameTexts = new TMP_Text[VisibleMessageCount];
        private readonly TMP_Text[] bodyTexts = new TMP_Text[VisibleMessageCount];
        private readonly CanvasGroup[] rowFades = new CanvasGroup[VisibleMessageCount];
        private static Sprite historyFadeSprite;
        private TMP_FontAsset cachedFont;
        private Sprite lastSendIcon;
        private Coroutine focusRoutine;
        private Coroutine clearRoutine;
        private float lastSendUnscaledTime = -1f;
        private float lastDeactivateUnscaledTime = -1f;
        private bool activated;
        private MatchChatHudMode mode = MatchChatHudMode.Full;
        private bool keepChromeVisible;
        private bool layoutReady;
        private bool fontPrewarmed;
        private Coroutine prewarmRoutine;
        private static bool pendingKeepChromeVisible;

        public event Action<string> SendRequested;
        public static bool BlocksPlayerInput { get; private set; }
        public bool IsActivated => activated;
        public bool ConsumedEscapeThisFrame { get; private set; }
        public MatchChatHudMode Mode => mode;
        public bool KeepChromeVisible => keepChromeVisible;
        public bool IsInputFocused =>
            activated || (inputField != null && inputField.isFocused);

        public static TMP_FontAsset ChatFont()
        {
            var regular = HomeUiFonts.ApplyRegular();
            if (IsPaperlogy(regular))
            {
                return regular;
            }

            return HomeUiFonts.Apply();
        }

        private TMP_FontAsset ResolveFont() => cachedFont ??= ChatFont();

        public static MatchChatView Create(Transform canvasParent, bool keepChromeVisible = false)
        {
            var root = new GameObject(
                "Match Chat",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(GraphicRaycaster));
            if (canvasParent != null)
            {
                root.transform.SetParent(canvasParent, false);
            }
            else
            {
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 20;
                root.AddComponent<CanvasScaler>().uiScaleMode =
                    CanvasScaler.ScaleMode.ScaleWithScreenSize;
            }

            return AttachTo(root, keepChromeVisible);
        }

        public static MatchChatView AttachTo(GameObject host, bool keepChromeVisible)
        {
            if (host == null)
            {
                throw new ArgumentNullException(nameof(host));
            }

            pendingKeepChromeVisible = keepChromeVisible;
            try
            {
                var view = host.GetComponent<MatchChatView>() ?? host.AddComponent<MatchChatView>();
                view.SetKeepChromeVisible(keepChromeVisible);
                return view;
            }
            finally
            {
                pendingKeepChromeVisible = false;
            }
        }

        public void SetKeepChromeVisible(bool value)
        {
            keepChromeVisible = value;
            EnsureLayout();
            ApplyPresentation();
        }

        public static IReadOnlyList<LobbyChatMessage> VisibleMessages(
            IReadOnlyList<LobbyChatMessage> messages)
        {
            var list = messages ?? Array.Empty<LobbyChatMessage>();
            var first = Mathf.Max(0, list.Count - VisibleMessageCount);
            if (first == 0)
            {
                return list;
            }

            var visible = new LobbyChatMessage[list.Count - first];
            for (var index = 0; index < visible.Length; index++)
            {
                visible[index] = list[first + index];
            }

            return visible;
        }

        public static float HistoryFadeAlpha(float normalizedFromTop)
        {
            return Mathf.Clamp01(normalizedFromTop);
        }

        public static bool ShouldOpenOnEnter(
            bool isActivated,
            bool isOpening,
            bool enterPressed,
            float now,
            float lastClosedAt)
        {
            return enterPressed &&
                   !isActivated &&
                   !isOpening &&
                   now - lastClosedAt >= OpenCooldownSeconds;
        }

        public static bool ShowsHistory(MatchChatHudMode hudMode)
        {
            return hudMode == MatchChatHudMode.Full;
        }

        public static bool ShowsInput(MatchChatHudMode hudMode, bool isActivated)
        {
            return hudMode != MatchChatHudMode.Hidden && isActivated;
        }

        public void SetMode(MatchChatHudMode value)
        {
            EnsureLayout();
            if (mode == value)
            {
                if (value == MatchChatHudMode.Hidden)
                {
                    if (gameObject.activeSelf)
                    {
                        gameObject.SetActive(false);
                    }

                    return;
                }

                if (!gameObject.activeSelf)
                {
                    gameObject.SetActive(true);
                    ApplyPresentation();
                }

                return;
            }

            mode = value;
            if (value == MatchChatHudMode.Hidden)
            {
                SetActivated(false);
                if (gameObject.activeSelf)
                {
                    gameObject.SetActive(false);
                }

                return;
            }

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            SetActivated(false);
        }

        private void Awake()
        {
            keepChromeVisible = pendingKeepChromeVisible || keepChromeVisible;
            EnsureLayout();
            ApplyFonts();
            SetActivated(false);
        }

        private void OnEnable()
        {
            if (inputField != null)
            {
                inputField.onSubmit.AddListener(HandleSubmit);
                inputField.onSelect.AddListener(HandleInputSelected);
            }

            if (sendButton != null)
            {
                sendButton.onClick.AddListener(HandleSendClicked);
            }

            if (!fontPrewarmed && prewarmRoutine == null)
            {
                prewarmRoutine = StartCoroutine(PrewarmChatFont());
            }
        }

        /// <summary>
        /// Lets go of the presentation, which outlives this view.
        /// </summary>
        /// <remarks>
        /// It is a project-wide object and its event would otherwise keep
        /// calling a view whose objects are gone. On destroy rather than on
        /// disable: a view that is switched off still has its lines and should
        /// have them right when it comes back.
        /// </remarks>
        private void OnDestroy()
        {
            if (presentation != null)
            {
                presentation.Changed -= Redraw;
                presentation = null;
            }
        }

        private void OnDisable()
        {
            if (inputField != null)
            {
                inputField.onSubmit.RemoveListener(HandleSubmit);
                inputField.onSelect.RemoveListener(HandleInputSelected);
            }

            if (sendButton != null)
            {
                sendButton.onClick.RemoveListener(HandleSendClicked);
            }

            if (focusRoutine != null)
            {
                StopCoroutine(focusRoutine);
                focusRoutine = null;
            }

            if (clearRoutine != null)
            {
                StopCoroutine(clearRoutine);
                clearRoutine = null;
            }

            if (prewarmRoutine != null)
            {
                StopCoroutine(prewarmRoutine);
                prewarmRoutine = null;
            }

            SetActivated(false);
        }

        private void Update()
        {
            ConsumedEscapeThisFrame = false;
            if (activated && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                ConsumedEscapeThisFrame = true;
                Deactivate();
                return;
            }
            if (!WasEnterPressedThisFrame())
            {
                return;
            }

            if (activated)
            {
                if (inputField != null && string.IsNullOrWhiteSpace(inputField.text))
                {
                    SetActivated(false);
                }

                return;
            }

            if (!ShouldOpenOnEnter(
                    activated,
                    focusRoutine != null,
                    true,
                    Time.unscaledTime,
                    lastDeactivateUnscaledTime))
            {
                return;
            }

            // Open on the next frame so the key that opens chat cannot submit it.
            focusRoutine = StartCoroutine(FocusInputNextFrame());
        }

        private IEnumerator PrewarmChatFont()
        {
            var font = ResolveFont();
            if (font != null && font.atlasPopulationMode == AtlasPopulationMode.Static)
            {
                fontPrewarmed = true;
                prewarmRoutine = null;
                yield break;
            }

            var glyphs = LoadKoreanGlyphs();
            if (font == null || glyphs == null || string.IsNullOrEmpty(glyphs.text))
            {
                fontPrewarmed = true;
                prewarmRoutine = null;
                yield break;
            }

            var set = glyphs.text.Replace("\r", string.Empty).Replace("\n", string.Empty);
            const int chunk = 160;
            for (var index = 0; index < set.Length; index += chunk)
            {
                font.TryAddCharacters(set.Substring(index, Mathf.Min(chunk, set.Length - index)));
                yield return null;
            }

            fontPrewarmed = true;
            prewarmRoutine = null;
        }

        private static TextAsset LoadKoreanGlyphs()
        {
            var glyphs = Resources.Load<TextAsset>("Fonts/KoreanGlyphs");
#if UNITY_EDITOR
            if (glyphs == null)
            {
                glyphs = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(
                    "Assets/_Game/Editor/FontAtlasCharacterSet.txt");
            }
#endif
            return glyphs;
        }

        private static bool WasEnterPressedThisFrame()
        {
            var keyboard = Keyboard.current;
            return keyboard != null &&
                   (keyboard.enterKey.wasPressedThisFrame ||
                    keyboard.numpadEnterKey.wasPressedThisFrame);
        }

        private Game.Core.Settings.InterfacePresentation presentation;

        /// <summary>
        /// The lines currently on screen, kept so a name that changes can be
        /// written again over the messages already there.
        /// </summary>
        private IReadOnlyList<LobbyChatMessage> shown = Array.Empty<LobbyChatMessage>();
        [VContainer.Inject]
        /// <summary>
        /// Whose names to show, and being told when that answer changes.
        /// </summary>
        /// <remarks>
        /// Redrawing on the change is what keeps a name off the screen after
        /// its owner has asked for it to be. A player who chats under their own
        /// name and then turns 스트리머 모드 on would otherwise leave every line
        /// they had already sent standing with their real name on it, which is
        /// the moment the setting is most likely to be turned on.
        /// </remarks>
        public void BindPresentation(Game.Core.Settings.InterfacePresentation value)
        {
            if (presentation != null)
            {
                presentation.Changed -= Redraw;
            }

            presentation = value;

            if (presentation != null)
            {
                presentation.Changed += Redraw;
            }
        }

        private void Redraw() => SetMessages(shown);

        public void SetMessages(IReadOnlyList<LobbyChatMessage> messages)
        {
            EnsureLayout();
            var list = messages ?? Array.Empty<LobbyChatMessage>();
            shown = list;
            var first = Mathf.Max(0, list.Count - VisibleMessageCount);
            var visibleCount = list.Count - first;
            var font = ResolveFont();
            for (var index = 0; index < VisibleMessageCount; index++)
            {
                var row = rows[index];
                if (row == null)
                {
                    continue;
                }

                if (index >= visibleCount)
                {
                    if (row.gameObject.activeSelf)
                    {
                        row.gameObject.SetActive(false);
                    }

                    continue;
                }

                if (!row.gameObject.activeSelf)
                {
                    row.gameObject.SetActive(true);
                }

                var message = list[first + index];
                ApplyLine(nameTexts[index], presentation == null ? message.SenderName : presentation.Name(message.SenderId, message.SenderName), font, NameFontSize, NameColor);
                ApplyLine(bodyTexts[index], message.Text, font, BodyFontSize, Color.white);
                bodyTexts[index]?.ForceMeshUpdate();
            }

            ApplyRowFade();
        }

        public void ClearInput()
        {
            if (inputField == null)
            {
                return;
            }

            Deactivate();
            if (!isActiveAndEnabled)
            {
                ApplyClearedInput(keepFocus: false);
                return;
            }

            if (clearRoutine != null)
            {
                StopCoroutine(clearRoutine);
            }

            clearRoutine = StartCoroutine(ClearInputNextFrame());
        }

        public void Deactivate()
        {
            SetActivated(false);
        }

        public void OnCancel(BaseEventData eventData)
        {
            Deactivate();
            eventData.Use();
        }

        private IEnumerator FocusInputNextFrame()
        {
            yield return null;
            SetActivated(true);
            focusRoutine = null;
        }

        private IEnumerator ClearInputNextFrame()
        {
            yield return null;
            ApplyClearedInput(keepFocus: false);
            clearRoutine = null;
        }

        private void ApplyClearedInput(bool keepFocus)
        {
            inputField.text = string.Empty;
            inputField.caretPosition = 0;
            inputField.selectionAnchorPosition = 0;
            inputField.selectionFocusPosition = 0;
            inputField.ForceLabelUpdate();
            if (keepFocus)
            {
                inputField.ActivateInputField();
                inputField.Select();
                return;
            }

            inputField.DeactivateInputField();
            if (EventSystem.current?.currentSelectedGameObject == inputField.gameObject)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        private void HandleInputSelected(string _)
        {
            if (!activated && mode != MatchChatHudMode.Hidden)
            {
                SetActivated(true);
            }
        }

        private void HandleSendClicked()
        {
            HandleSubmit(inputField != null ? inputField.text : string.Empty);
        }

        private void HandleSubmit(string text)
        {
            if (Time.unscaledTime - lastSendUnscaledTime < 0.08f)
            {
                return;
            }

            lastSendUnscaledTime = Time.unscaledTime;
            if (string.IsNullOrWhiteSpace(text))
            {
                SetActivated(false);
                ApplyClearedInput(keepFocus: false);
                return;
            }

            SendRequested?.Invoke(text.Trim());
            Deactivate();
        }

        private void SetActivated(bool value)
        {
            activated = value;
            BlocksPlayerInput = value;
            RefreshSendIcon();
            if (!value)
            {
                lastDeactivateUnscaledTime = Time.unscaledTime;
            }

            ApplyPresentation();
            if (inputField == null)
            {
                return;
            }

            if (value)
            {
                EventSystem.current?.SetSelectedGameObject(null);
                inputField.Select();
                inputField.ActivateInputField();
                return;
            }

            inputField.DeactivateInputField();
            if (EventSystem.current?.currentSelectedGameObject == inputField.gameObject)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        private void ApplyPresentation()
        {
            if (!layoutReady)
            {
                return;
            }

            var history = historyRect != null
                ? historyRect.gameObject
                : transform.Find("HistoryPanel")?.gameObject;
            var input = transform.Find("InputPanel")?.gameObject;
            var showHistory = ShouldShowHistory();
            var showInput = ShouldShowInput();
            if (history != null && history.activeSelf != showHistory)
            {
                history.SetActive(showHistory);
            }

            if (input != null && input.activeSelf != showInput)
            {
                input.SetActive(showInput);
            }

            if (transform is not RectTransform root)
            {
                return;
            }

            var height = 0f;
            if (showHistory)
            {
                height += HistoryHeight;
            }

            if (showHistory && showInput)
            {
                height += PanelGap;
            }

            if (showInput)
            {
                height += InputHeight;
            }

            root.sizeDelta = new Vector2(InputWidth, height);
        }

        private bool ShouldShowHistory() =>
            keepChromeVisible || ShowsHistory(mode);

        private bool ShouldShowInput() =>
            keepChromeVisible || ShowsInput(mode, activated);

        private void RefreshSendIcon()
        {
            if (sendImage == null)
            {
                return;
            }

            var icon = activated ? sendOrange : sendGray;
            if (icon == null || lastSendIcon == icon)
            {
                return;
            }

            lastSendIcon = icon;
            sendImage.sprite = icon;
        }

        private void EnsureLayout()
        {
            if (layoutReady && itemRoot != null && inputField != null)
            {
                FitTextViewport();
                ApplyInputOverflow();
                return;
            }

            sendOrange ??= Resources.Load<Sprite>(SendOrangeResource);
            sendGray ??= Resources.Load<Sprite>(SendGrayResource);
            if (transform.Find("HistoryPanel") == null)
            {
                ClearLegacyLayout();
                BuildLayout();
            }

            BindRefs();
            BindRows();
            FitPanels();
            FitTextViewport();
            IsolateCanvases();
            EnsureHistoryFade();
            ApplyRoundedPanels();
            ApplyFonts();
            layoutReady = true;
            ApplyInputOverflow();
            ApplyPresentation();
        }

        private void IsolateCanvases()
        {
            var rootCanvas = GetComponent<Canvas>();
            if (rootCanvas == null)
            {
                rootCanvas = gameObject.AddComponent<Canvas>();
            }

            if (GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }

            rootCanvas.overrideSorting = true;
            if (rootCanvas.sortingOrder < 25)
            {
                rootCanvas.sortingOrder = 25;
            }

            var input = transform.Find("InputPanel");
            if (input == null)
            {
                return;
            }

            var inputCanvas = input.GetComponent<Canvas>();
            if (inputCanvas == null)
            {
                inputCanvas = input.gameObject.AddComponent<Canvas>();
            }

            if (input.GetComponent<GraphicRaycaster>() == null)
            {
                input.gameObject.AddComponent<GraphicRaycaster>();
            }

            inputCanvas.overrideSorting = true;
            inputCanvas.sortingOrder = rootCanvas.sortingOrder + 1;
        }

        private static void ApplyLine(
            TMP_Text text,
            string value,
            TMP_FontAsset font,
            float fontSize,
            Color color)
        {
            if (text == null)
            {
                return;
            }

            if (text.text != value)
            {
                text.text = value;
            }

            if (text.font != font)
            {
                text.font = font;
            }

            if (!Mathf.Approximately(text.fontSize, fontSize))
            {
                text.fontSize = fontSize;
            }

            if (text.color != color)
            {
                text.color = color;
            }

            ApplyWrap(text);
        }

        private void ApplyFonts()
        {
            var font = ResolveFont();
            var texts = GetComponentsInChildren<TMP_Text>(true);
            for (var index = 0; index < texts.Length; index++)
            {
                var text = texts[index];
                if (text.font != font)
                {
                    text.font = font;
                }

                text.fontStyle = FontStyles.Normal;
                text.richText = false;
            }

            if (inputField != null)
            {
                inputField.fontAsset = font;
                inputField.richText = false;
                if (inputField.textComponent != null)
                {
                    if (inputField.textComponent.font != font)
                    {
                        inputField.textComponent.font = font;
                    }

                    inputField.textComponent.richText = false;
                }

                if (inputField.placeholder is TMP_Text placeholder)
                {
                    if (placeholder.font != font)
                    {
                        placeholder.font = font;
                    }

                    placeholder.richText = false;
                }
            }
        }

        private static bool IsPaperlogy(TMP_FontAsset font)
        {
            if (font == null)
            {
                return false;
            }

            if (font.name.IndexOf("Paperlogy", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            var family = font.faceInfo.familyName;
            return !string.IsNullOrEmpty(family) &&
                   family.IndexOf("Paperlogy", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void BindRefs()
        {
            if (historyRect == null)
            {
                historyRect = transform.Find("HistoryPanel") as RectTransform;
            }

            if (itemRoot == null)
            {
                itemRoot = transform.Find("HistoryPanel/Items");
            }

            if (inputField == null)
            {
                inputField = transform.Find("InputPanel")?.GetComponent<TMP_InputField>();
            }

            if (sendButton == null)
            {
                sendButton = transform.Find("InputPanel/Send")?.GetComponent<Button>();
            }

            if (sendImage == null)
            {
                sendImage = transform.Find("InputPanel/Send")?.GetComponent<Image>();
            }

            sendOrange ??= Resources.Load<Sprite>(SendOrangeResource);
            sendGray ??= Resources.Load<Sprite>(SendGrayResource);
        }

        private void BindRows()
        {
            if (itemRoot == null)
            {
                return;
            }

            for (var index = 0; index < VisibleMessageCount; index++)
            {
                var row = itemRoot.Find($"Row{index}") as RectTransform;
                rows[index] = row;
                nameTexts[index] = row != null ? row.Find("Name")?.GetComponent<TMP_Text>() : null;
                bodyTexts[index] = row != null ? row.Find("Body")?.GetComponent<TMP_Text>() : null;
                ApplyWrap(nameTexts[index]);
                ApplyWrap(bodyTexts[index]);
                if (row == null)
                {
                    rowFades[index] = null;
                    continue;
                }

                var group = row.GetComponent<CanvasGroup>();
                if (group == null)
                {
                    group = row.gameObject.AddComponent<CanvasGroup>();
                    group.blocksRaycasts = false;
                }

                rowFades[index] = group;
            }
        }

        private void FitPanels()
        {
            var root = transform as RectTransform;
            if (root != null)
            {
                root.sizeDelta = new Vector2(InputWidth, HistoryHeight + PanelGap + InputHeight);
            }

            var history = transform.Find("HistoryPanel") as RectTransform;
            if (history != null)
            {
                Place(
                    history,
                    new Vector2(0f, 1f),
                    new Vector2(0f, 0f),
                    new Vector2(InputWidth, HistoryHeight),
                    new Vector2(0f, 1f));
            }

            var input = transform.Find("InputPanel") as RectTransform;
            if (input == null)
            {
                return;
            }

            Place(
                input,
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(InputWidth, InputHeight),
                new Vector2(0f, 0f));
        }

        private void FitTextViewport()
        {
            var viewport = transform.Find("InputPanel/TextViewport") as RectTransform;
            if (viewport == null)
            {
                return;
            }

            Stretch(viewport);
            viewport.offsetMin = new Vector2(ContentPadding, 0f);
            viewport.offsetMax = new Vector2(-(SendIconSize + ContentPadding + SendIconGap), 0f);
            var textRect = viewport.Find("Text") as RectTransform;
            if (textRect != null && !Mathf.Approximately(textRect.anchorMax.x, 0f))
            {
                FitScrollingInputText(textRect);
            }

            FitInputLabel(viewport.Find("Placeholder") as RectTransform);
            ApplyInputOverflow();
        }

        private void ApplyInputOverflow()
        {
            var text = inputField != null
                ? inputField.textComponent
                : transform.Find("InputPanel/TextViewport/Text")?.GetComponent<TMP_Text>();
            if (text == null)
            {
                return;
            }

            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            if (inputField != null)
            {
                inputField.lineType = TMP_InputField.LineType.SingleLine;
            }
        }

        private void ApplyRowFade()
        {
            if (historyRect == null || itemRoot == null)
            {
                return;
            }

            var itemsRect = itemRoot as RectTransform;
            if (itemsRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(itemsRect);
            }

            var panelRect = historyRect.rect;
            var height = panelRect.height;
            if (height <= 1f)
            {
                return;
            }

            for (var index = 0; index < VisibleMessageCount; index++)
            {
                var row = rows[index];
                var group = rowFades[index];
                if (row == null || group == null || !row.gameObject.activeSelf)
                {
                    continue;
                }

                var local = (Vector2)historyRect.InverseTransformPoint(
                    row.TransformPoint(row.rect.center));
                var fromTop = (panelRect.yMax - local.y) / height;
                group.alpha = HistoryFadeAlpha(fromTop);
            }
        }

        private static void ApplyWrap(TMP_Text text)
        {
            if (text == null)
            {
                return;
            }

            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            text.enableAutoSizing = false;
            var layout = text.GetComponent<LayoutElement>();
            if (layout != null)
            {
                layout.minHeight = text.fontSize + 4f;
                layout.preferredHeight = -1f;
                layout.preferredWidth = -1f;
                layout.flexibleWidth = 1f;
                layout.flexibleHeight = -1f;
            }
        }

        private void EnsureHistoryFade()
        {
            var history = transform.Find("HistoryPanel") as RectTransform;
            if (history == null)
            {
                return;
            }

            var mask = history.GetComponent<Mask>();
            if (mask != null)
            {
                DestroyImmediate(mask);
            }

            if (history.GetComponent<RectMask2D>() == null)
            {
                history.gameObject.AddComponent<RectMask2D>();
            }

            var panelImage = history.GetComponent<Image>();
            if (panelImage != null)
            {
                panelImage.enabled = false;
            }

            var background = history.Find("Background") as RectTransform;
            if (background == null)
            {
                background = CreatePanel(history, "Background");
            }

            Stretch(background);
            ApplyHistoryBackground(background.GetComponent<Image>());
            background.SetSiblingIndex(0);
            itemRoot?.SetAsLastSibling();
        }

        private void ApplyRoundedPanels()
        {
            ApplyRoundedPanel(transform.Find("InputPanel")?.GetComponent<Image>());
        }

        private static void ApplyRoundedPanel(Image image)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = HomeUiFonts.Rounded(PanelRadius);
            image.type = Image.Type.Sliced;
            image.color = PanelColor;
            image.raycastTarget = true;
            image.preserveAspect = false;
        }

        private static void ApplyHistoryBackground(Image image)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = HistoryFadeSprite;
            image.type = Image.Type.Simple;
            image.color = PanelColor;
            image.raycastTarget = false;
            image.preserveAspect = false;
        }

        private static Sprite HistoryFadeSprite
        {
            get
            {
                if (historyFadeSprite != null)
                {
                    return historyFadeSprite;
                }

                var width = Mathf.RoundToInt(InputWidth);
                var height = Mathf.RoundToInt(HistoryHeight);
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };

                var halfWidth = width * 0.5f;
                var halfHeight = height * 0.5f;
                var radius = PanelRadius;
                for (var y = 0; y < height; y++)
                {
                    var fade = 1f - (y / (height - 1f));
                    for (var x = 0; x < width; x++)
                    {
                        var coverage = RoundedCoverage(x, y, halfWidth, halfHeight, radius);
                        texture.SetPixel(x, y, new Color(1f, 1f, 1f, coverage * fade));
                    }
                }

                texture.Apply(false, false);
                historyFadeSprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, width, height),
                    new Vector2(0.5f, 0.5f),
                    100f,
                    0,
                    SpriteMeshType.FullRect);
                historyFadeSprite.hideFlags = HideFlags.HideAndDontSave;
                return historyFadeSprite;
            }
        }

        private static float RoundedCoverage(
            int x,
            int y,
            float halfWidth,
            float halfHeight,
            float radius)
        {
            var dx = Mathf.Abs(x + 0.5f - halfWidth) - (halfWidth - radius);
            var dy = Mathf.Abs(y + 0.5f - halfHeight) - (halfHeight - radius);
            var outside = Mathf.Sqrt(
                (Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f)) +
                (Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f)));
            var distance = outside + Mathf.Min(Mathf.Max(dx, dy), 0f) - radius;
            return Mathf.Clamp01(0.5f - distance);
        }

        private void ClearLegacyLayout()
        {
            var rootImage = GetComponent<Image>();
            if (rootImage != null)
            {
                DestroyImmediate(rootImage);
            }

            for (var index = transform.childCount - 1; index >= 0; index--)
            {
                DestroyImmediate(transform.GetChild(index).gameObject);
            }
        }

        private void BuildLayout()
        {
            var root = (RectTransform)transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.zero;
            root.pivot = Vector2.zero;
            root.anchoredPosition = new Vector2(Margin, Margin);
            root.sizeDelta = new Vector2(InputWidth, HistoryHeight + PanelGap + InputHeight);

            var history = CreatePanel(root, "HistoryPanel");
            Place(
                history,
                new Vector2(0f, 1f),
                new Vector2(0f, 0f),
                new Vector2(InputWidth, HistoryHeight),
                new Vector2(0f, 1f));

            var items = new GameObject("Items", typeof(RectTransform), typeof(VerticalLayoutGroup));
            items.transform.SetParent(history, false);
            var itemsRect = (RectTransform)items.transform;
            Stretch(itemsRect, ContentPadding);
            var layout = items.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.LowerLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            itemRoot = items.transform;

            for (var index = 0; index < VisibleMessageCount; index++)
            {
                BuildRow(itemRoot, index);
            }

            var inputPanel = CreatePanel(root, "InputPanel");
            Place(
                inputPanel,
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(InputWidth, InputHeight),
                new Vector2(0f, 0f));

            var textArea = new GameObject("TextViewport", typeof(RectTransform), typeof(RectMask2D));
            textArea.transform.SetParent(inputPanel, false);
            var textAreaRect = (RectTransform)textArea.transform;
            Stretch(textAreaRect);
            textAreaRect.offsetMin = new Vector2(ContentPadding, 0f);
            textAreaRect.offsetMax = new Vector2(-(SendIconSize + ContentPadding + SendIconGap), 0f);

            var text = CreateText(
                textAreaRect,
                "Text",
                string.Empty,
                InputFontSize,
                Color.white);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            FitScrollingInputText(text.rectTransform);

            var placeholder = CreateText(
                textAreaRect,
                "Placeholder",
                PlaceholderText,
                InputFontSize,
                new Color(1f, 1f, 1f, 0.58f));
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;
            placeholder.textWrappingMode = TextWrappingModes.NoWrap;
            placeholder.overflowMode = TextOverflowModes.Truncate;
            FitInputLabel(placeholder.rectTransform);

            var send = CreateImage(inputPanel, "Send", Color.white, sendGray);
            send.preserveAspect = true;
            Place(
                send.rectTransform,
                new Vector2(1f, 0.5f),
                new Vector2(-ContentPadding, 0f),
                new Vector2(SendIconSize, SendIconSize),
                new Vector2(1f, 0.5f));
            sendButton = send.gameObject.AddComponent<Button>();
            sendButton.transition = Selectable.Transition.None;
            sendImage = send;

            inputField = inputPanel.gameObject.AddComponent<TMP_InputField>();
            inputField.targetGraphic = inputPanel.gameObject.GetComponent<Image>();
            inputField.textViewport = textAreaRect;
            inputField.textComponent = text;
            inputField.placeholder = placeholder;
            inputField.fontAsset = ChatFont();
            inputField.pointSize = InputFontSize;
            inputField.lineType = TMP_InputField.LineType.SingleLine;
            inputField.characterLimit = LobbyChatMessage.MaxTextLength;
            inputField.navigation = new Navigation { mode = Navigation.Mode.None };
            inputField.interactable = true;
            inputField.richText = false;
            inputField.onFocusSelectAll = false;
            inputField.restoreOriginalTextOnEscape = false;
            inputField.shouldHideSoftKeyboard = true;
            text.richText = false;
            text.parseCtrlCharacters = false;
            text.margin = Vector4.zero;
            placeholder.richText = false;
            placeholder.parseCtrlCharacters = false;
            placeholder.margin = Vector4.zero;
        }

        private static void BuildRow(Transform parent, int index)
        {
            var row = new GameObject(
                $"Row{index}",
                typeof(RectTransform),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            row.transform.SetParent(parent, false);
            var layout = row.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 2f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = row.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var name = CreateText(row.transform, "Name", string.Empty, NameFontSize, NameColor);
            name.alignment = TextAlignmentOptions.TopLeft;
            ApplyWrap(name);
            name.font = ChatFont();
            row.AddComponent<CanvasGroup>().blocksRaycasts = false;

            var body = CreateText(row.transform, "Body", string.Empty, BodyFontSize, Color.white);
            body.alignment = TextAlignmentOptions.TopLeft;
            ApplyWrap(body);
            body.font = ChatFont();
            row.SetActive(false);
        }

        private static RectTransform CreatePanel(Transform parent, string name)
        {
            var panel = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            panel.transform.SetParent(parent, false);
            var image = panel.GetComponent<Image>();
            image.sprite = HomeUiFonts.Rounded(PanelRadius);
            image.type = Image.Type.Sliced;
            image.color = PanelColor;
            image.raycastTarget = true;
            return panel.GetComponent<RectTransform>();
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
            image.raycastTarget = true;
            return image;
        }

        private static TMP_Text CreateText(
            Transform parent,
            string name,
            string content,
            float fontSize,
            Color color)
        {
            var gameObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI),
                typeof(LayoutElement));
            gameObject.transform.SetParent(parent, false);
            var text = gameObject.GetComponent<TextMeshProUGUI>();
            text.text = content;
            text.font = ChatFont();
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Normal;
            text.color = color;
            text.raycastTarget = false;
            text.richText = false;
            var layout = gameObject.GetComponent<LayoutElement>();
            layout.minHeight = fontSize + 4f;
            layout.preferredHeight = -1f;
            layout.flexibleWidth = 1f;
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
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private static void FitScrollingInputText(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(2048f, 0f);
            var text = rect.GetComponent<TMP_Text>();
            if (text != null)
            {
                text.margin = Vector4.zero;
                text.extraPadding = false;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.overflowMode = TextOverflowModes.Overflow;
            }

            var layout = rect.GetComponent<LayoutElement>();
            if (layout != null)
            {
                layout.ignoreLayout = true;
            }
        }

        private static void FitInputLabel(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            Stretch(rect);
            var text = rect.GetComponent<TMP_Text>();
            if (text != null)
            {
                text.margin = Vector4.zero;
                text.extraPadding = false;
            }

            var layout = rect.GetComponent<LayoutElement>();
            if (layout != null)
            {
                layout.ignoreLayout = true;
            }
        }

        private static void Stretch(RectTransform rect, float padding = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
        }
    }
}
