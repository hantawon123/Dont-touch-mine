using System;
using System.Collections.Generic;
using Game.Core.Players;
using UnityEngine;

namespace Game.Client.Character
{
    /// <summary>
    /// Everything the closet can offer: the categories, the parts in each, and
    /// the art each part is worn as.
    /// </summary>
    /// <remarks>
    /// An asset rather than a table in code because the art is what changes.
    /// The screen and the character both read the catalogue, so a part that is
    /// added to the grid is a part the character can already wear — the two
    /// cannot drift apart the way two lists would.
    /// <para>
    /// Ids, not indices, are what leaves this file. Reordering the asset is
    /// then a presentation change rather than a silent rewrite of what everyone
    /// is wearing.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "Game/Avatar Part Catalog", fileName = "AvatarPartCatalog")]
    public sealed class AvatarPartCatalog : ScriptableObject
    {
        [SerializeField]
        private AvatarPartGroup[] groups = Array.Empty<AvatarPartGroup>();

        /// <summary>The categories, in the order the closet lists them.</summary>
        public IReadOnlyList<AvatarPartGroup> Groups =>
            groups ?? Array.Empty<AvatarPartGroup>();

        /// <summary>
        /// The category's row, or <c>null</c> when the asset does not describe
        /// it. A missing category is a gap in the asset, not an error: the
        /// screen simply has one row fewer.
        /// </summary>
        public AvatarPartGroup Find(AvatarPartCategory category)
        {
            foreach (var group in Groups)
            {
                if (group != null && group.Category == category)
                {
                    return group;
                }
            }

            return null;
        }

        public bool TryFind(AvatarPartCategory category, string partId, out AvatarPart part)
        {
            part = null;
            if (string.IsNullOrEmpty(partId))
            {
                return false;
            }

            var group = Find(category);
            if (group == null)
            {
                return false;
            }

            foreach (var candidate in group.Parts)
            {
                if (candidate != null &&
                    string.Equals(candidate.Id, partId, StringComparison.Ordinal))
                {
                    part = candidate;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// What a new player wears. A configured default stays stable when the grid is reordered.
        /// </summary>
        public AvatarAppearance Default
        {
            get
            {
                var appearance = AvatarAppearance.Default;
                foreach (var group in Groups)
                {
                    if (group == null || group.Parts.Count == 0)
                    {
                        continue;
                    }

                    appearance = appearance.With(group.Category, group.DefaultPartId);
                }

                return appearance;
            }
        }

        /// <summary>
        /// The same appearance with anything this catalogue cannot dress
        /// dropped.
        /// </summary>
        /// <remarks>
        /// Saved appearances outlive the art. A part that is renamed or removed
        /// would otherwise leave a character wearing an id nothing can draw,
        /// and the grid would show no selection at all with no way to explain
        /// why. Falling back to the category's first part keeps the character
        /// dressed and the grid honest about what it is showing.
        /// </remarks>
        public AvatarAppearance Normalise(AvatarAppearance appearance)
        {
            var result = appearance;
            foreach (AvatarPartCategory category in Enum.GetValues(typeof(AvatarPartCategory)))
            {
                var worn = appearance.Get(category);
                if (string.IsNullOrEmpty(worn) || TryFind(category, worn, out _))
                {
                    continue;
                }

                var group = Find(category);
                result = result.With(
                    category,
                    group != null && group.Parts.Count > 0
                        ? group.DefaultPartId
                        : AvatarAppearance.NoPart);
            }

            return result;
        }
    }

    /// <summary>One category and the parts it offers.</summary>
    [Serializable]
    public sealed class AvatarPartGroup
    {
        [SerializeField]
        private AvatarPartCategory category;

        [SerializeField]
        [Tooltip("What the tab is called on screen, e.g. 몸 색상.")]
        private string label = string.Empty;

        [SerializeField]
        [Tooltip("The tab's icon. Optional; the tab shows its label without one.")]
        private Sprite icon;

        [SerializeField]
        private AvatarPart[] parts = Array.Empty<AvatarPart>();

        [SerializeField] private string defaultPartId = string.Empty;

        public string DefaultPartId
        {
            get
            {
                foreach (var part in Parts)
                    if (part != null && part.Id == defaultPartId) return defaultPartId;
                return Parts.Count > 0 ? Parts[0].Id : AvatarAppearance.NoPart;
            }
        }

        public AvatarPartCategory Category => category;

        public string Label => string.IsNullOrEmpty(label) ? category.ToString() : label;

        public Sprite Icon => icon;

        public IReadOnlyList<AvatarPart> Parts => parts ?? Array.Empty<AvatarPart>();
    }

    /// <summary>
    /// One thing that can be worn, and how it is worn.
    /// </summary>
    /// <remarks>
    /// The three ways of wearing are tried in order — material, then texture,
    /// then tint — so a category can be filled in before its art exists. Shoes
    /// and faces are picked with none of the three set today: they change the
    /// selection and nothing else, which is what "UI only" means here.
    /// <para>
    /// Every category always wears something. The design has no bare-headed
    /// thief, so there is no empty first cell and nothing has to describe one.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class AvatarPart
    {
        [SerializeField]
        [Tooltip("Stored and sent as-is. Renaming one changes what everybody " +
                 "wearing it is wearing, so treat it as permanent.")]
        private string id = string.Empty;

        [SerializeField]
        private string label = string.Empty;

        [SerializeField]
        [Tooltip("The grid cell's picture. Without one the cell falls back to " +
                 "the swatch colour.")]
        private Sprite thumbnail;

        [SerializeField]
        [Tooltip("What the cell shows when there is no thumbnail, and the tint " +
                 "the part is worn in when it has no material or texture.")]
        private Color swatch = Color.white;

        [SerializeField]
        [Tooltip("Replaces the renderer's material outright.")]
        private Material material;

        [SerializeField]
        [Tooltip("Kept on the authored material and swapped into _BaseMap.")]
        private Texture texture;

        [SerializeField]
        [Tooltip("Hood mesh in the authored hood's local coordinates.")]
        private Mesh mesh;

        [SerializeField] private AvatarWearable wearable;

        public string Id => id;

        public string Label => string.IsNullOrEmpty(label) ? id : label;

        public Sprite Thumbnail => thumbnail;

        public Color Swatch => swatch;

        public Material Material => material;

        public Texture Texture => texture;
        public Mesh Mesh => mesh;
        public AvatarWearable Wearable => wearable;
    }
}
