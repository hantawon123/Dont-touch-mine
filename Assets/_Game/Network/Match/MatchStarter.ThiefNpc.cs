using UnityEngine;

namespace Game.Network.Match
{
    /// <summary>
    /// Host-side read access for the thief NPC director (Assets/_Game/Training/Thief/InGame): where the players and
    /// their assigned items are and who holds what. Read only; the NPC acts through the NPC carry calls above.
    /// </summary>
    public sealed partial class MatchStarter
    {
        /// <summary>True on the state authority while the searching phase is open (after its intro).</summary>
        public bool IsNpcSearchOpen =>
            HasValidState && _session != null && _state.Object != null && _state.Object.HasStateAuthority &&
            _session.CurrentPhase == Game.Core.Match.MatchPhase.Searching && !_session.IsPhaseIntro(ServerTime);

        public double NpcServerTime => HasValidState ? ServerTime : 0d;

        public int NpcPlayerCount => _session == null ? 0 : Mathf.Min(_playing.Count, _session.Assignments.Count);

        public bool TryGetNpcPlayerView(int playerIndex, out Pose pose, out bool holdsSomething)
        {
            holdsSomething = false;
            if (!TryGetPlayerPose(playerIndex, out pose)) return false;
            var count = Mathf.Min(_state.ObjectStateCount, MatchSessionState.MaxReplicatedObjects);
            for (var index = 0; index < count && !holdsSomething; index++)
            {
                holdsSomething = _state.ObjectStates.Get(index).HolderPlayerIndex == playerIndex;
            }

            return true;
        }

        /// <summary>A player's assigned item: replicated pose and holder (-1 / npc id null when nobody holds it).</summary>
        public bool TryGetNpcAssignedItem(int playerIndex, out string itemId, out Pose pose, out int holderPlayerIndex, out string holderNpcId)
        {
            itemId = null;
            pose = default;
            holderPlayerIndex = -1;
            holderNpcId = null;
            if (_session == null || playerIndex < 0 || playerIndex >= _session.Assignments.Count) return false;
            itemId = _session.Assignments[playerIndex].Item.ItemId;
            var count = Mathf.Min(_state.ObjectStateCount, MatchSessionState.MaxReplicatedObjects);
            for (var index = 0; index < count; index++)
            {
                var state = _state.ObjectStates.Get(index);
                if (!string.Equals(state.ObjectId.ToString(), itemId, System.StringComparison.Ordinal)) continue;
                if (state.IsDestroyed) return false;
                pose = new Pose(state.Position, state.Rotation);
                holderPlayerIndex = state.HolderPlayerIndex;
                holderNpcId = state.HolderNpcId.Length > 0 ? state.HolderNpcId.ToString() : null;
                return true;
            }

            return false;
        }

        private bool IsAssignedItemId(string objectId)
        {
            if (_session == null) return false;
            for (var i = 0; i < _session.Assignments.Count; i++)
            {
                if (string.Equals(_session.Assignments[i].Item.ItemId, objectId, System.StringComparison.Ordinal)) return true;
            }

            return false;
        }
    }
}
