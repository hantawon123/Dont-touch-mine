using System;
using System.Collections.Generic;
using Game.Core.Players;
using UnityEngine;

namespace Game.Client.Character
{
    public sealed partial class AvatarAppearanceApplier
    {
        [SerializeField] private Transform wearableRig;
        [SerializeField] private Renderer[] defaultEyes = Array.Empty<Renderer>();
        private readonly Dictionary<AvatarWearable, MeshRenderer[]> wearableCache = new();
        private readonly Dictionary<string, Transform> wearableBones = new();
        private bool[] defaultEyeVisibility;

        private void ApplyWearables(AvatarAppearance appearance, AvatarPartCatalog source)
        {
            if (wearableRig == null) return;
            if (defaultEyeVisibility == null)
            {
                defaultEyeVisibility = new bool[defaultEyes.Length];
                for (int i = 0; i < defaultEyes.Length; i++)
                    defaultEyeVisibility[i] = defaultEyes[i] != null && defaultEyes[i].enabled;
                foreach (var bone in wearableRig.GetComponentsInChildren<Transform>(true))
                    wearableBones.TryAdd(bone.name, bone);
            }
            source.TryFind(AvatarPartCategory.Face, appearance.FaceId, out var face);
            source.TryFind(AvatarPartCategory.Shoes, appearance.ShoesId, out var shoes);
            source.TryFind(AvatarPartCategory.BodyColor, appearance.BodyColorId, out var body);
            var bodyColor = body?.Swatch ?? new Color32(243, 214, 70, 255);
            foreach (var pair in wearableCache)
                foreach (var renderer in pair.Value) if (renderer != null) renderer.gameObject.SetActive(false);
            bool dressedFace = ShowWearable(face?.Wearable, bodyColor, Color.white);
            ShowWearable(shoes?.Wearable, bodyColor, shoes?.Swatch ?? Color.white);
            for (int i = 0; i < defaultEyes.Length; i++)
                if (defaultEyes[i] != null) defaultEyes[i].enabled = !dressedFace && defaultEyeVisibility[i];
        }

        private bool ShowWearable(AvatarWearable wearable, Color body, Color shoe)
        {
            if (wearable == null || wearable.attachments == null || wearable.attachments.Length == 0) return false;
            if (!wearableCache.TryGetValue(wearable, out var renderers))
            {
                // Validate every binding before creating anything so a bad asset cannot hide the default eyes.
                foreach (var part in wearable.attachments)
                    if (part == null || part.mesh == null || string.IsNullOrEmpty(part.bone) ||
                        !wearableBones.ContainsKey(part.bone)) return false;
                renderers = new MeshRenderer[wearable.attachments.Length];
                for (int i = 0; i < renderers.Length; i++)
                {
                    var part = wearable.attachments[i];
                    var go = new GameObject("Wearable_" + wearable.name + "_" + part.name);
                    go.layer = wearableRig.gameObject.layer;
                    go.transform.SetParent(wearableBones[part.bone], false);
                    go.AddComponent<MeshFilter>().sharedMesh = part.mesh;
                    renderers[i] = go.AddComponent<MeshRenderer>();
                    renderers[i].sharedMaterials = part.materials;
                }
                wearableCache.Add(wearable, renderers);
            }
            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                renderer.gameObject.SetActive(true);
                var part = wearable.attachments[i];
                for (int slot = 0; slot < part.materials.Length; slot++)
                {
                    block ??= new MaterialPropertyBlock(); block.Clear();
                    int mode = part.tintModes != null && slot < part.tintModes.Length ? part.tintModes[slot] : 0;
                    if (mode != 0) block.SetColor(BaseColor, mode == 1 ? body : mode == 3 ? shoe * new Color(.81f,.85f,.9f,1) : shoe);
                    renderer.SetPropertyBlock(block, slot);
                }
            }
            return true;
        }

        private void OnDestroy()
        {
            foreach (var renderers in wearableCache.Values)
                foreach (var renderer in renderers)
                    if (renderer != null)
                    {
                        if (Application.isPlaying) Destroy(renderer.gameObject);
                        else DestroyImmediate(renderer.gameObject);
                    }
            wearableCache.Clear();
        }
    }
}
