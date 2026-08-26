using System;
using System.Collections.Generic;
using Game.Core.Lobby;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Lobby
{
    public interface ILobbyChatBubbleView
    {
        void Show(LobbyChatMessage message);
        void Clear();
    }

    [Serializable]
    public sealed class LobbyChatBubbleAnchor
    {
        public string playerId;
        public Transform headAnchor;
        public RectTransform bubbleRoot;
        public Text bubbleText;
    }

    /// <summary>
    /// World-space speech bubbles above sample head anchors.
    /// </summary>
    public sealed class LobbyChatBubbleView : MonoBehaviour, ILobbyChatBubbleView
    {
        private const float MinBubbleWidth = 140f;
        private const float MaxBubbleWidth = 420f;
        private const float MinBubbleHeight = 56f;
        private const float MaxBubbleHeight = 220f;

        [SerializeField]
        private float visibleSeconds = 3.5f;

        [SerializeField]
        private Font uiFont;

        [SerializeField]
        private List<LobbyChatBubbleAnchor> anchors = new();

        private readonly Dictionary<string, float> hideAt = new();

        private void LateUpdate()
        {
            var camera = Camera.main;
            var now = Time.unscaledTime;
            for (var i = 0; i < anchors.Count; i++)
            {
                var anchor = anchors[i];
                if (anchor?.bubbleRoot == null || anchor.headAnchor == null)
                {
                    continue;
                }

                var canvas = anchor.bubbleRoot.parent as RectTransform;
                var follow = canvas != null ? canvas : anchor.bubbleRoot;
                follow.position = anchor.headAnchor.position + Vector3.up * 0.55f;
                if (camera != null)
                {
                    follow.rotation = Quaternion.LookRotation(
                        follow.position - camera.transform.position,
                        Vector3.up);
                }

                if (hideAt.TryGetValue(anchor.playerId, out var until) && now >= until)
                {
                    anchor.bubbleRoot.gameObject.SetActive(false);
                    hideAt.Remove(anchor.playerId);
                }
            }
        }

        public void Show(LobbyChatMessage message)
        {
            var anchor = FindAnchor(message.SenderId);
            if (anchor == null || anchor.bubbleRoot == null || anchor.bubbleText == null)
            {
                return;
            }

            if (uiFont != null)
            {
                anchor.bubbleText.font = uiFont;
            }

            anchor.bubbleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            anchor.bubbleText.verticalOverflow = VerticalWrapMode.Overflow;
            anchor.bubbleText.alignment = TextAnchor.MiddleCenter;
            anchor.bubbleText.text = message.Text ?? string.Empty;

            ResizeBubble(anchor);
            anchor.bubbleRoot.gameObject.SetActive(true);
            hideAt[message.SenderId] = Time.unscaledTime + Mathf.Max(0.5f, visibleSeconds);
        }

        public void Clear()
        {
            hideAt.Clear();
            for (var i = 0; i < anchors.Count; i++)
            {
                var anchor = anchors[i];
                if (anchor?.bubbleRoot != null)
                {
                    anchor.bubbleRoot.gameObject.SetActive(false);
                }
            }
        }

        private static void ResizeBubble(LobbyChatBubbleAnchor anchor)
        {
            var text = anchor.bubbleText;
            var bubble = anchor.bubbleRoot;
            var textRect = text.rectTransform;

            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12f, 10f);
            textRect.offsetMax = new Vector2(-12f, -10f);

            // Measure with a generous wrap width, then clamp.
            bubble.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, MaxBubbleWidth);
            Canvas.ForceUpdateCanvases();

            var width = Mathf.Clamp(text.preferredWidth + 28f, MinBubbleWidth, MaxBubbleWidth);
            bubble.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            Canvas.ForceUpdateCanvases();

            var height = Mathf.Clamp(text.preferredHeight + 24f, MinBubbleHeight, MaxBubbleHeight);
            bubble.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }

        private LobbyChatBubbleAnchor FindAnchor(string playerId)
        {
            for (var i = 0; i < anchors.Count; i++)
            {
                var anchor = anchors[i];
                if (anchor != null &&
                    string.Equals(anchor.playerId, playerId, StringComparison.Ordinal))
                {
                    return anchor;
                }
            }

            return null;
        }
    }
}
