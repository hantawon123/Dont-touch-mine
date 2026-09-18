using System.Collections.Generic;
using Game.Core.Players;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Game.Client.Character
{
    /// <summary>
    /// Renders a character's face into a square texture and clips it to the
    /// circular profile slot it is bound to.
    /// </summary>
    public static class AvatarFacePortrait
    {
        public const string PortraitName = "Portrait";
        public const int TextureSize = 256;
        public const float FramePadding = 1.08f;
        private const string PreviewLayerName = "Item Preview";
        private static readonly Vector3 StagePosition = new(0f, -2800f, 0f);

        private static readonly Dictionary<string, RenderTexture> cache = new();
        private static GameObject stage;
        private static AvatarAppearanceApplier character;
        private static Camera camera;
        private static AvatarAppearance lastRendered;
        private static bool hasLastRendered;

        public static void Bind(RectTransform circle, AvatarAppearance appearance)
        {
            if (circle == null)
            {
                return;
            }

            EnsureCircleMask(circle);
            var portrait = EnsurePortrait(circle);
            if (!TryRender(appearance, out var texture))
            {
                portrait.enabled = false;
                SetMaskGraphicVisible(circle, true);
                return;
            }

            var image = circle.GetComponent<Image>();
            if (image != null)
            {
                image.color = Color.white;
            }

            SetMaskGraphicVisible(circle, false);
            portrait.texture = texture;
            portrait.color = Color.white;
            portrait.enabled = true;
        }

        public static void Clear(RectTransform circle)
        {
            if (circle == null)
            {
                return;
            }

            var portrait = circle.Find(PortraitName)?.GetComponent<RawImage>();
            if (portrait != null)
            {
                portrait.enabled = false;
                portrait.texture = null;
            }

            SetMaskGraphicVisible(circle, true);
        }

        internal static float OrthographicSizeForHead(Vector3 extents)
        {
            var radius = Mathf.Max(extents.x, Mathf.Max(extents.y, extents.z), 0.08f);
            return radius * FramePadding;
        }

        private static bool TryRender(AvatarAppearance appearance, out RenderTexture texture)
        {
            texture = null;
            if (!Application.isPlaying)
            {
                return false;
            }

            var key = appearance.ToString();
            if (cache.TryGetValue(key, out texture) && texture != null)
            {
                return true;
            }

            if (!EnsureStage())
            {
                return false;
            }

            if (!hasLastRendered || lastRendered != appearance)
            {
                character.Apply(character.ResolvePlayerAppearance(appearance));
                lastRendered = appearance;
                hasLastRendered = true;
            }

            FrameHead();
            texture = CreateTexture(key);
            Submit(camera, texture);
            cache[key] = texture;
            return true;
        }

        private static bool EnsureStage()
        {
            if (stage != null && character != null && camera != null)
            {
                return true;
            }

            var prefab = Resources.Load<GameObject>("AvatarPreview");
            if (prefab == null)
            {
                return false;
            }

            stage = new GameObject("AvatarFacePortraitStage");
            Object.DontDestroyOnLoad(stage);
            stage.transform.position = StagePosition;
            var instance = Object.Instantiate(prefab, stage.transform);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            ApplyPreviewLayer(stage);
            character = instance.GetComponent<AvatarAppearanceApplier>() ??
                        instance.GetComponentInChildren<AvatarAppearanceApplier>(true);
            if (character == null)
            {
                Object.Destroy(stage);
                stage = null;
                return false;
            }

            var cameraObject = new GameObject("FaceCamera");
            cameraObject.transform.SetParent(stage.transform, false);
            camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 12f;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.cullingMask = PreviewLayerMask;
            camera.aspect = 1f;
            cameraObject.AddComponent<AvatarPreviewLighting>();
            ApplyPreviewLayer(cameraObject);
            return true;
        }

        private static void FrameHead()
        {
            var head = FindNamed(character.transform, "Head") ?? character.transform;
            var bounds = Encapsulate(head);
            if (bounds.size.sqrMagnitude < 0.0001f)
            {
                bounds = Encapsulate(character.transform);
            }

            camera.orthographicSize = OrthographicSizeForHead(bounds.extents);
            camera.transform.position = bounds.center + Vector3.forward * (camera.orthographicSize * 6f + 1.2f);
            camera.transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
        }

        private static RenderTexture CreateTexture(string key)
        {
            return new RenderTexture(TextureSize, TextureSize, 16, RenderTextureFormat.ARGB32)
            {
                name = $"AvatarFace {key}",
                antiAliasing = 1,
                filterMode = FilterMode.Bilinear
            };
        }

        private static void Submit(Camera previewCamera, RenderTexture destination)
        {
            previewCamera.targetTexture = destination;
            var request = new RenderPipeline.StandardRequest { destination = destination };
            if (RenderPipeline.SupportsRenderRequest(previewCamera, request))
            {
                previewCamera.SubmitRenderRequest(request);
                return;
            }

            previewCamera.Render();
        }

        private static void EnsureCircleMask(RectTransform circle)
        {
            var image = circle.GetComponent<Image>();
            if (image == null)
            {
                image = circle.gameObject.AddComponent<Image>();
                image.sprite = Game.Client.Home.HomeUiFonts.CircleSprite;
                image.color = new Color(0.62f, 0.62f, 0.62f, 1f);
                image.raycastTarget = false;
            }

            var mask = circle.GetComponent<Mask>();
            if (mask == null)
            {
                mask = circle.gameObject.AddComponent<Mask>();
                mask.showMaskGraphic = true;
            }
        }

        private static void SetMaskGraphicVisible(RectTransform circle, bool visible)
        {
            var mask = circle.GetComponent<Mask>();
            if (mask != null)
            {
                mask.showMaskGraphic = visible;
            }
        }

        private static RawImage EnsurePortrait(RectTransform circle)
        {
            var child = circle.Find(PortraitName) as RectTransform;
            if (child == null)
            {
                var created = new GameObject(PortraitName, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                child = created.GetComponent<RectTransform>();
                child.SetParent(circle, false);
            }

            child.anchorMin = Vector2.zero;
            child.anchorMax = Vector2.one;
            child.offsetMin = Vector2.zero;
            child.offsetMax = Vector2.zero;
            var portrait = child.GetComponent<RawImage>();
            if (portrait == null)
            {
                portrait = child.gameObject.AddComponent<RawImage>();
            }

            portrait.raycastTarget = false;
            return portrait;
        }

        private static Bounds Encapsulate(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var bounds = new Bounds(root.position, Vector3.zero);
            var hasBounds = false;
            for (var index = 0; index < renderers.Length; index++)
            {
                var renderer = renderers[index];
                if (!renderer.enabled || renderer is ParticleSystemRenderer)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                    continue;
                }

                bounds.Encapsulate(renderer.bounds);
            }

            return bounds;
        }

        private static Transform FindNamed(Transform root, string name)
        {
            var all = root.GetComponentsInChildren<Transform>(true);
            for (var index = 0; index < all.Length; index++)
            {
                if (string.Equals(all[index].name, name, System.StringComparison.Ordinal))
                {
                    return all[index];
                }
            }

            return null;
        }

        private static void ApplyPreviewLayer(GameObject target)
        {
            var layer = PreviewLayer;
            foreach (var transform in target.GetComponentsInChildren<Transform>(true))
            {
                transform.gameObject.layer = layer;
            }
        }

        private static int PreviewLayer
        {
            get
            {
                var layer = LayerMask.NameToLayer(PreviewLayerName);
                return layer >= 0 ? layer : 31;
            }
        }

        private static int PreviewLayerMask
        {
            get
            {
                var layer = PreviewLayer;
                return layer >= 0 ? 1 << layer : ~0;
            }
        }
    }
}
