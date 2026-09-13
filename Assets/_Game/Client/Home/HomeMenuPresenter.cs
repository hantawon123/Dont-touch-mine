using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Client.Common;
using Game.Core.Flow;
using Game.Core.Home;
using UnityEngine;
using VContainer.Unity;

namespace Game.Client.Home
{
    public interface IHomeApplicationHost
    {
        void Quit();

        void OpenHome();

        void OpenRoomBrowser();

        /// <summary>Opens the character closet.</summary>
        void OpenCharacterCloset();

        /// <summary>Opens the settings screen.</summary>
        void OpenSettings();

        /// <summary>
        /// Opens a room with these settings and, if it opens, goes to its
        /// lobby.
        /// </summary>
        /// <remarks>
        /// Handed to the host rather than done in the presenter because
        /// creating a room is a network call, and the presenter's job ends at
        /// deciding that one should be made.
        /// </remarks>
        void CreateRoom(string title, bool isPublic, int maxPlayers);

        /// <summary>
        /// Enters the room a friend invited this player to, by its code, and
        /// opens the lobby. Failures are said on screen, as room creation's are.
        /// </summary>
        void JoinRoom(string roomCode);

        void OpenLobby();
    }

    public sealed class UnityHomeApplicationHost : IHomeApplicationHost
    {
        public const string HomeSceneName = "Home";
        public const string RoomBrowserSceneName = "Room";
        public const string LobbySceneName = "Lobby";
        public const string CharacterClosetSceneName = "Character";
        public const string SettingsSceneName = "Settings";

        public void Quit()
        {
            Application.Quit();
#if UNITY_EDITOR
            var editorApplicationType = Type.GetType("UnityEditor.EditorApplication, UnityEditor");
            editorApplicationType?.GetProperty("isPlaying")?.SetValue(null, false);
#endif
        }

        /// <summary>
        /// The two ways out of a screen, loaded across frames so the loading
        /// cover can keep drawing until activation.
        /// </summary>
        /// <remarks>
        /// Both are taken while something is still live: the browser may be
        /// connecting to matchmaking when Home is asked for, and the lobby is in
        /// a room when the browser is.
        /// <para>
        /// This alone does not make leaving safe. The unload still runs on the
        /// main thread as part of the load, so anything a departing scene does
        /// during <c>OnDestroy</c> that needs frames of its own will still
        /// deadlock here — see <c>NetworkRoomScreenBridge.Dispose</c>, which is
        /// where that was actually fixed.
        /// </para>
        /// </remarks>
        public void OpenHome()
        {
            SceneLoadSlicer.LoadSingleAsync(HomeSceneName)
                .Forget(exception => Debug.LogException(exception));
        }

        /// <inheritdoc cref="OpenHome"/>
        public void OpenRoomBrowser()
        {
            SceneLoadSlicer.LoadSingleAsync(RoomBrowserSceneName)
                .Forget(exception => Debug.LogException(exception));
        }

        /// <inheritdoc cref="OpenHome"/>
        public void OpenCharacterCloset()
        {
            SceneLoadSlicer.LoadSingleAsync(CharacterClosetSceneName)
                .Forget(exception => Debug.LogException(exception));
        }

        /// <inheritdoc cref="OpenHome"/>
        public void OpenSettings()
        {
            SceneLoadSlicer.LoadSingleAsync(SettingsSceneName)
                .Forget(exception => Debug.LogException(exception));
        }

        /// <summary>
        /// Nothing to do without a network runner. The scene that owns one
        /// replaces this host; this fallback exists for the editor and for
        /// tests, where there is no room to open.
        /// </summary>
        public void CreateRoom(string title, bool isPublic, int maxPlayers)
        {
            Debug.LogWarning(
                "[Home] Cannot create a room without the networked host.");
        }

        public void JoinRoom(string roomCode)
        {
            Debug.LogWarning(
                "[Home] Cannot join a room without the networked host.");
        }

        public void OpenLobby()
        {
            SceneLoadSlicer.LoadSingleAsync(LobbySceneName)
                .Forget(exception => Debug.LogException(exception));
        }
    }

    public sealed class HomeMenuPresenter : IStartable, IDisposable
    {
        private readonly PlayerProfile profile;
        private readonly HomeMenuSystem menu;
        private readonly FriendListSystem friends;
        private readonly FriendSearchSystem search;
        private readonly IHomeMenuView view;
        private readonly IHomeApplicationHost applicationHost;
        private readonly AppFlowSystem appFlow;
        private readonly ServerRegionSystem regions;
        private bool isFriendListVisible;
        private bool isRequestTabOpen;

