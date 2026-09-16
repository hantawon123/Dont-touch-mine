using System;
using Game.Core.Players;
using UnityEngine;

namespace Game.Client.Character
{
    /// <summary>
    /// Dresses one character in an <see cref="AvatarAppearance"/>.
    /// </summary>
    /// <remarks>
    /// Sits on the character rather than in the closet so the same component
    /// serves the preview, the lobby and, later, everyone else's character. The
    /// screen decides what is worn; this decides what that looks like.
    /// <para>
    /// The materials a part is worn as are never instantiated. Swapping in a
    /// material assigns it into <c>sharedMaterials</c>, and a colour or texture
    /// change goes through a <see cref="MaterialPropertyBlock"/>. Touching
    /// <c>Renderer.materials</c> instead would leak a copy on every click, and
    /// a closet is a screen the player clicks a great many times.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed partial class AvatarAppearanceApplier : MonoBehaviour
    {
        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField]
        [Tooltip("The wardrobe this character is dressed out of. The closet " +
                 "reads the same asset to fill its grid.")]
        private AvatarPartCatalog catalog;

        [SerializeField]
        [Tooltip("Which renderer each category dresses. A category with no " +
                 "target here is picked on screen and worn nowhere.")]
        private AvatarPartTarget[] targets = Array.Empty<AvatarPartTarget>();

        [SerializeField] private MeshFilter hoodMesh;
        private Mesh authoredHoodMesh;

        private MaterialPropertyBlock block;
        private Material[][] authoredMaterials;

        /// <summary>What this character is wearing now.</summary>
        public AvatarAppearance Current { get; private set; }

        /// <summary>
        /// Wears the appearance, out of the catalogue on this character.
        /// </summary>
        public void Apply(AvatarAppearance appearance) => Apply(appearance, catalog);

        /// <summary>
        /// Wears the appearance out of a catalogue given here instead.
        /// </summary>
        /// <remarks>
        /// A null catalogue leaves the character in what it was authored with
        /// rather than stripping it: an unwired scene should look like the
        /// model, not like a bug.
        /// </remarks>
        public void Apply(AvatarAppearance appearance, AvatarPartCatalog catalog)
        {
            Current = appearance;
            if (catalog == null)
            {
                return;
            }

            EnsureCaptured();
            if (hoodMesh != null)
            {
                if (authoredHoodMesh == null) authoredHoodMesh = hoodMesh.sharedMesh;
                catalog.TryFind(AvatarPartCategory.Hood, appearance.HoodShapeId, out var hood);
                hoodMesh.sharedMesh = hood?.Mesh != null ? hood.Mesh : authoredHoodMesh;
            }
            for (var index = 0; index < targets.Length; index++)
            {
                var target = targets[index];
                if (target?.Renderer == null)
                {
                    continue;
                }

                catalog.TryFind(target.Category, appearance.Get(target.Category), out var part);
                Wear(target, authoredMaterials[index], part);
            }
            ApplyWearables(appearance, catalog);
        }

        private void Wear(AvatarPartTarget target, Material[] authored, AvatarPart part)
        {
            var renderer = target.Renderer;
            var slot = Mathf.Clamp(target.MaterialIndex, 0, Mathf.Max(0, authored.Length - 1));
            if (authored.Length == 0)
            {
                return;
            }

            // Always from the authored material rather than from whatever the
            // last part left behind, so taking a part off is the same operation
            // as putting a different one on.
            var materials = renderer.sharedMaterials;
            if (materials.Length == authored.Length)
            {
                materials[slot] = part?.Material != null ? part.Material : authored[slot];
                renderer.sharedMaterials = materials;
            }

            block ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block, slot);
            block.Clear();

            if (part != null && part.Texture != null)
            {
                block.SetTexture(BaseMap, part.Texture);
                // Texture parts still need an explicit neutral tint. This
                // clears a stale dark _BaseColor left by a combat tint or an
                // imported FBX material while preserving the authored texture.
                block.SetColor(BaseColor, Color.white);
            }

            if (part != null && part.Material == null && part.Texture == null)
            {
                block.SetColor(BaseColor, part.Swatch);
            }

            renderer.SetPropertyBlock(block, slot);
        }

        /// <summary>
        /// Remembers what the model came dressed in, once.
        /// </summary>
        /// <remarks>
        /// Read on first use rather than in <c>Awake</c> because the preview
        /// character is spawned and dressed in the same frame, and a disabled
        /// object has not had <c>Awake</c> called on it yet.
        /// </remarks>
        private void EnsureCaptured()
        {
            if (authoredMaterials != null && authoredMaterials.Length == targets.Length)
            {
                return;
            }

            authoredMaterials = new Material[targets.Length][];
            for (var index = 0; index < targets.Length; index++)
            {
                var renderer = targets[index]?.Renderer;
                authoredMaterials[index] = renderer != null
                    ? renderer.sharedMaterials
                    : Array.Empty<Material>();
            }
        }
    }

    /// <summary>Where one category is worn.</summary>
    [Serializable]
    public sealed class AvatarPartTarget
    {
        [SerializeField]
        private AvatarPartCategory category;

        [SerializeField]
        private Renderer targetRenderer;

        [SerializeField]
        [Tooltip("Which of the renderer's material slots this category owns.")]
        private int materialIndex;

        public AvatarPartCategory Category => category;

        public Renderer Renderer => targetRenderer;

        public int MaterialIndex => materialIndex;
    }
}
