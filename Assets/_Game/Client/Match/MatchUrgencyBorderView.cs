using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Match
{
    public interface IMatchUrgencyBorderView
    {
        void Show();
        void Hide();
    }

    /// <summary>
    /// Full-screen red edge glow for the last thirty seconds of searching.
    /// Thickness breathes so the gradient grows and shrinks.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MatchUrgencyBorderView : MonoBehaviour, IMatchUrgencyBorderView
    {
        public const string RootName = "UrgencyBorder";
        public const float MinThickness = 50f;
        public const float MaxThickness = 100f;
        public const float PulsePeriod = 1.05f;
        public const float MinAlpha = 0.29f;
        public const float MaxAlpha = 0.5f;
        public static readonly Color EdgeColor = new Color(0.84f, 0.06f, 0.09f, 0.5f);

        private static Sprite verticalEdgeSprite;
        private static Sprite verticalEdgeSpriteFlipped;
        private static Sprite horizontalEdgeSprite;
        private static Sprite horizontalEdgeSpriteFlipped;

        [SerializeField]
        private RectTransform topEdge;

        [SerializeField]
        private RectTransform bottomEdge;

        [SerializeField]
        private RectTransform leftEdge;

        [SerializeField]
        private RectTransform rightEdge;

        [SerializeField]
        [Tooltip("Shows the border in the editor Game view without entering Play.")]
        private bool previewOnAwake;

        private Image[] edgeImages = System.Array.Empty<Image>();
        private bool shown;

        public static MatchUrgencyBorderView Create(Transform parent)
        {
            var rootObject = new GameObject(RootName, typeof(RectTransform));
            rootObject.transform.SetParent(parent, false);
            Stretch((RectTransform)rootObject.transform);
            var view = rootObject.AddComponent<MatchUrgencyBorderView>();
            view.EnsureLayout();
            if (!view.shown)
            {
                view.Hide();
            }

            return view;
        }

        public static float PulseAmount(float time)
        {
            return (Mathf.Sin((time / PulsePeriod) * (Mathf.PI * 2f)) + 1f) * 0.5f;
        }

        public static float ThicknessAt(float time)
        {
            return Mathf.Lerp(MinThickness, MaxThickness, PulseAmount(time));
        }

        private void Awake()
        {
            EnsureLayout();
            if (previewOnAwake && !shown)
            {
                Show();
                return;
            }

            if (!shown)
            {
                Hide();
            }
        }

        private void OnDisable()
        {
            if (!shown)
            {
                return;
            }

            ApplyPulse(0.5f);
        }

        private void LateUpdate()
        {
            if (!shown)
            {
                return;
            }

            transform.SetAsFirstSibling();
            ApplyPulse(PulseAmount(Time.unscaledTime));
        }

        public void Show()
        {
            shown = true;
            EnsureLayout();
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            transform.SetAsFirstSibling();
            ApplyPulse(PulseAmount(Time.unscaledTime));
        }

        public void Hide()
        {
            shown = false;
            ApplyPulse(0.5f);
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        private void EnsureLayout()
        {
            var rect = transform as RectTransform;
            if (rect != null)
            {
                Stretch(rect);
            }

            if (topEdge == null)
            {
                topEdge = transform.Find("Top") as RectTransform;
            }

            if (bottomEdge == null)
            {
                bottomEdge = transform.Find("Bottom") as RectTransform;
            }

            if (leftEdge == null)
            {
                leftEdge = transform.Find("Left") as RectTransform;
            }

            if (rightEdge == null)
            {
                rightEdge = transform.Find("Right") as RectTransform;
            }

            if (topEdge == null || bottomEdge == null || leftEdge == null || rightEdge == null)
            {
                BuildLayout();
            }

            BindEdges();
            CacheImages();
            ApplyPulse(shown ? PulseAmount(Time.unscaledTime) : 0.5f);
        }

        private void BuildLayout()
        {
            topEdge = CreateEdge(transform, "Top", VerticalEdgeSprite);
            bottomEdge = CreateEdge(transform, "Bottom", VerticalEdgeSpriteFlipped);
            leftEdge = CreateEdge(transform, "Left", HorizontalEdgeSprite);
            rightEdge = CreateEdge(transform, "Right", HorizontalEdgeSpriteFlipped);
            PlaceTop(topEdge);
            PlaceBottom(bottomEdge);
            PlaceLeft(leftEdge);
            PlaceRight(rightEdge);
        }

        private void BindEdges()
        {
            PlaceTop(topEdge);
            PlaceBottom(bottomEdge);
            PlaceLeft(leftEdge);
            PlaceRight(rightEdge);
        }

        private void CacheImages()
        {
            edgeImages = new[]
            {
                topEdge != null ? topEdge.GetComponent<Image>() : null,
                bottomEdge != null ? bottomEdge.GetComponent<Image>() : null,
                leftEdge != null ? leftEdge.GetComponent<Image>() : null,
                rightEdge != null ? rightEdge.GetComponent<Image>() : null
            };
        }

        private void ApplyPulse(float amount)
        {
            var thickness = Mathf.Lerp(MinThickness, MaxThickness, amount);
            if (topEdge != null)
            {
                topEdge.sizeDelta = new Vector2(0f, thickness);
            }

            if (bottomEdge != null)
            {
                bottomEdge.sizeDelta = new Vector2(0f, thickness);
            }

            if (leftEdge != null)
            {
                leftEdge.sizeDelta = new Vector2(thickness, 0f);
            }

            if (rightEdge != null)
            {
                rightEdge.sizeDelta = new Vector2(thickness, 0f);
            }

            var color = EdgeColor;
            color.a = Mathf.Lerp(MinAlpha, MaxAlpha, amount);
            for (var index = 0; index < edgeImages.Length; index++)
            {
                if (edgeImages[index] != null)
                {
                    edgeImages[index].color = color;
                }
            }
        }

        private static void PlaceTop(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition3D = Vector3.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private static void PlaceBottom(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition3D = Vector3.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private static void PlaceLeft(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition3D = Vector3.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private static void PlaceRight(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition3D = Vector3.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private static RectTransform CreateEdge(Transform parent, string name, Sprite sprite)
        {
            var gameObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            gameObject.transform.SetParent(parent, false);
            var image = gameObject.GetComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.color = EdgeColor;
            image.raycastTarget = false;
            image.preserveAspect = false;
            return gameObject.GetComponent<RectTransform>();
        }

        private static Sprite VerticalEdgeSprite
        {
            get
            {
                if (verticalEdgeSprite != null)
                {
                    return verticalEdgeSprite;
                }

                verticalEdgeSprite = CreateFadeSprite(8, 64, alongVertical: true, invert: false);
                return verticalEdgeSprite;
            }
        }

        private static Sprite VerticalEdgeSpriteFlipped
        {
            get
            {
                if (verticalEdgeSpriteFlipped != null)
                {
                    return verticalEdgeSpriteFlipped;
                }

                verticalEdgeSpriteFlipped = CreateFadeSprite(8, 64, alongVertical: true, invert: true);
                return verticalEdgeSpriteFlipped;
            }
        }

        private static Sprite HorizontalEdgeSprite
        {
            get
            {
                if (horizontalEdgeSprite != null)
                {
                    return horizontalEdgeSprite;
                }

                horizontalEdgeSprite = CreateFadeSprite(64, 8, alongVertical: false, invert: false);
                return horizontalEdgeSprite;
            }
        }

        private static Sprite HorizontalEdgeSpriteFlipped
        {
            get
            {
                if (horizontalEdgeSpriteFlipped != null)
                {
                    return horizontalEdgeSpriteFlipped;
                }

                horizontalEdgeSpriteFlipped = CreateFadeSprite(64, 8, alongVertical: false, invert: true);
                return horizontalEdgeSpriteFlipped;
            }
        }

        private static Sprite CreateFadeSprite(int width, int height, bool alongVertical, bool invert)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var span = alongVertical ? height - 1 : width - 1;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var along = alongVertical ? y / (float)span : 1f - (x / (float)span);
                    if (invert)
                    {
                        along = 1f - along;
                    }

                    var alpha = along * along;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply(false, false);
            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