        /// <summary>
        /// What was typed while the list tab was showing. Held here because the
        /// list is rebuilt from the friend system whenever it changes, and the
        /// filter has to survive that.
        /// </summary>
        private string listFilter = string.Empty;
        private bool isProfileSettingsVisible;
        private bool isServerSettingsVisible;

        public HomeMenuPresenter(
            PlayerProfile profile,
            HomeMenuSystem menu,
            IHomeMenuView view,
            IHomeApplicationHost applicationHost,
            AppFlowSystem appFlow,
            FriendListSystem friends,
            FriendSearchSystem search,
            ServerRegionSystem regions)
        {
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            this.menu = menu ?? throw new ArgumentNullException(nameof(menu));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.applicationHost = applicationHost
                ?? throw new ArgumentNullException(nameof(applicationHost));
            this.appFlow = appFlow ?? throw new ArgumentNullException(nameof(appFlow));
            this.friends = friends ?? throw new ArgumentNullException(nameof(friends));
            this.search = search ?? throw new ArgumentNullException(nameof(search));

            this.regions = regions ?? throw new ArgumentNullException(nameof(regions));

        }

        public void Start()
        {
            view.ActionClicked += OnActionClicked;
            view.FriendListDismissed += HideFriendList;
            view.ProfileSettingsDismissed += HideProfileSettings;
            view.ServerSettingsDismissed += HideServerSettings;
            view.RegionSelected += OnRegionSelected;
            view.RoomCreationRequested += OnRoomCreationRequested;
            view.CreateRoomDismissed += OnCreateRoomDismissed;
            view.NicknameChangeRequested += OnNicknameChangeRequested;
            view.NicknameEdited += OnNicknameEdited;
            view.FriendSearchOpened += OnFriendSearchOpened;
            view.FriendSearchClosed += OnFriendSearchClosed;
            view.FriendSearchRequested += OnFriendSearchRequested;
            profile.Changed += BindProfile;
            friends.FriendsChanged += BindFriends;
            search.ResultsChanged += BindSearchResults;
            BindProfile(profile);
            BindFriends();
            HideFriendList();
            HideProfileSettings();
            view.SetServerSettingsVisible(false);
            view.SetCreateRoomVisible(false);
            view.SetSelectedRegion(regions.Current.Code);
        }

        public void Dispose()
        {
            view.ActionClicked -= OnActionClicked;
            view.FriendListDismissed -= HideFriendList;
            view.ProfileSettingsDismissed -= HideProfileSettings;
            view.ServerSettingsDismissed -= HideServerSettings;
            view.RegionSelected -= OnRegionSelected;
            view.RoomCreationRequested -= OnRoomCreationRequested;
            view.CreateRoomDismissed -= OnCreateRoomDismissed;
            view.NicknameChangeRequested -= OnNicknameChangeRequested;
            view.NicknameEdited -= OnNicknameEdited;
            view.FriendSearchOpened -= OnFriendSearchOpened;
            view.FriendSearchClosed -= OnFriendSearchClosed;
            view.FriendSearchRequested -= OnFriendSearchRequested;
            profile.Changed -= BindProfile;
            friends.FriendsChanged -= BindFriends;
            search.ResultsChanged -= BindSearchResults;
        }

