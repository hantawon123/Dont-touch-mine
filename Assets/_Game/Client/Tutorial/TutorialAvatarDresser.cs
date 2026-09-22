using System;
using Game.Client.Character;
using Game.Core.Players;
using VContainer.Unity;

namespace Game.Client.Tutorial
{
    /// <summary>
    /// Dresses the tutorial character in the appearance the player settled on.
    /// </summary>
    /// <remarks>
    /// The match dresses avatars through NetworkAvatarAppearancePresenter, which
    /// walks the runner's spawned avatars. The tutorial runs offline with the
    /// character placed in the scene, so nothing in that loop reaches it and it
    /// used to show the raw art whatever the player was wearing.
    /// </remarks>
    public sealed class TutorialAvatarDresser : IStartable, IDisposable
    {
        private readonly AvatarAppearanceApplier applier;
        private readonly AvatarAppearanceState appearance;

        public TutorialAvatarDresser(
            AvatarAppearanceApplier applier, AvatarAppearanceState appearance)
        {
            this.applier = applier;
            this.appearance = appearance;
        }

        public void Start()
        {
            appearance.Changed += Wear;
            Wear(appearance.Current);
        }

        public void Dispose()
        {
            appearance.Changed -= Wear;
        }

        // Followed rather than read once: the pause screen can reach the closet,
        // and coming back should not leave the character in the old outfit.
        private void Wear(AvatarAppearance selected)
        {
            if (applier == null)
            {
                return;
            }

            var resolved = applier.ResolvePlayerAppearance(selected);
            if (applier.Current != resolved)
            {
                applier.Apply(resolved);
            }
        }
    }
}
