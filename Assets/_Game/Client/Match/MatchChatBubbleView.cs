using System;
using System.Collections.Generic;
using Game.Client.Home;
using Game.Client.Players;
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

    /// <summary>Shows the latest match chat message just above each player's nickname.</summary>
    public sealed class MatchChatBubbleView : MonoBehaviour, IMatchChatBubbleView
    {
        public const float FontSize = 8f;
        public static readonly Color TextColor = Color.white;
        public static readonly Color BubbleColor = new(0f, 0f, 0f, 0.8f);
        public const float MaxBubbleWidth = 210f;
        public const float MaxBubbleHeight = 80f;
        public const int BubbleRadius = 6;
        public const int BubbleSupersample = 8;
        internal const float NameplateClearance = 0.04f;
        private const float FallbackHeightOffset = 2f;
        private const float VisibleSeconds = 3.5f;
        private const float CanvasScale = 0.01f;
        private const float HorizontalPadding = 8f;
        private const float VerticalPadding = 5f;

        private readonly Dictionary<string, Bubble> bubbles = new(StringComparer.Ordinal);
        private readonly Dictionary<string, LobbyChatMessage> pending =
            new(StringComparer.Ordinal);
        private TMP_FontAsset font;
        private Camera followCamera;
        private Camera pinnedCamera;

        public static MatchChatBubbleView Create(Transform parent)
        {
            var root = new GameObject("Match Chat Bubbles");
            root.transform.SetParent(parent, false);
            var view = root.AddComponent<MatchChatBubbleView>();
            view.EnsureFont();
            return view;
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
            EnsureFont();
        }

        private void EnsureFont()
        {
            font ??= HomeUiFonts.ApplyRegular();
        }

        private void LateUpdate() => RefreshPlacement();

        /// <summary>
        /// 보는 카메라가 정해져 있는 화면에서 그 카메라를 못 박는다 (S15P21D205-1087).
        /// </summary>
        /// <remarks>
        /// 말풍선은 평소 <see cref="Camera.main"/> 을 보고 도는데, 결과 무대는 플레이어 카메라가
        /// 아니라 고정 카메라가 찍는다. 그대로 두면 말풍선이 보는 사람 쪽이 아니라 각자 자기
        /// 시선 쪽을 향해 옆모습이나 뒷면으로 보인다. null 을 넣으면 다시 Camera.main 을 따른다.
        /// </remarks>
        public void PinCamera(Camera camera) => pinnedCamera = camera;

        internal void RefreshPlacement()
        {
            if (pinnedCamera != null && pinnedCamera.isActiveAndEnabled)
            {
                followCamera = pinnedCamera;
            }
            else if (followCamera == null || !followCamera.isActiveAndEnabled)
            {
                followCamera = Camera.main;
            }

            foreach (var bubble in bubbles.Values)
            {
                bubble?.Tick(font, followCamera);
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
            canvasRect.pivot = new Vector2(0.5f, 0.5f);
            canvasRect.sizeDelta = new Vector2(HorizontalPadding * 2f, VerticalPadding * 2f);
            canvasRect.localScale = Vector3.one * CanvasScale;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            scaler.dynamicPixelsPerUnit = 200f;

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
            panel.sprite = HomeUiFonts.Rounded(BubbleRadius, BubbleSupersample);
            panel.type = Image.Type.Sliced;
            panel.pixelsPerUnitMultiplier = 1f;
            panel.color = BubbleColor;
            panel.raycastTarget = false;

            var textObject = new GameObject(
                "Text",
                typeof(RectTransform),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(panelRect, false);
            var textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(HorizontalPadding, VerticalPadding);
            textRect.offsetMax = new Vector2(-HorizontalPadding, -VerticalPadding);
            var text = textObject.GetComponent<TextMeshProUGUI>();
            EnsureFont();
            text.font = font;
            text.fontSize = FontSize;
            text.color = TextColor;
            text.faceColor = TextColor;
            text.enableVertexGradient = false;
            text.outlineWidth = 0f;
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
            private readonly Canvas canvasComponent;
            private readonly TMP_Text text;
            private Transform playerRoot;
            private Transform follow;
            private PlayerNameplateView nameplate;
            private float hideAt = -1f;

            public bool IsDestroyed => canvas == null;

            public Bubble(RectTransform canvas, TMP_Text text, Transform playerRoot)
            {
                this.canvas = canvas;
                canvasComponent = canvas.GetComponent<Canvas>();
                this.text = text;
                SetPlayerRoot(playerRoot);
            }

            public void SetPlayerRoot(Transform value)
            {
                playerRoot = value;
                follow = value == null ? null : value.Find("Visual") ?? value;
                nameplate = value == null ? null : value.GetComponentInChildren<PlayerNameplateView>();
            }

            public void Show(string value)
            {
                var message = value ?? string.Empty;
                text.text = message;
                var preferred = text.GetPreferredValues(
                    message,
                    MaxBubbleWidth - (HorizontalPadding * 2f),
                    MaxBubbleHeight - (VerticalPadding * 2f));
                canvas.sizeDelta = new Vector2(
                    Mathf.Min(preferred.x + (HorizontalPadding * 2f), MaxBubbleWidth),
                    Mathf.Min(preferred.y + (VerticalPadding * 2f), MaxBubbleHeight));
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

            public void Tick(TMP_FontAsset currentFont, Camera camera)
            {
                if (canvas == null || follow == null)
                {
                    return;
                }

                if (currentFont != null && text.font != currentFont)
                {
                    text.font = currentFont;
                }

                canvas.position = ResolveWorldPosition();
                if (camera != null)
                {
                    canvas.rotation = camera.transform.rotation;
                    if (canvasComponent != null)
                    {
                        canvasComponent.worldCamera = camera;
                    }
                }

                if (hideAt >= 0f && Time.unscaledTime >= hideAt)
                {
                    Hide();
                }
            }

            private Vector3 ResolveWorldPosition()
            {
                if (nameplate == null && playerRoot != null)
                {
                    nameplate = playerRoot.GetComponentInChildren<PlayerNameplateView>();
                }

                var halfHeight = canvas.rect.height * 0.5f * Mathf.Abs(canvas.lossyScale.y);
                if (nameplate != null)
                {
                    return nameplate.PositionAbove(NameplateClearance, halfHeight);
                }

                return follow.position + Vector3.up * (FallbackHeightOffset + halfHeight);
            }
        }
    }
}
