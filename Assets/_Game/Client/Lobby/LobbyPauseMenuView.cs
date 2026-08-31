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
        event Action SettingsClicked;
        event Action PlaySettingsClicked;
        event Action KeyGuideClicked;

        bool IsOpen { get; }

        void SetVisible(bool visible);
        void SetStartVisible(bool visible);
        void SetPlaySettingsVisible(bool visible);
    }

    /// <summary>
    /// The lobby's Esc menu: everything a player can reach with the mouse while
    /// the rest of the visit keeps the cursor captured for looking around.
    /// </summary>
    /// <remarks>
    /// Lives on the HUD canvas rather than on the panel it shows, the way
    /// <see cref="KeyGuideView"/> does. A view that sits on its own hidden panel
    /// only wires its buttons the first time the panel is switched on, which is
    /// one more thing to get wrong for no gain.
    /// <para>
    /// Play settings, key guide and settings used to be corner buttons on the
    /// always-on HUD. A captured cursor reports from the centre of the screen
    /// and could not reach them, so they are entries here now, for the same
    /// reason start and leave already were.
    /// </para>
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

        [SerializeField]
        private Button settingsButton;

        [SerializeField]
        private Button playSettingsButton;

        [SerializeField]
        private Button keyGuideButton;

        public event Action StartClicked;
        public event Action LeaveClicked;
        public event Action ResumeClicked;
        public event Action SettingsClicked;
        public event Action PlaySettingsClicked;
        public event Action KeyGuideClicked;

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

            // Bound one by one rather than behind the guard above. A scene saved
            // before these entries existed still opens and still leaves, which
            // is worth more than refusing to wire anything at all.
            BindOptional(settingsButton, HandleSettingsClicked, "SettingsButton");
            BindOptional(
                playSettingsButton, HandlePlaySettingsClicked, "PlaySettingsButton");
            BindOptional(keyGuideButton, HandleKeyGuideClicked, "KeyGuideButton");
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

            if (settingsButton != null)
            {
                settingsButton.onClick.RemoveListener(HandleSettingsClicked);
            }

            if (playSettingsButton != null)
            {
                playSettingsButton.onClick.RemoveListener(HandlePlaySettingsClicked);
            }

            if (keyGuideButton != null)
            {
                keyGuideButton.onClick.RemoveListener(HandleKeyGuideClicked);
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

        /// <summary>
        /// Shows the play settings entry to the host and nobody else, the way
        /// <see cref="SetStartVisible"/> does for starting.
        /// </summary>
        /// <remarks>
        /// The screen behind it refuses to open for a guest anyway. Leaving the
        /// entry visible would offer a button that answers nothing.
        /// </remarks>
        public void SetPlaySettingsVisible(bool visible)
        {
            if (playSettingsButton != null)
            {
                playSettingsButton.gameObject.SetActive(visible);
            }
        }

        private void BindOptional(
            Button button,
            UnityEngine.Events.UnityAction handler,
            string slotName)
        {
            if (button == null)
            {
                Debug.LogWarning(
                    $"PauseMenuPanel has no {slotName}. Run " +
                    "Game > Lobby > Build HUD Layout on the Lobby scene.",
                    this);
                return;
            }

            button.onClick.AddListener(handler);
        }

        private void HandleStartClicked() => StartClicked?.Invoke();

        private void HandleLeaveClicked() => LeaveClicked?.Invoke();

        private void HandleResumeClicked() => ResumeClicked?.Invoke();

        private void HandleSettingsClicked() => SettingsClicked?.Invoke();

        private void HandlePlaySettingsClicked() => PlaySettingsClicked?.Invoke();

        private void HandleKeyGuideClicked() => KeyGuideClicked?.Invoke();
    }
}
