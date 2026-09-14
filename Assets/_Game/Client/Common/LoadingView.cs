using System;
using Game.Client.Home;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Common
{
    public interface ILoadingView
    {
        bool IsPresented { get; }
        void Show();
        void Hide();
        void HideImmediate();
    }

    /// <summary>
    /// Full-screen loading cover: <c>BG_Loading</c> fitted to the canvas width,
    /// with the Loading copy sitting under the art. Timing belongs to the
    /// presenter; this view only paints.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class LoadingView : MonoBehaviour, ILoadingView
    {
        public const string LabelText = "Loading...";
        public const float LabelFontSize = 26f;
        public const float LabelGap = 16f;
        public const int SortingOrder = 3000;
        public const string GraphicAssetPath = "Assets/_Game/Content/Resources/UI/Loading/BG_Loading.png";
        public const string GraphicResource = "UI/Loading/BG_Loading";
        public const float FallbackAspectRatio = 1920f / 861f;

        public const float BounceHeight = 10f;
        public const float LetterSeconds = 0.14f;
        public static readonly Vector2 LabelSize = new Vector2(900f, 36f);

        [SerializeField]
        private Sprite graphicSprite;

        [SerializeField]
        private GameObject root;

        [SerializeField]
        private TMP_Text label;

        [SerializeField]
        [Tooltip("Shows the overlay in the editor Game view without entering Play.")]
        private bool previewOnAwake;

        private bool shown;
        private float animationElapsed;
        private Vector3[][] restVertices;
        private bool hasRestPose;

        private static LoadingView presentedView;
        public static bool IsAnyPresented => presentedView != null &&
            presentedView.isActiveAndEnabled && presentedView.IsPresented;
        public bool IsPresented => shown;

        public static LoadingView Create(Transform parent)
        {
            var rootObject = new GameObject("Loading", typeof(RectTransform));
            if (parent != null)
            {
                rootObject.transform.SetParent(parent, false);
            }

            Stretch((RectTransform)rootObject.transform);
            // Scene takeover briefly disables both outgoing and incoming
            // cameras. A ScreenSpaceOverlay canvas alone does not prevent the
            // Game view's "No cameras rendering" message during that gap.
            // This untagged camera draws only a background, below scene cameras,
            // and shares the persistent loading root's lifetime.
            var backgroundCamera = rootObject.AddComponent<Camera>();
            backgroundCamera.enabled = false;
            backgroundCamera.cullingMask = 0;
            backgroundCamera.clearFlags = CameraClearFlags.SolidColor;
            backgroundCamera.backgroundColor = Color.black;
            backgroundCamera.depth = -100f;
            backgroundCamera.allowHDR = false;
            backgroundCamera.allowMSAA = false;
            backgroundCamera.useOcclusionCulling = false;
            var view = rootObject.AddComponent<LoadingView>();
            view.WarmUp();
            return view;
        }

        public static Vector2 FitGraphicSize(float canvasWidth, float canvasHeight, float aspect)
        {
            var safeAspect = aspect > 0.01f ? aspect : FallbackAspectRatio;
            var maxHeight = Mathf.Max(1f, canvasHeight - LabelGap - LabelSize.y);
            var width = Mathf.Max(1f, canvasWidth);
            var height = width / safeAspect;
            if (height > maxHeight)
            {
                height = maxHeight;
                width = height * safeAspect;
            }

            return new Vector2(width, height);
        }

        public static float AspectOf(Sprite sprite)
        {
            if (sprite != null && sprite.rect.height > 0f)
            {
                return sprite.rect.width / sprite.rect.height;
            }

            return FallbackAspectRatio;
        }

        public static float LetterBounce(int letterIndex, int letterCount, float elapsed)
        {
            if (letterCount <= 0 || letterIndex < 0 || letterIndex >= letterCount)
            {
                return 0f;
            }

            var cycle = letterCount * LetterSeconds;
            if (cycle <= 0f)
            {
                return 0f;
            }

            var time = elapsed < 0f ? 0f : elapsed % cycle;
            var current = Mathf.FloorToInt(time / LetterSeconds);
            if (current != letterIndex)
            {
                return 0f;
            }

            var local = (time - (current * LetterSeconds)) / LetterSeconds;
            return Mathf.Sin(local * Mathf.PI) * BounceHeight;
        }

        private void OnEnable()
        {
            EnsureLayout();
            if (previewOnAwake && !shown)
            {
                Show();
                return;
            }

            if (!shown)
            {
                SetVisualsVisible(false);
            }
        }

        public void WarmUp()
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            EnsureLayout();
            TryCaptureRestPose();
            SetVisualsVisible(false);
        }

        public void Show()
        {
            presentedView = this;
            if (shown)
            {
                transform.SetAsLastSibling();
                SetVisualsVisible(true);
                return;
            }

            shown = true;
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            EnsureLayout();
            transform.SetAsLastSibling();
            animationElapsed = 0f;
            SetVisualsVisible(true);
        }

        // The caller owns readiness. Never keep a completed load covered for a timer.
        public void Hide() => HideImmediate();

        public void HideImmediate()
        {
            if (shown) Close();
        }

        private void LateUpdate()
        {
            if (!shown || label == null || !label.gameObject.activeInHierarchy)
            {
                return;
            }

            animationElapsed += Mathf.Min(Time.unscaledDeltaTime, 0.04f);
            AnimateLetters();
        }

        private void Close()
        {
            if (presentedView == this) presentedView = null;
            shown = false;
            animationElapsed = 0f;
            hasRestPose = false;
            SetVisualsVisible(false);
        }

        private void EnsureLayout()
        {
            if (root == null)
            {
                root = transform.Find("Content")?.gameObject ?? gameObject;
            }

            var rect = transform as RectTransform;
            if (rect != null)
            {
                Stretch(rect);
            }

            DestroyLegacy();
            if (transform.Find("Background") == null
                || transform.Find("Content") == null
                || transform.Find("Content/Graphic") == null
                || transform.Find("Content/Label") == null)
            {
                BuildLayout();
            }

            if (label == null)
            {
                label = transform.Find("Content/Label")?.GetComponent<TMP_Text>();
            }

            ApplyLabel();
            ApplyOverlayLayout();
        }

        private void DestroyLegacy()
        {
            DestroyChild("Content/Spotlight");
            var nestedLabel = transform.Find("Content/Graphic/Label");
            if (nestedLabel != null)
            {
                DestroyImmediate(nestedLabel.gameObject);
            }

            var graphic = transform.Find("Content/Graphic");
            var fitter = graphic != null ? graphic.GetComponent<AspectRatioFitter>() : null;
            if (fitter != null)
            {
                DestroyImmediate(fitter);
            }
        }

        private void BuildLayout()
        {
            EnsureOverlayCanvas();

            if (transform.Find("Background") == null)
            {
                var background = CreateImage(transform, "Background", Color.black, HomeUiFonts.WhiteSprite);
                background.raycastTarget = true;
                Stretch(background.rectTransform);
            }

            var content = transform.Find("Content") as RectTransform;
            if (content == null)
            {
                content = CreateRect(transform, "Content");
            }

            if (content.Find("Graphic") == null)
            {
                var graphic = CreateImage(content, "Graphic", Color.white, LoadGraphic());
                graphic.preserveAspect = true;
                graphic.raycastTarget = false;
            }

            if (content.Find("Label") == null)
            {
                label = CreateText(content, "Label", LabelText, LabelFontSize);
            }
        }

        private void ApplyOverlayLayout()
        {
            EnsureOverlayCanvas();

            var background = transform.Find("Background") as RectTransform;
            var content = transform.Find("Content") as RectTransform;
            if (background != null)
            {
                Stretch(background);
            }

            if (content == null)
            {
                return;
            }

            Stretch(content);

            var graphic = content.Find("Graphic") as RectTransform;
            if (graphic == null)
            {
                return;
            }

            var sprite = LoadGraphic();
            var image = graphic.GetComponent<Image>();
            if (image != null)
            {
                image.preserveAspect = true;
                image.raycastTarget = false;
                if (sprite != null)
                {
                    image.sprite = sprite;
                    image.color = Color.white;
                }
            }

            var canvasSize = ResolveCanvasSize();
            var graphicSize = FitGraphicSize(canvasSize.x, canvasSize.y, AspectOf(sprite));
            Place(
                graphic,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                graphicSize);

            if (label == null)
            {
                return;
            }

            var labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0.5f, 1f);
            labelRect.anchorMax = new Vector2(0.5f, 1f);
            labelRect.pivot = new Vector2(0.5f, 1f);
            labelRect.anchoredPosition3D = new Vector3(0f, -graphicSize.y - LabelGap, 0f);
            labelRect.sizeDelta = LabelSize;
            labelRect.SetAsLastSibling();
        }

        private void ApplyLabel()
        {
            if (label == null)
            {
                return;
            }

            var font = HomeUiFonts.ApplyMedium() ?? HomeUiFonts.Apply();
            if (font != null)
            {
                label.font = font;
                if (font.material != null)
                {
                    label.fontSharedMaterial = font.material;
                }
            }

            label.fontSize = LabelFontSize;
            label.fontStyle = FontStyles.Normal;
            label.characterSpacing = 0f;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.color = Color.white;
            label.raycastTarget = false;
            label.maskable = false;
            if (label.text != LabelText)
            {
                label.text = LabelText;
                hasRestPose = false;
            }
        }

        private void AnimateLetters()
        {
            if (!hasRestPose && !TryCaptureRestPose())
            {
                return;
            }

            var textInfo = label.textInfo;
            if (textInfo == null || textInfo.characterCount == 0)
            {
                return;
            }

            if (!RestoreRestPose(textInfo))
            {
                hasRestPose = false;
                return;
            }

            var visibleCount = 0;
            for (var index = 0; index < textInfo.characterCount; index++)
            {
                if (textInfo.characterInfo[index].isVisible)
                {
                    visibleCount++;
                }
            }

            var visibleIndex = 0;
            for (var index = 0; index < textInfo.characterCount; index++)
            {
                var character = textInfo.characterInfo[index];
                if (!character.isVisible)
                {
                    continue;
                }

                var offsetY = LetterBounce(visibleIndex, visibleCount, animationElapsed);
                visibleIndex++;
                if (Mathf.Abs(offsetY) < 0.01f)
                {
                    continue;
                }

                var vertices = textInfo.meshInfo[character.materialReferenceIndex].vertices;
                var vertexIndex = character.vertexIndex;
                vertices[vertexIndex].y += offsetY;
                vertices[vertexIndex + 1].y += offsetY;
                vertices[vertexIndex + 2].y += offsetY;
                vertices[vertexIndex + 3].y += offsetY;
            }

            label.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
        }

        private bool TryCaptureRestPose()
        {
            if (label == null)
            {
                return false;
            }

            label.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: false);
            var textInfo = label.textInfo;
            if (textInfo == null || textInfo.meshInfo == null || textInfo.characterCount == 0)
            {
                return false;
            }

            var meshes = textInfo.meshInfo;
            if (restVertices == null || restVertices.Length != meshes.Length)
            {
                restVertices = new Vector3[meshes.Length][];
            }

            for (var index = 0; index < meshes.Length; index++)
            {
                var source = meshes[index].vertices;
                if (source == null)
                {
                    continue;
                }

                if (restVertices[index] == null || restVertices[index].Length != source.Length)
                {
                    restVertices[index] = new Vector3[source.Length];
                }

                Array.Copy(source, restVertices[index], source.Length);
            }

            hasRestPose = true;
            return true;
        }

        private bool RestoreRestPose(TMP_TextInfo textInfo)
        {
            if (restVertices == null || restVertices.Length != textInfo.meshInfo.Length)
            {
                return false;
            }

            for (var index = 0; index < textInfo.meshInfo.Length; index++)
            {
                var vertices = textInfo.meshInfo[index].vertices;
                var rest = restVertices[index];
                if (vertices == null || rest == null || vertices.Length != rest.Length)
                {
                    return false;
                }

                Array.Copy(rest, vertices, rest.Length);
            }

            return true;
        }

        private Vector2 ResolveCanvasSize()
        {
            var rect = transform as RectTransform;
            if (rect != null)
            {
                var size = rect.rect.size;
                if (size.x > 1f && size.y > 1f)
                {
                    return size;
                }
            }

            return HomeStyle.ReferenceResolution;
        }

        private Sprite LoadGraphic()
        {
            if (graphicSprite != null)
            {
                return graphicSprite;
            }

            graphicSprite = LoadGraphicSprite();
            return graphicSprite;
        }

        private static Sprite LoadGraphicSprite()
        {
#if UNITY_EDITOR
            var sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(GraphicAssetPath);
            if (sprite != null)
            {
                return sprite;
            }

            var texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(GraphicAssetPath);
            if (texture != null)
            {
                return Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
            }
#endif

            var fromResources = Resources.Load<Sprite>(GraphicResource);
            if (fromResources != null)
            {
                return fromResources;
            }

            var resourceTexture = Resources.Load<Texture2D>(GraphicResource);
            if (resourceTexture == null)
            {
                return null;
            }

            return Sprite.Create(
                resourceTexture,
                new Rect(0f, 0f, resourceTexture.width, resourceTexture.height),
                new Vector2(0.5f, 0.5f),
                100f);
        }

        private void EnsureOverlayCanvas()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
            }

            var parentCanvas = transform.parent != null
                ? transform.parent.GetComponentInParent<Canvas>()
                : null;
            if (parentCanvas != null && parentCanvas != canvas)
            {
                canvas.renderMode = parentCanvas.renderMode;
                canvas.worldCamera = parentCanvas.worldCamera;
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = GetComponent<CanvasScaler>() ?? gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = HomeStyle.ReferenceResolution;
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }

            canvas.overrideSorting = true;
            canvas.sortingOrder = SortingOrder;
            canvas.additionalShaderChannels =
                AdditionalCanvasShaderChannels.TexCoord1
                | AdditionalCanvasShaderChannels.Normal
                | AdditionalCanvasShaderChannels.Tangent;

            if (GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }

            var uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0)
            {
                gameObject.layer = uiLayer;
            }
        }

        private void SetVisualsVisible(bool visible)
        {
            var backgroundCamera = GetComponent<Camera>();
            if (backgroundCamera != null)
            {
                backgroundCamera.enabled = visible;
            }

            var canvas = GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.enabled = visible;
            }

            var raycaster = GetComponent<GraphicRaycaster>();
            if (raycaster != null)
            {
                raycaster.enabled = visible;
            }
        }

        private void DestroyChild(string childName)
        {
            var child = transform.Find(childName);
            if (child == null)
            {
                return;
            }

            DestroyImmediate(child.gameObject);
        }

        private static RectTransform CreateRect(Transform parent, string name)
        {
            var created = new GameObject(name, typeof(RectTransform));
            created.transform.SetParent(parent, false);
            return created.GetComponent<RectTransform>();
        }

        private static Image CreateImage(Transform parent, string name, Color color, Sprite sprite)
        {
            var created = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            created.transform.SetParent(parent, false);
            var image = created.GetComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text CreateText(
            Transform parent,
            string name,
            string content,
            float fontSize)
        {
            var created = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            created.transform.SetParent(parent, false);

            var text = created.GetComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            text.maskable = false;
            text.richText = false;
            return text;
        }

        private static void Place(
            RectTransform rect,
            Vector2 anchor,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition3D = new Vector3(anchoredPosition.x, anchoredPosition.y, 0f);
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect, float padding = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
        }
    }
}
