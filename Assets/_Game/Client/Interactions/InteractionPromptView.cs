using Game.Client.Home;
using Game.Client.Match;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Interactions
{
    /// <summary>조준한 대상 위에 상호작용 키와 동작 문구를 띄운다.</summary>
    public sealed class InteractionPromptView : MonoBehaviour
    {
        public const float LabelFontSize = 18f;
        public const float KeyFontSize = 18f;
        public const float KeyBoxSize = 32f;
        public const float KeyIconSize = 24f;
        public const string LeftClickIconResource = "UI/ic_left_click";
        public static readonly Color KeyBoxColor = new(0f, 0f, 0f, 0.27f);
        public const float WorldLift = 0.08f;
        public const float ScaleReferenceDistance = 3f;
        public const float MinDistanceScale = 1f;
        public const float MaxDistanceScale = 1.5f;

        private const int SortingOrder = 220;
        private const float FollowSmoothTime = 0.05f;
        private static readonly Color DefaultActionColor = Color.white;

        private Canvas canvas;
        private RectTransform root;
        private Image keyBox;
        private LayoutElement keyBoxLayout;
        private Image keyIcon;
        private TMP_Text keyLabel;
        private TMP_Text actionLabel;
        private Transform follow;
        private Camera followCamera;
        private string shownKey;
        private string shownAction;
        private Sprite shownIcon;
        private Color shownActionColor;
        private Vector3 followLocalAnchor;
        private bool hasFollowLocalAnchor;
        private Vector3 dampedScreen;
        private Vector3 screenVelocity;
        private bool hasDampedScreen;

        public Image KeyBox => keyBox;

        public Image KeyIcon => keyIcon;

        public TMP_Text KeyLabel => keyLabel;

        public TMP_Text ActionLabel => actionLabel;

        public bool IsVisible => root != null && root.gameObject.activeSelf;

        public float CurrentScale => root != null ? root.localScale.x : 1f;

        public static float ScaleFromDistance(float distance)
        {
            if (distance <= 0.01f)
            {
                return MaxDistanceScale;
            }

            return Mathf.Clamp(ScaleReferenceDistance / distance, MinDistanceScale, MaxDistanceScale);
        }

        public static InteractionPromptView Create()
        {
            var root = new GameObject(
                "Interaction Prompt",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            var view = root.AddComponent<InteractionPromptView>();
            view.Build();
            return view;
        }

        public void Show(
            string key,
            string action,
            Transform target,
            Sprite icon = null,
            Color? actionColor = null,
            Vector3? worldAnchor = null)
        {
            EnsureBuilt();
            var nextAction = action ?? string.Empty;
            var nextColor = actionColor ?? DefaultActionColor;
            if (follow != target)
            {
                follow = target;
                hasFollowLocalAnchor = false;
                hasDampedScreen = false;
                screenVelocity = Vector3.zero;
            }

            if (worldAnchor.HasValue && target != null)
            {
                followLocalAnchor = target.InverseTransformPoint(worldAnchor.Value);
                hasFollowLocalAnchor = true;
            }

            if (shownKey != key || shownAction != nextAction || shownIcon != icon || shownActionColor != nextColor)
            {
                ApplyKeyContent(key, icon);
                actionLabel.text = nextAction;
                actionLabel.color = nextColor;
                shownKey = key;
                shownAction = nextAction;
                shownIcon = icon;
                shownActionColor = nextColor;
            }

            root.gameObject.SetActive(true);
            RefreshPosition();
        }

        public static Sprite LoadLeftClickIcon() =>
            Resources.Load<Sprite>(LeftClickIconResource);

        public void Hide()
        {
            follow = null;
            shownKey = null;
            shownAction = null;
            shownIcon = null;
            shownActionColor = default;
            hasFollowLocalAnchor = false;
            hasDampedScreen = false;
            screenVelocity = Vector3.zero;
            if (root != null)
            {
                root.localScale = Vector3.one;
                root.gameObject.SetActive(false);
            }
        }

        private void LateUpdate()
        {
            if (IsVisible)
            {
                RefreshPosition();
            }
        }

        private void EnsureBuilt()
        {
            if (root == null)
            {
                Build();
            }
        }

        private void Build()
        {
            canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            canvas.pixelPerfect = false;
            Game.Client.Common.HudScreenScale.Ensure(gameObject);

            var raycaster = GetComponent<GraphicRaycaster>();
            if (raycaster != null)
            {
                raycaster.enabled = false;
            }

            var font = HomeUiFonts.Apply();

            root = CreateRect("Prompt", transform);
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0f);
            root.sizeDelta = new Vector2(160f, 58f);

            var layout = root.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.LowerCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.spacing = 6f;

            var fitter = root.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var keyRow = CreateRect("KeyRow", root);
            var keyRowLayout = keyRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            keyRowLayout.childAlignment = TextAnchor.MiddleCenter;
            keyRowLayout.childControlWidth = true;
            keyRowLayout.childControlHeight = true;
            keyRowLayout.childForceExpandWidth = false;
            keyRowLayout.childForceExpandHeight = false;

            var keyBoxRect = CreateRect("KeyBox", keyRow);
            keyBox = keyBoxRect.gameObject.AddComponent<Image>();
            keyBox.sprite = HomeUiFonts.RoundedSprite;
            keyBox.type = Image.Type.Sliced;
            keyBox.pixelsPerUnitMultiplier = 2.4f;
            keyBox.color = KeyBoxColor;
            keyBox.raycastTarget = false;
            keyBoxLayout = keyBoxRect.gameObject.AddComponent<LayoutElement>();
            keyBoxLayout.minWidth = KeyBoxSize;
            keyBoxLayout.minHeight = KeyBoxSize;
            keyBoxLayout.preferredWidth = KeyBoxSize;
            keyBoxLayout.preferredHeight = KeyBoxSize;
            keyBoxLayout.flexibleWidth = 0f;
            var keyBoxGroup = keyBoxRect.gameObject.AddComponent<HorizontalLayoutGroup>();
            keyBoxGroup.padding = new RectOffset(4, 4, 4, 4);
            keyBoxGroup.childAlignment = TextAnchor.MiddleCenter;
            keyBoxGroup.childControlWidth = true;
            keyBoxGroup.childControlHeight = true;
            keyBoxGroup.childForceExpandWidth = false;
            keyBoxGroup.childForceExpandHeight = false;

            keyIcon = CreateIcon("Icon", keyBoxRect);
            keyLabel = CreateLabel("Key", keyBoxRect, font, KeyFontSize, FontStyles.Normal);

            actionLabel = CreateLabel("Action", root, font, LabelFontSize, FontStyles.Normal);

            root.gameObject.SetActive(false);
        }

        private void ApplyKeyContent(string key, Sprite icon)
        {
            var useIcon = icon != null;
            if (keyIcon != null)
            {
                keyIcon.sprite = icon;
                keyIcon.gameObject.SetActive(useIcon);
            }

            keyLabel.gameObject.SetActive(!useIcon);
            keyLabel.text = useIcon ? string.Empty : key ?? string.Empty;
            FitKeyBox(useIcon);
        }

        private void FitKeyBox(bool useIcon)
        {
            if (keyBoxLayout == null)
            {
                return;
            }

            var width = KeyBoxSize;
            if (!useIcon && keyLabel != null)
            {
                keyLabel.ForceMeshUpdate();
                width = HidingActiveHudView.MeasureKeyChipWidth(keyLabel.text, keyLabel.preferredWidth);
            }

            keyBoxLayout.minWidth = width;
            keyBoxLayout.preferredWidth = width;
            if (root != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(root);
            }
        }

        private static Image CreateIcon(string objectName, Transform parent)
        {
            var created = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            created.transform.SetParent(parent, false);
            var image = created.GetComponent<Image>();
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;
            var layout = created.AddComponent<LayoutElement>();
            layout.preferredWidth = KeyIconSize;
            layout.preferredHeight = KeyIconSize;
            layout.minWidth = KeyIconSize;
            layout.minHeight = KeyIconSize;
            created.SetActive(false);
            return image;
        }

        private void RefreshPosition()
        {
            if (follow == null || root == null)
            {
                return;
            }

            if (followCamera == null || !followCamera.isActiveAndEnabled)
            {
                followCamera = Camera.main;
            }

            if (followCamera == null)
            {
                return;
            }

            var world = ResolveFollowWorld();
            var screen = followCamera.WorldToScreenPoint(world);
            if (screen.z <= 0f)
            {
                root.gameObject.SetActive(false);
                return;
            }

            if (!root.gameObject.activeSelf)
            {
                root.gameObject.SetActive(true);
            }

            if (!hasDampedScreen)
            {
                dampedScreen = screen;
                screenVelocity = Vector3.zero;
                hasDampedScreen = true;
            }
            else
            {
                dampedScreen = Vector3.SmoothDamp(
                    dampedScreen,
                    screen,
                    ref screenVelocity,
                    FollowSmoothTime,
                    Mathf.Infinity,
                    Time.unscaledDeltaTime);
            }

            root.position = dampedScreen;
            var distance = Vector3.Distance(followCamera.transform.position, world);
            var scale = ScaleFromDistance(distance);
            root.localScale = new Vector3(scale, scale, 1f);
        }

        private Vector3 ResolveFollowWorld()
        {
            if (!hasFollowLocalAnchor)
            {
                followLocalAnchor = follow.InverseTransformPoint(ResolveAnchor(follow));
                hasFollowLocalAnchor = true;
            }

            return follow.TransformPoint(followLocalAnchor);
        }

        private static Vector3 ResolveAnchor(Transform target)
        {
            if (target.TryGetComponent<Collider>(out var targetCollider) && targetCollider.enabled)
            {
                var bounds = targetCollider.bounds;
                return bounds.center + (Vector3.up * (bounds.extents.y + WorldLift));
            }

            var renderers = target.GetComponentsInChildren<Renderer>();
            Bounds? combined = null;
            for (var index = 0; index < renderers.Length; index++)
            {
                if (ItemOutlineRenderers.IsGenerated(renderers[index]))
                {
                    continue;
                }

                if (!combined.HasValue)
                {
                    combined = renderers[index].bounds;
                    continue;
                }

                var bounds = combined.Value;
                bounds.Encapsulate(renderers[index].bounds);
                combined = bounds;
            }

            if (combined.HasValue)
            {
                var bounds = combined.Value;
                return bounds.center + (Vector3.up * (bounds.extents.y + WorldLift));
            }

            return target.position + (Vector3.up * 0.35f);
        }

        private static RectTransform CreateRect(string objectName, Transform parent)
        {
            var created = new GameObject(objectName, typeof(RectTransform));
            created.transform.SetParent(parent, false);
            return created.GetComponent<RectTransform>();
        }

        private static TMP_Text CreateLabel(
            string objectName,
            Transform parent,
            TMP_FontAsset font,
            float fontSize,
            FontStyles style)
        {
            var created = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            created.transform.SetParent(parent, false);
            var text = created.GetComponent<TextMeshProUGUI>();
            text.font = font;
            if (font != null)
            {
                text.fontSharedMaterial = font.material;
            }

            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = DefaultActionColor;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }
    }
}
