using System;
using System.Collections.Generic;
using Game.Core.Home;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Game.Client.Home
{
    [DisallowMultipleComponent]
    public sealed class HomeMenuView : MonoBehaviour, IHomeMenuView
    {
        private static readonly Color CharacterColor = new Color(0.86f, 0.86f, 0.86f, 1f);
        private static readonly Color AvatarColor = new Color(0.62f, 0.62f, 0.62f, 1f);
        private static readonly Color ExperienceBackground = new Color(0.82f, 0.82f, 0.82f, 1f);
        private static readonly Color ExperienceFillColor = new Color(0.31f, 0.62f, 0.91f, 1f);
        private static readonly Color TextHover = new Color(0.35f, 0.35f, 0.35f, 1f);
        private static readonly Color TextPressed = new Color(0.15f, 0.15f, 0.15f, 1f);
        private static readonly Color FriendRowColor = new Color(0.86f, 0.86f, 0.86f, 1f);
        private static readonly Color FriendStatusColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        private static readonly Color FriendSeparatorColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        private static readonly Color PanelShadowColor = new Color(0.1f, 0.1f, 0.1f, 0.38f);
        private static readonly Color ItemShadowColor = new Color(0.12f, 0.12f, 0.12f, 0.32f);

        [SerializeField]
        private string title = "로고 or 이름 두둥";

        [SerializeField]
        [Range(0f, 1f)]
        private float experienceRatio = 0.4f;

        [SerializeField]
        private TMP_FontAsset fontAsset;

        [SerializeField]
        private TMP_Text nicknameText;

        [SerializeField]
        private TMP_Text levelText;

        private readonly List<Button> menuButtons = new List<Button>();
        private readonly List<FriendRow> onlineRows = new List<FriendRow>();
        private readonly List<FriendRow> offlineRows = new List<FriendRow>();
        private TMP_FontAsset koreanFont;
        private GameObject friendListRoot;
        private TMP_Text onlineSectionText;
        private TMP_Text offlineSectionText;
        private RectTransform onlineItemsRoot;
        private RectTransform offlineItemsRoot;
        private Button dismissButton;

        public event Action<HomeMenuAction> ActionClicked;

        public event Action FriendListDismissed;

        private void Awake()
        {
            EnsureEventSystem();
            if (nicknameText == null || levelText == null)
            {
                BuildLayout();
            }

            SetFriendListVisible(false);
        }

        private void OnDestroy()
        {
            for (var index = 0; index < menuButtons.Count; index++)
            {
                if (menuButtons[index] != null)
                {
                    menuButtons[index].onClick.RemoveAllListeners();
                }
            }

            if (dismissButton != null)
            {
                dismissButton.onClick.RemoveAllListeners();
            }
        }

        public void SetNickname(string nickname)
        {
            if (nicknameText != null)
            {
                nicknameText.text = nickname;
            }
        }

        public void SetLevel(int level)
        {
            if (levelText != null)
            {
                levelText.text = $"Lv.{level}";
            }
        }

        public void SetFriendListVisible(bool visible)
        {
            if (friendListRoot != null)
            {
                friendListRoot.SetActive(visible);
            }
        }

        public void SetFriends(
            IReadOnlyList<FriendSummary> onlineFriends,
            IReadOnlyList<FriendSummary> offlineFriends)
        {
            if (onlineFriends == null)
            {
                throw new ArgumentNullException(nameof(onlineFriends));
            }

            if (offlineFriends == null)
            {
                throw new ArgumentNullException(nameof(offlineFriends));
            }

            if (onlineSectionText == null || offlineSectionText == null)
            {
                return;
            }

            onlineSectionText.text = $"온라인 {onlineFriends.Count}";
            offlineSectionText.text = $"오프라인 {offlineFriends.Count}";
            BindRows(onlineRows, onlineItemsRoot, onlineFriends);
            BindRows(offlineRows, offlineItemsRoot, offlineFriends);
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            var eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.transform.SetParent(transform, false);
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
        }

        private void BuildLayout()
        {
            koreanFont = HomeUiFonts.Apply(fontAsset);
            var canvas = CreateCanvas();
            CreateTitle(canvas);
            CreateProfile(canvas);
            CreateCharacter(canvas);
            CreateLeftButtons(canvas);
            CreateQuitButton(canvas);
            CreateBottomRightButtons(canvas);
            CreateFriendListRoot(canvas);
        }

        private RectTransform CreateCanvas()
        {
            var canvasObject = new GameObject("HomeCanvas", typeof(RectTransform));
            canvasObject.layer = LayerMask.NameToLayer("UI");
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvas.additionalShaderChannels =
                AdditionalCanvasShaderChannels.TexCoord1
                | AdditionalCanvasShaderChannels.Normal
                | AdditionalCanvasShaderChannels.Tangent;

            var canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.offsetMin = Vector2.zero;
            canvasRect.offsetMax = Vector2.zero;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            return canvasRect;
        }

        private void CreateTitle(RectTransform canvas)
        {
            var titleRect = CreateRect("Title", canvas);
            SetAnchor(titleRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            titleRect.anchoredPosition = new Vector2(0f, -36f);
            titleRect.sizeDelta = new Vector2(900f, 84f);
            AddText(titleRect, title, 58f, FontStyles.Bold, TextAlignmentOptions.Center);
        }

        private void CreateProfile(RectTransform canvas)
        {
            var profileRect = CreateRect("Profile", canvas);
            SetAnchor(profileRect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f));
            profileRect.anchoredPosition = new Vector2(-40f, -28f);
            profileRect.sizeDelta = new Vector2(360f, 88f);

            var layout = profileRect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var avatar = CreateRect("Avatar", profileRect);
            avatar.sizeDelta = new Vector2(72f, 72f);
            var avatarImage = AddImage(avatar, AvatarColor, HomeUiFonts.CircleSprite);
            avatarImage.preserveAspect = true;
            var avatarLayout = avatar.gameObject.AddComponent<LayoutElement>();
            avatarLayout.preferredWidth = 72f;
            avatarLayout.preferredHeight = 72f;

            var info = CreateRect("Info", profileRect);
            info.sizeDelta = new Vector2(260f, 72f);
            var infoLayout = info.gameObject.AddComponent<VerticalLayoutGroup>();
            infoLayout.spacing = 6f;
            infoLayout.childAlignment = TextAnchor.MiddleLeft;
            infoLayout.childControlWidth = true;
            infoLayout.childControlHeight = false;
            infoLayout.childForceExpandWidth = true;
            infoLayout.childForceExpandHeight = false;

            var nicknameRect = CreateRect("Nickname", info);
            nicknameRect.sizeDelta = new Vector2(260f, 32f);
            nicknameText = AddText(
                nicknameRect,
                "사용자닉네임",
                26f,
                FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft);

            var levelRow = CreateRect("LevelRow", info);
            levelRow.sizeDelta = new Vector2(260f, 22f);
            var levelLayout = levelRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            levelLayout.spacing = 10f;
            levelLayout.childAlignment = TextAnchor.MiddleLeft;
            levelLayout.childControlWidth = false;
            levelLayout.childControlHeight = true;
            levelLayout.childForceExpandWidth = false;
            levelLayout.childForceExpandHeight = true;

            var levelRect = CreateRect("Level", levelRow);
            levelRect.sizeDelta = new Vector2(56f, 22f);
            levelText = AddText(
                levelRect,
                "Lv.1",
                18f,
                FontStyles.Normal,
                TextAlignmentOptions.MidlineLeft);

            var experienceTrack = CreateRect("ExperienceTrack", levelRow);
            experienceTrack.sizeDelta = new Vector2(180f, 14f);
            AddImage(experienceTrack, ExperienceBackground);

            var experienceFill = CreateRect("ExperienceFill", experienceTrack);
            SetAnchor(experienceFill, Vector2.zero, Vector2.one, new Vector2(0f, 0.5f));
            experienceFill.offsetMin = Vector2.zero;
            experienceFill.offsetMax = Vector2.zero;
            var fillImage = AddImage(experienceFill, ExperienceFillColor);
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.fillAmount = experienceRatio;
        }

        private void CreateCharacter(RectTransform canvas)
        {
            var character = CreateRect("Character", canvas);
            SetAnchor(character, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            character.anchoredPosition = new Vector2(0f, -24f);
            character.sizeDelta = new Vector2(340f, 480f);
            AddImage(character, CharacterColor);

            var label = CreateRect("Label", character);
            SetAnchor(label, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            label.offsetMin = Vector2.zero;
            label.offsetMax = Vector2.zero;
            AddText(label, "캐릭터", 36f, FontStyles.Bold, TextAlignmentOptions.Center);
        }

        private void CreateLeftButtons(RectTransform canvas)
        {
            var left = CreateRect("PrimaryButtons", canvas);
            SetAnchor(left, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
            left.anchoredPosition = new Vector2(48f, 12f);
            left.sizeDelta = new Vector2(280f, 320f);
            AddVerticalLayout(left, TextAnchor.MiddleLeft);

            CreateTextButton(left, "바로 플레이", HomeMenuAction.QuickPlay, TextAlignmentOptions.MidlineLeft);
            CreateTextButton(left, "방 찾기", HomeMenuAction.FindRoom, TextAlignmentOptions.MidlineLeft);
            CreateSpacer(left, 18f);
            CreateTextButton(left, "프로필 설정", HomeMenuAction.ProfileSettings, TextAlignmentOptions.MidlineLeft);
        }

        private void CreateQuitButton(RectTransform canvas)
        {
            var quit = CreateRect("QuitButton", canvas);
            SetAnchor(quit, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f));
            quit.anchoredPosition = new Vector2(48f, 48f);
            quit.sizeDelta = new Vector2(280f, 56f);
            AddVerticalLayout(quit, TextAnchor.LowerLeft);
            CreateTextButton(quit, "게임 종료", HomeMenuAction.Quit, TextAlignmentOptions.MidlineLeft);
        }

        private void CreateBottomRightButtons(RectTransform canvas)
        {
            var row = CreateRect("BottomRightButtons", canvas);
            SetAnchor(row, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f));
            row.anchoredPosition = new Vector2(-48f, 48f);
            row.sizeDelta = new Vector2(400f, 56f);

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 28f;
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            CreateTextButton(row, "환경 설정", HomeMenuAction.Settings, TextAlignmentOptions.MidlineRight, 160f);
            CreateTextButton(row, "친구 목록", HomeMenuAction.Friends, TextAlignmentOptions.MidlineRight, 160f);
        }

        private void CreateFriendListRoot(RectTransform canvas)
        {
            var root = CreateRect("FriendListRoot", canvas);
            SetAnchor(root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            var dismiss = CreateRect("DismissArea", root);
            SetAnchor(dismiss, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            dismiss.offsetMin = Vector2.zero;
            dismiss.offsetMax = Vector2.zero;
            var dismissImage = AddImage(dismiss, new Color(0f, 0f, 0f, 0.01f), raycastTarget: true);
            dismissButton = dismiss.gameObject.AddComponent<Button>();
            dismissButton.targetGraphic = dismissImage;
            dismissButton.transition = Selectable.Transition.None;
            dismissButton.navigation = new Navigation { mode = Navigation.Mode.None };
            dismissButton.onClick.AddListener(() => FriendListDismissed?.Invoke());

            CreateFriendListPanel(root);
            friendListRoot = root.gameObject;
            friendListRoot.SetActive(false);
        }

        private void CreateFriendListPanel(RectTransform parent)
        {
            var panel = CreateRect("Panel", parent);
            SetAnchor(panel, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f));
            panel.anchoredPosition = new Vector2(-48f, 116f);
            panel.sizeDelta = new Vector2(320f, 460f);
            var panelImage = AddImage(panel, Color.white, HomeUiFonts.RoundedSprite, raycastTarget: true);
            panelImage.type = Image.Type.Sliced;
            AddDropShadow(panel.gameObject, PanelShadowColor, new Vector2(4f, -5f));
            AddDropShadow(panel.gameObject, new Color(0.1f, 0.1f, 0.1f, 0.18f), new Vector2(8f, -10f));

            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 16, 16);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var header = CreateRect("Header", panel);
            var headerLayout = header.gameObject.AddComponent<LayoutElement>();
            headerLayout.preferredHeight = 36f;
            headerLayout.minHeight = 36f;
            AddText(header, "친구", 24f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);

            var scroll = CreateRect("Scroll", panel);
            var scrollLayout = scroll.gameObject.AddComponent<LayoutElement>();
            scrollLayout.flexibleHeight = 1f;
            scrollLayout.minHeight = 80f;

            var scrollRect = scroll.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 24f;

            var viewport = CreateRect("Viewport", scroll);
            SetAnchor(viewport, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;
            viewport.gameObject.AddComponent<RectMask2D>();
            var viewportImage = AddImage(viewport, Color.clear, raycastTarget: true);
            viewportImage.color = Color.clear;

            var content = CreateRect("Content", viewport);
            SetAnchor(content, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 10f;
            contentLayout.childAlignment = TextAnchor.UpperLeft;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            var contentFitter = content.gameObject.AddComponent<ContentSizeFitter>();
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewport;
            scrollRect.content = content;

            onlineSectionText = CreateSectionTitle(content, "온라인 0");
            onlineItemsRoot = CreateItemGroup(content, "OnlineItems");
            CreateSeparator(content);
            offlineSectionText = CreateSectionTitle(content, "오프라인 0");
            offlineItemsRoot = CreateItemGroup(content, "OfflineItems");
        }

        private TMP_Text CreateSectionTitle(RectTransform parent, string label)
        {
            var titleRect = CreateRect("SectionTitle", parent);
            var layout = titleRect.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 28f;
            layout.minHeight = 28f;
            return AddText(titleRect, label, 18f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        }

        private static RectTransform CreateItemGroup(RectTransform parent, string name)
        {
            var group = CreateRect(name, parent);
            var layout = group.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = group.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return group;
        }

        private static void CreateSeparator(RectTransform parent)
        {
            var separator = CreateRect("Separator", parent);
            var layout = separator.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 2f;
            layout.minHeight = 2f;
            AddImage(separator, FriendSeparatorColor);
        }

        private void BindRows(
            List<FriendRow> pool,
            RectTransform parent,
            IReadOnlyList<FriendSummary> friends)
        {
            while (pool.Count < friends.Count)
            {
                pool.Add(CreateFriendRow(parent));
            }

            for (var index = 0; index < pool.Count; index++)
            {
                var row = pool[index];
                var isVisible = index < friends.Count;
                row.Root.SetActive(isVisible);
                if (!isVisible)
                {
                    continue;
                }

                var friend = friends[index];
                row.Nickname.text = friend.Nickname;
                row.Status.text = friend.Presence == FriendPresence.InGame ? "게임중" : string.Empty;
            }
        }

        private FriendRow CreateFriendRow(RectTransform parent)
        {
            var row = CreateRect("FriendRow", parent);
            var rowLayout = row.gameObject.AddComponent<LayoutElement>();
            rowLayout.preferredHeight = 52f;
            rowLayout.minHeight = 52f;
            var rowImage = AddImage(row, FriendRowColor, HomeUiFonts.RoundedSprite);
            rowImage.type = Image.Type.Sliced;
            rowImage.pixelsPerUnitMultiplier = 1.4f;
            AddDropShadow(row.gameObject, ItemShadowColor, new Vector2(2f, -3f));
            AddDropShadow(row.gameObject, new Color(0.12f, 0.12f, 0.12f, 0.16f), new Vector2(4f, -6f));

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 8, 8);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            var avatar = CreateRect("Avatar", row);
            var avatarLayout = avatar.gameObject.AddComponent<LayoutElement>();
            avatarLayout.preferredWidth = 36f;
            avatarLayout.preferredHeight = 36f;
            avatarLayout.minWidth = 36f;
            avatarLayout.minHeight = 36f;
            var avatarImage = AddImage(avatar, AvatarColor, HomeUiFonts.CircleSprite);
            avatarImage.preserveAspect = true;

            var nicknameRect = CreateRect("Nickname", row);
            var nicknameLayout = nicknameRect.gameObject.AddComponent<LayoutElement>();
            nicknameLayout.flexibleWidth = 1f;
            nicknameLayout.minWidth = 40f;
            var nickname = AddText(
                nicknameRect,
                string.Empty,
                18f,
                FontStyles.Normal,
                TextAlignmentOptions.MidlineLeft);
            nickname.overflowMode = TextOverflowModes.Ellipsis;

            var statusRect = CreateRect("Status", row);
            var statusLayout = statusRect.gameObject.AddComponent<LayoutElement>();
            statusLayout.preferredWidth = 76f;
            statusLayout.minWidth = 76f;
            statusLayout.preferredHeight = 28f;
            var statusImage = AddImage(statusRect, FriendStatusColor, HomeUiFonts.RoundedSprite);
            statusImage.type = Image.Type.Sliced;
            statusImage.pixelsPerUnitMultiplier = 1.8f;

            var statusLabelRect = CreateRect("Label", statusRect);
            SetAnchor(statusLabelRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            statusLabelRect.offsetMin = Vector2.zero;
            statusLabelRect.offsetMax = Vector2.zero;
            var status = AddText(
                statusLabelRect,
                string.Empty,
                14f,
                FontStyles.Normal,
                TextAlignmentOptions.Center);

            return new FriendRow
            {
                Root = row.gameObject,
                Nickname = nickname,
                Status = status
            };
        }

        private static void AddDropShadow(GameObject target, Color color, Vector2 distance)
        {
            var shadow = target.AddComponent<Shadow>();
            shadow.effectColor = color;
            shadow.effectDistance = distance;
            shadow.useGraphicAlpha = true;
        }

        private RectTransform CreateTextButton(
            RectTransform parent,
            string label,
            HomeMenuAction action,
            TextAlignmentOptions alignment,
            float preferredWidth = 280f)
        {
            var buttonRect = CreateRect(action.ToString(), parent);
            buttonRect.sizeDelta = new Vector2(preferredWidth, 56f);
            var layoutElement = buttonRect.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = preferredWidth;
            layoutElement.minWidth = preferredWidth;
            layoutElement.preferredHeight = 56f;
            layoutElement.minHeight = 56f;

            var text = AddText(buttonRect, label, 28f, FontStyles.Normal, alignment, raycastTarget: true);
            var button = buttonRect.gameObject.AddComponent<Button>();
            button.targetGraphic = text;
            button.transition = Selectable.Transition.ColorTint;
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.black;
            colors.highlightedColor = TextHover;
            colors.pressedColor = TextPressed;
            colors.selectedColor = Color.black;
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(() => ActionClicked?.Invoke(action));
            menuButtons.Add(button);
            return buttonRect;
        }

        private static void AddVerticalLayout(RectTransform parent, TextAnchor alignment)
        {
            var layout = parent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = alignment;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        private static void CreateSpacer(RectTransform parent, float height)
        {
            var spacer = CreateRect("Spacer", parent);
            spacer.sizeDelta = new Vector2(0f, height);
            var layoutElement = spacer.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = height;
            layoutElement.minHeight = height;
        }

        private TMP_Text AddText(
            RectTransform target,
            string content,
            float fontSize,
            FontStyles style,
            TextAlignmentOptions alignment,
            bool raycastTarget = false)
        {
            if (koreanFont == null)
            {
                throw new InvalidOperationException("Cafe24 Ssurround TMP font is missing.");
            }

            target.gameObject.SetActive(false);
            var tmp = target.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = koreanFont;
            tmp.fontSharedMaterial = koreanFont.material;
            tmp.text = content;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.alignment = alignment;
            tmp.color = Color.black;
            tmp.raycastTarget = raycastTarget;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            target.gameObject.SetActive(true);
            return tmp;
        }

        private static Image AddImage(
            RectTransform rect,
            Color color,
            Sprite sprite = null,
            bool raycastTarget = false)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite != null ? sprite : HomeUiFonts.WhiteSprite;
            image.type = Image.Type.Simple;
            image.color = color;
            image.raycastTarget = raycastTarget;
            return image;
        }

        private sealed class FriendRow
        {
            public GameObject Root;
            public TMP_Text Nickname;
            public TMP_Text Status;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var rectObject = new GameObject(name, typeof(RectTransform));
            rectObject.layer = LayerMask.NameToLayer("UI");
            var rect = rectObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void SetAnchor(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
        }
    }
}
