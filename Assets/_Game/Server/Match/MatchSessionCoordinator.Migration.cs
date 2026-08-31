using System;
using Game.Core.Match;
using UnityEngine;

namespace Game.Server.Match
{
    public sealed partial class MatchSessionCoordinator
    {
        public MatchMigrationPlayer CaptureMigrationPlayer(int index, Pose pose)
        {
            var hasPlacement = placements.TryGetPlacement(index, out var placement);
            return new MatchMigrationPlayer
            {
                PlayerId = Players.GetPlayer(index).PlayerId,
                ItemId = Assignments[index].Item.ItemId,
                Active = Players.IsActive(index), Pose = pose,
                HidingSpawn = hidingSpawnPoses[index], SearchingSpawn = searchingSpawnPoses[index],
                CompletedHidingTurn = completedHidingTurns[index],
                HasPlacement = hasPlacement, Placement = placement.Pose, AutoPlaced = placement.WasAutoPlaced,
                HitCount = interactions.GetHitCount(index),
                DestructionUses = interactions.GetRemainingDestructionUses(index),
                StunEndsAt = interactions.GetStunEndsAt(index),
                InvulnerableEndsAt = interactions.GetInvulnerableEndsAt(index),
            };
        }

        public bool TryGetPendingEjection(string objectId, out double ejectsAt, out Pose pose)
        {
            var found = pendingMapObjectEjections.TryGetValue(objectId, out var pending);
            ejectsAt = pending.EjectsAt;
            pose = pending.Pose;
            return found;
        }

        internal void RestoreMigration(MatchMigrationState snapshot, double now)
        {
            if (snapshot == null || snapshot.Players == null || snapshot.Objects == null ||
                snapshot.Players.Length != Assignments.Count ||
                !double.IsFinite(snapshot.CapturedAt) || snapshot.CapturedAt < 0d ||
                !double.IsFinite(now) || now < 0d ||
                snapshot.Phase.Phase == MatchPhase.Waiting)
                throw new ArgumentException("Invalid match migration state.", nameof(snapshot));

            var shift = now - snapshot.CapturedAt;
            for (var i = 0; i < Assignments.Count; i++)
            {
                var player = snapshot.Players[i];
                if (player.PlayerId != Players.GetPlayer(i).PlayerId || player.ItemId != Assignments[i].Item.ItemId)
                    throw new ArgumentException("Migration line-up or assignment mismatch.", nameof(snapshot));
                hidingSpawnPoses[i] = player.HidingSpawn;
                searchingSpawnPoses[i] = player.SearchingSpawn;
                completedHidingTurns[i] = player.CompletedHidingTurn;
                if (!player.Active) Players.TryDeactivate(i);
                if (player.HasPlacement) placements.RecordPlacement(i, player.Placement, player.AutoPlaced);
                interactions.Restore(i, player.HitCount, player.DestructionUses,
                    ShiftDeadline(player.StunEndsAt, shift), ShiftDeadline(player.InvulnerableEndsAt, shift));
            }

            foreach (var item in snapshot.Objects)
            {
                var owner = -1;
                for (var i = 0; i < Assignments.Count; i++)
                    if (Assignments[i].Item.ItemId == item.ObjectId) owner = i;
                if (owner >= 0)
                {
                    if (placements.TryGetPlacement(owner, out var placement))
                        placements.RecordPlacement(owner, item.Pose, placement.WasAutoPlaced);
                    if (item.Destroyed) outcome.DestroyItem(item.ObjectId);
                    else if (item.Holder >= 0 && !outcome.TryHoldItem(item.Holder, item.ObjectId))
                        throw new ArgumentException("Invalid migrated item holder.", nameof(snapshot));
                }
                else if (!worldObjects.TrySetPose(item.ObjectId, item.Pose))
                    throw new ArgumentException("Unknown migrated world object.", nameof(snapshot));
                else if (item.Holder >= 0)
                {
                    if (item.Holder >= heldMapObjectIdsByPlayer.Length ||
                        heldMapObjectIdsByPlayer[item.Holder] != null || outcome.GetHeldItemOwner(item.Holder) >= 0)
                        throw new ArgumentException("Invalid migrated prop holder.", nameof(snapshot));
                    heldMapObjectIdsByPlayer[item.Holder] = item.ObjectId;
                    mapObjectHolderById.Add(item.ObjectId, item.Holder);
                }
                if (item.PendingEjection)
                    pendingMapObjectEjections.Add(item.ObjectId,
                        new PendingMapObjectEjection(ShiftDeadline(item.EjectsAt, shift), item.EjectionPose));
            }

            // The old host's replay buffer is not in Fusion's snapshot. Never fabricate a partial POTG.
            hasExplicitHighlightCandidates = true;
            replayUnavailable = true;
            finalWarningStarted = snapshot.Phase.Phase == MatchPhase.Searching &&
                snapshot.Phase.PhaseEndsAt - snapshot.CapturedAt <= rules.FinalWarningSeconds;
            if (snapshot.Result.HasValue)
            {
                var previous = snapshot.Result.Value;
                var winners = new int[previous.WinnerPlayerIndices.Count];
                for (var i = 0; i < winners.Length; i++) winners[i] = previous.WinnerPlayerIndices[i];
                result = new MatchResult(previous.EndReason, Math.Max(0d, previous.EndedAt + shift), winners);
            }
            var phase = snapshot.Phase.Phase;
            if (phase == MatchPhase.Highlight)
            {
                if (!result.HasValue) throw new ArgumentException("Highlight migration needs a result.", nameof(snapshot));
                phase = MatchPhase.Result;
            }
            state.EnterPhase(phase, phase == MatchPhase.Result ? 0d : ShiftDeadline(snapshot.Phase.PhaseEndsAt, shift));
        }

        private static double ShiftDeadline(double time, double shift) => time > 0d ? Math.Max(0d, time + shift) : 0d;
    }
}
