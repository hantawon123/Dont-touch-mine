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
    public sealed partial class HomeMenuView : MonoBehaviour, IHomeMenuView
    {
        private static readonly Color CharacterColor = new Color(0.86f, 0.86f, 0.86f, 1f);
        private static readonly Color AvatarColor = new Color(0.62f, 0.62f, 0.62f, 1f);
        private static readonly Color TextHover = new Color(0.35f, 0.35f, 0.35f, 1f);
        private static readonly Color TextPressed = new Color(0.15f, 0.15f, 0.15f, 1f);
        private static readonly Color MenuHover = new Color(0.18f, 0.47f, 0.98f, 1f);
        private static readonly Color MenuPressed = new Color(0.10f, 0.32f, 0.78f, 1f);
        private static readonly Color FriendRowColor = new Color(0.86f, 0.86f, 0.86f, 1f);
        private static readonly Color FriendStatusColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        private static readonly Color FriendSeparatorColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        private static readonly Color PanelShadowColor = new Color(0.1f, 0.1f, 0.1f, 0.38f);
        private static readonly Color ItemShadowColor = new Color(0.12f, 0.12f, 0.12f, 0.32f);
        private static readonly Color SearchBarColor = new Color(0.88f, 0.88f, 0.88f, 1f);
        private static readonly Color SearchButtonColor = new Color(0.22f, 0.22f, 0.22f, 1f);
        private const float HeaderActionSize = 28f;

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
            CreateProfileSettingsRoot(canvas);
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
            root.gameObject.SetActive(false);
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
        }

        private void CreateProfileSettingsRoot(RectTransform canvas)
        {
            var root = CreateRect("ProfileSettingsRoot", canvas);
            root.gameObject.SetActive(false);
            SetAnchor(root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            AddImage(root, Color.white, raycastTarget: true);

            CreateProfileSettingsHeader(root);
            CreateProfileSettingsBody(root);
            CreateAppliedFeedback(root);
            profileSettingsRoot = root.gameObject;
        }

        private void CreateProfileSettingsHeader(RectTransform parent)
        {
            var header = CreateRect("Header", parent);
            SetAnchor(header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            header.anchoredPosition = Vector2.zero;
            header.sizeDelta = new Vector2(0f, 96f);

            var layout = header.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(40, 40, 24, 12);
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            CreateSimpleTextButton(header, "Back", "<", 36f, FontStyles.Bold, TextAlignmentOptions.Center, () =>
            {
                ProfileSettingsDismissed?.Invoke();
            }, 48f, 56f);

            var titleRect = CreateRect("Title", header);
            var titleLayout = titleRect.gameObject.AddComponent<LayoutElement>();
            titleLayout.preferredWidth = 280f;
            titleLayout.minWidth = 200f;
            titleLayout.preferredHeight = 56f;
            titleLayout.flexibleWidth = 1f;
            AddText(titleRect, "프로필 설정", 36f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        }

        private void CreateProfileSettingsBody(RectTransform parent)
        {
            var body = CreateRect("Body", parent);
            SetAnchor(body, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            body.offsetMin = new Vector2(80f, 100f);
            body.offsetMax = new Vector2(-80f, -96f);

            var layout = body.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 96f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            CreateProfileCharacter(body);
            CreateProfileInfo(body);
        }

        private void CreateProfileCharacter(RectTransform parent)
        {
            var character = CreateRect("Character", parent);
            character.sizeDelta = new Vector2(440f, 600f);
            var characterLayout = character.gameObject.AddComponent<LayoutElement>();
            characterLayout.preferredWidth = 440f;
            characterLayout.preferredHeight = 600f;
            characterLayout.minWidth = 440f;
            characterLayout.minHeight = 600f;
            AddImage(character, CharacterColor);

            var label = CreateRect("Label", character);
            SetAnchor(label, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            label.offsetMin = Vector2.zero;
            label.offsetMax = Vector2.zero;
            AddText(label, "캐릭터", 36f, FontStyles.Bold, TextAlignmentOptions.Center);
        }

        private void CreateProfileInfo(RectTransform parent)
        {
            var info = CreateRect("Info", parent);
            info.sizeDelta = new Vector2(400f, 420f);
            var infoLayout = info.gameObject.AddComponent<LayoutElement>();
            infoLayout.preferredWidth = 400f;
            infoLayout.minWidth = 400f;
            infoLayout.preferredHeight = 420f;

            var layout = info.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 20f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var avatar = CreateRect("Avatar", info);
            avatar.sizeDelta = new Vector2(120f, 120f);
            var avatarLayout = avatar.gameObject.AddComponent<LayoutElement>();
            avatarLayout.preferredWidth = 120f;
            avatarLayout.preferredHeight = 120f;
            avatarLayout.minWidth = 120f;
            avatarLayout.minHeight = 120f;
            var avatarImage = AddImage(avatar, CharacterColor, HomeUiFonts.CircleSprite);
            avatarImage.preserveAspect = true;

            CreateProfileNicknameField(info);
            CreateSimpleTextButton(
                info,
                "ChangeNickname",
                "변경하기",
                24f,
                FontStyles.Normal,
                TextAlignmentOptions.Center,
                OnChangeNicknameClicked,
                160f,
                48f);
        }

        private void CreateProfileNicknameField(RectTransform parent)
        {
            var fieldRect = CreateRect("NicknameField", parent);
            fieldRect.sizeDelta = new Vector2(400f, 56f);
            var fieldLayout = fieldRect.gameObject.AddComponent<LayoutElement>();
            fieldLayout.preferredWidth = 400f;
            fieldLayout.preferredHeight = 56f;
            fieldLayout.minWidth = 400f;
            fieldLayout.minHeight = 56f;
            var fieldImage = AddImage(fieldRect, CharacterColor, HomeUiFonts.PillSprite, raycastTarget: true);
            fieldImage.type = Image.Type.Sliced;
            fieldImage.pixelsPerUnitMultiplier = 1f;

            var textArea = CreateRect("TextArea", fieldRect);
            SetAnchor(textArea, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            textArea.offsetMin = new Vector2(28f, 8f);
            textArea.offsetMax = new Vector2(-28f, -8f);
            textArea.gameObject.AddComponent<RectMask2D>();

            var textRect = CreateRect("Text", textArea);
            SetAnchor(textRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = AddText(
                textRect,
                "사용자닉네임",
                24f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                raycastTarget: true);

            fieldRect.gameObject.SetActive(false);
            var input = fieldRect.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = textArea;
            input.textComponent = text;
            input.fontAsset = koreanFont;
            input.pointSize = 24f;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterLimit = 32;
            input.text = "사용자닉네임";
            input.onValueChanged.AddListener(value => NicknameEdited?.Invoke(value));
            fieldRect.gameObject.SetActive(true);
            profileNicknameInput = input;
        }

        private void CreateAppliedFeedback(RectTransform parent)
        {
            var feedbackRect = CreateRect("AppliedFeedback", parent);
            SetAnchor(feedbackRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            feedbackRect.anchoredPosition = new Vector2(0f, 48f);
            feedbackRect.sizeDelta = new Vector2(400f, 40f);
            appliedFeedbackText = AddText(
                feedbackRect,
                "반영되었습니다",
                22f,
                FontStyles.Normal,
                TextAlignmentOptions.Center);
            appliedFeedbackText.gameObject.SetActive(false);

            // Sits where the success line does, because only one of them can be
            // true at a time and stacking them would leave a gap under whichever
            // is hidden.
            var errorRect = CreateRect("NicknameError", parent);
            SetAnchor(errorRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            errorRect.anchoredPosition = new Vector2(0f, 48f);
            errorRect.sizeDelta = new Vector2(400f, 40f);
            nicknameErrorText = AddText(
                errorRect,
                string.Empty,
                22f,
                FontStyles.Normal,
                TextAlignmentOptions.Center);
            nicknameErrorText.color = new Color(0.78f, 0.20f, 0.20f, 1f);
            nicknameErrorText.gameObject.SetActive(false);
        }

        private void OnChangeNicknameClicked()
        {
            NicknameChangeRequested?.Invoke(
                profileNicknameInput != null ? profileNicknameInput.text : string.Empty);
        }

        private RectTransform CreateSimpleTextButton(
            RectTransform parent,
            string name,
            string label,
            float fontSize,
            FontStyles style,
            TextAlignmentOptions alignment,
            Action onClicked,
            float preferredWidth,
            float preferredHeight)
        {
            var buttonRect = CreateRect(name, parent);
            buttonRect.sizeDelta = new Vector2(preferredWidth, preferredHeight);
            var layoutElement = buttonRect.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = preferredWidth;
            layoutElement.minWidth = preferredWidth;
            layoutElement.preferredHeight = preferredHeight;
            layoutElement.minHeight = preferredHeight;
            layoutElement.flexibleWidth = 0f;
            layoutElement.flexibleHeight = 0f;

            var text = AddText(buttonRect, label, fontSize, style, alignment, raycastTarget: true);
            text.color = Color.white;
            var button = buttonRect.gameObject.AddComponent<Button>();
            button.targetGraphic = text;
            button.transition = Selectable.Transition.ColorTint;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.black;
            colors.highlightedColor = MenuHover;
            colors.pressedColor = MenuPressed;
            colors.selectedColor = Color.black;
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(() => onClicked?.Invoke());
            menuButtons.Add(button);
            return buttonRect;
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

            CreatePanelHeader(panel);

            var bodyHost = CreateRect("Body", panel);
            var bodyHostLayout = bodyHost.gameObject.AddComponent<LayoutElement>();
            bodyHostLayout.flexibleHeight = 1f;
            bodyHostLayout.minHeight = 80f;

            var scroll = CreateRect("Scroll", bodyHost);
            SetAnchor(scroll, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            scroll.offsetMin = Vector2.zero;
            scroll.offsetMax = Vector2.zero;

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
            AddImage(viewport, Color.clear, raycastTarget: true);

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
            friendListBody = scroll.gameObject;
            CreateFriendSearchBody(bodyHost);
        }

        private void CreatePanelHeader(RectTransform panel)
        {
            var header = CreateRect("Header", panel);
            var headerLayout = header.gameObject.AddComponent<LayoutElement>();
            headerLayout.preferredHeight = 36f;
            headerLayout.minHeight = 36f;
            AddHeaderRow(header);

            var titleRect = CreateRect("Title", header);
            var titleLayout = titleRect.gameObject.AddComponent<LayoutElement>();
            titleLayout.preferredWidth = 0f;
            titleLayout.flexibleWidth = 1f;
            titleLayout.minWidth = 80f;
            titleLayout.preferredHeight = 36f;
            panelHeaderText = AddText(titleRect, "친구", 24f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);

            // Spelled out rather than drawn as a circular arrow. The font atlas
            // is baked from a fixed character set that has no such glyph, and it
            // is close enough to full that adding one means rebaking eight
            // megabytes of asset — for an icon that still has to be guessed at.
            refreshButton = CreateHeaderActionButton(
                header, "Refresh", "새로고침", OnRefreshClicked, 72f, 14f);
            addFriendButton = CreateHeaderActionButton(header, "Add", "+", OnAddFriendClicked);
            closeSearchButton = CreateHeaderActionButton(header, "Close", "X", OnCloseSearchClicked);
            closeSearchButton.SetActive(false);
        }

        private void OnRefreshClicked()
        {
            FriendListRefreshRequested?.Invoke();
        }

        private void OnAddFriendClicked()
        {
            SetFriendSearchVisible(true);
            FriendSearchOpened?.Invoke();
        }

        private void OnCloseSearchClicked()
        {
            SetFriendSearchVisible(false);
            FriendSearchClosed?.Invoke();
        }

        private void CreateFriendSearchBody(RectTransform parent)
        {
            var body = CreateRect("SearchBody", parent);
            SetAnchor(body, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            body.offsetMin = Vector2.zero;
            body.offsetMax = Vector2.zero;
            friendSearchBody = body.gameObject;

            var layout = body.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            requestsItemsRoot = CreateRequestsSection(body, "받은 요청", out requestsSection);
            sentItemsRoot = CreateRequestsSection(body, "보낸 요청", out sentSection);
            CreateSearchBar(body);
            searchItemsRoot = CreateSearchResults(body);
            friendSearchBody.SetActive(false);
        }

        private static void AddHeaderRow(RectTransform header)
        {
            var row = header.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = true;
            row.childControlHeight = false;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;
            row.spacing = 8f;
        }

        private GameObject CreateHeaderActionButton(
            RectTransform parent,
            string name,
            string label,
            Action onClicked,
            float width = HeaderActionSize,
            float fontSize = 22f)
        {
            var buttonRect = CreateRect(name, parent);
            buttonRect.sizeDelta = new Vector2(width, HeaderActionSize);
            var layout = buttonRect.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.preferredHeight = HeaderActionSize;
            layout.minWidth = width;
            layout.minHeight = HeaderActionSize;
            layout.flexibleWidth = 0f;
            layout.flexibleHeight = 0f;
            var background = AddImage(
                buttonRect,
                Color.white,
                HomeUiFonts.RoundedSprite,
                raycastTarget: true);
            background.type = Image.Type.Sliced;
            var labelRect = CreateRect("Label", buttonRect);
            SetAnchor(labelRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            AddText(
                labelRect,
                label,
                fontSize,
                FontStyles.Bold,
                TextAlignmentOptions.Center);
            var button = buttonRect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.ColorTint;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.selectedColor = Color.white;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(() => onClicked?.Invoke());
            menuButtons.Add(button);
            return buttonRect.gameObject;
        }

        private void CreateSearchBar(RectTransform parent)
        {
            var bar = CreateRect("SearchBar", parent);
            var barLayout = bar.gameObject.AddComponent<LayoutElement>();
            barLayout.preferredHeight = 48f;
            barLayout.minHeight = 48f;
            barLayout.flexibleHeight = 0f;
            barLayout.flexibleWidth = 1f;
            var barImage = AddImage(bar, SearchBarColor, HomeUiFonts.RoundedSprite, raycastTarget: true);
            barImage.type = Image.Type.Sliced;
            barImage.pixelsPerUnitMultiplier = 1.2f;

            var row = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(14, 8, 8, 8);
            row.spacing = 8f;
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = true;
            row.childForceExpandHeight = true;

            friendSearchInput = CreateSearchInput(bar);

            var searchRect = CreateRect("SearchButton", bar);
            var searchLayout = searchRect.gameObject.AddComponent<LayoutElement>();
            searchLayout.preferredWidth = 72f;
            searchLayout.minWidth = 72f;
            searchLayout.preferredHeight = 32f;
            searchLayout.minHeight = 32f;
            searchLayout.flexibleWidth = 0f;
            searchLayout.flexibleHeight = 0f;
            var searchImage = AddImage(searchRect, SearchButtonColor, HomeUiFonts.RoundedSprite, raycastTarget: true);
            searchImage.type = Image.Type.Sliced;
            searchImage.pixelsPerUnitMultiplier = 1.6f;
            var searchLabelRect = CreateRect("Label", searchRect);
            SetAnchor(searchLabelRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            searchLabelRect.offsetMin = Vector2.zero;
            searchLabelRect.offsetMax = Vector2.zero;
            var searchLabel = AddText(
                searchLabelRect,
                "검색",
                16f,
                FontStyles.Bold,
                TextAlignmentOptions.Center);
            searchLabel.color = Color.white;
            var searchButton = searchRect.gameObject.AddComponent<Button>();
            searchButton.targetGraphic = searchImage;
            searchButton.transition = Selectable.Transition.ColorTint;
            searchButton.navigation = new Navigation { mode = Navigation.Mode.None };
            searchButton.onClick.AddListener(OnSearchClicked);
            menuButtons.Add(searchButton);
        }

        private TMP_InputField CreateSearchInput(RectTransform parent)
        {
            var fieldRect = CreateRect("Input", parent);
            var fieldLayout = fieldRect.gameObject.AddComponent<LayoutElement>();
            fieldLayout.flexibleWidth = 1f;
            fieldLayout.minWidth = 80f;
            fieldLayout.preferredHeight = 32f;
            fieldLayout.flexibleHeight = 1f;
            AddImage(fieldRect, Color.clear, raycastTarget: true);

            var textArea = CreateRect("TextArea", fieldRect);
            SetAnchor(textArea, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            textArea.offsetMin = new Vector2(4f, 2f);
            textArea.offsetMax = new Vector2(-4f, -2f);
            textArea.gameObject.AddComponent<RectMask2D>();

            var placeholderRect = CreateRect("Placeholder", textArea);
            SetAnchor(placeholderRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            placeholderRect.offsetMin = Vector2.zero;
            placeholderRect.offsetMax = Vector2.zero;
            var placeholder = AddText(
                placeholderRect,
                "아이디로 검색",
                16f,
                FontStyles.Italic,
                TextAlignmentOptions.MidlineLeft);
            placeholder.color = new Color(0.45f, 0.45f, 0.45f, 1f);

            var textRect = CreateRect("Text", textArea);
            SetAnchor(textRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = AddText(
                textRect,
                string.Empty,
                16f,
                FontStyles.Normal,
                TextAlignmentOptions.MidlineLeft,
                raycastTarget: true);

            fieldRect.gameObject.SetActive(false);
            var input = fieldRect.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = textArea;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.fontAsset = koreanFont;
            input.pointSize = 16f;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterLimit = 32;
            input.onSubmit.AddListener(_ => OnSearchClicked());
            fieldRect.gameObject.SetActive(true);
            return input;
        }

        private RectTransform CreateSearchResults(RectTransform parent)
        {
            var scroll = CreateRect("Results", parent);
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
            AddImage(viewport, Color.clear, raycastTarget: true);

            var content = CreateRect("Content", viewport);
            SetAnchor(content, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 12f;
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
            var items = CreateItemGroup(content, "SearchItems");

            var emptyRect = CreateRect("EmptyHint", content);
            var emptyLayout = emptyRect.gameObject.AddComponent<LayoutElement>();
            emptyLayout.preferredHeight = 48f;
            emptyLayout.minHeight = 48f;
            searchEmptyText = AddText(
                emptyRect,
                "아이디를 검색해 보세요",
                16f,
                FontStyles.Normal,
                TextAlignmentOptions.Center);
            searchEmptyText.color = new Color(0.45f, 0.45f, 0.45f, 1f);
            searchEmptyText.textWrappingMode = TextWrappingModes.Normal;
            return items;
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
                row.PlayerId = friend.PlayerId;
                row.Nickname.text = friend.Nickname;
                row.Status.text = friend.Presence == FriendPresence.InGame ? "게임중" : string.Empty;

                // Disarmed on every draw. A row half way through asking for
                // confirmation must not stay that way for whoever lands in it
                // next, and a refresh is exactly when that happens.
                row.BlockArmed = false;
                if (row.BlockLabel != null)
                {
                    row.BlockLabel.text = "차단";
                }

                if (row.BlockButton != null)
                {
                    row.BlockButton.interactable = true;
                }

                if (row.RemoveButton != null)
                {
                    row.RemoveButton.interactable = true;
                }
            }
        }

        private void BindSearchRows(IReadOnlyList<FriendSearchHit> results)
        {
            while (searchRows.Count < results.Count)
            {
                searchRows.Add(CreateSearchRow(searchItemsRoot));
            }

            for (var index = 0; index < searchRows.Count; index++)
            {
                var row = searchRows[index];
                var isVisible = index < results.Count;
                row.Root.SetActive(isVisible);
                if (!isVisible)
                {
                    continue;
                }

                var hit = results[index];
                row.PlayerId = hit.PlayerId;
                row.Nickname.text = hit.Nickname;
                row.RequestLabel.text = hit.IsPending ? "요청 중" : "친구요청";
                row.RequestButton.interactable = !hit.IsPending;
            }
        }

        private SearchRow CreateSearchRow(RectTransform parent)
        {
            var rowRect = CreateRect("SearchRow", parent);
            var rowLayout = rowRect.gameObject.AddComponent<LayoutElement>();
            rowLayout.preferredHeight = 52f;
            rowLayout.minHeight = 52f;
            var rowImage = AddImage(rowRect, FriendRowColor, HomeUiFonts.RoundedSprite);
            rowImage.type = Image.Type.Sliced;
            rowImage.pixelsPerUnitMultiplier = 1.4f;
            AddDropShadow(rowRect.gameObject, ItemShadowColor, new Vector2(2f, -3f));
            AddDropShadow(rowRect.gameObject, new Color(0.12f, 0.12f, 0.12f, 0.16f), new Vector2(4f, -6f));

            var layout = rowRect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 8, 8);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            var avatar = CreateRect("Avatar", rowRect);
            var avatarLayout = avatar.gameObject.AddComponent<LayoutElement>();
            avatarLayout.preferredWidth = 36f;
            avatarLayout.preferredHeight = 36f;
            avatarLayout.minWidth = 36f;
            avatarLayout.minHeight = 36f;
            var avatarImage = AddImage(avatar, AvatarColor, HomeUiFonts.CircleSprite);
            avatarImage.preserveAspect = true;

            var nicknameRect = CreateRect("Nickname", rowRect);
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

            var requestRect = CreateRect("Request", rowRect);
            var requestLayout = requestRect.gameObject.AddComponent<LayoutElement>();
            requestLayout.preferredWidth = 88f;
            requestLayout.minWidth = 88f;
            requestLayout.preferredHeight = 32f;
            var requestLabel = AddText(
                requestRect,
                "친구요청",
                16f,
                FontStyles.Normal,
                TextAlignmentOptions.MidlineRight,
                raycastTarget: true);

            var row = new SearchRow
            {
                Root = rowRect.gameObject,
                Nickname = nickname,
                RequestLabel = requestLabel
            };
            var requestButton = requestRect.gameObject.AddComponent<Button>();
            requestButton.targetGraphic = requestLabel;
            requestButton.transition = Selectable.Transition.ColorTint;
            requestButton.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.black;
            colors.highlightedColor = TextHover;
            colors.pressedColor = TextPressed;
            colors.selectedColor = Color.black;
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 1f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            requestButton.colors = colors;
            requestButton.onClick.AddListener(() =>
            {
                if (!string.IsNullOrEmpty(row.PlayerId))
                {
                    FriendRequestClicked?.Invoke(row.PlayerId);
                }
            });
            row.RequestButton = requestButton;
            return row;
        }

        /// <summary>
        /// The received requests, above the search box.
        /// </summary>
        /// <remarks>
        /// Above rather than on a tab of its own: a request is something waiting
        /// for an answer, and putting it behind a tab hides the one thing in
        /// this panel that somebody else is waiting on.
        /// <para>
        /// No scroll view. Requests are few, and the section takes only the
        /// height its rows need, so the search results below keep the rest.
        /// </para>
        /// </remarks>
        private RectTransform CreateRequestsSection(
            RectTransform parent, string heading, out GameObject section)
        {
            var sectionRect = CreateRect(heading, parent);
            var sectionObject = sectionRect.gameObject;
            var section_ = sectionRect;
            var sectionLayout = sectionObject.AddComponent<VerticalLayoutGroup>();
            sectionLayout.spacing = 8f;
            sectionLayout.childAlignment = TextAnchor.UpperLeft;
            sectionLayout.childControlWidth = true;
            sectionLayout.childControlHeight = true;
            sectionLayout.childForceExpandWidth = true;
            sectionLayout.childForceExpandHeight = false;

            var headerRect = CreateRect("Header", section_);
            var headerLayout = headerRect.gameObject.AddComponent<LayoutElement>();
            headerLayout.preferredHeight = 24f;
            headerLayout.minHeight = 24f;
            AddText(
                headerRect,
                heading,
                18f,
                FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft);

            var items = CreateRect("Items", section_);
            var itemsLayout = items.gameObject.AddComponent<VerticalLayoutGroup>();
            itemsLayout.spacing = 8f;
            itemsLayout.childAlignment = TextAnchor.UpperLeft;
            itemsLayout.childControlWidth = true;
            itemsLayout.childControlHeight = true;
            itemsLayout.childForceExpandWidth = true;
            itemsLayout.childForceExpandHeight = false;

            section = sectionObject;
            section.SetActive(false);
            return items;
        }

        private void BindRequestRows(
            List<RequestRow> rows,
            RectTransform parent,
            IReadOnlyList<FriendRequestSummary> requests,
            bool sent)
        {
            while (rows.Count < requests.Count)
            {
                rows.Add(CreateRequestRow(parent, sent));
            }

            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                var isVisible = index < requests.Count;
                row.Root.SetActive(isVisible);
                if (!isVisible)
                {
                    continue;
                }

                var request = requests[index];
                row.PlayerId = request.PlayerId;
                row.Nickname.text = request.Nickname;

                // Re-enabled on every bind. A row is reused by whoever lands in
                // that slot next, and a button left insensitive by the previous
                // occupant would refuse the new one.
                if (row.AcceptButton != null)
                {
                    row.AcceptButton.interactable = true;
                }

                row.DeclineButton.interactable = true;
            }
        }

        /// <param name="sent">
        /// True for a request this player sent, which can only be taken back.
        /// A received one is answered either way, so it carries two buttons.
        /// </param>
        private RequestRow CreateRequestRow(RectTransform parent, bool sent)
        {
            var rowRect = CreateRect("RequestRow", parent);
            var rowLayout = rowRect.gameObject.AddComponent<LayoutElement>();
            rowLayout.preferredHeight = 52f;
            rowLayout.minHeight = 52f;
            var rowImage = AddImage(rowRect, FriendRowColor, HomeUiFonts.RoundedSprite);
            rowImage.type = Image.Type.Sliced;
            rowImage.pixelsPerUnitMultiplier = 1.4f;
            AddDropShadow(rowRect.gameObject, ItemShadowColor, new Vector2(2f, -3f));

            var layout = rowRect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(10, 8, 8, 8);
            layout.spacing = 4f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            // No avatar, unlike the friend and search rows. This row carries two
            // buttons where they carry one or none, and with a circle in front
            // as well a twelve character nickname is cut to about six.
            var nicknameRect = CreateRect("Nickname", rowRect);
            var nicknameLayout = nicknameRect.gameObject.AddComponent<LayoutElement>();
            nicknameLayout.flexibleWidth = 1f;
            nicknameLayout.minWidth = 40f;
            var nickname = AddText(
                nicknameRect,
                string.Empty,
                17f,
                FontStyles.Normal,
                TextAlignmentOptions.MidlineLeft);
            nickname.overflowMode = TextOverflowModes.Ellipsis;

            var row = new RequestRow
            {
                Root = rowRect.gameObject,
                Nickname = nickname
            };

            if (!sent)
            {
                row.AcceptButton = CreateRowTextButton(
                    rowRect,
                    "Accept",
                    "수락",
                    40f,
                    () => AnswerRequest(row, accepted: true));
            }

            row.DeclineButton = CreateRowTextButton(
                rowRect,
                sent ? "Cancel" : "Decline",
                sent ? "취소" : "거절",
                40f,
                () => AnswerRequest(row, accepted: false, cancelled: sent));

            return row;
        }

        /// <remarks>
        /// Both buttons go insensitive on either click. The answer takes a round
        /// trip, and a second click would answer a request that no longer
        /// exists. The next bind turns them back on.
        /// </remarks>
        private void AnswerRequest(RequestRow row, bool accepted, bool cancelled = false)
        {
            if (string.IsNullOrEmpty(row.PlayerId))
            {
                return;
            }

            if (row.AcceptButton != null)
            {
                row.AcceptButton.interactable = false;
            }

            row.DeclineButton.interactable = false;

            if (cancelled)
            {
                FriendRequestCancelled?.Invoke(row.PlayerId);
                return;
            }

            if (accepted)
            {
                FriendRequestAccepted?.Invoke(row.PlayerId);
            }
            else
            {
                FriendRequestDeclined?.Invoke(row.PlayerId);
            }
        }

        private Button CreateRowTextButton(
            RectTransform parent,
            string name,
            string label,
            float width,
            Action onClicked)
        {
            var rect = CreateRect(name, parent);
            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.minWidth = width;
            layout.preferredHeight = 32f;
            var text = AddText(
                rect,
                label,
                16f,
                FontStyles.Normal,
                TextAlignmentOptions.Center,
                raycastTarget: true);

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = text;
            button.transition = Selectable.Transition.ColorTint;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.black;
            colors.highlightedColor = TextHover;
            colors.pressedColor = TextPressed;
            colors.selectedColor = Color.black;
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 1f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(() => onClicked?.Invoke());
            return button;
        }

        private void OnSearchClicked()
        {
            FriendSearchRequested?.Invoke(friendSearchInput != null ? friendSearchInput.text : string.Empty);
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

            // Tighter than the other rows because this one carries two actions
            // beside a status badge. The panel leaves 284 for a row, and at the
            // old spacing a second button pushed the nickname under its own
            // minimum and out of the panel.
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 8, 8);
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            // No avatar. It was a plain grey circle carrying no picture and no
            // information, and the 42 it took left the nickname 76 to work with —
            // enough to cut a six character name in half.
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
            statusLayout.preferredWidth = 56f;
            statusLayout.minWidth = 56f;
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

            var friendRow = new FriendRow
            {
                Root = row.gameObject,
                Nickname = nickname,
                Status = status
            };

            // Ending the friendship first, blocking second: the pair reads left
            // to right from the lighter action to the heavier one.
            friendRow.RemoveButton = CreateRowTextButton(
                row,
                "Remove",
                "삭제",
                36f,
                () => RemoveFriend(friendRow));

            friendRow.BlockButton = CreateRowTextButton(
                row,
                "Block",
                "차단",
                36f,
                () => ConfirmBlock(friendRow));

            friendRow.BlockLabel = friendRow.BlockButton.GetComponentInChildren<TMP_Text>();
            return friendRow;
        }

        /// <summary>
        /// Ends the friendship on the first press.
        /// </summary>
        /// <remarks>
        /// No confirmation, unlike blocking. Both people can still find each
        /// other afterwards and either can ask again, so the worst a stray press
        /// costs is one more request.
        /// </remarks>
        private void RemoveFriend(FriendRow row)
        {
            if (string.IsNullOrEmpty(row.PlayerId))
            {
                return;
            }

            row.RemoveButton.interactable = false;
            row.BlockButton.interactable = false;
            FriendRemoved?.Invoke(row.PlayerId);
        }

        /// <summary>
        /// Two presses to block: the first asks, the second does it.
        /// </summary>
        /// <remarks>
        /// A confirmation step rather than a dialog, because the row is small and
        /// a modal over a modal is worse than a label that changes. Blocking is
        /// not only hiding someone — the server ends the friendship and drops any
        /// request with it — so a single stray press should not do it.
        /// <para>
        /// The armed state lasts until this list is drawn again, which happens on
        /// every refresh, so it cannot sit armed for long.
        /// </para>
        /// </remarks>
        private void ConfirmBlock(FriendRow row)
        {
            if (string.IsNullOrEmpty(row.PlayerId))
            {
                return;
            }

            if (!row.BlockArmed)
            {
                row.BlockArmed = true;
                row.BlockLabel.text = "확인?";
                return;
            }

            row.BlockArmed = false;
            row.BlockButton.interactable = false;
            FriendBlocked?.Invoke(row.PlayerId);
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
            text.color = Color.white;
            var button = buttonRect.gameObject.AddComponent<Button>();
            button.targetGraphic = text;
            button.transition = Selectable.Transition.ColorTint;
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.black;
            colors.highlightedColor = MenuHover;
            colors.pressedColor = MenuPressed;
            colors.selectedColor = Color.black;
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(() => ActionClicked?.Invoke(action));
            menuButtons.Add(button);
            return buttonRect;
        }

        private static void ClearButtons(List<Button> buttons)
        {
            for (var index = 0; index < buttons.Count; index++)
            {
                if (buttons[index] != null)
                {
                    buttons[index].onClick.RemoveAllListeners();
                }
            }
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
            public string PlayerId;
            public TMP_Text Nickname;
            public TMP_Text Status;
            public Button RemoveButton;
            public Button BlockButton;
            public TMP_Text BlockLabel;
            public bool BlockArmed;
        }

        private sealed class RequestRow
        {
            public GameObject Root;
            public string PlayerId;
            public TMP_Text Nickname;
            public Button AcceptButton;
            public Button DeclineButton;
        }

        private sealed class SearchRow
        {
            public GameObject Root;
            public string PlayerId;
            public TMP_Text Nickname;
            public TMP_Text RequestLabel;
            public Button RequestButton;
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
