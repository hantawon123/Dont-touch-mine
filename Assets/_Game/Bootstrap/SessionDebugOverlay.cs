using System.Text;
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

        /// <summary>Room code, player count, authority and the leave button.</summary>
        private const int BaseHeight = 116;

        /// <summary>Added per participant so the list is never clipped.</summary>
        private const int LineHeight = 16;

        private const int Margin = 12;
        private const int ButtonHeight = 26;

        private static SessionDebugOverlay _instance;

        private NetworkRunnerService _network;
        private RoomUiCommands _commands;
        private RoomBrowserSystem _state;
        private GUIStyle _boxStyle;
        private bool _leaving;
        private readonly StringBuilder _text = new StringBuilder();

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

            var height = BaseHeight
                         + ParticipantCount * LineHeight
                         + MatchLineCount * LineHeight
                         + (CanRequestStart ? ButtonHeight + 4 : 0);

            GUI.Box(new Rect(Margin, Margin, Width, height), Describe(), _boxStyle);

            if (!InRoom())
            {
                return;
            }

            var button = new Rect(
                Margin + 10,
                Margin + height - ButtonHeight - 10,
                Width - 20,
                ButtonHeight);

            GUI.enabled = !_leaving;
            if (GUI.Button(button, _leaving ? "나가는 중…" : "방 나가기"))
            {
                Leave().Forget();
            }

            GUI.enabled = true;

            DrawStartButton(button);
        }

        /// <summary>
        /// Asks the authority to start a match. Stands in for the host's START
        /// button until the lobby screen has one.
        /// </summary>
        /// <remarks>
        /// Shown to everyone, not only the host, on purpose. Refusing a request
        /// is the authority's job, and hiding the button would mean a wrong
        /// refusal never gets exercised.
        /// </remarks>
        private void DrawStartButton(Rect leaveButton)
        {
            if (_state == null || _state.IsMatchStarted)
            {
                return;
            }

            var start = new Rect(
                leaveButton.x,
                leaveButton.y - ButtonHeight - 4,
                leaveButton.width,
                ButtonHeight);

            if (GUI.Button(start, "게임 시작"))
            {
                _network.RequestMatchStart();
            }
        }

        private bool InRoom() => _network.IsRunning && !_network.IsBrowsingLobby;

        private int ParticipantCount =>
            _state == null ? 0 : _state.Participants.CurrentValue.Count;

        /// <summary>Lines the match status adds: the status itself, plus a refusal.</summary>
        private int MatchLineCount
        {
            get
            {
                if (_state == null)
                {
                    return 0;
                }

                return _state.LastStartRefusal.CurrentValue.HasValue ? 2 : 1;
            }
        }

        private bool CanRequestStart => _state != null && !_state.IsMatchStarted;

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

            _text.Clear();
            _text.Append("Room      ").Append(_network.RoomCode).Append('\n')
                 .Append("Players   ").Append(_network.PlayerCount)
                 .Append('/').Append(_network.MaxPlayers).Append('\n')
                 .Append("Authority ").Append(_network.IsServer ? "host" : "client");

            AppendParticipants();
            AppendMatchStatus();
            return _text.ToString();
        }

        /// <summary>
        /// Says whether a match is running and where the local player sits in
        /// it. The index is the one the match rules use, not a seat number.
        /// </summary>
        private void AppendMatchStatus()
        {
            if (_state == null)
            {
                return;
            }

            _text.Append('\n').Append("Match     ");

            if (_state.IsMatchStarted)
            {
                _text.Append("started, you are #").Append(_state.LocalPlayerIndex);
            }
            else
            {
                _text.Append("waiting");
            }

            var refusal = _state.LastStartRefusal.CurrentValue;

            if (refusal.HasValue)
            {
                _text.Append('\n').Append("Refused   ").Append(refusal.Value);
            }
        }

        /// <summary>
        /// Lists the room by seat. Shows that every peer agrees on the seats and
        /// on who the host is, which is the whole point of replicating them.
        /// </summary>
        /// <remarks>
        /// Being the local player is worked out here rather than read off the
        /// participant, because the answer differs per screen. This is the same
        /// comparison the lobby screen will make.
        /// </remarks>
        private void AppendParticipants()
        {
            if (_state == null)
            {
                return;
            }

            var participants = _state.Participants.CurrentValue;
            var localId = _state.LocalPlayerId.CurrentValue;

            for (var index = 0; index < participants.Count; index++)
            {
                var participant = participants[index];

                _text.Append('\n')
                     .Append(participant.Seat).Append("  ")
                     .Append(participant.PlayerId);

                if (participant.IsHost)
                {
                    _text.Append("  host");
                }

                if (participant.PlayerId == localId)
                {
                    _text.Append("  <- you");
                }
            }
        }
    }
}
