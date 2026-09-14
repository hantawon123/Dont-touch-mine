#if UNITY_EDITOR
using System;
using Game.Network.Lobby;
using UnityEditor;
using UnityEngine;

namespace Game.Network.Session
{
    // SessionState survives script reloads, but is local to this Editor process.
    // It never enters a player build or changes the EC2 release override.
    public static class EditorDevelopmentSession
    {
        public enum PeerRole { Normal, Client, Server }
        private const string Prefix = "Game.DevelopmentSession.";
        public static PeerRole Role => (PeerRole)SessionState.GetInt(Prefix + "Role", 0);
        public static bool Enabled => Role != PeerRole.Normal;
        public static bool IsServer => Role == PeerRole.Server;
        public static string Code => SessionState.GetString(Prefix + "Code", "DEV001");
        public static string AppVersion => $"editor-dev-v1-{Application.version}-{Code}";
        public static string Status => SessionState.GetString(Prefix + "Status", "중지됨");

        public static void Configure(PeerRole role, string code)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before changing development peers.");
            code = RoomCodeGenerator.Normalize(code);
            if (!RoomCodeGenerator.IsWellFormed(code))
                throw new ArgumentException("Use a valid six-character test code.", nameof(code));
            SessionState.SetString(Prefix + "Code", code);
            SessionState.SetInt(Prefix + "Role", (int)role);
            Report("중지됨");
        }

        // Only a completed server session requests another Play. Manual Stop and
        // startup failures must stay stopped.
        public static bool RestartRequested
        {
            get => SessionState.GetBool(Prefix + "Restart", false);
            set => SessionState.SetBool(Prefix + "Restart", value);
        }

        public static void Report(string status) => SessionState.SetString(Prefix + "Status", status);
    }
}
#endif
