using System.Collections.Generic;
using Game.Client.Character;
using Game.Core.Players;
using Game.Network.Session;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>Publishes the owner's selection and dresses each replicated avatar.</summary>
    public sealed class NetworkAvatarAppearancePresenter : ITickable
    {
        private readonly NetworkRunnerService network;
        private readonly AvatarAppearanceState appearance;
        private readonly List<(string playerId, string userId, AvatarAppearance appearance)> published = new();

        public NetworkAvatarAppearancePresenter(NetworkRunnerService network, AvatarAppearanceState appearance)
        {
            this.network = network;
            this.appearance = appearance;
        }

        public void Tick()
        {
            // The authority replicates appearance fields in PlayerAvatar; only clients dress the model.
            if (network.IsDedicatedServer) return;
            published.Clear();
            AvatarAppearanceBoard.SetLocal(appearance.Current);
            foreach (var avatar in network.SpawnedAvatars)
            {
                if (avatar == null || !avatar.HasNetworkState) continue;
                var applier = avatar.GetComponentInChildren<AvatarAppearanceApplier>(true);
                if (applier == null) continue;
                if (avatar.IsOwner) avatar.PublishAppearance(applier.ResolvePlayerAppearance(appearance.Current));
                if (!avatar.IsOwner && !avatar.HasAppearance) continue;

                // Local changes (including a failed-save rollback) show immediately.
                // Remote players use replicated state, including on late join.
                var selected = avatar.IsOwner ? appearance.Current : avatar.Appearance;
                selected = applier.ResolvePlayerAppearance(selected);
                if (applier.Current != selected) applier.Apply(selected);
                published.Add((avatar.PlayerId, avatar.UserId.ToString(), selected));
            }

            AvatarAppearanceBoard.Replace(published);
        }
    }
}
