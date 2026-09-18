using Game.Client.Home;
using Game.Client.Interactions;
using Game.Client.Match;
using Game.Core.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.Client
{
    /// <summary>
    /// The on-screen key-setting guide. Lobby and playground each attach one
    /// copy; the list itself does not belong to a match phase.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KeySettingGuideView : MonoBehaviour
    {
        public const string RootName = "KeySettingGuide";
        public const float MarginRight = 48f;
        public const float ActionFontSize = 18f;
        public const string ClickKeyLabel = "클릭";
        public const string RightClickKeyLabel = "우클릭";
        public const string ScrollKeyLabel = "스크롤";
        public const string RotateYawKeyLabel = "Q / E";
        public const string LeftClickIconResource = "UI/ic_left_click";
        public const string RightClickIconResource = "UI/ic_right_click";
        public const string ScrollIconResource = "UI/ic_mouse_scroll";
        public const string ToggleAction = "키 가이드 on/off";
        public const string ToggleKeyLabel = "L";
        public const float RowStep = 48f;
        public const float CompactKeyChipFontSize = 12f;
        public static readonly Vector2 PanelSize = new Vector2(280f, 368f);
        public static readonly Vector2 CarryingPanelSize = new Vector2(280f, 464f);
        public static readonly Vector2 PlacingPanelSize = new Vector2(280f, 512f);

        public enum Mode
        {
            Default,
            Carrying,
            Placing
        }

        public static readonly string[] Actions =
        {
            "공격",
            "앉기",
            "엎드리기",
            "시점 변경",
            "달리기",
            "점프",
            ToggleAction
        };

        public static readonly string[] Labels =
        {
            ClickKeyLabel,
            "C",
            "Z",
            "V",
            "Shift",
            "Space",
            ToggleKeyLabel
        };

        public static readonly string[] CarryingActions =
        {
            "배치 모드",
            "던지기",
            "놓기",
            "앉기",
            "엎드리기",
            "시점 변경",
            "달리기",
            "점프",
            ToggleAction
        };

        public static readonly string[] CarryingLabels =
        {
            RightClickKeyLabel,
            ClickKeyLabel,
            "F",
            "C",
            "Z",
            "V",
            "Shift",
            "Space",
            ToggleKeyLabel
        };

        public static readonly string[] PlacingActions =
        {
            "배치 모드 끄기",
            "배치하기",
            "가로축 회전",
            "세로축 회전",
            "앉기",
            "엎드리기",
            "시점 변경",
            "달리기",
            "점프",
            ToggleAction
        };

        public static readonly string[] PlacingLabels =
        {
            RightClickKeyLabel,
            ClickKeyLabel,
            RotateYawKeyLabel,
            ScrollKeyLabel,
            "C",
            "Z",
            "V",
            "Shift",
            "Space",
            ToggleKeyLabel
        };

        private static readonly ControlAction[] DefaultBindings =
        {
            ControlAction.PrimaryAction,
            ControlAction.Crouch,
            ControlAction.Prone,
            ControlAction.ToggleView,
            ControlAction.Sprint,
            ControlAction.Jump
        };

        private static readonly ControlAction[] CarryingBindings =
        {
            ControlAction.PlacementMode,
            ControlAction.PrimaryAction,
            ControlAction.Interact,
            ControlAction.Crouch,
            ControlAction.Prone,
            ControlAction.ToggleView,
            ControlAction.Sprint,
            ControlAction.Jump
        };

        private static readonly ControlAction[] PlacingBindings =
        {
            ControlAction.PlacementMode,
            ControlAction.PrimaryAction,
            ControlAction.RotateLeft,
            ControlAction.RaiseObject,
            ControlAction.Crouch,
            ControlAction.Prone,
            ControlAction.ToggleView,
            ControlAction.Sprint,
            ControlAction.Jump
        };

        private static ControlSettingsSystem sharedSettings;
        private static int lastToggleFrame = -1;
        private CanvasGroup fade;
        private PlayerInteractor localInteractor;
        private Mode mode;
        private string focusLabel;
        private ControlAction[] focusActions = System.Array.Empty<ControlAction>();
        private static readonly Color FocusColor = new Color(1f, .79f, .28f);

        public void SetFocus(string label, params ControlAction[] actions)
        {
            focusLabel = label;
            focusActions = actions ?? System.Array.Empty<ControlAction>();
            ApplyStyle();
        }

        public static string CurrentKeyLabel(ControlAction action) => ControlCatalog.KeyLabel(
            (sharedSettings != null ? sharedSettings.Current : ControlCatalog.Defaults).Get(action));

        public static bool UserVisible { get; private set; } = true;
        public bool AlwaysVisible { get; set; }
        public bool ShowFocusCaption { get; set; } = true;
        public bool IsCarrying => mode == Mode.Carrying;
        public bool IsPlacing => mode == Mode.Placing;
        public Mode CurrentMode => mode;

        public static string[] ActionsFor(bool isCarrying) =>
            ActionsFor(isCarrying ? Mode.Carrying : Mode.Default);

        public static string[] ActionsFor(Mode guideMode) =>
            guideMode == Mode.Placing
                ? PlacingActions
                : guideMode == Mode.Carrying
                    ? CarryingActions
                    : Actions;

        public static string[] LabelsFor(bool isCarrying) =>
            LabelsFor(isCarrying ? Mode.Carrying : Mode.Default);

        public static string[] LabelsFor(Mode guideMode) =>
            LabelsFor(guideMode, sharedSettings != null ? sharedSettings.Current : default, bound: sharedSettings != null);

        public static string[] LabelsFor(Mode guideMode, ControlSettings settings)
        {
            return LabelsFor(guideMode, settings, bound: true);
        }

        /// <summary>
        /// Hands the 컨트롤 tab's applied keys to every guide. Pass null to
        /// fall back to the built-in labels, as tests do.
        /// </summary>
        public static void UseSettings(ControlSettingsSystem settings)
        {
            if (sharedSettings != null)
            {
                sharedSettings.Changed -= OnSharedSettingsChanged;
            }

            sharedSettings = settings;
            if (sharedSettings != null)
            {
                sharedSettings.Changed += OnSharedSettingsChanged;
            }

            RefreshBoundGuides();
        }

        private static string[] LabelsFor(Mode guideMode, ControlSettings settings, bool bound)
        {
            if (!bound)
            {
                return BuiltInLabelsFor(guideMode);
            }

            var bindings = BindingsFor(guideMode);
            var labels = new string[bindings.Length + 1];
            for (var index = 0; index < bindings.Length; index++)
            {
                labels[index] = LabelForBinding(guideMode, bindings[index], settings);
            }

            labels[labels.Length - 1] = ControlCatalog.KeyLabel(settings.Get(ControlAction.ToggleKeyGuide));
            return labels;
        }

        private static string[] BuiltInLabelsFor(Mode guideMode) =>
            guideMode == Mode.Placing
                ? PlacingLabels
                : guideMode == Mode.Carrying
                    ? CarryingLabels
                    : Labels;

        private static ControlAction[] BindingsFor(Mode guideMode) =>
            guideMode == Mode.Placing
                ? PlacingBindings
                : guideMode == Mode.Carrying
                    ? CarryingBindings
                    : DefaultBindings;

        private static string LabelForBinding(Mode guideMode, ControlAction action, ControlSettings settings)
        {
            if (guideMode == Mode.Placing && action == ControlAction.RotateLeft)
            {
                return CombinedKeyLabel(
                    settings.Get(ControlAction.RotateLeft),
                    settings.Get(ControlAction.RotateRight));
            }

            if (guideMode == Mode.Placing && action == ControlAction.RaiseObject)
            {
                var raise = settings.Get(ControlAction.RaiseObject);
                var lower = settings.Get(ControlAction.LowerObject);
                if (IsScrollCode(raise) && IsScrollCode(lower))
                {
                    return ScrollKeyLabel;
                }

                return CombinedKeyLabel(raise, lower);
            }

            return ControlCatalog.KeyLabel(settings.Get(action));
        }

        private static string CombinedKeyLabel(string leftCode, string rightCode) =>
            $"{ControlCatalog.KeyLabel(leftCode)} / {ControlCatalog.KeyLabel(rightCode)}";

        private static bool IsScrollCode(string code) =>
            code == ControlCatalog.ScrollUp || code == ControlCatalog.ScrollDown;

        private static void OnSharedSettingsChanged(ControlSettings _) => RefreshBoundGuides();

        private static void RefreshBoundGuides()
        {
            var guides = UnityEngine.Object.FindObjectsByType<KeySettingGuideView>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (var index = 0; index < guides.Length; index++)
            {
                if (guides[index] != null)
                {
                    guides[index].ApplyStyle();
                }
            }
        }

        public static Vector2 PanelSizeFor(bool isCarrying) =>
            PanelSizeFor(isCarrying ? Mode.Carrying : Mode.Default);

        public static Vector2 PanelSizeFor(Mode guideMode) =>
            guideMode == Mode.Placing
                ? PlacingPanelSize
                : guideMode == Mode.Carrying
                    ? CarryingPanelSize
                    : PanelSize;

        public static bool ShouldToggle(bool pressed, bool inputBlocked)
        {
            return pressed && !inputBlocked;
        }

        public static void SetUserVisible(bool visible)
        {
            UserVisible = visible;
            lastToggleFrame = -1;
        }

        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible)
            {
                gameObject.SetActive(visible);
            }

            if (visible)
            {
                ApplyUserVisible();
            }
        }

        public void SetCarrying(bool isCarrying)
        {
            SetMode(isCarrying ? Mode.Carrying : Mode.Default);
        }

        public void SetMode(Mode guideMode)
        {
            if (mode == guideMode && transform.Find($"Row{ActionsFor(guideMode).Length - 1}") != null)
            {
                return;
            }

            mode = guideMode;
            SyncRows();
            ApplyStyle();
        }

        public static KeySettingGuideView Create(Transform parent)
        {
            var root = new GameObject(RootName, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var view = root.AddComponent<KeySettingGuideView>();
            view.BuildLayout();
            view.ApplyStyle();
            view.ApplyUserVisible();
            return view;
        }

        public static KeySettingGuideView Ensure(Transform parent)
        {
            if (parent == null)
            {
                return null;
            }

            var existing = parent.Find(RootName) ?? parent.Find("KeyGuide");
            if (existing == null)
            {
                return Create(parent);
            }

            if (existing.name != RootName)
            {
                existing.name = RootName;
            }

            var view = existing.GetComponent<KeySettingGuideView>();
            if (view == null)
            {
                view = existing.gameObject.AddComponent<KeySettingGuideView>();
            }

            view.EnsureLayout();
            view.ApplyUserVisible();
            return view;
        }

        public void ApplyStyle()
        {
            PlacePanel();
            var actions = ActionsFor(mode);
            var labels = LabelsFor(mode);
            var light = HomeUiFonts.ApplyLight();
            var bindings = BindingsFor(mode);
            var interactionFocus = mode == Mode.Default && System.Array.IndexOf(focusActions, ControlAction.Interact) >= 0;
            for (var index = 0; index < actions.Length; index++)
            {
                var focused = interactionFocus && index == 0 || index < bindings.Length && System.Array.IndexOf(focusActions, bindings[index]) >= 0;
                var row = transform.Find($"Row{index}");
                if (row == null)
                {
                    continue;
                }

                row.gameObject.SetActive(true);
                var action = row.Find("Action")?.GetComponent<TMP_Text>();
                if (action != null)
                {
                    action.text = interactionFocus && index == 0 ? "상호작용 · 들기" : actions[index];
                    action.font = light;
                    action.fontSize = ActionFontSize;
                    action.fontStyle = focused ? FontStyles.Bold : FontStyles.Normal;
                    action.color = focused ? FocusColor : Color.white;
                    action.alignment = TextAlignmentOptions.MidlineRight;
                }

                var chip = row.Find("Key") as RectTransform;
                var keyLabel = row.Find("Key/Label")?.GetComponent<TMP_Text>();
                if (keyLabel != null)
                {
                    keyLabel.text = interactionFocus && index == 0 ? CurrentKeyLabel(ControlAction.Interact) : labels[index];
                    keyLabel.fontStyle = focused ? FontStyles.Bold : FontStyles.Normal;
                    keyLabel.color = focused ? FocusColor : Color.white;
                }

                if (chip != null)
                {
                    ApplyKeyChipLook(chip.GetComponent<Image>());
                    FitKeyChip(chip, keyLabel);
                    if (keyLabel != null)
                    {
                        keyLabel.fontStyle = focused ? FontStyles.Bold : FontStyles.Normal;
                        keyLabel.color = focused ? FocusColor : Color.white;
                    }
                    if (focused) chip.GetComponent<Image>().color = new Color(.3f, .23f, .07f, .95f);
                    var icon = chip.Find("Icon")?.GetComponent<Image>();
                    if (icon != null) icon.color = focused ? FocusColor : Color.white;
                }

                if (action != null && chip != null)
                {
                    PlaceAction(action.rectTransform, chip.sizeDelta.x);
                }
            }

            HideUnusedRows(actions.Length);
            RefreshFocusLabel();
        }

        private void RefreshFocusLabel()
        {
            var caption = transform.Find("Focus")?.GetComponent<TMP_Text>();
            if (!ShowFocusCaption || string.IsNullOrEmpty(focusLabel))
            {
                if (caption != null) caption.gameObject.SetActive(false);
                return;
            }
            if (caption == null)
                caption = CreateText(transform, "Focus", string.Empty, 20f, HomeUiFonts.ApplyLight());
            caption.gameObject.SetActive(true);
            var settings = sharedSettings != null ? sharedSettings.Current : ControlCatalog.Defaults;
            var keys = new string[focusActions.Length];
            for (var i = 0; i < keys.Length; i++)
                keys[i] = ControlCatalog.KeyLabel(settings.Get(focusActions[i]));
            caption.text = $"{focusLabel}\n<size=17>{string.Join(" / ", keys)}</size>";
            caption.color = FocusColor;
            caption.fontStyle = FontStyles.Bold;
            caption.alignment = TextAlignmentOptions.BottomRight;
            caption.textWrappingMode = TextWrappingModes.Normal;
            Place(caption.rectTransform, Vector2.one, new Vector2(0, 12), new Vector2(360, 64), new Vector2(1, 0));
        }

        private void Awake()
        {
            EnsureLayout();
            ApplyUserVisible();
        }

        private void Update()
        {
            SetMode(ReadLocalMode());
            if (!ShouldToggle(WasTogglePressed(), IsInputBlocked()))
            {
                ApplyUserVisible();
                return;
            }

            if (lastToggleFrame == Time.frameCount)
            {
                ApplyUserVisible();
                return;
            }

            lastToggleFrame = Time.frameCount;
            UserVisible = !UserVisible;
            ApplyUserVisible();
        }

        private void ApplyUserVisible()
        {
            if (fade == null)
            {
                fade = GetComponent<CanvasGroup>();
                if (fade == null)
                {
                    fade = gameObject.AddComponent<CanvasGroup>();
                }

                fade.blocksRaycasts = false;
                fade.interactable = false;
            }

            fade.alpha = (AlwaysVisible || UserVisible) ? 1f : 0f;
        }

        private static bool WasTogglePressed()
        {
            var code = sharedSettings != null
                ? sharedSettings.Current.Get(ControlAction.ToggleKeyGuide)
                : "l";
            return WasBoundKeyPressed(code);
        }

        private static bool WasBoundKeyPressed(string code)
        {
            if (string.IsNullOrEmpty(code))
            {
                return false;
            }

            switch (code)
            {
                case ControlCatalog.MouseLeft:
                    return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
                case ControlCatalog.MouseRight:
                    return Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
                case ControlCatalog.MouseMiddle:
                    return Mouse.current != null && Mouse.current.middleButton.wasPressedThisFrame;
                default:
                    var keyboard = Keyboard.current;
                    if (keyboard == null)
                    {
                        return false;
                    }

                    var control = keyboard.TryGetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>(code);
                    return control != null && control.wasPressedThisFrame;
            }
        }

        private static bool IsInputBlocked()
        {
            if (MatchChatView.BlocksPlayerInput)
            {
                return true;
            }

            var selected = EventSystem.current != null
                ? EventSystem.current.currentSelectedGameObject
                : null;
            if (selected == null)
            {
                return false;
            }

            var tmp = selected.GetComponent<TMP_InputField>();
            if (tmp != null && tmp.isFocused)
            {
                return true;
            }

            var legacy = selected.GetComponent<InputField>();
            return legacy != null && legacy.isFocused;
        }

        private void EnsureLayout()
        {
            SyncRows();
            ApplyStyle();
        }

        private void BuildLayout()
        {
            SyncRows();
        }

        private void SyncRows()
        {
            PlacePanel();
            var actions = ActionsFor(mode);
            var labels = LabelsFor(mode);
            for (var index = 0; index < actions.Length; index++)
            {
                var row = transform.Find($"Row{index}") as RectTransform;
                if (row == null)
                {
                    row = CreateRect(transform, $"Row{index}");
                    var action = CreateText(
                        row,
                        "Action",
                        actions[index],
                        ActionFontSize,
                        HomeUiFonts.ApplyLight());
                    action.alignment = TextAlignmentOptions.MidlineRight;

                    var chip = CreateImage(
                        row,
                        "Key",
                        HidingActiveHudView.KeyChipColor,
                        HidingActiveHudView.KeyChipSprite);
                    chip.type = Image.Type.Sliced;
                    var keyLabel = CreateText(
                        chip.transform,
                        "Label",
                        labels[index],
                        HidingActiveHudView.KeyChipFontSize,
                        HomeUiFonts.ApplyLight());
                    Stretch(keyLabel.rectTransform);
                }

                row.gameObject.SetActive(true);
                Place(
                    row,
                    new Vector2(1f, 1f),
                    new Vector2(-140f, -24f - (index * RowStep)),
                    new Vector2(280f, 40f));
            }

            HideUnusedRows(actions.Length);
        }

        private void HideUnusedRows(int usedCount)
        {
            for (var index = usedCount; ; index++)
            {
                var row = transform.Find($"Row{index}");
                if (row == null)
                {
                    break;
                }

                row.gameObject.SetActive(false);
            }
        }

        private void PlacePanel()
        {
            Place(
                (RectTransform)transform,
                new Vector2(1f, 0.5f),
                new Vector2(-MarginRight, 0f),
                PanelSizeFor(mode),
                new Vector2(1f, 0.5f));
        }

        private Mode ReadLocalMode()
        {
            if (localInteractor == null || !localInteractor.isActiveAndEnabled)
            {
                localInteractor = FindLocalInteractor();
            }

            if (localInteractor == null)
            {
                return Mode.Default;
            }

            var placement = localInteractor.GetComponent<ItemPlacementController>();
            if (placement != null && placement.isActiveAndEnabled && placement.IsPlacing)
            {
                return Mode.Placing;
            }

            return localInteractor.CarriedItem != null ? Mode.Carrying : Mode.Default;
        }

        private static PlayerInteractor FindLocalInteractor()
        {
            var interactors = FindObjectsByType<PlayerInteractor>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (var index = 0; index < interactors.Length; index++)
            {
                var interactor = interactors[index];
                if (interactor != null && interactor.isActiveAndEnabled)
                {
                    return interactor;
                }
            }

            return null;
        }

        private static void ApplyKeyChipLook(Image chip)
        {
            if (chip == null)
            {
                return;
            }

            chip.color = HidingActiveHudView.KeyChipColor;
            chip.sprite = HidingActiveHudView.KeyChipSprite;
            chip.type = Image.Type.Sliced;
            chip.pixelsPerUnitMultiplier = 1f;
        }

        private static void FitKeyChip(RectTransform chip, TMP_Text label)
        {
            var iconResource = IconResourceFor(label != null ? label.text : null);
            var icon = chip.Find("Icon")?.GetComponent<Image>();
            if (iconResource != null)
            {
                if (label != null)
                {
                    label.gameObject.SetActive(false);
                }

                icon = EnsureClickIcon(chip, iconResource);
                if (icon != null)
                {
                    icon.gameObject.SetActive(true);
                    Place(
                        icon.rectTransform,
                        new Vector2(0.5f, 0.5f),
                        Vector2.zero,
                        new Vector2(HidingActiveHudView.KeyIconSize, HidingActiveHudView.KeyIconSize));
                }

                Place(
                    chip,
                    new Vector2(1f, 0.5f),
                    Vector2.zero,
                    new Vector2(HidingActiveHudView.KeyChipWidth, HidingActiveHudView.KeyChipHeight),
                    new Vector2(1f, 0.5f));
                return;
            }

            if (icon != null)
            {
                icon.gameObject.SetActive(false);
            }

            var width = HidingActiveHudView.KeyChipWidth;
            if (label != null)
            {
                label.gameObject.SetActive(true);
                label.font = HomeUiFonts.ApplyLight();
                label.fontSize = KeyChipFontSizeFor(label.text);
                label.fontStyle = FontStyles.Normal;
                label.color = Color.white;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Overflow;
                label.ForceMeshUpdate();
                label.fontSize = KeyChipFontSizeFor(label.text);
                width = HidingActiveHudView.MeasureKeyChipWidth(label.text, label.preferredWidth);
            }

            Place(
                chip,
                new Vector2(1f, 0.5f),
                Vector2.zero,
                new Vector2(width, HidingActiveHudView.KeyChipHeight),
                new Vector2(1f, 0.5f));
        }

        private static string IconResourceFor(string label)
        {
            if (label == ClickKeyLabel || label == "좌클릭")
            {
                return LeftClickIconResource;
            }

            if (label == RightClickKeyLabel)
            {
                return RightClickIconResource;
            }

            if (label == ScrollKeyLabel || label == "스크롤 ↑" || label == "스크롤 ↓")
            {
                return ScrollIconResource;
            }

            return null;
        }

        private static float KeyChipFontSizeFor(string label)
        {
            return !string.IsNullOrEmpty(label) && (label == RotateYawKeyLabel || label.Contains(" / "))
                ? CompactKeyChipFontSize
                : HidingActiveHudView.KeyChipFontSize;
        }

        private static Image EnsureClickIcon(RectTransform chip, string resource)
        {
            if (chip == null)
            {
                return null;
            }

            var sprite = string.IsNullOrEmpty(resource) ? null : Resources.Load<Sprite>(resource);
            var existing = chip.Find("Icon")?.GetComponent<Image>();
            if (existing != null)
            {
                existing.sprite = sprite;
                return existing;
            }

            var icon = CreateImage(chip, "Icon", Color.white, sprite);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            return icon;
        }

        private static void PlaceAction(RectTransform action, float chipWidth)
        {
            Place(
                action,
                new Vector2(1f, 0.5f),
                new Vector2(-(chipWidth + 8f), 0f),
                new Vector2(220f, HidingActiveHudView.KeyChipHeight),
                new Vector2(1f, 0.5f));
        }

        private static RectTransform CreateRect(Transform parent, string name)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject.GetComponent<RectTransform>();
        }

        private static Image CreateImage(Transform parent, string name, Color color, Sprite sprite)
        {
            var gameObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            gameObject.transform.SetParent(parent, false);
            var image = gameObject.GetComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text CreateText(
            Transform parent,
            string name,
            string content,
            float fontSize,
            TMP_FontAsset font = null)
        {
            var gameObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            gameObject.transform.SetParent(parent, false);
            var text = gameObject.GetComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            text.richText = true;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.font = font != null ? font : HomeUiFonts.Apply();
            text.fontStyle = FontStyles.Normal;
            return text;
        }

        private static void Place(
            RectTransform rect,
            Vector2 anchor,
            Vector2 anchoredPosition,
            Vector2 size,
            Vector2? pivot = null)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
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
