using System;
using System.Collections.Generic;
using Game.Core.Lobby;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Lobby
{
    public sealed class LobbyPlayerListView : MonoBehaviour, ILobbyPlayerListView
    {
        [SerializeField]
        private Text titleText;

        [SerializeField]
        private RectTransform rowRoot;

        [SerializeField]
        private Font uiFont;

        private readonly List<GameObject> rowObjects = new();

        public event Action<string, string> KickClicked;
        public event Action<string, string> TransferClicked;

        private void Awake()
        {
            EnsureLayout();
        }

        public void SetParticipants(
            IReadOnlyList<LobbyParticipant> participants,
            bool localIsHost,
            string localPlayerId)
        {
            EnsureLayout();
            ClearRows();

            if (participants == null || participants.Count == 0)
            {
                CreateInfoRow("참가자가 없습니다.");
                return;
            }

            for (var index = 0; index < participants.Count; index++)
            {
                CreateParticipantRow(participants[index], localIsHost, localPlayerId);
            }
        }

        private void EnsureLayout()
        {
            if (titleText == null)
            {
                titleText = transform.Find("Title")?.GetComponent<Text>();
            }

            if (rowRoot == null)
            {
                var existing = transform.Find("RowRoot") as RectTransform;
                if (existing == null)
                {
                    var go = new GameObject("RowRoot", typeof(RectTransform));
                    go.transform.SetParent(transform, false);
                    existing = go.GetComponent<RectTransform>();
                    existing.anchorMin = Vector2.zero;
                    existing.anchorMax = Vector2.one;
                    existing.offsetMin = new Vector2(12f, 12f);
                    existing.offsetMax = new Vector2(-12f, -48f);
                }

                rowRoot = existing;
            }

            var bodyText = transform.Find("BodyText");
            if (bodyText != null)
            {
                bodyText.gameObject.SetActive(false);
            }

            var leftoverLabel = transform.Find("Label");
            if (leftoverLabel != null)
            {
                leftoverLabel.gameObject.SetActive(false);
            }

            if (titleText != null)
            {
                titleText.text = "참가자 목록";
                titleText.font = ResolveFont();
                titleText.fontStyle = FontStyle.Bold;
                titleText.color = Color.white;
            }
        }

        private void ClearRows()
        {
            for (var index = 0; index < rowObjects.Count; index++)
            {
                if (rowObjects[index] != null)
                {
                    Destroy(rowObjects[index]);
                }
            }

            rowObjects.Clear();
        }

        private void CreateInfoRow(string message)
        {
            var row = CreateRowRoot("InfoRow");
            var text = CreateText(row, "Label", message, TextAnchor.MiddleLeft, 18);
            Stretch(text.rectTransform, 8f, 0f, -8f, 0f);
        }

        private void CreateParticipantRow(
            LobbyParticipant participant,
            bool localIsHost,
            string localPlayerId)
        {
            var row = CreateRowRoot($"Row_{participant.Id}");
            var label = participant.IsHost
                ? $"★ {participant.DisplayName}"
                : participant.DisplayName;
            var nameText = CreateText(row, "Name", label, TextAnchor.MiddleLeft, 18);
            Stretch(nameText.rectTransform, 8f, 0f, -120f, 0f);

            var canManage = localIsHost &&
                !string.Equals(participant.Id, localPlayerId, StringComparison.Ordinal);
            if (!canManage)
            {
                return;
            }

            var kick = CreateActionButton(row, "Kick", "강퇴", new Vector2(-64f, 0f));
            var transfer = CreateActionButton(row, "Transfer", "위임", new Vector2(-8f, 0f));
            var playerId = participant.Id;
            var displayName = participant.DisplayName;
            kick.onClick.AddListener(() => KickClicked?.Invoke(playerId, displayName));
            transfer.onClick.AddListener(() => TransferClicked?.Invoke(playerId, displayName));
        }

        private RectTransform CreateRowRoot(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(rowRoot, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, 36f);
            rect.anchoredPosition = new Vector2(0f, -rowObjects.Count * 40f);
            var image = go.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.04f);
            image.raycastTarget = false;
            rowObjects.Add(go);
            return rect;
        }

        private Button CreateActionButton(
            RectTransform parent,
            string name,
            string label,
            Vector2 anchoredPosition)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(52f, 28f);
            rect.anchoredPosition = anchoredPosition;
            go.GetComponent<Image>().color = new Color(0.35f, 0.35f, 0.4f, 1f);
            var text = CreateText(rect, "Label", label, TextAnchor.MiddleCenter, 14);
            Stretch(text.rectTransform, 2f, 2f, -2f, -2f);
            return go.GetComponent<Button>();
        }

        private Text CreateText(
            Transform parent,
            string name,
            string value,
            TextAnchor alignment,
            int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.text = value;
            text.font = ResolveFont();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private Font ResolveFont()
        {
            if (uiFont != null)
            {
                return uiFont;
            }

            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private static void Stretch(
            RectTransform rect,
            float left,
            float bottom,
            float right,
            float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(right, top);
        }
    }
}
