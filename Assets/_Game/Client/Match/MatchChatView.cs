using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Game.Client.Home;
using Game.Core.Lobby;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.Client.Match
{
    public interface IMatchChatView
    {
        event Action<string> SendRequested;

        void SetMessages(IReadOnlyList<LobbyChatMessage> messages);
        void ClearInput();
    }

    /// <summary>한 줄 입력과 최근 메시지만 표시하는 인게임 채팅 View.</summary>
    public sealed class MatchChatView : MonoBehaviour, IMatchChatView
    {
        private const int VisibleMessageCount = 4;
        private const float PanelWidth = 480f;
        private const float PanelHeight = 132f;
        private const float Margin = 24f;

        private TMP_InputField inputField;
        private TMP_Text historyText;
        private Coroutine focusRoutine;
        private Coroutine clearRoutine;
        private float lastSendUnscaledTime = -1f;
        private TMP_FontAsset cachedFont;

        public event Action<string> SendRequested;

        public static MatchChatView Create(Transform canvasParent)
        {
            var root = new GameObject("Match Chat", typeof(RectTransform));
            if (canvasParent != null)
            {
                root.transform.SetParent(canvasParent, false);
            }
            else
            {
                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 20;
                root.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                root.AddComponent<GraphicRaycaster>();
            }

            return root.AddComponent<MatchChatView>();
        }

        private void Awake()
        {
            BuildLayout();
        }

        private void OnEnable()
        {
            if (inputField != null)
            {
                inputField.onSubmit.AddListener(HandleSubmit);
            }
        }

        private void OnDisable()
        {
            if (inputField != null)
            {
                inputField.onSubmit.RemoveListener(HandleSubmit);
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
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (inputField == null || inputField.isFocused || focusRoutine != null || keyboard == null ||
                (!keyboard.enterKey.wasPressedThisFrame && !keyboard.numpadEnterKey.wasPressedThisFrame))
            {
                return;
            }

            // Open on the next frame so the key that opens chat cannot submit it.
            focusRoutine = StartCoroutine(FocusInputNextFrame());
        }

        public void SetMessages(IReadOnlyList<LobbyChatMessage> messages)
        {
            if (historyText == null)
            {
                return;
            }

            var list = messages ?? Array.Empty<LobbyChatMessage>();
            var first = Mathf.Max(0, list.Count - VisibleMessageCount);
            var builder = new StringBuilder();
            for (var index = first; index < list.Count; index++)
            {
                if (builder.Length > 0)
                {
                    builder.Append('\n');
                }

                var message = list[index];
                builder.Append('[').Append(message.SenderName).Append("] ").Append(message.Text);
            }

            historyText.text = builder.ToString();
        }

        public void ClearInput()
        {
            if (inputField == null)
            {
                return;
            }

            if (!isActiveAndEnabled)
            {
                ApplyClearedInput();
                return;
            }

            if (clearRoutine != null)
            {
                StopCoroutine(clearRoutine);
            }

            clearRoutine = StartCoroutine(ClearInputNextFrame());
        }

        private IEnumerator FocusInputNextFrame()
        {
            yield return null;
            if (inputField != null)
            {
                EventSystem.current?.SetSelectedGameObject(null);
                inputField.Select();
                inputField.ActivateInputField();
            }

            focusRoutine = null;
        }

        private IEnumerator ClearInputNextFrame()
        {
            yield return null;
            ApplyClearedInput();
            clearRoutine = null;
        }

        private void ApplyClearedInput()
        {
            inputField.text = string.Empty;
            inputField.caretPosition = 0;
            inputField.selectionAnchorPosition = 0;
            inputField.selectionFocusPosition = 0;
            inputField.ForceLabelUpdate();
            inputField.DeactivateInputField();
            if (EventSystem.current?.currentSelectedGameObject == inputField.gameObject)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        private void HandleSubmit(string text)
        {
            if (Time.unscaledTime - lastSendUnscaledTime < 0.08f)
            {
                return;
            }

            lastSendUnscaledTime = Time.unscaledTime;
            SendRequested?.Invoke(text ?? string.Empty);
        }

        private void BuildLayout()
        {
            var root = (RectTransform)transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.zero;
            root.pivot = Vector2.zero;
            root.anchoredPosition = new Vector2(Margin, Margin);
            root.sizeDelta = new Vector2(PanelWidth, PanelHeight);

            var panel = gameObject.AddComponent<Image>();
            panel.sprite = HomeUiFonts.WhiteSprite;
            panel.color = new Color(0f, 0f, 0f, 0.62f);
            panel.raycastTarget = true;

            historyText = CreateText(
                "History",
                root,
                new Vector2(8f, 42f),
                new Vector2(-8f, -8f),
                16,
                Color.white);
            historyText.alignment = TextAlignmentOptions.BottomLeft;
            historyText.textWrappingMode = TextWrappingModes.NoWrap;
            historyText.overflowMode = TextOverflowModes.Truncate;

            var inputRoot = new GameObject(
                "Input",
                typeof(RectTransform),
                typeof(Image));
            inputRoot.SetActive(false);
            inputRoot.transform.SetParent(root, false);
            var inputRect = (RectTransform)inputRoot.transform;
            inputRect.anchorMin = new Vector2(0f, 0f);
            inputRect.anchorMax = new Vector2(1f, 0f);
            inputRect.pivot = new Vector2(0.5f, 0f);
            inputRect.offsetMin = new Vector2(8f, 8f);
            inputRect.offsetMax = new Vector2(-8f, 40f);
            var inputBackground = inputRoot.GetComponent<Image>();
            inputBackground.sprite = HomeUiFonts.WhiteSprite;
            inputBackground.color = new Color(1f, 1f, 1f, 0.14f);
            inputBackground.raycastTarget = true;

            var textArea = new GameObject("TextViewport", typeof(RectTransform), typeof(RectMask2D));
            textArea.transform.SetParent(inputRect, false);
            var textAreaRect = (RectTransform)textArea.transform;
            textAreaRect.anchorMin = Vector2.zero;
            textAreaRect.anchorMax = Vector2.one;
            textAreaRect.offsetMin = new Vector2(8f, 0f);
            textAreaRect.offsetMax = new Vector2(-8f, 0f);

            var text = CreateText("Text", textAreaRect, Vector2.zero, Vector2.zero, 16, Color.white);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Truncate;

            var placeholder = CreateText(
                "Placeholder",
                textAreaRect,
                Vector2.zero,
                Vector2.zero,
                16,
                new Color(1f, 1f, 1f, 0.58f));
            placeholder.text = "Enter 채팅 입력";
            placeholder.fontStyle = FontStyles.Italic;
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;
            placeholder.textWrappingMode = TextWrappingModes.NoWrap;
            placeholder.overflowMode = TextOverflowModes.Truncate;

            inputField = inputRoot.AddComponent<TMP_InputField>();
            inputField.targetGraphic = inputBackground;
            inputField.textViewport = textAreaRect;
            inputField.textComponent = text;
            inputField.placeholder = placeholder;
            inputField.fontAsset = ResolveFont();
            inputField.pointSize = 16f;
            inputField.lineType = TMP_InputField.LineType.SingleLine;
            inputField.characterLimit = LobbyChatMessage.MaxTextLength;
            inputField.navigation = new Navigation { mode = Navigation.Mode.None };
            inputField.interactable = true;
            inputRoot.SetActive(true);
        }

        private TMP_Text CreateText(
            string name,
            Transform parent,
            Vector2 offsetMin,
            Vector2 offsetMax,
            int fontSize,
            Color color)
        {
            var textObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            var rect = (RectTransform)textObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = ResolveFont();
            text.fontSize = fontSize;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private TMP_FontAsset ResolveFont()
        {
            if (cachedFont != null)
            {
                return cachedFont;
            }

            cachedFont = HomeUiFonts.Apply();
            return cachedFont;
        }
    }
}
