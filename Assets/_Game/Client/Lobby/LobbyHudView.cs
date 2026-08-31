using UnityEngine;

namespace Game.Client.Lobby
{
    /// <summary>
    /// The lobby's always-on screen furniture.
    /// </summary>
    /// <remarks>
    /// Nothing here is pressed. The cursor is captured for looking around for
    /// most of the visit, and a captured cursor reports from the centre of the
    /// screen, so a button pinned to a corner cannot be reached at all. Every
    /// button the lobby had up here — start, leave, settings, play settings,
    /// the key guide — is an entry in the Esc menu now. See
    /// <see cref="LobbyPauseMenuView"/>.
    /// <para>
    /// What is left is the things a player reads rather than clicks, and the
    /// chat field, which the keyboard reaches on its own.
    /// </para>
    /// </remarks>
    public sealed class LobbyHudView : MonoBehaviour
    {
        [SerializeField]
        private RectTransform playerListRoot;

        [SerializeField]
        private RectTransform chatRoot;

        [SerializeField]
        private RectTransform voiceButton;
    }
}
