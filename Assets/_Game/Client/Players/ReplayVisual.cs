using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Game.Client.Interactions;

namespace Game.Client.Players
{
    /// <summary>Rendering-only copy. Never instantiates gameplay/network components.</summary>
    public sealed class ReplayVisual : IDisposable
    {
        private readonly Renderer[] originals;
        private readonly Renderer[] copies;
        private readonly bool[] originalVisibility;
        private bool hidden;

        public Transform Target { get; }
        public Animator Animator { get; }

        public ReplayVisual(Transform source, Transform parent)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var transforms = new Dictionary<Transform, Transform>();
            Target = CopyHierarchy(source, parent, transforms);
            Target.SetPositionAndRotation(source.position, source.rotation);
            Target.localScale = source.lossyScale;
            originals = source.GetComponentsInChildren<Renderer>(true);
            originalVisibility = new bool[originals.Length];
            foreach (var original in originals)
            {
                if (!transforms.ContainsKey(original.transform)) continue;
                Renderer copy;
                if (original is SkinnedMeshRenderer skin)
                {
                    var mesh = transforms[skin.transform].gameObject.AddComponent<SkinnedMeshRenderer>();
                    mesh.sharedMesh = skin.sharedMesh;
                    mesh.localBounds = skin.localBounds;
                    mesh.updateWhenOffscreen = true;
                    var bones = skin.bones;
                    for (var i = 0; i < bones.Length; i++)
                        bones[i] = bones[i] != null && transforms.TryGetValue(bones[i], out var bone) ? bone : null;
                    mesh.bones = bones;
                    if (skin.rootBone != null && transforms.TryGetValue(skin.rootBone, out var root))
                        mesh.rootBone = root;
                    copy = mesh;
                }
                else if (original is MeshRenderer && original.TryGetComponent<MeshFilter>(out var filter))
                {
                    var node = transforms[original.transform].gameObject;
                    node.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                    copy = node.AddComponent<MeshRenderer>();
                }
                else continue;
                copy.sharedMaterials = original.sharedMaterials;
                var properties = new MaterialPropertyBlock();
                original.GetPropertyBlock(properties);
                copy.SetPropertyBlock(properties);
                copy.shadowCastingMode = ShadowCastingMode.On;
                copy.receiveShadows = original.receiveShadows;
                copy.enabled = original.enabled;
            }

            var animator = source.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                Animator = transforms[animator.transform].gameObject.AddComponent<Animator>();
                Animator.avatar = animator.avatar;
                Animator.runtimeAnimatorController = animator.runtimeAnimatorController;
                Animator.applyRootMotion = false;
                Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                Animator.keepAnimatorStateOnDisable = true;
                Animator.Rebind();
                Animator.Update(0f);
                Animator.speed = 0f;
            }
            copies = Target.GetComponentsInChildren<Renderer>(true);
            foreach (var copy in copies) copy.forceRenderingOff = true;
            Target.gameObject.SetActive(Animator != null);
        }

        public void SetPlaying(bool playing)
        {
            if (playing && !hidden)
            {
                for (var i = 0; i < originals.Length; i++)
                    if (originals[i] != null) originalVisibility[i] = originals[i].forceRenderingOff;
            }
            if (playing || hidden)
                for (var i = 0; i < originals.Length; i++)
                    if (originals[i] != null) originals[i].forceRenderingOff = playing || originalVisibility[i];
            hidden = playing;
            if (Target == null) return;
            foreach (var copy in copies) copy.forceRenderingOff = !playing;
            Target.gameObject.SetActive(playing || Animator != null);
        }

        public void Dispose()
        {
            SetPlaying(false);
            if (Target == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(Target.gameObject);
            else UnityEngine.Object.DestroyImmediate(Target.gameObject);
        }

        private static Transform CopyHierarchy(Transform source, Transform parent,
            Dictionary<Transform, Transform> transforms)
        {
            var copy = new GameObject(source.name).transform;
            copy.gameObject.layer = source.gameObject.layer;
            copy.SetParent(parent, false);
            copy.localPosition = source.localPosition;
            copy.localRotation = source.localRotation;
            copy.localScale = source.localScale;
            transforms.Add(source, copy);
            foreach (Transform child in source)
            {
                // Held items have their own replay track; do not bake them into the actor's mesh copy.
                if (child.GetComponent<CarryableItem>() != null || child.GetComponent<TMPro.TMP_Text>() != null) continue;
                CopyHierarchy(child, copy, transforms);
            }
            return copy;
        }
    }
}
