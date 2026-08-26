using System;
using Game.Core.Match;
using Game.Server.Match;

namespace Game.Network.Match
{
    /// <summary>
    /// Match-wide messages already confirmed by the network authority.
    /// Presentation and application flow observe these without depending on
    /// Photon or the runner that delivered them.
    /// </summary>
    public interface INetworkMatchEvents
    {
        event Action<MatchStateSnapshot> MatchStateReceived;
        event Action<MatchResult> MatchResultReceived;
    }
}
