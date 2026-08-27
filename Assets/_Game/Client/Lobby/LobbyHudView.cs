using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Lobby
{
    /// <summary>
    /// The lobby's always-on screen furniture.
    /// </summary>
    /// <remarks>
    /// Starting and leaving are not here. The cursor is captured for looking
    /// around for most of the visit, and a captured cursor reports from the
    /// centre of the screen, so buttons pinned to a corner could not be pressed
    /// at all. Both moved to the Esc menu — see <see cref="LobbyPauseMenuView"/>.
    /// </remarks>
    public sealed class LobbyHudView : MonoBehaviour
    {
        [SerializeField]
        private RectTransform settingsButton;

        [SerializeField]
        private RectTransform playSettingsButton;

        [SerializeField]
        private RectTransform keyGuideButton;

        [SerializeField]
        private RectTransform playerListRoot;

        [SerializeField]
        private RectTransform chatRoot;

        [SerializeField]
        private RectTransform voiceButton;

        public event Action PlaySettingsClicked;

        private Button playSettingsUiButton;

        private void OnEnable()
        {
            playSettingsUiButton = playSettingsButton != null
                ? playSettingsButton.GetComponent<Button>()
                : null;

            if (playSettingsUiButton != null)
            {
                playSettingsUiButton.onClick.AddListener(HandlePlaySettingsClicked);
            }
            else
            {
                Debug.LogError(
                    "PlaySettingsButton has no Button component. Run " +
                    "Game > Lobby > Build HUD Layout on the Lobby scene.",
                    this);
            }
        }

        private void OnDisable()
        {
            if (playSettingsUiButton != null)
            {
                playSettingsUiButton.onClick.RemoveListener(HandlePlaySettingsClicked);
            }
        }

        public void SetHostControlsVisible(bool visible)
        {
            if (playSettingsButton != null)
            {
                playSettingsButton.gameObject.SetActive(visible);
            }
        }

        private void HandlePlaySettingsClicked() => PlaySettingsClicked?.Invoke();
    }
}
