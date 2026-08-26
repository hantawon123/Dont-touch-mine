using System;
using System.Collections.Generic;
using System.Text;
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
        private Text bodyText;

        private void Awake()
        {
            EnsureLayout();
        }

        public void SetParticipants(IReadOnlyList<LobbyParticipant> participants)
        {
            EnsureLayout();
            if (bodyText == null)
            {
                return;
            }

            if (participants == null || participants.Count == 0)
            {
                bodyText.text = "참가자가 없습니다.";
                return;
            }

            var builder = new StringBuilder(participants.Count * 24);
            for (var index = 0; index < participants.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append('\n');
                }

                var participant = participants[index];
                if (participant.IsHost)
                {
                    builder.Append("★ ");
                }

                builder.Append(participant.DisplayName);
            }

            bodyText.text = builder.ToString();
        }

        private void EnsureLayout()
        {
            if (titleText == null)
            {
                titleText = transform.Find("Title")?.GetComponent<Text>();
            }

            if (bodyText == null)
            {
                bodyText = transform.Find("BodyText")?.GetComponent<Text>();
            }

            if (titleText != null)
            {
                titleText.text = "참가자 목록";
                titleText.fontStyle = FontStyle.Bold;
                titleText.color = Color.white;
            }

            if (bodyText != null)
            {
                bodyText.alignment = TextAnchor.UpperLeft;
                bodyText.color = Color.white;
                bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
                bodyText.verticalOverflow = VerticalWrapMode.Overflow;
                bodyText.lineSpacing = 1.2f;
            }

            var leftoverLabel = transform.Find("Label");
            if (leftoverLabel != null)
            {
                leftoverLabel.gameObject.SetActive(false);
            }
        }
    }
}
