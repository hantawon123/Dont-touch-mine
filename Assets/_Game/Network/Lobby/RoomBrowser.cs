using System.Threading;
using System;
using Cysharp.Threading.Tasks;
using Game.Core.Lobby;
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
        private readonly NetworkRunnerService _network;

        public RoomBrowser(NetworkRunnerService network, RoomCodeGenerator codes)
        {
            _network = network;
        }

        /// <summary>
        /// Photon pushes the room list and every later change while the lobby
        /// connection is alive. Reuse that live snapshot instead of paying for
        /// a disconnect and a second matchmaking handshake on every refresh.
        /// </summary>
        public async UniTask<RoomEntryFailure> RefreshAsync(CancellationToken cancellation)
        {
            if (_network.IsBrowsingLobby)
            {
                return RoomEntryFailure.None;
            }

            var result = await _network.JoinLobbyAsync(cancellation);
            return result.Ok
                ? RoomEntryFailure.None
                : Translate(result.Failure);
        }

        public async UniTask<RoomEntryResult> CreateAsync(
            RoomCreateRequest request, CancellationToken cancellation)
        {
            if (!request.TryCreateSettings(
                    RoomSettings.MaxPlayerCount,
                    out var settings,
                    out var error))
            {
                Debug.LogWarning($"[Rooms] Invalid room settings: {error}");
                return RoomEntryResult.Failed(RoomEntryFailure.InvalidRequest);
            }

            try
            {
                var code = await _network.FindAvailableServerAsync(cancellation);
                if (string.IsNullOrEmpty(code))
                {
                    Debug.LogWarning("[Rooms] No running game server is available.");
                    return RoomEntryResult.Failed(RoomEntryFailure.ConnectionFailed);
                }
                var result = await _network.StartAsync(
                    SessionRequest.Join(code, null), cancellation);
                if (!result.Ok) return RoomEntryResult.Failed(
                    result.Failure is SessionFailure.Rejected or SessionFailure.RoomFull
                        ? RoomEntryFailure.CodeUnavailable : Translate(result.Failure));
                if (await _network.ClaimRoomAsync(request, cancellation))
                {
                    Debug.Log($"[Rooms] Claimed server room as Client. Code={code}");
                    return RoomEntryResult.Opened(code);
                }
                _network.Shutdown();
                return RoomEntryResult.Failed(RoomEntryFailure.CodeUnavailable);
            }
            catch (OperationCanceledException)
            {
                _network.Shutdown();
                if (cancellation.IsCancellationRequested) throw;
                return RoomEntryResult.Failed(RoomEntryFailure.ConnectionFailed);
            }
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
        public async UniTask LeaveAsync(CancellationToken cancellation)
        {
            _network.Shutdown();
            await UniTask.WaitUntil(() => !_network.IsRoomExitPending, cancellationToken: cancellation);
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
