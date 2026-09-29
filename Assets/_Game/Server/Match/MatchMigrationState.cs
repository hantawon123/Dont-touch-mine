using Game.Core.Match;
using UnityEngine;

namespace Game.Server.Match
{
    // Transfer values only. The live mutable state stays in the owning systems.
    public sealed class MatchMigrationState
    {
        public double CapturedAt;
        public MatchStateSnapshot Phase;
        public MatchMigrationPlayer[] Players;
        public MatchMigrationObject[] Objects;
        public MatchResult? Result;
    }

    public struct MatchMigrationPlayer
    {
        public string PlayerId;
        public string ItemId;
        public bool Active;
        public Pose Pose;
        public Pose HidingSpawn;
        public Pose SearchingSpawn;
        public bool CompletedHidingTurn;
        public bool HasPlacement;
        public Pose Placement;
        public bool AutoPlaced;
        public int HitCount;
        public int DestructionUses;
        public double StunEndsAt;
        public double InvulnerableEndsAt;
    }

    public struct MatchMigrationObject
    {
        public string ObjectId;
        public Pose Pose;
        public int Holder;
        public bool Destroyed;
        public bool PendingEjection;
        public double EjectsAt;
        public Pose EjectionPose;
    }
}
