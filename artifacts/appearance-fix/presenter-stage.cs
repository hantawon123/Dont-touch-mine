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

        public NetworkAvatarAppearancePresenter(NetworkRunnerService network, AvatarAppearanceState appearance)
        {
            this.network = network;
            this.appearance = appearance;
        }

        public void Tick()
        {
            foreach (var avatar in network.SpawnedAvatars)
            {
                if (avatar == null || !avatar.HasNetworkState) continue;
                var applier = avatar.GetComponentInChildren<AvatarAppearanceApplier>(true);
                if (applier == null) continue;
                if (avatar.IsOwner) avatar.PublishAppearance(appearance.Current);
                if (!avatar.IsOwner && !avatar.HasAppearance) continue;

                // Local changes (including a failed-save rollback) show immediately.
                // Remote players use replicated state, including on late join.
                var selected = avatar.IsOwner ? appearance.Current : avatar.Appearance;

                if (applier.Current != selected) applier.Apply(selected);
            }
        }
    }
}
