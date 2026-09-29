using System;
using Game.Client.Match;
using Game.Network.Session;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>
    /// Binds match chat bubbles to every live avatar. Shared by lobby and playground.
    /// </summary>
    internal sealed class ChatBubbleBinder : ITickable, IDisposable
    {
        private readonly NetworkRunnerService network;
        private readonly IMatchChatBubbleView bubbles;

        public ChatBubbleBinder(
            NetworkRunnerService network,
            IMatchChatBubbleView bubbles)
        {
            this.network = network ?? throw new ArgumentNullException(nameof(network));
            this.bubbles = bubbles ?? throw new ArgumentNullException(nameof(bubbles));
        }

        public void Tick()
        {
            var avatars = network.PlayerAvatars;
            for (var index = 0; index < avatars.Count; index++)
            {
                var avatar = avatars[index];
                if (avatar == null || !avatar.isActiveAndEnabled || string.IsNullOrEmpty(avatar.PlayerId))
                {
                    continue;
                }

                bubbles.BindPlayer(avatar.PlayerId, avatar.transform);
            }
        }

        public void Dispose() => bubbles.Clear();
    }
}
