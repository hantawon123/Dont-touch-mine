using System;
using Game.Core.Settings;
using Game.Network.Session;
using UnityEngine;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>
    /// Tells the room what this player wants to be called, and takes down what
    /// everybody else has said about themselves.
    /// </summary>
    /// <remarks>
    /// <see cref="PlayerAvatarNaming"/> holds the three answers a player can
    /// publish. Anything else that arrives is treated as 아직 모름, so a build
    /// that speaks an older dialect cannot cause a name to be shown that its
    /// owner wanted hidden.
    /// </remarks>
    public sealed class NetworkInterfaceSettings : IStartable, ITickable, IDisposable
    {
        /// <summary>
        /// What travels in <c>PlayerAvatar.NicknameVisibility</c>.
        /// </summary>
        private static class PlayerAvatarNaming
        {
            /// <summary>The owner has not said yet. Nothing is shown.</summary>
            public const int Unsaid = 0;

            /// <summary>The owner is happy to be called by their own name.</summary>
            public const int RealName = 1;

            /// <summary>
            /// 스트리머 모드. The name to use travels beside this, in the same
            /// string that used to carry who was allowed to see the real one.
            /// </summary>
            public const int Pseudonymous = 2;
        }

        private readonly NetworkRunnerService network;
        private readonly InterfaceSettingsSystem settings;
        private readonly InterfacePresentation presentation;
        private readonly PublishedPlayerName publishedName;
        private bool hadSession;
        private double initialVisibilityStarted;
        private const float InitialVisibilityTimeoutSeconds = 1.5f;
        private bool pendingOwnPublish;
        private int pendingOwnMode;
        private string pendingOwnName;

        public NetworkInterfaceSettings(NetworkRunnerService network, InterfaceSettingsSystem settings,
            InterfacePresentation presentation, PublishedPlayerName publishedName)
        {
            this.network = network; this.settings = settings; this.presentation = presentation;
            this.publishedName = publishedName;
        }

        public void Start() => settings.Changed += OnSettingsChanged;

        /// <summary>
        /// The room list's copy of the host's name lives in the session, not in
        /// anybody's avatar, so a change of 스트리머 모드 has to be carried there
        /// separately. Only the host's write goes through; everybody else's
        /// call is refused inside and costs nothing.
        /// </summary>
        private void OnSettingsChanged(InterfaceSettings _) => network.RefreshHostNickname();

        public void Tick()
        {
            if (!network.HasRoomSession)
            {
                if (hadSession)
                {
                    presentation.ClearPermissions();

                    // The visit is over, so the name it was made for is too.
                    // The next room gets a new one, which is what keeps a
                    // pseudonym from becoming a name somebody is known by.
                    publishedName.ForgetPseudonym();
                    pendingOwnPublish = false;
                    initialVisibilityStarted = 0d;
                }
                hadSession = false;
                return;
            }

            if (!hadSession)
            {
                initialVisibilityStarted = Time.unscaledTimeAsDouble;
            }

            hadSession = true;
            var avatars = network.PlayerAvatars;
            var awaitingVisibility = false;
            var sawAvatar = false;
            foreach (var avatar in avatars)
            {
                if (avatar == null || avatar.Object == null || !avatar.Object.IsValid) continue;
                if (avatar.IsOwner)
                {
                    avatar.GetComponent<Game.Client.Interactions.PlayerInteractor>()?.SetInterfaceHudVisible(
                        network.IsWaitingForMatch || settings.Current.IsOn(InterfaceOption.InGameUi));

                    PublishOwnName(avatar);
                }

                // Read every frame. A 0.25s poll used to leave a joiner's chat
                // name blank until the next tick after their visibility arrived.
                ApplyPublishedName(avatar);
                sawAvatar = true;
                if (string.IsNullOrEmpty(avatar.PlayerId)
                    || avatar.NicknameVisibility == PlayerAvatarNaming.Unsaid)
                {
                    awaitingVisibility = true;
                }
            }

            var timedOut = Time.unscaledTimeAsDouble - initialVisibilityStarted
                >= InitialVisibilityTimeoutSeconds;
            if (!presentation.InitialVisibilityReady
                && ((sawAvatar && !awaitingVisibility) || timedOut))
            {
                presentation.MarkInitialVisibilityReady();
            }
        }

        /// <summary>
        /// Sends this player's naming choice as soon as the avatar exists,
        /// once, so the rest of the room does not wait on a poll interval.
        /// </summary>
        private void PublishOwnName(Game.Network.Players.PlayerAvatar avatar)
        {
            var streaming = publishedName.IsPseudonymous;
            var mode = streaming
                ? PlayerAvatarNaming.Pseudonymous
                : PlayerAvatarNaming.RealName;

            // The same name the room list was given, so a host is not
            // one person in the browser and another inside.
            var name = streaming ? publishedName.Pseudonym : string.Empty;
            if (avatar.NicknameVisibility == mode && avatar.NicknameViewers.ToString() == name)
            {
                pendingOwnPublish = false;
                return;
            }

            if (pendingOwnPublish && pendingOwnMode == mode
                && string.Equals(pendingOwnName, name, StringComparison.Ordinal))
            {
                return;
            }

            pendingOwnPublish = true;
            pendingOwnMode = mode;
            pendingOwnName = name;
            avatar.RPC_SetNicknameVisibility(mode, name);
        }

        private void ApplyPublishedName(Game.Network.Players.PlayerAvatar avatar)
        {
            switch (avatar.NicknameVisibility)
            {
                case PlayerAvatarNaming.RealName:
                    presentation.SetPublishedName(avatar.PlayerId, real: true, pseudonym: null);
                    break;
                case PlayerAvatarNaming.Pseudonymous:
                    presentation.SetPublishedName(
                        avatar.PlayerId, real: false, avatar.NicknameViewers.ToString());
                    break;
                default:
                    presentation.ClearPublishedName(avatar.PlayerId);
                    break;
            }
        }

        public void Dispose()
        {
            settings.Changed -= OnSettingsChanged;
            presentation.ClearPermissions(notify: false);
        }
    }
}
