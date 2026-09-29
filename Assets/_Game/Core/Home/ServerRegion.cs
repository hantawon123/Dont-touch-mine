using System;
using System.Collections.Generic;

namespace Game.Core.Home
{
    /// <summary>
    /// One of the places the game can be played from: what the player is shown
    /// and what Photon is told.
    /// </summary>
    public readonly struct ServerRegion
    {
        public ServerRegion(string code, string displayName)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ArgumentException("Region code is required.", nameof(code));
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("Display name is required.", nameof(displayName));
            }

            Code = code.Trim();
            DisplayName = displayName.Trim();
        }

        /// <summary>
        /// The token Photon's name server knows the region by, which goes into
        /// <c>AppSettings.FixedRegion</c>.
        /// </summary>
        public string Code { get; }

        public string DisplayName { get; }
    }

    /// <summary>
    /// The four regions the design offers, in the order it lists them.
    /// </summary>
    /// <remarks>
    /// <b>The codes here are unconfirmed.</b> Photon does not ship a list —
    /// the name server hands one out at connect time — so the authority is the
    /// Photon dashboard for this project's AppId, which is also where a region
    /// is switched on or off. A code that is wrong, or right but disabled for
    /// the AppId, fails to connect without saying why.
    /// <para>
    /// Worth revisiting whether 아시아 should be Seoul rather than Singapore:
    /// this is a Korean team and the difference is most of the latency.
    /// </para>
    /// </remarks>
    public static class ServerRegionCatalog
    {
        public static readonly IReadOnlyList<ServerRegion> All = new[]
        {
            new ServerRegion("kr", "한국"),
            new ServerRegion("asia", "아시아"),
            new ServerRegion("us", "북미"),
            new ServerRegion("au", "오세아니아"),
            new ServerRegion("eu", "유럽")
        };

        public static ServerRegion Default => All[0];

        public static bool TryFind(string code, out ServerRegion region)
        {
            if (!string.IsNullOrWhiteSpace(code))
            {
                var wanted = code.Trim();
                for (var index = 0; index < All.Count; index++)
                {
                    if (!string.Equals(All[index].Code, wanted, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    region = All[index];
                    return true;
                }
            }

            region = default;
            return false;
        }
    }
}
