#if UNITY_EDITOR
using System;
using Unity.Multiplayer.PlayMode;
#endif

namespace Game.Bootstrap
{
    /// <summary>
    /// What a Multiplayer Play Mode instance should do when its scene plays.
    /// </summary>
    public enum SessionRole
    {
        /// <summary>No tag is set, so the scene's own settings decide.</summary>
        Unassigned = 0,

        /// <summary>Open a room and take authority over it.</summary>
        Host = 1,

        /// <summary>Enter the room the host opened.</summary>
        Client = 2,
    }

    /// <summary>
    /// Reads the Multiplayer Play Mode tag of the instance that is running.
    /// </summary>
    /// <remarks>
    /// Virtual players share one project, so every instance loads the same scene
    /// with the same inspector values. Without a per-instance signal they would
    /// all open a room and never meet each other. Tags are that signal.
    /// <para>
    /// A build has no virtual players, so this is always
    /// <see cref="SessionRole.Unassigned"/> there and the inspector keeps
    /// deciding exactly as it did before.
    /// </para>
    /// </remarks>
    public static class SessionRoles
    {
        /// <summary>Tag to type into the Multiplayer Play Mode window.</summary>
        public const string HostTag = "Host";

        /// <summary>Tag to type into the Multiplayer Play Mode window.</summary>
        public const string ClientTag = "Client";

        public static SessionRole Current
        {
            get
            {
#if UNITY_EDITOR
                var tags = CurrentPlayer.ReadOnlyTags();

                if (tags == null)
                {
                    return SessionRole.Unassigned;
                }

                // A player can carry several tags. Ours are mutually exclusive,
                // so the first one that matches decides and the rest are free to
                // mean something else.
                foreach (var tag in tags)
                {
                    if (string.Equals(tag, HostTag, StringComparison.OrdinalIgnoreCase))
                    {
                        return SessionRole.Host;
                    }

                    if (string.Equals(tag, ClientTag, StringComparison.OrdinalIgnoreCase))
                    {
                        return SessionRole.Client;
                    }
                }
#endif
                return SessionRole.Unassigned;
            }
        }

        /// <summary>
        /// Names the running instance for logs, so two consoles side by side can
        /// be told apart at a glance.
        /// </summary>
        public static string Describe()
        {
#if UNITY_EDITOR
            var role = Current;
            var where = CurrentPlayer.IsMainEditor ? "main editor" : "virtual player";
            return role == SessionRole.Unassigned
                ? where
                : $"{where}, tagged {role}";
#else
            return "player";
#endif
        }
    }
}
