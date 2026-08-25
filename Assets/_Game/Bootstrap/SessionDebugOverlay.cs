using Game.Server.Network;
using UnityEngine;

namespace Game.Bootstrap
{
    /// <summary>
    /// Draws the current session state in the corner of the screen.
    /// </summary>
    /// <remarks>
    /// A built player sends <see cref="Debug.Log"/> to a file rather than to any
    /// visible console, so the room code a tester has to type into the next
    /// instance is otherwise only reachable by opening Player.log.
    /// <para>
    /// Temporary scaffolding: the lobby screen presents all of this properly, at
    /// which point this can go.
    /// </para>
    /// </remarks>
    public sealed class SessionDebugOverlay : MonoBehaviour
    {
        private const string HostObjectName = "[SessionDebugOverlay]";
        private const int Width = 260;
        private const int Height = 76;
        private const int Margin = 12;

        private static SessionDebugOverlay _instance;

        private NetworkRunnerService _network;
        private GUIStyle _style;

        /// <summary>
        /// Puts the overlay on screen for the rest of the run. Calling it again
        /// does nothing, so a scene reload cannot stack duplicates.
        /// </summary>
        public static void Attach(NetworkRunnerService network)
        {
            if (_instance != null)
            {
                return;
            }

            var host = new GameObject(HostObjectName);
            DontDestroyOnLoad(host);

            _instance = host.AddComponent<SessionDebugOverlay>();
            _instance._network = network;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void OnGUI()
        {
            if (_network == null)
            {
                return;
            }

            // GUI.skin is only valid inside OnGUI, so the style is built here.
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.UpperLeft,
                    padding = new RectOffset(10, 10, 8, 8),
                    fontSize = 13,
                    richText = false,
                };
            }

            GUI.Box(new Rect(Margin, Margin, Width, Height), Describe(), _style);
        }

        private string Describe()
        {
            if (!_network.IsRunning)
            {
                return "Not connected";
            }

            if (_network.IsBrowsingLobby)
            {
                return "Browsing the room list";
            }

            return $"Room      {_network.RoomCode}\n" +
                   $"Players   {_network.PlayerCount}/{_network.MaxPlayers}\n" +
                   $"Authority {(_network.IsServer ? "host" : "client")}";
        }
    }
}
