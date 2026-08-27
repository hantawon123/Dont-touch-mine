using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Lobby
{
    public interface ILobbyPauseMenuView
    {
        event Action StartClicked;
        event Action LeaveClicked;
        event Action ResumeClicked;

        bool IsOpen { get; }

        void SetVisible(bool visible);
        void SetStartVisible(bool visible);
    }

    /// <summary>
    /// The lobby's Esc menu: the three things a player can do with the mouse
    /// while the rest of the visit keeps the cursor captured for looking around.
    /// </summary>
    /// <remarks>
    /// Lives on the HUD canvas rather than on the panel it shows, the way
    /// <see cref="KeyGuideView"/> does. A view that sits on its own hidden panel
    /// only wires its buttons the first time the panel is switched on, which is
    /// one more thing to get wrong for no gain.
    /// </remarks>
    public sealed class LobbyPauseMenuView : MonoBehaviour, ILobbyPauseMenuView
    {
        [SerializeField]
        private GameObject panel;

        [SerializeField]
        private Button startButton;

        [SerializeField]
        private Button leaveButton;

        [SerializeField]
        private Button resumeButton;

        public event Action StartClicked;
        public event Action LeaveClicked;
        public event Action ResumeClicked;

        public bool IsOpen => panel != null && panel.activeSelf;

        private void OnEnable()
        {
            if (panel == null || startButton == null ||
                leaveButton == null || resumeButton == null)
            {
                Debug.LogError(
                    "PauseMenuPanel is not wired. Run " +
                    "Game > Lobby > Build HUD Layout on the Lobby scene.",
                    this);
                return;
            }

            startButton.onClick.AddListener(HandleStartClicked);
            leaveButton.onClick.AddListener(HandleLeaveClicked);
            resumeButton.onClick.AddListener(HandleResumeClicked);
        }

        private void OnDisable()
        {
            if (startButton != null)
            {
                startButton.onClick.RemoveListener(HandleStartClicked);
            }

            if (leaveButton != null)
            {
                leaveButton.onClick.RemoveListener(HandleLeaveClicked);
            }

            if (resumeButton != null)
            {
                resumeButton.onClick.RemoveListener(HandleResumeClicked);
            }
        }

        public void SetVisible(bool visible)
        {
            if (panel != null)
            {
                panel.SetActive(visible);
            }
        }

        public void SetStartVisible(bool visible)
        {
            if (startButton != null)
            {
                startButton.gameObject.SetActive(visible);
            }
        }

        private void HandleStartClicked() => StartClicked?.Invoke();

        private void HandleLeaveClicked() => LeaveClicked?.Invoke();

        private void HandleResumeClicked() => ResumeClicked?.Invoke();
    }
}
