using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Lobby
{
    public sealed class LobbyHudView : MonoBehaviour
    {
        [SerializeField]
        private RectTransform settingsButton;

        [SerializeField]
        private RectTransform playSettingsButton;

        [SerializeField]
        private RectTransform startButton;

        [SerializeField]
        private RectTransform leaveButton;

        [SerializeField]
        private RectTransform keyGuideButton;

        [SerializeField]
        private RectTransform playerListRoot;

        [SerializeField]
        private RectTransform chatRoot;

        [SerializeField]
        private RectTransform voiceButton;

        public event Action PlaySettingsClicked;
        public event Action StartClicked;

        private Button playSettingsUiButton;
        private Button startUiButton;

        private void OnEnable()
        {
            playSettingsUiButton = playSettingsButton != null
                ? playSettingsButton.GetComponent<Button>()
                : null;
            startUiButton = startButton != null
                ? startButton.GetComponent<Button>()
                : null;

            if (playSettingsUiButton != null)
            {
                playSettingsUiButton.onClick.AddListener(HandlePlaySettingsClicked);
            }

            if (startUiButton != null)
            {
                startUiButton.onClick.AddListener(HandleStartClicked);
            }
        }

        private void OnDisable()
        {
            if (playSettingsUiButton != null)
            {
                playSettingsUiButton.onClick.RemoveListener(HandlePlaySettingsClicked);
            }

            if (startUiButton != null)
            {
                startUiButton.onClick.RemoveListener(HandleStartClicked);
            }
        }

        public void SetHostControlsVisible(bool visible)
        {
            if (playSettingsButton != null)
            {
                playSettingsButton.gameObject.SetActive(visible);
            }

            if (startButton != null)
            {
                startButton.gameObject.SetActive(visible);
            }
        }

        private void HandlePlaySettingsClicked() => PlaySettingsClicked?.Invoke();

        private void HandleStartClicked() => StartClicked?.Invoke();
    }
}
