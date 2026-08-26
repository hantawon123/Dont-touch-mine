using Fusion;

namespace Game.Network.Match
{
    /// <summary>
    /// Moves the room into the map once a match is confirmed.
    /// </summary>
    /// <remarks>
    /// This exists so that deciding a match may start and changing the scene
    /// stay separate. <c>MatchStarter</c> judges the request and reports the
    /// line-up; it does not know what a scene is. The session service, which
    /// already owns the runner's scene manager, the initial scene and the scene
    /// callbacks, implements this.
    /// <para>
    /// Declared here rather than in <c>Core/Ports</c> because it takes a
    /// <see cref="NetworkRunner"/>, and Fusion types do not leave this layer.
    /// </para>
    /// </remarks>
    public interface IMatchSceneDirector
    {
        /// <summary>
        /// Loads the map for everyone in the room. Called on the authority only,
        /// which is the peer allowed to change the networked scene; Fusion
        /// replicates the change to the others.
        /// </summary>
        void EnterMatchScene(NetworkRunner runner);

        /// <summary>
        /// Returns everyone to the waiting room without leaving the network session.
        /// </summary>
        void EnterLobbyScene(NetworkRunner runner);
    }
}
