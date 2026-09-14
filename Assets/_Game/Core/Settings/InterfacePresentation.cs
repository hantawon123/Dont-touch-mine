using System;
using System.Collections.Generic;
using Game.Core.Home;
using Game.Core.Lobby;

namespace Game.Core.Settings
{
    // Presentation policy only: identity and gameplay data remain unchanged.
    public sealed class InterfacePresentation : IDisposable
    {
        /// <summary>
        /// What a player has published about their own name: whether they have
        /// said anything yet, and the pseudonym to use if they are in
        /// 스트리머 모드.
        /// </summary>
        /// <remarks>
        /// Three states rather than two, because "has not said yet" is not the
        /// same as "real name". A late joiner hears nothing for a moment, and
        /// showing a streamer's real name for that moment is the one thing the
        /// setting exists to prevent — so nothing is shown until they speak.
        /// </remarks>
        private readonly struct Published
        {
            public Published(bool real, string pseudonym)
            {
                Real = real;
                Pseudonym = pseudonym;
            }

            public bool Real { get; }

            public string Pseudonym { get; }
        }

        private readonly InterfaceSettingsSystem settings;
        private readonly FriendListSystem friends;
        private readonly RoomBrowserSystem room;
        private readonly Dictionary<string, Published> published = new();

        /// <summary>
        /// True after the first lobby visit has heard how everyone already
        /// there wants to be named. The participant list stays empty until
        /// then, so a first glance does not flash ids or blanks. Later
        /// joiners are not held: the gate opens once per room.
        /// </summary>
        public bool InitialVisibilityReady { get; private set; }

        public event Action Changed;
        public InterfacePresentation(InterfaceSettingsSystem settings, FriendListSystem friends, RoomBrowserSystem room)
        {
            this.settings = settings; this.friends = friends; this.room = room;
            settings.Changed += OnSettingsChanged;
            friends.FriendsChanged += Refresh;
        }
        public bool IsFriend(string playerId)
        {
            foreach (var player in room.Participants.CurrentValue)
            {
                if (player.PlayerId != playerId || string.IsNullOrEmpty(player.UserId)) continue;
                foreach (var friend in friends.OnlineFriends) if (friend.PlayerId == player.UserId) return true;
                foreach (var friend in friends.OfflineFriends) if (friend.PlayerId == player.UserId) return true;
            }
            return false;
        }
        public bool ShowsChat(string playerId) => playerId == room.LocalPlayerId.CurrentValue ||
            !settings.Current.IsOn(InterfaceOption.ChatScope) || IsFriend(playerId);

        /// <summary>
        /// What to call somebody, everywhere a name is read: the nameplate, the
        /// participant list, a chat line, the hiding notice, a highlight's
        /// title.
        /// </summary>
        /// <remarks>
        /// 스트리머 모드 only. Whether this player wants to see the nameplates
        /// at all is a separate question and a separate method — see
        /// <see cref="NameplateName"/> — because it is about this screen and
        /// must not reach the chat log, where a message with no sender would
        /// read as nobody's.
        /// <para>
        /// A player's own name is never replaced. Hiding it from them would
        /// cost them the ability to find their own message in the chat, and
        /// the mode is there to keep a name from the room, not from the person
        /// whose name it is.
        /// </para>
        /// </remarks>
        public string Name(string playerId, string nickname)
        {
            if (playerId == room.LocalPlayerId.CurrentValue)
            {
                return nickname;
            }

            if (!published.TryGetValue(playerId ?? string.Empty, out var said))
            {
                return string.Empty;
            }

            return said.Real ? nickname : said.Pseudonym;
        }

        /// <summary>
        /// What to write over a character's head, which is nothing at all when
        /// this player has turned the nameplates off.
        /// </summary>
        public string NameplateName(string playerId, string nickname) =>
            settings.Current.IsOn(InterfaceOption.PlayerNames)
                ? Name(playerId, nickname)
                : string.Empty;

        /// <summary>
        /// Takes down what a player has said about their own name: their real
        /// one, or the pseudonym they are going by.
        /// </summary>
        public void SetPublishedName(string playerId, bool real, string pseudonym)
        {
            if (string.IsNullOrEmpty(playerId)) return;
            var next = new Published(real, pseudonym ?? string.Empty);
            if (published.TryGetValue(playerId, out var previous)
                && previous.Real == next.Real
                && string.Equals(previous.Pseudonym, next.Pseudonym, StringComparison.Ordinal))
            {
                return;
            }
            published[playerId] = next;
            Refresh();
        }

        /// <summary>Forgets a player's name, which is how a late joiner starts.</summary>
        public void ClearPublishedName(string playerId)
        {
            if (string.IsNullOrEmpty(playerId) || !published.Remove(playerId)) return;
            Refresh();
        }

        public bool HasPublishedName(string playerId) =>
            !string.IsNullOrEmpty(playerId) && published.ContainsKey(playerId);

        /// <summary>
        /// The first naming pass for this room is done. The participant list
        /// may be shown; later joiners are not gated again.
        /// </summary>
        public void MarkInitialVisibilityReady()
        {
            if (InitialVisibilityReady)
            {
                return;
            }

            InitialVisibilityReady = true;
            Refresh();
        }

        public void ClearPermissions() => ClearPermissions(notify: true);

        public void ClearPermissions(bool notify)
        {
            published.Clear();
            InitialVisibilityReady = false;
            if (notify)
            {
                Refresh();
            }
        }
        private void OnSettingsChanged(InterfaceSettings value) => Refresh();
        private void Refresh() => Changed?.Invoke();
        public void Dispose()
        {
            settings.Changed -= OnSettingsChanged;
            friends.FriendsChanged -= Refresh;
        }
    }
}
