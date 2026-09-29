using System.Collections.Generic;
using Fusion;
using Game.Core.Match;
using Game.Network.Players;
using Game.Server.Items;
using UnityEngine;

namespace Game.Network.Match
{
    public sealed partial class MatchStarter
    {
        private LobbyObjectAuthority lobbyObjects;
        private bool IsLobby => HasValidState && !_state.IsStarted && _state.Phase == MatchPhase.Waiting;

        public bool ConfigureLobbyObjects(IReadOnlyList<WorldObjectState> objects)
        {
            if (!IsLobby || !_state.Object.HasStateAuthority) return false;
            var authority = new LobbyObjectAuthority(objects);
            if (!_state.TryResetLobbyObjects(objects)) return false;
            lobbyObjects = authority;
            return true;
        }

        private bool TryGetLobbyPlayer(ref PlayerRef source, out PlayerAvatar avatar, out Pose pose)
        {
            avatar = null;
            pose = default;
            if (!IsLobby || !_state.Object.HasStateAuthority || lobbyObjects == null || _roster == null) return false;
            if (!source.IsRealPlayer) source = _state.Runner.LocalPlayer;
            if (!source.IsRealPlayer || !_roster.TryGetAvatar(PlayerRegistry.IdOf(source), out avatar)) return false;
            var motor = avatar.GetComponent<NetworkPlayerMotor>();
            return motor != null && motor.ControlsEnabled &&
                _roster.TryGetPose(PlayerRegistry.IdOf(source), out pose);
        }

        private bool TryHoldLobbyObject(PlayerRef source, string objectId)
        {
            if (!TryGetLobbyPlayer(ref source, out var avatar, out var pose) ||
                !_state.CanHoldObject(objectId) || !_state.CanTrackObject(objectId) ||
                !lobbyObjects.TryHold(PlayerRegistry.IdOf(source), objectId, pose)) return false;
            if (_state.TrySetObjectHeld(objectId, avatar.Seat))
            {
                avatar.GetComponent<NetworkPlayerMotor>()?.CancelEmote();
                return true;
            }
            lobbyObjects.Forget(PlayerRegistry.IdOf(source));
            return false;
        }

        private bool TryReleaseLobbyObject(PlayerRef source, Pose pose, Vector3 velocity, bool throwing)
        {
            if (!TryGetLobbyPlayer(ref source, out var avatar, out var playerPose) ||
                !lobbyObjects.TryGetHeld(PlayerRegistry.IdOf(source), out var objectId) ||
                !_state.CanTrackObject(objectId) ||
                !lobbyObjects.TryRelease(PlayerRegistry.IdOf(source), playerPose, pose, velocity, throwing)) return false;
            var released = _state.TrySetObjectReleased(objectId, pose, velocity);
            if (released) avatar.GetComponent<NetworkPlayerMotor>()?.CancelEmote();
            return released;
        }

        private bool ReleaseDepartedLobbyObject(PlayerRef player)
        {
            if (!IsLobby || !_state.Object.HasStateAuthority || lobbyObjects == null || !player.IsRealPlayer ||
                !lobbyObjects.TryGetHeld(PlayerRegistry.IdOf(player), out var objectId)) return false;
            lobbyObjects.Forget(PlayerRegistry.IdOf(player));
            for (var i = 0; i < _state.ObjectStateCount; i++)
            {
                var item = _state.ObjectStates.Get(i);
                if (item.ObjectId.ToString() != objectId) continue;
                var pose = new Pose(item.Position, item.Rotation);
                if (_roster != null && _roster.TryGetPose(PlayerRegistry.IdOf(player), out var playerPose)) pose = playerPose;
                lobbyObjects.TrySetPose(objectId, pose);
                return _state.TrySetObjectReleased(objectId, pose);
            }
            return false;
        }
    }
}