        private void OnActionClicked(HomeMenuAction action)
        {
            menu.Request(action);
            if (action == HomeMenuAction.Quit)
            {
                applicationHost.Quit();
                return;
            }

            if (action == HomeMenuAction.CreateRoom)
            {
                HideFriendList();
                HideProfileSettings();
                HideServerSettings();
                view.SetCreateRoomVisible(true);
                return;
            }

            // The three panels that hang off a control of their own are
            // toggles: the control that opened one closes it again. Opening
            // any of them puts away whatever else was up, so only one panel is
            // ever on screen.
            if (action == HomeMenuAction.ServerSettings)
            {
                var opening = !isServerSettingsVisible;
                if (opening)
                {
                    HideFriendList();
                    HideProfileSettings();
                }

                SetServerSettingsVisible(opening);
                return;
            }

            if (action == HomeMenuAction.Friends)
            {
                if (isFriendListVisible)
                {
                    HideFriendList();
                    return;
                }

                HideProfileSettings();
                HideServerSettings();
                ShowFriendList();
                return;
            }

            if (action == HomeMenuAction.ProfileSettings)
            {
                if (isProfileSettingsVisible)
                {
                    HideProfileSettings();
                    return;
                }

                HideFriendList();
                HideServerSettings();
                ShowProfileSettings();
                return;
            }

            if (action == HomeMenuAction.Character &&
                Transition(action, AppFlowState.CharacterCloset))
            {
                HideFriendList();
                HideProfileSettings();
                HideServerSettings();
                applicationHost.OpenCharacterCloset();
                return;
            }

            if (action == HomeMenuAction.Settings &&
                Transition(action, AppFlowState.Settings))
            {
                HideFriendList();
                HideProfileSettings();
                HideServerSettings();
                applicationHost.OpenSettings();
                return;
            }

            if (action == HomeMenuAction.FindRoom &&
                Transition(action, AppFlowState.RoomBrowser))
            {
                HideFriendList();
                HideProfileSettings();
                HideServerSettings();
                applicationHost.OpenRoomBrowser();
            }
        }

        /// <summary>
        /// Moves the flow for a menu button, and says so when it cannot.
        /// </summary>
        /// <remarks>
        /// A refused move used to be silent, and a button that does nothing
        /// looks the same whether the flow refused it or the button is broken.
        /// Naming the state it was refused from is what tells the two apart —
        /// and what tells us how the flow got there, which is the real bug.
        /// </remarks>
        private bool Transition(HomeMenuAction action, AppFlowState destination)
        {
            if (appFlow.TryTransitionTo(destination))
            {
                return true;
            }

            Debug.LogWarning(
                $"[Home] {action} refused: cannot go to {destination} from {appFlow.CurrentState}.");
            return false;
        }

        /// <summary>
        /// Takes the name locally and lets the bridge carry it to the account.
        /// </summary>
        /// <remarks>
        /// The screen changes at once because a field that waits on a round
        /// trip feels broken; <c>HomeProfileBridge</c> puts the old name back
        /// if the server refuses it.
        /// </remarks>
        private void OnNicknameChangeRequested(string nickname)
        {
            if (!isProfileSettingsVisible)
            {
                return;
            }

            profile.TryChangeNickname(nickname, out _);
        }

        /// <summary>
        /// Takes the message down as soon as the player types something else.
        /// </summary>
        /// <remarks>
        /// "이미 사용 중인 이름입니다" is about the name that was applied. Left
        /// up while a different one is being typed it reads as a verdict on
        /// what is in the box now, which nobody has checked.
        /// </remarks>
        private void OnNicknameEdited(string nickname)
        {
            if (!isProfileSettingsVisible)
            {
                return;
            }

            if (!string.Equals(nickname, profile.Nickname, StringComparison.Ordinal))
            {
                view.SetNicknameError(string.Empty);
            }
        }

        private void OnFriendSearchOpened()
        {
            // The box is shared, and the view empties it on the way across, so
            // the list's filter goes with it.
            isRequestTabOpen = true;
            listFilter = string.Empty;
            search.ClearResults();
            view.SetFriendSearchVisible(true);
            BindFriends();
            BindSearchResults();
        }

        private void OnFriendSearchClosed()
        {
            isRequestTabOpen = false;
            listFilter = string.Empty;
            HideFriendSearch();
            BindFriends();
        }

        /// <summary>
        /// One box, two jobs: on the request tab it asks the directory for a
        /// player, and on the list tab it narrows the friends already shown.
        /// </summary>
        private void OnFriendSearchRequested(string query)
        {
            if (isRequestTabOpen)
            {
                search.Search(query, CollectFriendIds());
                return;
            }

            listFilter = query == null ? string.Empty : query.Trim();
            BindFriends();
        }

        /// <summary>
        /// Reads the list again. With no service behind it yet this only
        /// redraws, which is still worth having: it is the one control that
        /// picks up a friend who came online while the panel was open.
        /// </summary>
        private void OnFriendListRefreshRequested()
        {
            BindFriends();
            if (isRequestTabOpen)
            {
                BindSearchResults();
            }
        }

        // Nothing here for a friend request. Marking the row was this class's
        // job before the request reached a server; now the command that sends it
        // marks the row itself, and doing it here as well made the command see a
        // row already waiting and decide there was nothing to send. The request
        // never left, and the row said 요청 중 all the same.

