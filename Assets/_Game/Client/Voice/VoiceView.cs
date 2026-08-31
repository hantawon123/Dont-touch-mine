using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Voice
{
    public interface IVoiceView
    {
        event Action MuteToggleRequested;

        /// <summary>
        /// Paints the microphone button: whether there is a room to talk to,
        /// whether the player muted themselves, whether the microphone is
        /// latched open, and whether audio is leaving right now.
        /// </summary>
        void SetState(bool available, bool muted, bool latched, bool transmitting);
    }

    /// <summary>
    /// The microphone button in the corner of the lobby HUD.
    /// </summary>
    /// <remarks>
    /// Stays on the always-on HUD rather than moving into the Esc menu the way
    /// the other buttons did. Muting is something a player does mid-sentence,
    /// and a menu that has to be opened first is too slow for that. The talk key
    /// is the main way in either case — this shows what the microphone is doing
    /// and gives the mouse a way to silence it.
    /// </remarks>
    public sealed class VoiceView : MonoBehaviour, IVoiceView
    {
        /// <summary>Muted, and saying so before the player wonders.</summary>
        private static readonly Color MutedColor = new(0.55f, 0.2f, 0.22f, 0.95f);

        /// <summary>Audio is leaving this machine right now.</summary>
        private static readonly Color TalkingColor = new(0.25f, 0.65f, 0.35f, 0.95f);

        /// <summary>Live, but silent.</summary>
        private static readonly Color IdleColor = new(0.25f, 0.25f, 0.28f, 0.9f);

        /// <summary>No room to talk to yet.</summary>
        private static readonly Color OfflineColor = new(0.25f, 0.25f, 0.28f, 0.45f);

        [SerializeField]
        private Button muteButton;

        [SerializeField]
        private Image background;

        [SerializeField]
        private Text label;

        /// <summary>
        /// The same label where the screen was built with TextMeshPro.
        /// </summary>
        /// <remarks>
        /// Two fields because the two screens that show this button were laid
        /// out with different text stacks: the lobby with Unity's own Text and
        /// the match HUD with TextMeshPro. Only one is ever filled. Converting
        /// either screen wholesale is a bigger change than this button, and a
        /// legacy Text in the match HUD would need its Korean font wired by hand
        /// where TextMeshPro already has one.
        /// </remarks>
        [SerializeField]
        private TMP_Text tmpLabel;

        public event Action MuteToggleRequested;

        private void OnEnable()
        {
            if (muteButton == null)
            {
                Debug.LogError(
                    "VoiceButton is not wired. Run " +
                    "Game > Lobby > Build HUD Layout on the Lobby scene.",
                    this);
                return;
            }

            muteButton.onClick.AddListener(HandleMuteClicked);
        }

        private void OnDisable()
        {
            if (muteButton != null)
            {
                muteButton.onClick.RemoveListener(HandleMuteClicked);
            }
        }

        public void SetState(
            bool available,
            bool muted,
            bool latched,
            bool transmitting)
        {
            if (background != null)
            {
                background.color = !available
                    ? OfflineColor
                    : muted
                        ? MutedColor
                        : transmitting || latched
                            ? TalkingColor
                            : IdleColor;
            }

            // The latch is called out by name. A microphone left open is the
            // one state a player can be in without meaning to be, and a colour
            // alone is easy to stop noticing.
            var caption = muted
                ? "음소거"
                : latched
                    ? "ON"
                    : "MIC";

            if (label != null)
            {
                label.text = caption;
            }

            if (tmpLabel != null)
            {
                tmpLabel.text = caption;
            }

            if (muteButton != null)
            {
                // Nothing to mute before a room is joined, and a button that
                // answers then would leave the player wondering why the colour
                // did not change.
                muteButton.interactable = available;
            }
        }

        private void HandleMuteClicked() => MuteToggleRequested?.Invoke();
    }
}
