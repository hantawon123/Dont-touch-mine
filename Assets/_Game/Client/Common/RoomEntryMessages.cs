using Game.Core.Rooms;
using Game.Core.Settings;

namespace Game.Client.Common
{
    /// <summary>
    /// Which way into a room a refusal came back from.
    /// </summary>
    /// <remarks>
    /// The same failure asks for different things depending on how the player
    /// got it. A room that is not there is worth refreshing the list over when
    /// it was picked from the list, and worth re-reading the code over when a
    /// code was typed.
    /// </remarks>
    public enum RoomEntrySource
    {
        RoomList,
        RoomCode,

        /// <summary>
        /// The create-room form on the home screen. Opening a room is entering
        /// it too, so the same failures come back — but a room that is full or
        /// gone means something different when the player is the one making it.
        /// </summary>
        RoomCreate,

        /// <summary>
        /// A friend's invitation, taken up from the card. The player typed no
        /// code and picked nothing from a list, so anything that tells them to
        /// check what they entered is talking about something they never did.
        /// </summary>
        Invite,
    }

    /// <summary>
    /// What the player is told when a room refuses them.
    /// </summary>
    /// <remarks>
    /// The heading never changes: from the player's side every one of these is
    /// the same event, which is that they tried to get into a room and did not.
    /// The line under it says what to do about it.
    /// </remarks>
    public static class RoomEntryMessages
    {
        public static string Title(string language = "ko") =>
            UiTextCatalog.Shipped.Get(UiText.Home.ConnectionError, language);

        /// <summary>
        /// For the failures a player can do nothing about, and for anything new
        /// that arrives before it has wording of its own.
        /// </summary>
        public static string Generic =>
            UiTextCatalog.Shipped.Get(UiText.Rooms.Generic, "ko");

        public static string Describe(RoomEntryFailure failure, RoomEntrySource source) =>
            Describe(failure, source, "ko");

        public static string Describe(RoomEntryFailure failure, RoomEntrySource source, string language)
        {
            string Copy(string key) => UiTextCatalog.Shipped.Get(key, language);

            switch (failure)
            {
                case RoomEntryFailure.NotFound:
                    if (source == RoomEntrySource.RoomCreate)
                    {
                        return Copy(UiText.Rooms.CreateFailed);
                    }

                    if (source == RoomEntrySource.Invite)
                    {
                        return Copy(UiText.Rooms.InviteGone);
                    }

                    return source == RoomEntrySource.RoomCode
                        ? Copy(UiText.Rooms.CodeMissing)
                        : Copy(UiText.Rooms.ListGone);

                case RoomEntryFailure.InvalidCode when source == RoomEntrySource.Invite:
                    return Copy(UiText.Rooms.InviteExpired);

                case RoomEntryFailure.Full:
                    if (source == RoomEntrySource.RoomCreate)
                    {
                        return Copy(UiText.Rooms.CreateFailed);
                    }

                    return source is RoomEntrySource.RoomCode or RoomEntrySource.Invite
                        ? Copy(UiText.Rooms.Full)
                        : Copy(UiText.Rooms.FullPickAnother);

                case RoomEntryFailure.Closed:
                    return source == RoomEntrySource.RoomCreate
                        ? Copy(UiText.Rooms.CreateFailed)
                        : Copy(UiText.Rooms.AlreadyStarted);

                case RoomEntryFailure.InvalidRequest when source == RoomEntrySource.RoomCreate:
                    return Copy(UiText.Rooms.CheckSettings);

                case RoomEntryFailure.InvalidCode:
                    return Copy(UiText.Rooms.CheckCode);

                case RoomEntryFailure.AlreadyInRoom:
                    return Copy(UiText.Rooms.AlreadyInRoom);

                default:
                    return Copy(UiText.Rooms.Generic);
            }
        }
    }
}
