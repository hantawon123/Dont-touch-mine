using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Training
{
    /// <summary>Analysis tags of a hiding spot. Never observed by a policy; used to read results.</summary>
    [Flags]
    public enum HidingSpotTags : byte
    {
        None = 0,
        Under = 1,        // something solid within 1.2 m above the spot
        OnFurniture = 2,  // the surface is at least 0.3 m above the floor the bot stands on
        Corner = 4,       // at least three of the four sides are blocked within 1 m
    }

    [Serializable]
    public struct HidingSpot
    {
        /// <summary>Point on the support surface (the bottom centre of the placed box).</summary>
        public Vector3 Position;

        /// <summary>NavMesh point the bot walks to before putting the prop here.</summary>
        public Vector3 StandPosition;

        /// <summary>Bit i set: a prop of size class i (Small, Medium, Large) fits here.</summary>
        public byte SizeMask;

        public HidingSpotTags Tags;

        /// <summary>Hide score v3 exposure per size class (lower = better hidden); -1 when the size does not fit.</summary>
        public float ExposureSmall;
        public float ExposureMedium;
        public float ExposureLarge;

        public bool Fits(int sizeClass) => (SizeMask & (1 << sizeClass)) != 0;

        public float Exposure(int sizeClass) => sizeClass switch
        {
            0 => ExposureSmall,
            1 => ExposureMedium,
            _ => ExposureLarge,
        };
    }

    /// <summary>
    /// Hiding Spot Bank (place v3): every spot in a map where a prop can be put down by the game's placement
    /// rule and reached by the bot, with its hide score. Built once per map by Tools > AI > Build Hiding Spot Bank.
    /// </summary>
    public sealed class HidingSpotBank : ScriptableObject
    {
        public string SceneName;
        public string BuiltAt;
        public float GridSpacing;
        public float ExposureScale;

        /// <summary>Placement box half extents per size class (median of the map's carryable props).</summary>
        public Vector3[] HalfExtents = new Vector3[3];

        /// <summary>Seeker eye points used for the score (stand and crouch heights).</summary>
        public List<Vector3> Eyes = new();

        public List<HidingSpot> Spots = new();

        public float Hide(float exposure) => HideScoreV3.Hide(exposure, ExposureScale);
    }
}
