using System;
using Game.Client.Interactions;
using Game.Core.Items;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Match
{
    /// <summary>
    /// Renders the assigned carryable's mesh onto a RawImage so the hiding
    /// briefing can show the object itself, not only its name.
    /// </summary>
    internal sealed class HidingIntroItemPreview
    {
        private const float RotationDegreesPerSecond = 28f;
        private static readonly Vector3 StagePosition = new(0f, -2500f, 0f);

        private readonly RawImage target;
        private GameObject stage;
        private Transform model;
        private Camera camera;
        private Light light;
        private RenderTexture texture;

        public HidingIntroItemPreview(RawImage target)
        {
            this.target = target;
        }

        public void Show(string itemId)
        {
            Clear();
            if (target == null)
            {
                return;
            }

            var source = FindSource(itemId);
            if (source == null)
            {
                target.enabled = false;
                return;
            }

            EnsureStage();
            model = CopyVisuals(source.transform, stage.transform);
            if (model == null)
            {
                target.enabled = false;
                return;
            }

            FitCamera(model);
            target.texture = texture;
            target.enabled = true;
            stage.SetActive(true);
        }

        public void Tick(float deltaTime)
        {
            if (model == null)
            {
                return;
            }

            model.Rotate(Vector3.up, RotationDegreesPerSecond * deltaTime, Space.World);
        }

        public void Dispose()
        {
            Clear();
            if (stage != null)
            {
                UnityEngine.Object.Destroy(stage);
                stage = null;
                camera = null;
                light = null;
            }
        }

        public void Clear()
        {
            if (target != null)
            {
                target.texture = null;
                target.enabled = false;
            }

            if (model != null)
            {
                UnityEngine.Object.Destroy(model.gameObject);
                model = null;
            }

            if (texture != null)
            {
                if (camera != null)
                {
                    camera.targetTexture = null;
                }

                texture.Release();
                UnityEngine.Object.Destroy(texture);
                texture = null;
            }

            if (stage != null)
            {
                stage.SetActive(false);
            }
        }

        private void EnsureStage()
        {
            if (stage != null)
            {
                return;
            }

            stage = new GameObject("Hiding Intro Preview Stage");
            UnityEngine.Object.DontDestroyOnLoad(stage);
            stage.transform.position = StagePosition;

            texture = new RenderTexture(512, 512, 16)
            {
                name = "Hiding Intro Preview",
                antiAliasing = 2
            };

            var cameraObject = new GameObject("Preview Camera");
            cameraObject.transform.SetParent(stage.transform, false);
            camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.orthographic = true;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 20f;
            camera.targetTexture = texture;
            camera.cullingMask = ~0;
            camera.depth = -100;
            camera.allowHDR = false;
            camera.allowMSAA = false;

            var lightObject = new GameObject("Preview Light");
            lightObject.transform.SetParent(stage.transform, false);
            lightObject.transform.localPosition = new Vector3(-1.2f, 2f, -1.5f);
            light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 12f;
            light.intensity = 2.4f;
            light.color = Color.white;
        }

        private static Transform CopyVisuals(Transform source, Transform parent)
        {
            var root = new GameObject("Preview Model");
            root.transform.SetParent(parent, false);
            CopyVisualRecursive(source, root.transform);
            if (root.GetComponentInChildren<Renderer>() == null)
            {
                UnityEngine.Object.Destroy(root);
                return null;
            }

            return root.transform;
        }

        private static void CopyVisualRecursive(Transform source, Transform dest)
        {
            dest.localPosition = source.localPosition;
            dest.localRotation = source.localRotation;
            dest.localScale = source.localScale;

            var filter = source.GetComponent<MeshFilter>();
            var renderer = source.GetComponent<MeshRenderer>();
            if (filter != null && renderer != null && filter.sharedMesh != null)
            {
                dest.gameObject.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var copy = dest.gameObject.AddComponent<MeshRenderer>();
                copy.sharedMaterials = renderer.sharedMaterials;
            }

            foreach (Transform child in source)
            {
                var childCopy = new GameObject(child.name);
                childCopy.transform.SetParent(dest, false);
                CopyVisualRecursive(child, childCopy.transform);
            }
        }

        private void FitCamera(Transform preview)
        {
            var bounds = Encapsulate(preview);
            preview.position -= bounds.center - StagePosition;
            bounds = Encapsulate(preview);

            var radius = Mathf.Max(0.12f, bounds.extents.magnitude);
            camera.transform.position = bounds.center + new Vector3(0.55f, 0.4f, -1f).normalized * (radius * 2.6f);
            camera.transform.LookAt(bounds.center);
            camera.orthographicSize = Mathf.Max(bounds.extents.x, bounds.extents.y) * 1.2f + 0.04f;
        }

        private static Bounds Encapsulate(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return new Bounds(root.position, Vector3.one * 0.2f);
            }

            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            return bounds;
        }

        private static CarryableItem FindSource(string itemId)
        {
            var visualId = ItemCatalog.VisualSourceIdOf(itemId);
            if (string.IsNullOrEmpty(visualId) && string.IsNullOrEmpty(itemId))
            {
                return null;
            }

            CarryableItem assigned = null;
            var items = UnityEngine.Object.FindObjectsByType<CarryableItem>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (var index = 0; index < items.Length; index++)
            {
                var item = items[index];
                if (!string.IsNullOrEmpty(visualId) &&
                    string.Equals(item.ObjectId, visualId, StringComparison.Ordinal))
                {
                    return item;
                }

                if (assigned == null &&
                    string.Equals(item.ObjectId, itemId, StringComparison.Ordinal))
                {
                    assigned = item;
                }
            }

            return assigned;
        }
    }
}