        private void ShowFriendList()
        {
            if (isFriendListVisible)
            {
                return;
            }

            isFriendListVisible = true;
            HideFriendSearch();
            view.SetFriendListVisible(true);
        }

        private void HideFriendList()
        {
            HideFriendSearch();
            isFriendListVisible = false;
            view.SetFriendListVisible(false);
        }

        /// <summary>
        /// Records the pick. The panel has already moved its own mark, so this
        /// only puts the mark back when the code was not one we offer.
        /// </summary>
        private void OnRegionSelected(string code)
        {
            if (!regions.TrySelect(code))
            {
                view.SetSelectedRegion(regions.Current.Code);
            }
        }

        /// <summary>
        /// Closes the modal before asking for the room, so the screen is not
        /// left with a live form over a lobby that is loading behind it.
        /// </summary>
        /// <summary>
        /// Sends the filled-in form on, once the flow allows leaving for a
        /// lobby at all.
        /// </summary>
        /// <remarks>
        /// Asked, not moved. Opening a room is a network call that can be
        /// refused, and a flow already moved to Lobby cannot come back to Home
        /// — the rules have no such move — so a refusal would leave the player
        /// on this screen with the app believing they are in a lobby. The host
        /// moves the flow when a room actually opens.
        /// </remarks>
        private void OnRoomCreationRequested(string title, bool isPublic, int maxPlayers)
        {
            if (appFlow.CurrentState != AppFlowState.Lobby &&
                !appFlow.CanTransitionTo(AppFlowState.Lobby))
            {
                Debug.LogWarning($"Cannot open a room from {appFlow.CurrentState}.");
                return;
            }

            view.SetCreateRoomVisible(false);
            applicationHost.CreateRoom(title, isPublic, maxPlayers);
        }

        private void OnCreateRoomDismissed()
        {
            view.SetCreateRoomVisible(false);
        }

        private void SetServerSettingsVisible(bool visible)
        {
            if (isServerSettingsVisible == visible)
            {
                return;
            }

            isServerSettingsVisible = visible;
            view.SetServerSettingsVisible(visible);
        }

        private void HideServerSettings()
        {
            SetServerSettingsVisible(false);
        }

        private void ShowProfileSettings()
        {
            if (isProfileSettingsVisible)
            {
                return;
            }

            isProfileSettingsVisible = true;
            view.SetNickname(profile.Nickname);
            view.SetNicknameSettled(profile.NicknameSet);
            view.SetProfileSettingsVisible(true);
        }

        private void HideProfileSettings()
        {
            isProfileSettingsVisible = false;
            view.SetNickname(profile.Nickname);
            view.SetProfileSettingsVisible(false);
        }

        private void HideFriendSearch()
        {
            search.ClearResults();
            view.SetFriendSearchVisible(false);
        }

        private void BindFriends()
        {
            view.SetFriends(
                Filtered(friends.OnlineFriends), Filtered(friends.OfflineFriends));
        }

        /// <summary>
        /// The friends whose nickname contains what was typed. One character is
        /// enough to start narrowing; nothing typed shows everyone.
        /// </summary>
        private IReadOnlyList<FriendSummary> Filtered(IReadOnlyList<FriendSummary> source)
        {
            if (listFilter.Length == 0)
            {
                return source;
            }

            var kept = new List<FriendSummary>();
            for (var index = 0; index < source.Count; index++)
            {
                if (source[index].Nickname.IndexOf(listFilter, StringComparison.Ordinal) >= 0)
                {
                    kept.Add(source[index]);
                }
            }

            return kept;
        }




        private void BindSearchResults()
        {
            view.SetFriendSearchResults(search.Results);
        }

        private string[] CollectFriendIds()
        {
            var ids = new string[friends.OnlineFriends.Count + friends.OfflineFriends.Count];
            var index = 0;
            for (var i = 0; i < friends.OnlineFriends.Count; i++)
            {
                ids[index++] = friends.OnlineFriends[i].PlayerId;
            }

            for (var i = 0; i < friends.OfflineFriends.Count; i++)
            {
                ids[index++] = friends.OfflineFriends[i].PlayerId;
            }

            return ids;
        }

        private void BindProfile(PlayerProfile source)
        {
            view.SetNickname(source.Nickname);
            view.SetNicknameSettled(source.NicknameSet);
        }
    }
}
