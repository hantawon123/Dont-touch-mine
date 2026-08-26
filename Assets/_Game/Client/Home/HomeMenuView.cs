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
        private TMP_FontAsset koreanFont;

        public event Action<HomeMenuAction> ActionClicked;

        private void Awake()
        {
            EnsureEventSystem();
            if (nicknameText == null || levelText == null)
            {
                BuildLayout();
            }
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
            CreateSystemButtons(canvas);
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
            CreateTextButton(left, "친구 목록", HomeMenuAction.Friends, TextAlignmentOptions.MidlineLeft);
        }

        private void CreateSystemButtons(RectTransform canvas)
        {
            var right = CreateRect("SystemButtons", canvas);
            SetAnchor(right, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f));
            right.anchoredPosition = new Vector2(-48f, 48f);
            right.sizeDelta = new Vector2(280f, 140f);
            AddVerticalLayout(right, TextAnchor.LowerRight);

            CreateTextButton(right, "환경 설정", HomeMenuAction.Settings, TextAlignmentOptions.MidlineRight);
            CreateTextButton(right, "게임 종료", HomeMenuAction.Quit, TextAlignmentOptions.MidlineRight);
        }

        private void CreateTextButton(
            RectTransform parent,
            string label,
            HomeMenuAction action,
            TextAlignmentOptions alignment)
        {
            var buttonRect = CreateRect(action.ToString(), parent);
            buttonRect.sizeDelta = new Vector2(280f, 56f);
            var layoutElement = buttonRect.gameObject.AddComponent<LayoutElement>();
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

        private static Image AddImage(RectTransform rect, Color color, Sprite sprite = null)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite != null ? sprite : HomeUiFonts.WhiteSprite;
            image.type = Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
            return image;
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
