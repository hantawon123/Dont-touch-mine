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
        private readonly List<SearchRow> searchRows = new List<SearchRow>();
        private TMP_FontAsset koreanFont;
        private GameObject friendListRoot;
        private GameObject friendListBody;
        private GameObject friendSearchBody;
        private GameObject addFriendButton;
        private GameObject closeSearchButton;
        private TMP_Text panelHeaderText;
        private TMP_Text onlineSectionText;
        private TMP_Text offlineSectionText;
        private RectTransform onlineItemsRoot;
        private RectTransform offlineItemsRoot;
        private RectTransform searchItemsRoot;
        private TMP_InputField friendSearchInput;
        private TMP_Text searchEmptyText;
        private Button dismissButton;
        private GameObject profileSettingsRoot;
        private TMP_InputField profileNicknameInput;
        private TMP_Text profileLevelText;
        private TMP_Text appliedFeedbackText;

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
            if (nicknameText == null || levelText == null)
            {
                BuildLayout();
            }

            SetFriendListVisible(false);
            SetProfileSettingsVisible(false);
            SetNicknameAppliedFeedbackVisible(false);
        }

        private void OnDestroy()
        {
            ClearButtons(menuButtons);
            for (var index = 0; index < searchRows.Count; index++)
            {
                if (searchRows[index].RequestButton != null)
                {
                    searchRows[index].RequestButton.onClick.RemoveAllListeners();
                }
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
        }

        public void SetNickname(string nickname)
        {
            if (nicknameText != null)
            {
                nicknameText.text = nickname;
            }

            if (profileNicknameInput != null && profileNicknameInput.text != nickname)
            {
                profileNicknameInput.text = nickname;
            }
        }

        public void SetLevel(int level)
        {
            var label = $"Lv.{level}";
            if (levelText != null)
            {
                levelText.text = label;
            }

            if (profileLevelText != null)
            {
                profileLevelText.text = label;
            }
        }

        public void SetProfileSettingsVisible(bool visible)
        {
            if (profileSettingsRoot == null)
            {
                return;
            }

            profileSettingsRoot.SetActive(visible);
        }

        public void SetNicknameAppliedFeedbackVisible(bool visible)
        {
            if (appliedFeedbackText == null)
            {
                return;
            }

            appliedFeedbackText.gameObject.SetActive(visible);
        }

        public void SetFriendListVisible(bool visible)
        {
            if (friendListRoot == null)
            {
                return;
            }

            SetFriendSearchVisible(false);
            friendListRoot.SetActive(visible);
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

        public void SetFriendSearchVisible(bool visible)
        {
            if (friendListBody == null || friendSearchBody == null)
            {
                return;
            }

            friendListBody.SetActive(!visible);
            friendSearchBody.SetActive(visible);
            if (panelHeaderText != null)
            {
                panelHeaderText.text = visible ? "친구 검색" : "친구";
            }

            if (addFriendButton != null)
            {
                addFriendButton.SetActive(!visible);
            }

            if (closeSearchButton != null)
            {
                closeSearchButton.SetActive(visible);
            }

            if (visible && friendSearchInput != null)
            {
                friendSearchInput.text = string.Empty;
            }

            if (visible)
            {
                UpdateSearchEmptyHint(Array.Empty<FriendSearchHit>());
            }
        }

        public void SetFriendSearchResults(IReadOnlyList<FriendSearchHit> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            if (searchItemsRoot == null)
            {
                return;
            }

            BindSearchRows(results);
            UpdateSearchEmptyHint(results);
        }

        private void UpdateSearchEmptyHint(IReadOnlyList<FriendSearchHit> results)
        {
            if (searchEmptyText == null)
            {
                return;
            }

            var hasQuery = friendSearchInput != null && !string.IsNullOrWhiteSpace(friendSearchInput.text);
            searchEmptyText.gameObject.SetActive(results.Count == 0);
            if (results.Count > 0)
            {
                return;
            }

            searchEmptyText.text = hasQuery ? "검색 결과가 없습니다" : "아이디를 검색해 보세요";
        }

    }
}

