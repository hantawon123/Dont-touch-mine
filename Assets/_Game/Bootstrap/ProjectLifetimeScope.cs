using System;
using Game.Client.Home;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Bootstrap
{
    public sealed class ProjectLifetimeScope : LifetimeScope
    {
        [SerializeField]
        private Font koreanSourceFont;

        protected override void Configure(IContainerBuilder builder)
        {
            if (koreanSourceFont == null)
            {
                throw new InvalidOperationException(
                    "Korean source font must be assigned on ProjectLifetimeScope.");
            }

            HomeUiFonts.Apply(koreanSourceFont);
        }
    }
}
