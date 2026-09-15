using System;
using System.Collections.Generic;
using Game.Core.Home;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Home
{
    public sealed partial class HomeMenuView
    {
        [Header("Art")]
        [SerializeField]
        private Sprite backgroundSprite;

        [SerializeField]
        private Sprite friendIcon;

        [SerializeField]
        private Sprite serverIcon;

        [SerializeField]
        private Sprite checkIcon;

        [SerializeField]
        private Sprite searchIcon;

        [SerializeField]
        private Sprite clearIcon;

        [SerializeField]
        private Sprite refreshIcon;

        [SerializeField]
        private Sprite steamIcon;

        [SerializeField]
        private Sprite acceptIcon;

        [SerializeField]
        private Sprite rejectIcon;

        [SerializeField]
        private Sprite friendPlusIcon;

        [SerializeField]
        private Sprite closeIcon;

        [SerializeField]
        private Sprite alertIcon;

        [Header("Fonts")]
        [SerializeField]
        private TMP_FontAsset fontAsset;

        /// <summary>
        /// The menu is drawn in SemiBold where the rest of the screen is not,
        /// so it is a second asset rather than a style flag: TextMeshPro fakes a
        /// bold weight by smearing the glyph, and the mock-up wants the weight
        /// the type designer drew.
        /// </summary>
        [SerializeField]
        private TMP_FontAsset semiBoldFont;

        [SerializeField]
        private TMP_Text nicknameText;

        private RectTransform profileChip;

        private readonly List<Button> menuButtons = new List<Button>();

        /// <summary>
        /// The hairline of each bottom-bar button, by what it opens.
        /// </summary>
        private readonly Dictionary<HomeMenuAction, HomeHoverHighlight> actionHighlights =
            new Dictionary<HomeMenuAction, HomeHoverHighlight>();
        private TMP_FontAsset koreanFont;
        private GameObject friendListRoot;
        private GameObject friendListBody;
        private GameObject friendSearchBody;
        private TMP_Text onlineSectionText;
        private TMP_Text offlineSectionText;
        private TMP_Text searchSectionText;
        private TMP_Text requestSectionText;
        private RectTransform onlineItemsRoot;
        private RectTransform offlineItemsRoot;
        private RectTransform searchItemsRoot;
        private RectTransform requestItemsRoot;
        private RectTransform listContentRoot;
        private RectTransform requestContentRoot;
        private TMP_Text friendListTab;
        private TMP_Text friendRequestTab;
        private Image listRule;
        private Image requestRule;
        private GameObject requestBadge;
        private TMP_Text requestBadgeText;
        private GameObject friendButtonBadge;
        private TMP_Text friendButtonBadgeText;
        private bool isRequestTabOpen;

        /// <summary>
        /// The syllable the IME is still building, which never reaches the
        /// input field's own text.
        /// </summary>
        private string composingText = string.Empty;
        private TMP_InputField friendSearchInput;

        /// <summary>
        /// What the last search came back with, kept so that clearing an error
        /// can tell an empty result from a result nobody has looked at yet.
        /// </summary>
        /// <remarks>
        /// Without it a successful search told the panel twice: the rows
        /// arrived, and then the success cleared the last failure, which had no
        /// way to know the rows were there and put "찾을 수 없습니다" back over
        /// a player who had just been found.
        /// </remarks>
        private IReadOnlyList<FriendSearchHit> lastSearchResults =
            Array.Empty<FriendSearchHit>();
        private TMP_Text searchEmptyText;
        private TMP_Text onlineEmptyText;
        private TMP_Text offlineEmptyText;
        private int shownOnlineCount;
        private int shownOfflineCount;
        private Button dismissButton;
        private GameObject profileSettingsRoot;
        private RectTransform profilePanel;
        private GameObject serverSettingsRoot;
        private GameObject createRoomRoot;
        private TMP_InputField roomNameInput;
        private TMP_Text roomNameCounter;

        /// <summary>
        /// The syllable the IME is still building in the room-name box, which
        /// never reaches the field's own text. See <c>composingText</c>.
        /// </summary>
        private string roomNameComposing = string.Empty;

        /// <summary>
        /// Set when the room-name box was deselected to end a refused
        /// syllable, so the next frame gives it focus back.
        /// </summary>
        private bool roomNameRefocusPending;

        /// <summary>
        /// The syllable that was last dropped that way, so the same one is not
        /// dropped again if the IME kept it.
        /// </summary>
        private string roomNameDroppedComposing = string.Empty;
        private TMP_Text privateSegment;
        private TMP_Text publicSegment;
        private RectTransform scopeIndicator;
        private TMP_Text playerCountText;
        private Button decreaseButton;
        private Button increaseButton;
        private Button createRoomButton;
        private Image createRoomFill;
        private TMP_Text createRoomLabel;
        private bool isPublicRoom = true;
        private int playerCount = 6;
        private TMP_InputField profileNicknameInput;
        private TMP_Text nicknameMessageText;
        private TMP_Text nicknameCounterText;
        private Image searchAllowFill;
        private Image searchAllowKnobImage;
        private RectTransform searchAllowKnob;
        private TMP_Text searchAllowMessageText;
        private Game.Client.Common.ConnectionToast connectionToast;
        private Button applyButton;
        private Image applyFill;
        private TMP_Text applyLabel;
        private bool isSearchAllowed;

        /// <summary>
        /// Set while the field is being corrected, so the edit handler does not
        /// answer its own rewrite.
        /// </summary>
        private bool isRewritingNickname;

        /// <summary>
        /// The name in use, so the apply button can tell a change from a
        /// re-typing of what is already there.
        /// </summary>
        private string currentNickname = string.Empty;
        private bool currentNicknameSet;
        private bool isConfirmingNickname;
        private GameObject confirmRow;

        public event Action<HomeMenuAction> ActionClicked;

        public event Action FriendListDismissed;

        public event Action ProfileSettingsDismissed;

        public event Action<string> NicknameChangeRequested;

        public event Action<string> NicknameEdited;

        public event Action FriendSearchOpened;

        public event Action FriendSearchClosed;

        public event Action<string> FriendSearchRequested;

        public event Action<string> FriendRequestClicked;

        private void Awake()
        {
            EnsureEventSystem();
            if (nicknameText == null)
            {
                BuildLayout();
            }

            SetFriendListVisible(false);
            SetProfileSettingsVisible(false);
        }

        /// <summary>
        /// The IME reports its half-built syllable by polling only, so the
        /// boxes that care read it here each frame they have focus.
        /// </summary>
        private void LateUpdate()
        {
            PollRoomNameComposition();
            PollFriendSearchComposition();
        }

        private void OnDestroy()
        {
            ClearButtons(menuButtons);
            ClearRowButtons(requestRows);
            actionHighlights.Clear();

            // A root of its own is not a child, so it does not go with this
            // object. It is taken down by hand rather than left behind.
            if (inviteRoot != null)
            {
                Destroy(inviteRoot);
                inviteRoot = null;
            }

            if (dismissButton != null)
            {
                dismissButton.onClick.RemoveAllListeners();
            }

            if (friendSearchInput != null)
            {
                friendSearchInput.onSubmit.RemoveAllListeners();
            }

            if (profileNicknameInput != null)
            {
                profileNicknameInput.onValueChanged.RemoveAllListeners();
            }

            WatchComposition(false);
            WatchRoomNameComposition(false);
        }

        public void SetNickname(string nickname)
        {
            if (nicknameText != null)
            {
                nicknameText.text = nickname;

                // The chip is only as wide as the name inside it, so a longer
                // one has to move the chip's own edge before it is drawn.
                nicknameText.ForceMeshUpdate();
                ResizeProfileChip();
            }

            // The field opens on the name the player already has, which is what
            // the design calls the default.
            if (profileNicknameInput != null && profileNicknameInput.text != nickname)
            {
                profileNicknameInput.text = nickname;
            }

            currentNickname = nickname ?? string.Empty;
            UpdateNicknameCounter(currentNickname);
            ClearNicknameMessage();
            UpdateNicknameApplyEnabled();
        }

        /// <summary>
        /// Nothing to draw. The revised design shows no level anywhere on Home
        /// — the chip carries the name alone, and the profile panel is only
        /// about the nickname.
        /// </summary>
        /// <remarks>
        /// Kept because <see cref="IHomeMenuView"/> still declares it and the
        /// presenter still binds the profile. Dropping it from the interface is
        /// a change to the presenter and its tests, not to this screen.
        /// </remarks>
        public void SetLevel(int level)
        {
        }

        /// <summary>
        /// Nothing to draw. The revised panel answers with the message line
        /// under the field rather than a separate "applied" note.
        /// </summary>
        public void SetNicknameAppliedFeedbackVisible(bool visible)
        {
        }

        public void SetProfileSettingsVisible(bool visible)
        {
            SetActionSelected(HomeMenuAction.ProfileSettings, visible);
            if (profileSettingsRoot == null)
            {
                return;
            }

            profileSettingsRoot.SetActive(visible);
        }

        /// <summary>
        /// Marks the button that opens a panel while that panel is up.
        /// </summary>
        /// <remarks>
        /// Driven from the same setters the presenter already calls, so the
        /// mark cannot drift from what is on screen: there is no second flag
        /// to keep in step, and a panel closed by any route clears its own.
        /// </remarks>
        private void SetActionSelected(HomeMenuAction action, bool selected)
        {
            if (actionHighlights.TryGetValue(action, out var highlight)
                && highlight != null)
            {
                highlight.SetSelected(selected);
            }
        }

        public bool FriendListVisible => friendListRoot != null && friendListRoot.activeInHierarchy;

        public void SetFriendListVisible(bool visible)
        {
            SetActionSelected(HomeMenuAction.Friends, visible);
            if (friendListRoot == null)
            {
                return;
            }

            SetFriendSearchVisible(false);
            friendListRoot.SetActive(visible);

            // The keyboard is the whole application's, so the panel stops
            // listening to it the moment it goes away.
            WatchComposition(visible);
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

            BindFriendRows(onlineItemsRoot, onlineFriends, online: true);
            BindFriendRows(offlineItemsRoot, offlineFriends, online: false);
            if (friendContextPlayerId != null)
            {
                var stillVisible = false;
                foreach (var friend in onlineFriends)
                    stillVisible |= friend.PlayerId == friendContextPlayerId;
                foreach (var friend in offlineFriends)
                    stillVisible |= friend.PlayerId == friendContextPlayerId;
                if (!stillVisible) CloseFriendContextMenu();
            }
        }

        public void SetFriendSearchVisible(bool visible)
        {
            CloseFriendContextMenu();
            isRequestTabOpen = visible;
            if (friendListBody == null || friendSearchBody == null)
            {
                return;
            }

            friendListBody.SetActive(!visible);
            friendSearchBody.SetActive(visible);
            ApplyTabColours();

            // The box is shared by both tabs, so what was typed on one would
            // otherwise still be filtering the other.
            ClearFriendSearch();
            WatchComposition(true);

            lastSearchResults = Array.Empty<FriendSearchHit>();
            UpdateSearchEmptyHint(lastSearchResults);
        }

        public void SetFriendSearchResults(IReadOnlyList<FriendSearchHit> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            lastSearchResults = results;
            BindSearchRows(results);
            UpdateSearchEmptyHint(results);
        }


    }
}

