using System;
using System.Collections.Generic;
using Game.Client.Home;
using Game.Core.Lobby;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Match
{
    public interface IMatchChatBubbleView
    {
        void BindPlayer(string playerId, Transform playerRoot);
        void Show(LobbyChatMessage message);
        void Clear();
    }

    /// <summary>Shows the latest match chat message above each player.</summary>
    public sealed class MatchChatBubbleView : MonoBehaviour, IMatchChatBubbleView
    {
        private const float HeightOffset = 2f;
        private const float VisibleSeconds = 3.5f;
        private const float CanvasScale = 0.01f;
        private const float MinBubbleWidth = 80f;
        private const float MaxBubbleWidth = 420f;
        private const float MinBubbleHeight = 48f;
        private const float MaxBubbleHeight = 160f;
        private const float HorizontalPadding = 36f;
        private const float VerticalPadding = 20f;

        private readonly Dictionary<string, Bubble> bubbles = new(StringComparer.Ordinal);
        private readonly Dictionary<string, LobbyChatMessage> pending =
            new(StringComparer.Ordinal);
        private TMP_FontAsset font;

        public static MatchChatBubbleView Create(Transform parent)
        {
            var root = new GameObject("Match Chat Bubbles");
            root.transform.SetParent(parent, false);
            return root.AddComponent<MatchChatBubbleView>();
        }

        public void BindPlayer(string playerId, Transform playerRoot)
        {
            if (string.IsNullOrWhiteSpace(playerId) || playerRoot == null)
            {
                return;
            }

            var id = playerId.Trim();
            if (!bubbles.TryGetValue(id, out var bubble) || bubble == null || bubble.IsDestroyed)
            {
                bubble = CreateBubble(playerRoot);
                bubbles[id] = bubble;
            }
            else
            {
                bubble.SetPlayerRoot(playerRoot);
            }

            if (pending.TryGetValue(id, out var message))
            {
                pending.Remove(id);
                bubble.Show(message.Text);
            }
        }

        public void Show(LobbyChatMessage message)
        {
            if (!bubbles.TryGetValue(message.SenderId, out var bubble) ||
                bubble == null ||
                bubble.IsDestroyed)
            {
                pending[message.SenderId] = message;
                return;
            }

            bubble.Show(message.Text);
        }

        public void Clear()
        {
            pending.Clear();
            foreach (var bubble in bubbles.Values)
            {
                bubble?.Hide();
            }
        }

        private void Awake()
        {
            font = HomeUiFonts.Apply();
        }

        private void LateUpdate()
        {
            foreach (var bubble in bubbles.Values)
            {
                bubble?.Tick(font);
            }
        }

        private Bubble CreateBubble(Transform playerRoot)
        {
            var canvasObject = new GameObject(
                "Match Chat Bubble",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            canvasObject.transform.SetParent(playerRoot, false);

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 120;

            var canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(MinBubbleWidth, MinBubbleHeight);
            canvasRect.localScale = Vector3.one * CanvasScale;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            scaler.dynamicPixelsPerUnit = 100f;

            var panelObject = new GameObject(
                "Panel",
                typeof(RectTransform),
                typeof(Image));
            panelObject.transform.SetParent(canvasRect, false);
            var panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            var panel = panelObject.GetComponent<Image>();
            panel.sprite = HomeUiFonts.RoundedSprite;
            panel.color = new Color(0.06f, 0.07f, 0.09f, 0.92f);
            panel.raycastTarget = false;

            var textObject = new GameObject(
                "Text",
                typeof(RectTransform),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(panelRect, false);
            var textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(18f, 10f);
            textRect.offsetMax = new Vector2(-18f, -10f);
            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = 20f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;

            var bubble = new Bubble(canvasRect, text, playerRoot);
            bubble.Hide();
            return bubble;
        }

        private sealed class Bubble
        {
            private readonly RectTransform canvas;
            private readonly TMP_Text text;
            private Transform playerRoot;
            private float hideAt = -1f;

            public bool IsDestroyed => canvas == null;

            public Bubble(RectTransform canvas, TMP_Text text, Transform playerRoot)
            {
                this.canvas = canvas;
                this.text = text;
                this.playerRoot = playerRoot;
            }

            public void SetPlayerRoot(Transform value) => playerRoot = value;

            public void Show(string value)
            {
                var message = value ?? string.Empty;
                text.text = message;
                var preferred = text.GetPreferredValues(
                    message,
                    MaxBubbleWidth - HorizontalPadding,
                    MaxBubbleHeight - VerticalPadding);
                canvas.sizeDelta = new Vector2(
                    Mathf.Clamp(preferred.x + HorizontalPadding, MinBubbleWidth, MaxBubbleWidth),
                    Mathf.Clamp(preferred.y + VerticalPadding, MinBubbleHeight, MaxBubbleHeight));
                canvas.gameObject.SetActive(true);
                hideAt = Time.unscaledTime + VisibleSeconds;
            }

            public void Hide()
            {
                hideAt = -1f;
                if (canvas != null)
                {
                    canvas.gameObject.SetActive(false);
                }
            }

            public void Tick(TMP_FontAsset currentFont)
            {
                if (canvas == null || playerRoot == null)
                {
                    return;
                }

                if (currentFont != null && text.font != currentFont)
                {
                    text.font = currentFont;
                }

                var follow = playerRoot.Find("Visual") ?? playerRoot;
                canvas.position = follow.position + Vector3.up * HeightOffset;
                var camera = Camera.main;
                if (camera != null)
                {
                    canvas.rotation = camera.transform.rotation;
                    canvas.GetComponent<Canvas>().worldCamera = camera;
                }

                if (hideAt >= 0f && Time.unscaledTime >= hideAt)
                {
                    Hide();
                }
            }
        }
    }
}
