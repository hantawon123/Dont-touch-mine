using System.Collections.Generic;
using Game.Core.Players;
using UnityEngine;
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

        private static readonly Dictionary<string, Texture2D> cache = new();
        private static GameObject stage;
        private static AvatarAppearanceApplier character;
        private static Camera camera;
        private static RenderTexture scratch;

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

            if (portrait.texture == texture && portrait.enabled)
            {
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

        private static bool TryRender(AvatarAppearance appearance, out Texture texture)
        {
            texture = null;
            // Headless peers have no render target; retain the existing fallback icon.
            if (!Application.isPlaying || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                return false;
            }

            var key = appearance.ToString();
            if (cache.TryGetValue(key, out var still) && still != null)
            {
                texture = still;
                return true;
            }

            if (!EnsureStage())
            {
                return false;
            }

            character.Apply(character.ResolvePlayerAppearance(appearance));
            FreezePose();
            FrameHead();
            Submit(camera, ScratchTexture);
            still = CaptureStill(ScratchTexture, key);
            if (still == null)
            {
                return false;
            }

            cache[key] = still;
            texture = still;
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

            FreezePose();

            var cameraObject = new GameObject("FaceCamera");
            cameraObject.transform.SetParent(stage.transform, false);
            camera = cameraObject.AddComponent<Camera>();
            // Bind/Submit render explicitly. An enabled Base camera with no RT
            // survives into the match and clears the game view to black.
            camera.enabled = false;
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 12f;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.useOcclusionCulling = false;
            camera.cullingMask = PreviewLayerMask;
            camera.aspect = 1f;
            camera.depth = -100;
            camera.targetTexture = ScratchTexture;
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

        private static void FreezePose()
        {
            if (character == null)
            {
                return;
            }

            var animators = character.GetComponentsInChildren<Animator>(true);
            for (var index = 0; index < animators.Length; index++)
            {
                var animator = animators[index];
                animator.applyRootMotion = false;
                animator.speed = 0f;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                if (animator.enabled && animator.gameObject.activeInHierarchy)
                {
                    animator.Update(0f);
                }

                animator.enabled = false;
            }
        }

        private static void Submit(Camera previewCamera, RenderTexture destination)
        {
            previewCamera.enabled = false;
            previewCamera.targetTexture = destination;
            previewCamera.Render();
            previewCamera.targetTexture = ScratchTexture;
        }

        private static Texture2D CaptureStill(RenderTexture source, string key)
        {
            if (source == null)
            {
                return null;
            }

            var previous = RenderTexture.active;
            RenderTexture.active = source;
            var still = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false)
            {
                name = $"AvatarFace {key}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            still.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0, false);
            still.Apply(false, false);
            RenderTexture.active = previous;
            return still;
        }

        private static RenderTexture ScratchTexture
        {
            get
            {
                if (scratch != null)
                {
                    return scratch;
                }

                scratch = CreateTexture("_scratch");
                return scratch;
            }
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
