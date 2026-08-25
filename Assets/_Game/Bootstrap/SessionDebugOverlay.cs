using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core.Lobby;
using Game.Network.Session;
using UnityEngine;

namespace Game.Bootstrap
{
    /// <summary>
    /// Draws the current session state in the corner of the screen and offers a
    /// leave button.
    /// </summary>
    /// <remarks>
    /// A built player sends <see cref="Debug.Log"/> to a file rather than to any
    /// visible console, so the room code a tester has to type into the next
    /// instance is otherwise only reachable by opening Player.log.
    /// <para>
    /// The leave button exists because stopping play is not the same code path as
    /// leaving: it disposes the container instead of shutting the session down,
    /// so a deliberate departure would never be exercised without it. Pressing it
    /// on the host instance is also the only way to test the host walking out.
    /// </para>
    /// <para>
    /// Temporary scaffolding: the lobby and waiting room screens present all of
    /// this properly, at which point this can go.
    /// </para>
    /// </remarks>
    public sealed class SessionDebugOverlay : MonoBehaviour
    {
        private const string HostObjectName = "[SessionDebugOverlay]";
        private const int Width = 260;
        private const int Height = 116;
        private const int Margin = 12;
        private const int ButtonHeight = 26;

        private static SessionDebugOverlay _instance;

        private NetworkRunnerService _network;
        private RoomUiCommands _commands;
        private RoomBrowserSystem _state;
        private GUIStyle _boxStyle;
        private bool _leaving;

        /// <summary>
        /// Puts the overlay on screen for the rest of the run. Calling it again
        /// does nothing, so a scene reload cannot stack duplicates.
        /// </summary>
        public static void Attach(
            NetworkRunnerService network, RoomUiCommands commands, RoomBrowserSystem state)
        {
            if (_instance != null)
            {
                return;
            }

            var host = new GameObject(HostObjectName);
            DontDestroyOnLoad(host);

            _instance = host.AddComponent<SessionDebugOverlay>();
            _instance._network = network;
            _instance._commands = commands;
            _instance._state = state;
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
            if (_boxStyle == null)
            {
                _boxStyle = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.UpperLeft,
                    padding = new RectOffset(10, 10, 8, 8),
                    fontSize = 13,
                    richText = false,
                };
            }

            GUI.Box(new Rect(Margin, Margin, Width, Height), Describe(), _boxStyle);

            if (!InRoom())
            {
                return;
            }

            var button = new Rect(
                Margin + 10,
                Margin + Height - ButtonHeight - 10,
                Width - 20,
                ButtonHeight);

            GUI.enabled = !_leaving;
            if (GUI.Button(button, _leaving ? "나가는 중…" : "방 나가기"))
            {
                Leave().Forget();
            }

            GUI.enabled = true;
        }

        private bool InRoom() => _network.IsRunning && !_network.IsBrowsingLobby;

        private async UniTaskVoid Leave()
        {
            _leaving = true;
            try
            {
                await _commands.LeaveAsync(CancellationToken.None);
            }
            catch (System.Exception error)
            {
                Debug.LogError($"[Session] Leaving failed: {error}");
            }
            finally
            {
                _leaving = false;
            }
        }

        private string Describe()
        {
            if (!_network.IsRunning)
            {
                var exit = _state?.LastExit.CurrentValue;
                return exit.HasValue
                    ? $"Not connected\nLast exit  {exit.Value}"
                    : "Not connected";
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
