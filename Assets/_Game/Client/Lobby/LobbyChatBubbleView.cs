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

        /// <summary>
        /// The name shown under the bubble. Unlike the bubble it stays visible,
        /// so it is what tells characters apart when nobody is talking.
        /// </summary>
        public Text nameText;
    }

    /// <summary>
    /// Everything that floats above a lobby character: the nameplate, which is
    /// always up, and the speech bubble, which comes and goes.
    /// </summary>
    /// <remarks>
    /// Both share one world-space canvas per player, so they share the anchor
    /// pool and the single billboarding pass in <c>LateUpdate</c> rather than
    /// each keeping their own copy of it. The type name predates the nameplate.
    /// </remarks>
    public sealed class LobbyChatBubbleView : MonoBehaviour, ILobbyChatBubbleView
    {
        /// <summary>
        /// Names the generator gives the two labels, so a cloned canvas can be
        /// taken apart again. Changing either means changing LobbyHudLayoutMenu.
        /// </summary>
        private const string templateBubbleName = "Bubble";
        private const string templateNameplateName = "Nameplate";

        private const float MinBubbleWidth = 140f;
        private const float MaxBubbleWidth = 420f;
        private const float MinBubbleHeight = 56f;
        private const float MaxBubbleHeight = 220f;

        [SerializeField]
        private float visibleSeconds = 3.5f;

        [SerializeField, Min(0f)]
        private float heightOffset = 1.35f;

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
                follow.position = anchor.headAnchor.position + Vector3.up * heightOffset;
                if (camera != null)
                {
                    follow.rotation = camera.transform.rotation;
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

        public void ClearBindings()
        {
            Clear();
            for (var i = 0; i < anchors.Count; i++)
            {
                var anchor = anchors[i];
                if (anchor == null)
                {
                    continue;
                }

                anchor.playerId = string.Empty;
                anchor.headAnchor = null;
                ApplyName(anchor, null);
            }
        }

        /// <param name="displayName">
        /// What to show on the nameplate. Empty hides it rather than showing a
        /// blank plate, which is what a character whose name has not replicated
        /// yet would otherwise get.
        /// </param>
        public void BindPlayer(string playerId, Transform headAnchor, string displayName)
        {
            if (string.IsNullOrWhiteSpace(playerId) || headAnchor == null)
            {
                return;
            }

            var anchor = FindAnchor(playerId) ?? FindFreeAnchor() ?? CloneAnchor();
            if (anchor == null)
            {
                return;
            }

            anchor.playerId = playerId.Trim();
            anchor.headAnchor = headAnchor;
            ApplyName(anchor, displayName);
        }

        private static void ApplyName(LobbyChatBubbleAnchor anchor, string displayName)
        {
            if (anchor.nameText == null)
            {
                return;
            }

            var trimmed = displayName?.Trim();
            var hasName = !string.IsNullOrEmpty(trimmed);

            anchor.nameText.text = hasName ? trimmed : string.Empty;
            anchor.nameText.gameObject.SetActive(hasName);
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

        private LobbyChatBubbleAnchor FindFreeAnchor()
        {
            for (var i = 0; i < anchors.Count; i++)
            {
                var anchor = anchors[i];
                if (anchor != null && string.IsNullOrEmpty(anchor.playerId))
                {
                    return anchor;
                }
            }

            return null;
        }

        private LobbyChatBubbleAnchor CloneAnchor()
        {
            if (anchors.Count == 0 || anchors[0]?.bubbleRoot == null)
            {
                return null;
            }

            var templateCanvas = anchors[0].bubbleRoot.parent as RectTransform;
            if (templateCanvas == null)
            {
                return null;
            }

            var canvas = Instantiate(templateCanvas, transform);

            // Found by name because the clone carries two labels now, and which
            // is which cannot be told apart by type.
            var bubble = canvas.Find(templateBubbleName) as RectTransform;
            var text = bubble == null ? null : bubble.GetComponentInChildren<Text>(true);
            if (bubble == null || text == null)
            {
                Destroy(canvas.gameObject);
                return null;
            }

            var anchor = new LobbyChatBubbleAnchor
            {
                bubbleRoot = bubble,
                bubbleText = text,
                nameText = canvas.Find(templateNameplateName)?.GetComponent<Text>()
            };
            bubble.gameObject.SetActive(false);
            anchors.Add(anchor);
            return anchor;
        }
    }
}
