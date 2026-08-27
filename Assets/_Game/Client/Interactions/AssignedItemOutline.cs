using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Client.Interactions
{
    /// <summary>Draws a red inverted-hull outline without changing item materials.</summary>
    [DisallowMultipleComponent]
    public sealed class AssignedItemOutline : MonoBehaviour
    {
        private const string ShaderResourceName = "AssignedItemOutline";

        [SerializeField]
        private Color color = new(0.9f, 0.05f, 0.05f, 1f);

        [SerializeField, Range(0.001f, 0.05f)]
        private float width = 0.012f;

        private readonly List<Renderer> outlineRenderers = new();
        private Material outlineMaterial;
        private bool built;

        public bool IsVisible { get; private set; }

        public void SetVisible(bool visible)
        {
            if (visible)
            {
                EnsureBuilt();
            }

            IsVisible = visible;
            for (var index = 0; index < outlineRenderers.Count; index++)
            {
                outlineRenderers[index].enabled = visible;
            }
        }

        private void EnsureBuilt()
        {
            if (built)
            {
                return;
            }

            built = true;
            var shader = Resources.Load<Shader>(ShaderResourceName);
            if (shader == null)
            {
                Debug.LogError(
                    $"Assigned-item outline shader resource '{ShaderResourceName}' is missing.",
                    this);
                return;
            }

            outlineMaterial = new Material(shader)
            {
                name = $"{name} Assigned Outline (Runtime)",
                hideFlags = HideFlags.DontSave,
            };
            outlineMaterial.SetColor("_OutlineColor", color);
            outlineMaterial.SetFloat("_OutlineWidth", width);

            var sources = GetComponentsInChildren<Renderer>(includeInactive: true);
            for (var index = 0; index < sources.Length; index++)
            {
                switch (sources[index])
                {
                    case MeshRenderer meshRenderer:
                        CreateMeshOutline(meshRenderer);
                        break;
                    case SkinnedMeshRenderer skinnedRenderer:
                        CreateSkinnedOutline(skinnedRenderer);
                        break;
                }
            }
        }

        private void CreateMeshOutline(MeshRenderer source)
        {
            var sourceFilter = source.GetComponent<MeshFilter>();
            if (sourceFilter == null || sourceFilter.sharedMesh == null)
            {
                return;
            }

            var child = CreateOutlineChild(source.transform);
            child.AddComponent<MeshFilter>().sharedMesh = sourceFilter.sharedMesh;
            var renderer = child.AddComponent<MeshRenderer>();
            Configure(renderer, source.sharedMaterials.Length);
        }

        private void CreateSkinnedOutline(SkinnedMeshRenderer source)
        {
            if (source.sharedMesh == null)
            {
                return;
            }

            var child = CreateOutlineChild(source.transform);
            var renderer = child.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = source.sharedMesh;
            renderer.rootBone = source.rootBone;
            renderer.bones = source.bones;
            renderer.localBounds = source.localBounds;
            renderer.updateWhenOffscreen = source.updateWhenOffscreen;
            Configure(renderer, source.sharedMaterials.Length);
        }

        private GameObject CreateOutlineChild(Transform parent)
        {
            var child = new GameObject("[Assigned Item Outline]")
            {
                layer = parent.gameObject.layer,
                hideFlags = HideFlags.DontSave,
            };
            child.transform.SetParent(parent, worldPositionStays: false);
            return child;
        }

        private void Configure(Renderer renderer, int materialCount)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            var materials = new Material[Mathf.Max(1, materialCount)];
            for (var index = 0; index < materials.Length; index++)
            {
                materials[index] = outlineMaterial;
            }

            renderer.sharedMaterials = materials;
            renderer.enabled = false;
            outlineRenderers.Add(renderer);
        }

        private void OnDestroy()
        {
            if (outlineMaterial == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(outlineMaterial);
            }
            else
            {
                DestroyImmediate(outlineMaterial);
            }
        }
    }
}
