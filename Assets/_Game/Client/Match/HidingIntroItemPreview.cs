using System;
using Game.Client.Interactions;
using Game.Core.Items;
using Game.SOAP.Config;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Game.Client.Match
{
    /// <summary>
    /// Renders the catalog prefab onto a RawImage for the hiding briefing
    /// and the destroyed-items HUD.
    /// </summary>
    internal sealed class HidingIntroItemPreview
    {
        public const string IntroSlotName = "ItemPreview";
        public const float IntroImageSize = 360f;
        public const float IntroCenterOffsetY = 118f;
        private const string PreviewLayerName = "Item Preview";
        private const float RotationDegreesPerSecond = 28f;
        private static readonly Vector3 StagePosition = new(0f, -2500f, 0f);

        private readonly RawImage target;
        private readonly int textureSize;
        private readonly Color backgroundColor;
        private readonly Vector3 stageOffset;
        private readonly bool rotates;
        private GameObject stage;
        private Transform model;
        private Camera camera;
        private Light light;
        private RenderTexture texture;
        private Texture2D grayscaleTexture;
        private PreviewSpin spin;

        public HidingIntroItemPreview(
            RawImage target,
            int textureSize = 512,
            Color? backgroundColor = null,
            Vector3? stageOffset = null,
            bool rotates = true)
        {
            this.target = target;
            this.textureSize = Mathf.Clamp(textureSize, 64, 512);
            this.backgroundColor = backgroundColor ?? Color.black;
            this.stageOffset = stageOffset ?? Vector3.zero;
            this.rotates = rotates;
        }

        public bool HasPreview => target != null && target.enabled && target.texture != null;

        public static RawImage EnsureIntroSlot(Transform introRoot)
        {
            if (introRoot == null)
            {
                return null;
            }

            var existing = introRoot.Find(IntroSlotName)?.GetComponent<RawImage>();
            if (existing == null)
            {
                var slot = new GameObject(
                    IntroSlotName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(RawImage));
                slot.transform.SetParent(introRoot, false);
                existing = slot.GetComponent<RawImage>();
                existing.raycastTarget = false;
            }

            existing.color = Color.white;
            existing.uvRect = new Rect(0f, 0f, 1f, 1f);
            PlaceIntroSlot((RectTransform)existing.transform);
            return existing;
        }

        private static void PlaceIntroSlot(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, IntroCenterOffsetY);
            rect.sizeDelta = new Vector2(IntroImageSize, IntroImageSize);
        }

        public void Show(string itemId)
        {
            Clear();
            if (target == null)
            {
                return;
            }

            if (!TryResolveVisual(itemId, out var source, out var copyRootPose))
            {
                target.enabled = false;
                return;
            }

            EnsureStage();
            model = CopyVisuals(source, stage.transform, copyRootPose);
            if (model == null)
            {
                target.enabled = false;
                return;
            }

            FitCamera(model);
            target.texture = texture;
            target.enabled = true;
            stage.SetActive(true);
            BindSpin();
            camera.Render();
        }

        public void SetGrayscale(bool enabled)
        {
            if (target == null || texture == null)
            {
                return;
            }

            if (!enabled)
            {
                target.texture = texture;
                return;
            }

            if (grayscaleTexture == null)
            {
                grayscaleTexture = CreateGrayscaleCopy();
            }

            if (grayscaleTexture != null)
            {
                target.texture = grayscaleTexture;
            }
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

            if (grayscaleTexture != null)
            {
                UnityEngine.Object.Destroy(grayscaleTexture);
                grayscaleTexture = null;
            }

            if (spin != null)
            {
                spin.Bind(null, null);
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
            stage.transform.position = StagePosition + stageOffset;
            ApplyPreviewLayer(stage);

            texture = new RenderTexture(textureSize, textureSize, 16)
            {
                name = "Hiding Intro Preview",
                antiAliasing = 2
            };

            var cameraObject = new GameObject("Preview Camera");
            cameraObject.transform.SetParent(stage.transform, false);
            ApplyPreviewLayer(cameraObject);
            camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = backgroundColor;
            camera.orthographic = true;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 20f;
            camera.targetTexture = texture;
            camera.cullingMask = PreviewLayerMask;
            camera.depth = -100;
            camera.allowHDR = false;
            camera.allowMSAA = false;

            // Studio lights live on the offscreen stage. Item prefabs stay unlit
            // so in-world props are not changed by the HUD/intro preview.
            // Point lights only: extra directionals can steal URP's main light.
            light = AddStageLight(
                "Preview Key Light",
                LightType.Point,
                5.5f,
                new Color(1f, 0.98f, 0.94f),
                new Vector3(-1.1f, 2.1f, -1.6f),
                Quaternion.identity);
            light.range = 20f;
            var fill = AddStageLight(
                "Preview Fill Light",
                LightType.Point,
                2.6f,
                new Color(0.82f, 0.88f, 1f),
                new Vector3(1.8f, 1.1f, 1.5f),
                Quaternion.identity);
            fill.range = 20f;

            if (rotates)
            {
                spin = stage.GetComponent<PreviewSpin>() ?? stage.AddComponent<PreviewSpin>();
            }
        }

        private void BindSpin()
        {
            if (spin == null)
            {
                return;
            }

            spin.Bind(model, camera);
        }

        private sealed class PreviewSpin : MonoBehaviour
        {
            private Transform model;
            private Camera previewCamera;

            public void Bind(Transform model, Camera previewCamera)
            {
                this.model = model;
                this.previewCamera = previewCamera;
                enabled = model != null && previewCamera != null;
            }

            private void LateUpdate()
            {
                if (model == null || previewCamera == null)
                {
                    return;
                }

                model.Rotate(Vector3.up, RotationDegreesPerSecond * Time.unscaledDeltaTime, Space.World);
                previewCamera.Render();
            }
        }

        private static int PreviewLayer
        {
            get
            {
                var layer = LayerMask.NameToLayer(PreviewLayerName);
                return layer >= 0 ? layer : 0;
            }
        }

        private static int PreviewLayerMask
        {
            get
            {
                var layer = LayerMask.NameToLayer(PreviewLayerName);
                return layer >= 0 ? 1 << layer : ~0;
            }
        }

        private static void ApplyPreviewLayer(GameObject target)
        {
            if (target != null)
            {
                target.layer = PreviewLayer;
            }
        }

        private static Transform CopyVisuals(Transform source, Transform parent, bool copyRootPose)
        {
            var root = new GameObject("Preview Model");
            root.transform.SetParent(parent, false);
            ApplyPreviewLayer(root);
            CopyVisualRecursive(source, root.transform, copyRootPose);
            if (root.GetComponentInChildren<Renderer>() == null)
            {
                UnityEngine.Object.Destroy(root);
                return null;
            }

            return root.transform;
        }

        private static void CopyVisualRecursive(Transform source, Transform dest, bool copyLocalPose)
        {
            dest.gameObject.SetActive(source.gameObject.activeSelf);
            ApplyPreviewLayer(dest.gameObject);
            if (copyLocalPose)
            {
                dest.localPosition = source.localPosition;
                dest.localRotation = source.localRotation;
                dest.localScale = source.localScale;
            }

            var filter = source.GetComponent<MeshFilter>();
            var renderer = source.GetComponent<MeshRenderer>();
            if (filter != null &&
                renderer != null &&
                filter.sharedMesh != null &&
                !ItemOutlineRenderers.IsGenerated(renderer))
            {
                dest.gameObject.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var copy = dest.gameObject.AddComponent<MeshRenderer>();
                copy.sharedMaterials = renderer.sharedMaterials;
                copy.enabled = renderer.enabled;
                copy.shadowCastingMode = ShadowCastingMode.Off;
                copy.receiveShadows = false;
                copy.lightProbeUsage = LightProbeUsage.Off;
                copy.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }

            foreach (Transform child in source)
            {
                if (IsGeneratedOutline(child))
                {
                    continue;
                }

                var childCopy = new GameObject(child.name);
                childCopy.transform.SetParent(dest, false);
                CopyVisualRecursive(child, childCopy.transform, copyLocalPose: true);
            }
        }

        private static bool IsGeneratedOutline(Transform target)
        {
            var renderer = target.GetComponent<Renderer>();
            if (renderer != null)
            {
                return ItemOutlineRenderers.IsGenerated(renderer);
            }

            return target.name.StartsWith("[", StringComparison.Ordinal) &&
                   target.name.Contains("Outline");
        }

        private Light AddStageLight(
            string name,
            LightType type,
            float intensity,
            Color color,
            Vector3 localPosition,
            Quaternion localRotation)
        {
            var lightObject = new GameObject(name);
            lightObject.transform.SetParent(stage.transform, false);
            ApplyPreviewLayer(lightObject);
            lightObject.transform.localPosition = localPosition;
            lightObject.transform.localRotation = localRotation;
            var stageLight = lightObject.AddComponent<Light>();
            stageLight.type = type;
            stageLight.intensity = intensity;
            stageLight.color = color;
            stageLight.shadows = LightShadows.None;
            stageLight.cullingMask = PreviewLayerMask;
            return stageLight;
        }

        private void FitCamera(Transform preview)
        {
            var stageOrigin = stage != null ? stage.transform.position : StagePosition;
            var bounds = Encapsulate(preview);
            preview.position -= bounds.center - stageOrigin;
            bounds = Encapsulate(preview);

            var radius = Mathf.Max(0.12f, bounds.extents.magnitude);
            camera.transform.position = bounds.center + new Vector3(0.55f, 0.4f, -1f).normalized * (radius * 2.6f);
            camera.transform.LookAt(bounds.center);
            camera.orthographicSize = Mathf.Max(bounds.extents.x, bounds.extents.y) * 1.2f + 0.04f;
        }

        private static Bounds Encapsulate(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            Bounds? bounds = null;
            for (var index = 0; index < renderers.Length; index++)
            {
                if (!renderers[index].enabled)
                {
                    continue;
                }

                if (bounds == null)
                {
                    bounds = renderers[index].bounds;
                    continue;
                }

                var current = bounds.Value;
                current.Encapsulate(renderers[index].bounds);
                bounds = current;
            }

            return bounds ?? new Bounds(root.position, Vector3.one * 0.2f);
        }

        private Texture2D CreateGrayscaleCopy()
        {
            if (camera != null)
            {
                camera.Render();
            }

            var copy = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear
            };
            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = texture;
                copy.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                var pixels = copy.GetPixels32();
                for (var index = 0; index < pixels.Length; index++)
                {
                    var pixel = pixels[index];
                    var gray = (byte)(((pixel.r * 77) + (pixel.g * 150) + (pixel.b * 29)) >> 8);
                    pixels[index] = new Color32(gray, gray, gray, pixel.a);
                }

                copy.SetPixels32(pixels);
                copy.Apply(false, false);
            }
            finally
            {
                RenderTexture.active = previous;
            }

            return copy;
        }

        private static bool TryResolveVisual(string itemId, out Transform source, out bool copyRootPose)
        {
            var catalog = Resources.Load<ItemCatalogSO>(ItemCatalogSO.ResourcePath);
            var prefab = catalog != null ? catalog.PrefabOf(itemId) : null;
            if (prefab != null)
            {
                source = prefab.transform;
                copyRootPose = true;
                return true;
            }

            var sceneItem = FindSceneItem(itemId);
            if (sceneItem != null)
            {
                source = sceneItem.transform;
                copyRootPose = false;
                return true;
            }

            source = null;
            copyRootPose = false;
            return false;
        }

        private static CarryableItem FindSceneItem(string itemId)
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
