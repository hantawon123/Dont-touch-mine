using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core.Ports;
using Game.Core.Rooms;
using Game.Network.Session;
using UnityEngine;

namespace Game.Network.Lobby
{
    /// <summary>
    /// Opens and enters rooms through Photon matchmaking.
    /// </summary>
    /// <remarks>
    /// The only place that knows a room code doubles as the Photon session name.
    /// Presentation hands over an opaque <see cref="RoomId"/> or a typed code and
    /// never learns the address, so moving room addressing elsewhere later stays
    /// contained to this class.
    /// </remarks>
    public sealed class RoomBrowser : IRoomBrowser
    {
        /// <summary>
        /// How many codes to try before giving up. Collisions are vanishingly
        /// rare against a billion combinations, but a taken code must not
        /// surface to the host as a failure they cannot act on.
        /// </summary>
        private const int CodeAttempts = 3;

        private readonly NetworkRunnerService _network;
        private readonly RoomCodeGenerator _codes;

        public RoomBrowser(NetworkRunnerService network, RoomCodeGenerator codes)
        {
            _network = network;
            _codes = codes;
        }

        /// <summary>
        /// Photon pushes the room list on its own once the lobby is joined, so
        /// this only has to guarantee we are in the lobby.
        /// </summary>
        public async UniTask RefreshAsync(CancellationToken cancellation)
        {
            if (_network.IsBrowsingLobby)
            {
                return;
            }

            await _network.JoinLobbyAsync(cancellation);
        }

        public async UniTask<RoomEntryResult> CreateAsync(
            RoomCreateRequest request, CancellationToken cancellation)
        {
            for (var attempt = 0; attempt < CodeAttempts; attempt++)
            {
                var code = _codes.Next();

                var result = await _network.StartAsync(
                    SessionRequest.Create(
                        code,
                        request.DisplayName,
                        request.MapId,
                        request.MaxPlayers,
                        request.Password),
                    cancellation);

                if (result.Ok)
                {
                    Debug.Log($"[Rooms] Opened room. Code={code}");
                    return RoomEntryResult.Opened(code);
                }

                if (result.Failure != SessionFailure.CodeTaken)
                {
                    return RoomEntryResult.Failed(Translate(result.Failure));
                }

                Debug.LogWarning($"[Rooms] Code {code} already taken, drawing another.");
            }

            return RoomEntryResult.Failed(RoomEntryFailure.CodeUnavailable);
        }

        public async UniTask<RoomEntryResult> EnterAsync(
            RoomId room, string password, CancellationToken cancellation)
        {
            if (!room.IsValid)
            {
                return RoomEntryResult.Failed(RoomEntryFailure.NotFound);
            }

            // No code comes back: the player picked this room from the list and
            // is not entitled to the code that would let them back into a locked
            // room later.
            var result = await Enter(room.Value, password, cancellation);
            return result.Ok
                ? RoomEntryResult.Entered()
                : result;
        }

        public async UniTask<RoomEntryResult> EnterByCodeAsync(
            string roomCode, string password, CancellationToken cancellation)
        {
            var code = RoomCodeGenerator.Normalize(roomCode);

            if (!RoomCodeGenerator.IsWellFormed(code))
            {
                return RoomEntryResult.Failed(RoomEntryFailure.InvalidCode);
            }

            // The code only says which room. A locked room still checks the
            // password, so learning a code off the browser grants nothing.
            var result = await Enter(code, password, cancellation);
            return result.Ok
                ? RoomEntryResult.Opened(code)
                : result;
        }

        /// <summary>
        /// Leaves the room. The departure itself is reported through
        /// <see cref="IRoomSessionSink.RoomClosed"/> by the shutdown callback, so
        /// presentation sees a voluntary exit the same way as any other.
        /// </summary>
        public UniTask LeaveAsync(CancellationToken cancellation)
        {
            _network.Shutdown();
            return UniTask.CompletedTask;
        }

        private async UniTask<RoomEntryResult> Enter(
            string roomCode, string password, CancellationToken cancellation)
        {
            var result = await _network.StartAsync(
                SessionRequest.Join(roomCode, password), cancellation);

            return result.Ok
                ? RoomEntryResult.Entered()
                : RoomEntryResult.Failed(Translate(result.Failure));
        }

        private static RoomEntryFailure Translate(SessionFailure failure)
        {
            switch (failure)
            {
                case SessionFailure.RoomNotFound:
                    return RoomEntryFailure.NotFound;
                case SessionFailure.RoomFull:
                    return RoomEntryFailure.Full;
                case SessionFailure.CodeTaken:
                    return RoomEntryFailure.CodeUnavailable;
                case SessionFailure.Rejected:
                    return RoomEntryFailure.WrongPassword;
                case SessionFailure.ConnectionFailed:
                    return RoomEntryFailure.ConnectionFailed;
                case SessionFailure.AlreadyRunning:
                    return RoomEntryFailure.AlreadyInRoom;
                default:
                    return RoomEntryFailure.Unknown;
            }
        }
    }
}
