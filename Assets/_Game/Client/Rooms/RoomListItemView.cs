using System;
using Game.Core.Lobby;
using Game.Core.Rooms;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Rooms
{
    [DisallowMultipleComponent]
    public sealed class RoomListItemView : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text statusBadgeText;

        [SerializeField]
        private Image statusBadgeBackground;

        [SerializeField]
        private TMP_Text titleText;

        [SerializeField]
        private TMP_Text hostNicknameText;

        [SerializeField]
        private TMP_Text mapText;

        [SerializeField]
        private TMP_Text playerCountText;

        [SerializeField]
        private GameObject lockIcon;

        [SerializeField]
        private Button selectButton;

        [SerializeField]
        private Color waitingBadgeColor = new Color(0.35f, 0.75f, 0.45f);

        [SerializeField]
        private Color playingBadgeColor = new Color(0.92f, 0.4f, 0.53f);

        private string roomId;

        public event Action<string> Selected;

        private void Awake()
        {
            selectButton.onClick.AddListener(OnSelectButtonClicked);
        }

        private void OnDestroy()
        {
            selectButton.onClick.RemoveListener(OnSelectButtonClicked);
        }

        public void Bind(RoomSummary room)
        {
            roomId = room.RoomId;
            titleText.text = room.Settings.Title;
            hostNicknameText.text = string.IsNullOrEmpty(room.HostNickname)
                ? string.Empty
                : $"{room.HostNickname}의 방";
            mapText.text = room.Settings.MapId;
            playerCountText.text = $"{room.CurrentPlayerCount}/{room.Settings.MaxPlayers}";
            lockIcon.SetActive(room.Settings.IsLocked);

            var isWaiting = room.Status == RoomStatus.Waiting;
            statusBadgeText.text = isWaiting ? "대기중" : "게임중";
            statusBadgeBackground.color = isWaiting ? waitingBadgeColor : playingBadgeColor;
            selectButton.interactable = room.CanJoin;
        }

        private void OnSelectButtonClicked()
        {
            Selected?.Invoke(roomId);
        }
    }
}
