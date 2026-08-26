using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Lobby
{
    public interface ILobbyConfirmView
    {
        event Action Confirmed;
        event Action Cancelled;

        void Show(string message);
        void Hide();
    }

    public interface IKickConfirmView : ILobbyConfirmView
    {
    }

    public interface IHostTransferConfirmView : ILobbyConfirmView
    {
    }

    public class LobbyConfirmView : MonoBehaviour, ILobbyConfirmView
    {
        [SerializeField]
        private GameObject panel;

        [SerializeField]
        private Text messageText;

        [SerializeField]
        private Button confirmButton;

        [SerializeField]
        private Button cancelButton;

        public event Action Confirmed;
        public event Action Cancelled;

        private void OnEnable()
        {
            if (confirmButton != null)
            {
                confirmButton.onClick.AddListener(HandleConfirm);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.AddListener(HandleCancel);
            }
        }

        private void OnDisable()
        {
            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveListener(HandleConfirm);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveListener(HandleCancel);
            }
        }

        public void Show(string message)
        {
            if (messageText != null)
            {
                messageText.text = message ?? string.Empty;
            }

            if (panel != null)
            {
                panel.SetActive(true);
            }
        }

        public void Hide()
        {
            if (panel != null)
            {
                panel.SetActive(false);
            }
        }

        private void HandleConfirm() => Confirmed?.Invoke();

        private void HandleCancel() => Cancelled?.Invoke();
    }

    public sealed class KickConfirmView : LobbyConfirmView, IKickConfirmView
    {
    }

    public sealed class HostTransferConfirmView : LobbyConfirmView, IHostTransferConfirmView
    {
    }
}
