using System;
using System.Collections.Generic;
using System.Text;
using Game.Core.Lobby;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Lobby
{
    public sealed class KeyGuideView : MonoBehaviour, IKeyGuideView
    {
        private static readonly string[] RuntimeOverlayNames =
        {
            "VisualGuideRoot",
            "InteractCaption",
            "RotateCaption",
            "MouseCaption",
            "ScrollCaption",
            "ThrowCaption",
            "PlaceModeCaption",
            "RotateLeftKey",
            "RotateRightKey",
            "InteractKey",
            "AttackKey",
            "PlacementKey",
            "ScrollKey",
        };

        [SerializeField]
        private Button openButton;

        [SerializeField]
        private Button closeButton;

        [SerializeField]
        private GameObject panel;

        [SerializeField]
        private Text bodyText;

        private IReadOnlyList<ControlKeyBinding> currentBindings;

        public event Action OpenRequested;
        public event Action CloseRequested;

        private void OnEnable()
        {
            RestoreTextOnlyPanel();

            if (openButton != null)
            {
                openButton.onClick.AddListener(HandleOpenClicked);
            }

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(HandleCloseClicked);
            }
        }

        private void OnDisable()
        {
            if (openButton != null)
            {
                openButton.onClick.RemoveListener(HandleOpenClicked);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(HandleCloseClicked);
            }
        }

        public void SetVisible(bool visible)
        {
            if (visible && currentBindings != null)
            {
                SetEntries(currentBindings);
            }

            if (panel != null)
            {
                panel.SetActive(visible);
            }
        }

        public void SetEntries(IReadOnlyList<ControlKeyBinding> bindings)
        {
            currentBindings = bindings;
            if (bodyText == null)
            {
                return;
            }

            bodyText.gameObject.SetActive(true);

            if (bindings == null || bindings.Count == 0)
            {
                bodyText.text = string.Empty;
                return;
            }

            var builder = new StringBuilder(bindings.Count * 32);
            for (var index = 0; index < bindings.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append('\n');
                }

                builder.Append(bindings[index].Action);
                builder.Append("  :  ");
                builder.Append(bindings[index].KeyLabel);
            }

            bodyText.text = builder.ToString();
        }

        private void RestoreTextOnlyPanel()
        {
            if (panel == null)
            {
                return;
            }

            for (var index = 0; index < RuntimeOverlayNames.Length; index++)
            {
                var child = panel.transform.Find(RuntimeOverlayNames[index]);
                if (child != null)
                {
                    DestroyImmediate(child.gameObject);
                }
            }

            var leftoverLabel = panel.transform.Find("Label");
            if (leftoverLabel != null)
            {
                leftoverLabel.gameObject.SetActive(false);
            }

            var panelRect = panel.transform as RectTransform;
            if (panelRect != null)
            {
                panelRect.sizeDelta = new Vector2(560f, 420f);
            }

            var background = panel.GetComponent<Image>();
            if (background != null)
            {
                background.sprite = null;
                background.color = new Color(0.12f, 0.13f, 0.18f, 0.96f);
            }

            var title = panel.transform.Find("Title")?.GetComponent<Text>();
            if (title != null)
            {
                title.text = "조작키 목록";
                title.fontSize = 28;
                title.fontStyle = FontStyle.Bold;
                title.color = Color.white;
            }

            if (bodyText != null)
            {
                bodyText.gameObject.SetActive(true);
                bodyText.alignment = TextAnchor.UpperLeft;
                bodyText.fontSize = 20;
                bodyText.color = Color.white;
                bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
                bodyText.verticalOverflow = VerticalWrapMode.Overflow;
                bodyText.lineSpacing = 1.15f;

                var bodyRect = bodyText.rectTransform;
                bodyRect.anchorMin = Vector2.zero;
                bodyRect.anchorMax = Vector2.one;
                bodyRect.offsetMin = new Vector2(28f, 72f);
                bodyRect.offsetMax = new Vector2(-28f, -64f);
            }
        }

        public void RequestClose() => CloseRequested?.Invoke();

        private void HandleOpenClicked() => OpenRequested?.Invoke();

        private void HandleCloseClicked() => CloseRequested?.Invoke();
    }
}
