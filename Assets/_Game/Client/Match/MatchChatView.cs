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
        Searching,
        HidingWait
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
        public const string PlaceholderText = "[Enter]로 채팅 시작하기";
        public const float InputWidth = 320f;
        public const int PanelRadius = 10;
        public const float ContentPadding = 16f;
        public const float ItemSpacing = 10f;
        public const float NameBodySpacing = 2f;
        public const float SendIconGap = 8f;
        public const float OpenCooldownSeconds = 0.12f;
        public static readonly Color NameColor = new Color32(0xC1, 0xC1, 0xC1, 0xFF);
        public static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.62f);
        public const float MinHistoryHeight = 248f;
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
        private ScrollRect scrollRect;
        private readonly List<RectTransform> rows = new();
        private readonly List<TMP_Text> nameTexts = new();
        private readonly List<TMP_Text> bodyTexts = new();
        private readonly List<CanvasGroup> rowFades = new();
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
        private bool allowsActivation = true;
        private bool layoutReady;
        private float appliedListScale = 1f;
        private bool fontPrewarmed;
        private Coroutine prewarmRoutine;
        private Coroutine pendingSubmit;
        private static bool pendingKeepChromeVisible;
        private string composingText = string.Empty;

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
                Game.Client.Common.HudScreenScale.Ensure(root);
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

        public void SetAllowsActivation(bool value)
        {
            allowsActivation = value;
            if (!value && (activated || focusRoutine != null))
            {
                if (focusRoutine != null)
                {
                    StopCoroutine(focusRoutine);
                    focusRoutine = null;
                }

                SetActivated(false);
            }
        }

        public static void ApplyAllowsActivation(bool allowed)
        {
            var chats = FindObjectsByType<MatchChatView>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < chats.Length; i++)
            {
                chats[i].SetAllowsActivation(allowed);
            }
        }

        public static IReadOnlyList<LobbyChatMessage> VisibleMessages(
            IReadOnlyList<LobbyChatMessage> messages) =>
            messages ?? Array.Empty<LobbyChatMessage>();

        public static float HistoryFadeAlpha(float normalizedFromTop)
        {
            return Mathf.Clamp01(normalizedFromTop);
        }

        /// <summary>
        /// UI 크기와 글자 크기가 커질 때 목록 간격도 같은 비율로 키운다.
        /// CanvasScaler만으로는 행 높이와 간격이 따라가지 않아 메시지가 겹친다.
        /// </summary>
        public static float ListScale(float uiScale, float fontScale) =>
            Mathf.Max(0.01f, uiScale) * Mathf.Max(0.01f, fontScale);

        public static float ScaledItemSpacing(float uiScale, float fontScale = 1f) =>
            ItemSpacing * ListScale(uiScale, fontScale);

        public static float ScaledNameBodySpacing(float uiScale, float fontScale = 1f) =>
            NameBodySpacing * ListScale(uiScale, fontScale);

        public static float TextColumnWidth => InputWidth - (ContentPadding * 2f);

        public static float MeasuredLineHeight(TMP_Text text, float width)
        {
            if (text == null)
            {
                return 0f;
            }

            var value = text.text ?? string.Empty;
            if (value.Length == 0)
            {
                return text.fontSize + 4f;
            }

            var preferred = text.GetPreferredValues(value, Mathf.Max(1f, width), 0f);
            return Mathf.Max(text.fontSize + 4f, preferred.y);
        }

        public static float RowHeight(float nameHeight, float bodyHeight, float scale) =>
            nameHeight + (NameBodySpacing * Mathf.Max(0.01f, scale)) + bodyHeight;

        public static float ContentHeightForRows(IReadOnlyList<float> rowHeights, float scale)
        {
            var total = ContentPadding * 2f;
            var added = 0;
            if (rowHeights == null)
            {
                return total;
            }

            for (var index = 0; index < rowHeights.Count; index++)
            {
                if (rowHeights[index] <= 0f)
                {
                    continue;
                }

                if (added > 0)
                {
                    total += ItemSpacing * Mathf.Max(0.01f, scale);
                }

                total += rowHeights[index];
                added++;
            }

            return total;
        }

        /// <summary>
        /// 커밋된 글자와 IME가 아직 조합 중인 음절을 한 문자열로 붙인다.
        /// 한글 한 글자는 다음 키를 치기 전까지 <c>TMP_InputField.text</c>에 안 들어가서,
        /// 엔터가 빈 칸으로 오인되면 전송이 아니라 창이 닫힌다.
        /// </summary>
        public static string CombinedDraft(string committed, string composing)
        {
            if (string.IsNullOrEmpty(composing))
            {
                return committed ?? string.Empty;
            }

            return (committed ?? string.Empty) + composing;
        }

        public static string ResolveSubmitText(string submitted, string committed, string composing)
        {
            return ResolveSubmitText(submitted, committed, composing, string.Empty);
        }

        public static string ResolveSubmitText(
            string submitted,
            string committed,
            string composing,
            string label)
        {
            if (!string.IsNullOrWhiteSpace(submitted))
            {
                return submitted.Trim();
            }

            return VisibleDraft(committed, label, composing);
        }

        /// <summary>
        /// 엔터가 조합을 커밋하면서 compositionString을 먼저 비운다.
        /// 이미 입력칸에 들어간 음절만 버리고, 아직 안 들어간 마지막 글자는 남긴다.
        /// </summary>
        public static string NextComposing(string live, string committed, string held)
        {
            if (!string.IsNullOrEmpty(live))
            {
                return live;
            }

            if (!string.IsNullOrEmpty(held) &&
                !string.IsNullOrEmpty(committed) &&
                committed.EndsWith(held, StringComparison.Ordinal))
            {
                return string.Empty;
            }

            return held ?? string.Empty;
        }

        public static string VisibleDraft(string committed, string label, string composing)
        {
            committed ??= string.Empty;
            label ??= string.Empty;
            if (label.Length > committed.Length && !string.IsNullOrWhiteSpace(label))
            {
                return label.Trim();
            }

            return CombinedDraft(committed, composing).Trim();
        }

        public static bool AllowsActivationOnScreen(
            bool highlightInProgress,
            bool localHighlightComplete,
            bool resultSceneLoaded) =>
            !resultSceneLoaded && !(highlightInProgress && !localHighlightComplete);

        public static bool ShouldOpenOnEnter(
            bool isActivated,
            bool isOpening,
            bool enterPressed,
            float now,
            float lastClosedAt,
            bool allowsActivation = true)
        {
            return allowsActivation &&
                   enterPressed &&
                   !isActivated &&
                   !isOpening &&
                   now - lastClosedAt >= OpenCooldownSeconds;
        }

        public static bool ShowsHistory(MatchChatHudMode hudMode)
        {
            return hudMode == MatchChatHudMode.Full || hudMode == MatchChatHudMode.HidingWait;
        }

        public static bool ShowsInput(MatchChatHudMode hudMode, bool isActivated)
        {
            if (hudMode == MatchChatHudMode.Hidden)
            {
                return false;
            }

            return hudMode == MatchChatHudMode.HidingWait || isActivated;
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

            Game.Client.Common.WebTextInput.ComposingChanged += OnBrowserComposing;

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

            Game.Client.Common.WebTextInput.ComposingChanged -= OnBrowserComposing;
            composingText = string.Empty;

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

            if (pendingSubmit != null)
            {
                StopCoroutine(pendingSubmit);
                pendingSubmit = null;
            }

            SetActivated(false);
        }

        private void Update()
        {
            PollComposition();
            ConsumedEscapeThisFrame = false;
            if (activated && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                ConsumedEscapeThisFrame = true;
                Deactivate();
                return;
            }

            if (!allowsActivation || activated || !WasEnterPressedThisFrame())
            {
                return;
            }

            if (!ShouldOpenOnEnter(
                    activated,
                    focusRoutine != null,
                    true,
                    Time.unscaledTime,
                    lastDeactivateUnscaledTime,
                    allowsActivation))
            {
                return;
            }

            // Open on the next frame so the key that opens chat cannot submit it.
            focusRoutine = StartCoroutine(FocusInputNextFrame());
        }

        private void LateUpdate()
        {
            PollComposition();
            if (!activated)
            {
                return;
            }

            if (pendingSubmit != null)
            {
                TrySendPendingImeDraft();
                return;
            }

            if (!WasEnterPressedThisFrame())
            {
                return;
            }

            HandleSubmit(ReadDraft());
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
            EnsureRowCount(list.Count);
            var font = ResolveFont();
            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                if (row == null)
                {
                    continue;
                }

                if (index >= list.Count)
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

                var message = list[index];
                ApplyLine(nameTexts[index], presentation == null ? message.SenderName : presentation.Name(message.SenderId, message.SenderName), font, NameFontSize, NameColor);
                ApplyLine(bodyTexts[index], message.Text, font, BodyFontSize, Color.white);
                bodyTexts[index]?.ForceMeshUpdate();
            }

            ApplyListMetrics(appliedListScale, scrollToLatest: true);
        }

        /// <summary>
        /// Measures wrapped lines and sizes each row to that height inside a
        /// fixed panel. Overflow is scrolled; a new message jumps to the bottom.
        /// </summary>
        public void ApplyListMetrics(float scale, bool scrollToLatest = false)
        {
            EnsureLayout();
            var safe = Mathf.Max(0.01f, scale);
            appliedListScale = safe;
            if (itemRoot == null)
            {
                return;
            }

            ConfigureItemList(safe);
            var width = TextColumnWidth;
            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                if (row == null)
                {
                    continue;
                }

                var rowLayout = row.GetComponent<VerticalLayoutGroup>();
                if (rowLayout != null)
                {
                    rowLayout.spacing = NameBodySpacing * safe;
                }

                if (!row.gameObject.activeSelf)
                {
                    ApplyRowHeight(row, 0f);
                    continue;
                }

                var nameHeight = ApplyMeasuredHeight(nameTexts[index], width);
                var bodyHeight = ApplyMeasuredHeight(bodyTexts[index], width);
                ApplyRowHeight(row, RowHeight(nameHeight, bodyHeight, safe));
            }

            if (itemRoot is RectTransform itemsRect)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(itemsRect);
            }

            if (scrollToLatest)
            {
                ScrollToLatest();
            }

            ApplyRowFade();
        }

        public void ScrollToLatest()
        {
            if (scrollRect == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 0f;
        }

        private void ConfigureItemList(float scale)
        {
            var layout = itemRoot.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
            {
                return;
            }

            layout.spacing = ItemSpacing * scale;
            layout.padding = new RectOffset(0, 0, Mathf.RoundToInt(ContentPadding), Mathf.RoundToInt(ContentPadding));
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        private static float ApplyMeasuredHeight(TMP_Text text, float width)
        {
            if (text == null)
            {
                return 0f;
            }

            ApplyWrap(text);
            text.ForceMeshUpdate();
            var height = MeasuredLineHeight(text, width);
            var layout = text.GetComponent<LayoutElement>();
            if (layout != null)
            {
                layout.minHeight = height;
                layout.preferredHeight = height;
                layout.flexibleHeight = -1f;
            }

            return height;
        }

        private static void ApplyRowHeight(RectTransform row, float height)
        {
            var layout = row.GetComponent<LayoutElement>();
            if (layout == null)
            {
                return;
            }

            layout.minHeight = height;
            layout.preferredHeight = height;
            layout.flexibleHeight = -1f;
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
            if (!allowsActivation)
            {
                inputField?.DeactivateInputField();
                return;
            }

            if (!activated && mode != MatchChatHudMode.Hidden)
            {
                SetActivated(true);
            }
        }

        private void HandleSendClicked()
        {
            HandleSubmit(ReadDraft());
        }

        private void HandleSubmit(string text)
        {
            var draft = ResolveSubmitText(
                text,
                inputField != null ? inputField.text : string.Empty,
                ReadComposing(),
                inputField != null && inputField.textComponent != null
                    ? inputField.textComponent.text
                    : string.Empty);
            if (string.IsNullOrWhiteSpace(draft))
            {
                if (pendingSubmit == null && isActiveAndEnabled)
                {
                    pendingSubmit = StartCoroutine(SubmitAfterImeCommit());
                }

                return;
            }

            SendDraft(draft);
        }

        private IEnumerator SubmitAfterImeCommit()
        {
            // 한글 IME는 엔터를 뗄 때 조합을 커밋하는 경우가 많다.
            // onSubmit은 키를 누르는 순간에 빈 칸으로 와서, 한 프레임만 기다리면 글자가
            // 아직 필드에 없고 채팅이 닫힌다.
            var keyboard = Keyboard.current;
            while (keyboard != null && EnterKeyIsHeld(keyboard))
            {
                if (TrySendPendingImeDraft())
                {
                    yield break;
                }

                yield return null;
                keyboard = Keyboard.current;
            }

            yield return null;
            pendingSubmit = null;
            if (!TrySendPendingImeDraft())
            {
                SetActivated(false);
                ApplyClearedInput(keepFocus: false);
            }
        }

        private bool TrySendPendingImeDraft()
        {
            PollComposition();
            var draft = ReadDraft();
            if (string.IsNullOrWhiteSpace(draft))
            {
                return false;
            }

            pendingSubmit = null;
            SendDraft(draft);
            return true;
        }

        private static bool EnterKeyIsHeld(Keyboard keyboard) =>
            keyboard.enterKey.isPressed || keyboard.numpadEnterKey.isPressed;

        private void SendDraft(string draft)
        {
            draft = LobbyChatMessage.NormalizeText(draft);
            if (string.IsNullOrEmpty(draft))
            {
                return;
            }

            if (Time.unscaledTime - lastSendUnscaledTime < 0.08f)
            {
                return;
            }

            lastSendUnscaledTime = Time.unscaledTime;
            composingText = string.Empty;
            if (pendingSubmit != null)
            {
                StopCoroutine(pendingSubmit);
                pendingSubmit = null;
            }

            SendRequested?.Invoke(draft);
            Deactivate();
        }

        private void PollComposition()
        {
            if (inputField == null || !activated)
            {
                return;
            }

            composingText = NextComposing(
                Input.compositionString ?? string.Empty,
                inputField.text ?? string.Empty,
                composingText);
        }

        private string ReadDraft() =>
            VisibleDraft(
                inputField != null ? inputField.text : string.Empty,
                inputField != null && inputField.textComponent != null
                    ? inputField.textComponent.text
                    : string.Empty,
                ReadComposing());

        private string ReadComposing()
        {
            if (!string.IsNullOrEmpty(composingText))
            {
                return composingText;
            }

            return inputField != null && inputField.isFocused
                ? Input.compositionString ?? string.Empty
                : string.Empty;
        }

        private void OnBrowserComposing(TMP_InputField field, string composing)
        {
            if (field == inputField)
            {
                composingText = composing ?? string.Empty;
            }
        }

        private void SetActivated(bool value)
        {
            activated = value;
            BlocksPlayerInput = value;
            RefreshSendIcon();
            if (!value)
            {
                lastDeactivateUnscaledTime = Time.unscaledTime;
                composingText = string.Empty;
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
                Keyboard.current?.SetIMEEnabled(true);
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
                height += MinHistoryHeight;
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
            EnsureScroll();
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

            Game.Client.Common.HudScreenScale.Ensure(gameObject);

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

                    placeholder.text = PlaceholderText;
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
            scrollRect = historyRect != null
                ? historyRect.GetComponent<ScrollRect>()
                : null;
        }

        private void EnsureScroll()
        {
            if (historyRect == null || itemRoot == null)
            {
                return;
            }

            var scroll = historyRect.GetComponent<ScrollRect>() ??
                         historyRect.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true;
            scroll.scrollSensitivity = 24f;
            scroll.viewport = historyRect;
            ConfigureScrollContent();
            scroll.content = itemRoot as RectTransform;
            scrollRect = scroll;
        }

        private void ConfigureScrollContent()
        {
            if (itemRoot is not RectTransform items)
            {
                return;
            }

            items.anchorMin = new Vector2(0f, 0f);
            items.anchorMax = new Vector2(1f, 0f);
            items.pivot = new Vector2(0.5f, 0f);
            items.offsetMin = new Vector2(ContentPadding, 0f);
            items.offsetMax = new Vector2(-ContentPadding, 0f);
            items.anchoredPosition = Vector2.zero;
            var fitter = items.GetComponent<ContentSizeFitter>() ??
                         items.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private void BindRows()
        {
            rows.Clear();
            nameTexts.Clear();
            bodyTexts.Clear();
            rowFades.Clear();
            if (itemRoot == null)
            {
                return;
            }

            ConfigureItemList(appliedListScale);
            for (var index = 0; ; index++)
            {
                var row = itemRoot.Find($"Row{index}") as RectTransform;
                if (row == null)
                {
                    break;
                }

                BindRow(row);
            }
        }

        private void EnsureRowCount(int count)
        {
            if (itemRoot == null)
            {
                return;
            }

            while (rows.Count < count)
            {
                var index = rows.Count;
                var row = itemRoot.Find($"Row{index}") as RectTransform;
                if (row == null)
                {
                    BuildRow(itemRoot, index);
                    row = itemRoot.Find($"Row{index}") as RectTransform;
                }

                if (row == null)
                {
                    break;
                }

                BindRow(row);
            }
        }

        private void BindRow(RectTransform row)
        {
            nameTexts.Add(row.Find("Name")?.GetComponent<TMP_Text>());
            bodyTexts.Add(row.Find("Body")?.GetComponent<TMP_Text>());
            ApplyWrap(nameTexts[nameTexts.Count - 1]);
            ApplyWrap(bodyTexts[bodyTexts.Count - 1]);
            if (row.GetComponent<LayoutElement>() == null)
            {
                row.gameObject.AddComponent<LayoutElement>();
            }

            var group = row.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = row.gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
            }

            rowFades.Add(group);
            rows.Add(row);
        }

        private void FitPanels()
        {
            var root = transform as RectTransform;
            if (root != null)
            {
                root.sizeDelta = new Vector2(InputWidth, MinHistoryHeight + PanelGap + InputHeight);
            }

            var history = transform.Find("HistoryPanel") as RectTransform;
            if (history != null)
            {
                Place(
                    history,
                    new Vector2(0f, 1f),
                    new Vector2(0f, 0f),
                    new Vector2(InputWidth, MinHistoryHeight),
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

            for (var index = 0; index < rows.Count; index++)
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

        private static bool ApplyWrap(TMP_Text text)
        {
            if (text == null)
            {
                return false;
            }

            var changed = false;
            if (text.textWrappingMode != TextWrappingModes.Normal)
            {
                text.textWrappingMode = TextWrappingModes.Normal;
                changed = true;
            }

            if (text.overflowMode != TextOverflowModes.Overflow)
            {
                text.overflowMode = TextOverflowModes.Overflow;
                changed = true;
            }

            if (text.enableAutoSizing)
            {
                text.enableAutoSizing = false;
                changed = true;
            }

            var layout = text.GetComponent<LayoutElement>();
            if (layout == null)
            {
                return changed;
            }

            var minHeight = text.fontSize + 4f;
            if (!Mathf.Approximately(layout.minHeight, minHeight))
            {
                layout.minHeight = minHeight;
                changed = true;
            }

            if (!Mathf.Approximately(layout.preferredHeight, -1f))
            {
                layout.preferredHeight = -1f;
                changed = true;
            }

            if (!Mathf.Approximately(layout.preferredWidth, -1f))
            {
                layout.preferredWidth = -1f;
                changed = true;
            }

            if (!Mathf.Approximately(layout.flexibleWidth, 1f))
            {
                layout.flexibleWidth = 1f;
                changed = true;
            }

            if (!Mathf.Approximately(layout.flexibleHeight, -1f))
            {
                layout.flexibleHeight = -1f;
                changed = true;
            }

            return changed;
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
            image.raycastTarget = true;
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
                var height = Mathf.RoundToInt(MinHistoryHeight);
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
            root.sizeDelta = new Vector2(InputWidth, MinHistoryHeight + PanelGap + InputHeight);

            var history = CreatePanel(root, "HistoryPanel");
            Place(
                history,
                new Vector2(0f, 1f),
                new Vector2(0f, 0f),
                new Vector2(InputWidth, MinHistoryHeight),
                new Vector2(0f, 1f));

            var items = new GameObject(
                "Items",
                typeof(RectTransform),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            items.transform.SetParent(history, false);
            itemRoot = items.transform;
            ConfigureItemList(1f);
            ConfigureScrollContent();

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
            layout.spacing = NameBodySpacing;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = row.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            row.AddComponent<LayoutElement>();

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
