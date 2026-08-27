using System;
using System.Collections;
using System.Collections.Generic;
using Game.Core.Lobby;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.Client.Lobby
{
    public interface ILobbyChatView
    {
        event Action<string> SendRequested;

        void SetMessages(IReadOnlyList<LobbyChatMessage> messages);
        void ClearInput();
    }

    public sealed class LobbyChatView : MonoBehaviour, ILobbyChatView
    {
        private static readonly Color NameColor = new Color(1f, 0.84f, 0.35f, 1f);

        [SerializeField]
        private RectTransform messageRoot;

        [SerializeField]
        private InputField inputField;

        [SerializeField]
        private Button sendButton;

        [SerializeField]
        private ScrollRect scrollRect;

        [SerializeField]
        private Font uiFont;

        private readonly List<GameObject> rowObjects = new();
        private readonly List<string> rowKeys = new();
        private Coroutine scrollRoutine;
        private Coroutine clearRoutine;
        private Coroutine focusRoutine;
        private float lastSendUnscaledTime = -1f;
        private Font cachedFont;

        public event Action<string> SendRequested;

        private void OnEnable()
        {
            if (sendButton != null)
            {
                sendButton.onClick.AddListener(HandleSendClicked);
            }

            if (inputField != null)
            {
                inputField.lineType = InputField.LineType.SingleLine;
                inputField.characterLimit = LobbyChatMessage.MaxTextLength;
                // Enter is handled only via onSubmit so we don't fight InputField's KeyPressed.
                inputField.onSubmit.AddListener(HandleSubmit);
            }
        }

        private void OnDisable()
        {
            if (sendButton != null)
            {
                sendButton.onClick.RemoveListener(HandleSendClicked);
            }

            if (inputField != null)
            {
                inputField.onSubmit.RemoveListener(HandleSubmit);
            }

            if (scrollRoutine != null)
            {
                StopCoroutine(scrollRoutine);
                scrollRoutine = null;
            }

            if (clearRoutine != null)
            {
                StopCoroutine(clearRoutine);
                clearRoutine = null;
            }

            if (focusRoutine != null)
            {
                StopCoroutine(focusRoutine);
                focusRoutine = null;
            }
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (inputField == null || inputField.isFocused || focusRoutine != null ||
                keyboard == null ||
                (!keyboard.enterKey.wasPressedThisFrame &&
                 !keyboard.numpadEnterKey.wasPressedThisFrame))
            {
                return;
            }

            // Focus on the next frame so the Enter that opened chat cannot also
            // submit it through InputField.onSubmit in the same frame.
            focusRoutine = StartCoroutine(FocusInputNextFrame());
        }

        private IEnumerator FocusInputNextFrame()
        {
            yield return null;
            inputField.Select();
            inputField.ActivateInputField();
            focusRoutine = null;
        }

        public void SetMessages(IReadOnlyList<LobbyChatMessage> messages)
        {
            EnsureRefs();
            if (messageRoot == null)
            {
                return;
            }

            var list = messages ?? Array.Empty<LobbyChatMessage>();
            if (TryAppendOnly(list))
            {
                QueueScrollToBottom();
                return;
            }

            RebuildAll(list);
            QueueScrollToBottom();
        }

        public void ClearInput()
        {
            if (inputField == null)
            {
                return;
            }

            // InputField finishes Return handling later this frame; clearing immediately
            // corrupts caret/IME state (Substring exception, leftover char, focus loss).
            if (!isActiveAndEnabled)
            {
                ApplyClearedInput();
                return;
            }

            if (clearRoutine != null)
            {
                StopCoroutine(clearRoutine);
            }

            clearRoutine = StartCoroutine(ClearInputAfterInputFieldSettles());
        }

        private IEnumerator ClearInputAfterInputFieldSettles()
        {
            yield return null;
            ApplyClearedInput();
            clearRoutine = null;
        }

        private void ApplyClearedInput()
        {
            if (inputField == null)
            {
                return;
            }

            inputField.text = string.Empty;
            inputField.caretPosition = 0;
            inputField.selectionAnchorPosition = 0;
            inputField.selectionFocusPosition = 0;
            inputField.ForceLabelUpdate();
            inputField.DeactivateInputField();
        }

        private void HandleSubmit(string submitted)
        {
            RequestSend(submitted);
        }

        private void HandleSendClicked()
        {
            if (inputField == null)
            {
                return;
            }

            RequestSend(inputField.text);
        }

        private void RequestSend(string text)
        {
            if (Time.unscaledTime - lastSendUnscaledTime < 0.08f)
            {
                return;
            }

            lastSendUnscaledTime = Time.unscaledTime;
            SendRequested?.Invoke(text ?? string.Empty);
        }

        private bool TryAppendOnly(IReadOnlyList<LobbyChatMessage> list)
        {
            if (list.Count <= rowKeys.Count)
            {
                return false;
            }

            for (var i = 0; i < rowKeys.Count; i++)
            {
                if (!string.Equals(rowKeys[i], MakeKey(list[i]), StringComparison.Ordinal))
                {
                    return false;
                }
            }

            for (var i = rowKeys.Count; i < list.Count; i++)
            {
                CreateMessageRow(list[i]);
            }

            return true;
        }

        private void RebuildAll(IReadOnlyList<LobbyChatMessage> list)
        {
            ClearRows();
            for (var i = 0; i < list.Count; i++)
            {
                CreateMessageRow(list[i]);
            }
        }

        private void ClearRows()
        {
            for (var i = 0; i < rowObjects.Count; i++)
            {
                if (rowObjects[i] != null)
                {
                    Destroy(rowObjects[i]);
                }
            }

            rowObjects.Clear();
            rowKeys.Clear();
        }

        private void CreateMessageRow(LobbyChatMessage message)
        {
            var rowGo = new GameObject(
                $"ChatRow_{rowObjects.Count}",
                typeof(RectTransform),
                typeof(HorizontalLayoutGroup),
                typeof(ContentSizeFitter));
            rowGo.transform.SetParent(messageRoot, false);

            var row = rowGo.GetComponent<RectTransform>();
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);

            var layout = rowGo.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.spacing = 0f;
            layout.padding = new RectOffset(0, 0, 0, 4);

            var fitter = rowGo.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var sender = string.IsNullOrWhiteSpace(message.SenderName)
                ? "?"
                : message.SenderName.Trim();

            var nameText = CreateRowText(
                row,
                "Name",
                $"[{sender}] : ",
                NameColor,
                FontStyle.Normal,
                16);
            nameText.horizontalOverflow = HorizontalWrapMode.Overflow;
            nameText.verticalOverflow = VerticalWrapMode.Truncate;
            var nameLe = nameText.gameObject.AddComponent<LayoutElement>();
            nameLe.minWidth = nameText.preferredWidth;
            nameLe.preferredWidth = nameText.preferredWidth;
            nameLe.flexibleWidth = 0f;

            var bodyText = CreateRowText(
                row,
                "Body",
                message.Text ?? string.Empty,
                Color.white,
                FontStyle.Normal,
                16);
            bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bodyText.verticalOverflow = VerticalWrapMode.Overflow;
            var bodyLe = bodyText.gameObject.AddComponent<LayoutElement>();
            bodyLe.flexibleWidth = 1f;
            bodyLe.minWidth = 48f;

            rowObjects.Add(rowGo);
            rowKeys.Add(MakeKey(message));
        }

        private Text CreateRowText(
            Transform parent,
            string name,
            string value,
            Color color,
            FontStyle style,
            int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.text = value;
            text.font = ResolveFont();
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.UpperLeft;
            text.raycastTarget = false;
            text.supportRichText = false;
            return text;
        }

        private static string MakeKey(LobbyChatMessage message) =>
            message.SenderId + "\n" + message.SenderName + "\n" + message.Text;

        private void SyncContentWidth()
        {
            if (messageRoot == null || scrollRect == null || scrollRect.viewport == null)
            {
                return;
            }

            messageRoot.anchorMin = new Vector2(0f, 1f);
            messageRoot.anchorMax = new Vector2(1f, 1f);
            messageRoot.pivot = new Vector2(0.5f, 1f);
            messageRoot.anchoredPosition = Vector2.zero;
            messageRoot.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                Mathf.Max(1f, scrollRect.viewport.rect.width - 8f));
        }

        private void QueueScrollToBottom()
        {
            if (!isActiveAndEnabled)
            {
                SyncContentWidth();
                ScrollToBottom();
                return;
            }

            if (scrollRoutine != null)
            {
                StopCoroutine(scrollRoutine);
            }

            scrollRoutine = StartCoroutine(ScrollToBottomNextFrame());
        }

        private IEnumerator ScrollToBottomNextFrame()
        {
            SyncContentWidth();
            // One layout pass after the new row is in the hierarchy.
            yield return null;
            ScrollToBottom();
            scrollRoutine = null;
        }

        private void ScrollToBottom()
        {
            if (scrollRect == null)
            {
                return;
            }

            scrollRect.verticalNormalizedPosition = 0f;
        }

        private void EnsureRefs()
        {
            if (messageRoot == null)
            {
                messageRoot = transform.Find("HistoryViewport/HistoryContent") as RectTransform
                    ?? transform.Find("HistoryContent") as RectTransform;
            }

            if (scrollRect == null)
            {
                scrollRect = transform.Find("HistoryViewport")?.GetComponent<ScrollRect>();
            }

            if (inputField != null)
            {
                inputField.characterLimit = LobbyChatMessage.MaxTextLength;
            }

            var leftover = transform.Find("HistoryViewport/HistoryText");
            if (leftover != null)
            {
                leftover.gameObject.SetActive(false);
            }
        }

        private Font ResolveFont()
        {
            if (cachedFont != null)
            {
                return cachedFont;
            }

            cachedFont = uiFont != null
                ? uiFont
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                  ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            return cachedFont;
        }
    }
}
