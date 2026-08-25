using UnityEngine;

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
    }
}
