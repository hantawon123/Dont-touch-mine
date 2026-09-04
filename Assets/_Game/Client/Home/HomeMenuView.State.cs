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
        private TMP_FontAsset fontAsset;

        [SerializeField]
        private TMP_Text nicknameText;

        private readonly List<Button> menuButtons = new List<Button>();
        private readonly List<FriendRow> onlineRows = new List<FriendRow>();
        private readonly List<FriendRow> offlineRows = new List<FriendRow>();
        private readonly List<SearchRow> searchRows = new List<SearchRow>();
        private readonly List<RequestRow> requestRows = new List<RequestRow>();
        private readonly List<RequestRow> sentRows = new List<RequestRow>();
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
        private GameObject requestsSection;
        private RectTransform requestsItemsRoot;
        private GameObject sentSection;
        private RectTransform sentItemsRoot;
        private TMP_Text requestsEmptyText;
        private TMP_Text sentEmptyText;
        private GameObject refreshButton;
        private TMP_InputField friendSearchInput;
        private TMP_Text searchEmptyText;
        private Button dismissButton;
        private GameObject profileSettingsRoot;
        private TMP_InputField profileNicknameInput;
        private TMP_Text appliedFeedbackText;
        private TMP_Text nicknameErrorText;

        public event Action<HomeMenuAction> ActionClicked;

        public event Action FriendListDismissed;

        public event Action ProfileSettingsDismissed;

        public event Action<string> NicknameChangeRequested;

        public event Action<string> NicknameEdited;

        public event Action FriendSearchOpened;

        public event Action FriendSearchClosed;

        public event Action<string> FriendSearchRequested;

        public event Action<string> FriendRequestClicked;

        public event Action<string> FriendRequestAccepted;

        public event Action<string> FriendRequestDeclined;

        public event Action<string> FriendRequestCancelled;

        public event Action FriendListRefreshRequested;

        public event Action<string> FriendRemoved;


        private void Awake()
        {
            EnsureEventSystem();
            if (nicknameText == null)
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

            ClearRequestRows(requestRows);
            ClearRequestRows(sentRows);
            ClearFriendRows(onlineRows);
            ClearFriendRows(offlineRows);

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

        public void SetProfileSettingsVisible(bool visible)
        {
            if (profileSettingsRoot == null)
            {
                return;
            }

            profileSettingsRoot.SetActive(visible);
        }

        public void SetNicknameError(string message)
        {
            if (nicknameErrorText == null)
            {
                return;
            }

            var hasMessage = !string.IsNullOrEmpty(message);
            nicknameErrorText.text = message ?? string.Empty;
            nicknameErrorText.gameObject.SetActive(hasMessage);

            // The two never show together. One says the name was taken and the
            // other says it was saved, and both at once is a contradiction.
            if (hasMessage && appliedFeedbackText != null)
            {
                appliedFeedbackText.gameObject.SetActive(false);
            }
        }

        public void SetNicknameAppliedFeedbackVisible(bool visible)
        {
            if (appliedFeedbackText == null)
            {
                return;
            }

            appliedFeedbackText.gameObject.SetActive(visible);

            if (visible && nicknameErrorText != null)
            {
                nicknameErrorText.gameObject.SetActive(false);
            }
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

            if (refreshButton != null)
            {
                refreshButton.SetActive(!visible);
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

        /// <remarks>
        /// The section stays on screen either way. It used to disappear when it
        /// held nothing, which saved room but hid the fact that requests can be
        /// accepted here at all.
        /// </remarks>
        private static void ShowEmptyLine(TMP_Text line, bool isEmpty)
        {
            if (line != null)
            {
                line.gameObject.SetActive(isEmpty);
            }
        }

        private static void ClearFriendRows(List<FriendRow> rows)
        {
            for (var index = 0; index < rows.Count; index++)
            {
                if (rows[index].RemoveButton != null)
                {
                    rows[index].RemoveButton.onClick.RemoveAllListeners();
                }
            }
        }

        private static void ClearRequestRows(List<RequestRow> rows)
        {
            for (var index = 0; index < rows.Count; index++)
            {
                if (rows[index].AcceptButton != null)
                {
                    rows[index].AcceptButton.onClick.RemoveAllListeners();
                }

                if (rows[index].DeclineButton != null)
                {
                    rows[index].DeclineButton.onClick.RemoveAllListeners();
                }
            }
        }

        public void SetOutgoingRequests(IReadOnlyList<FriendRequestSummary> requests)
        {
            if (requests == null)
            {
                throw new ArgumentNullException(nameof(requests));
            }

            if (sentItemsRoot == null || sentSection == null)
            {
                return;
            }

            ShowEmptyLine(sentEmptyText, requests.Count == 0);
            BindRequestRows(sentRows, sentItemsRoot, requests, sent: true);
        }

        public void SetIncomingRequests(IReadOnlyList<FriendRequestSummary> requests)
        {
            if (requests == null)
            {
                throw new ArgumentNullException(nameof(requests));
            }

            if (requestsItemsRoot == null || requestsSection == null)
            {
                return;
            }

            ShowEmptyLine(requestsEmptyText, requests.Count == 0);
            BindRequestRows(requestRows, requestsItemsRoot, requests, sent: false);
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

