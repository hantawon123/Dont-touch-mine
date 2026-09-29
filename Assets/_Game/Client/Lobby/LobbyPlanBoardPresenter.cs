using System;
using Game.Core.Lobby;
using UnityEngine;
using VContainer.Unity;

namespace Game.Client.Lobby
{
    /// <summary>
    /// Connects the plan board in the room to the play settings screen.
    /// </summary>
    /// <remarks>
    /// The board is part of the environment, which arrives with the scene
    /// rather than through the container, so it is looked up instead of
    /// injected. The lookup is retried a few frames apart until it succeeds:
    /// the environment can be a prefab that is still being set up when this
    /// presenter starts.
    /// </remarks>
    public sealed class LobbyPlanBoardPresenter : IStartable, ITickable, IDisposable
    {
        private const int LookupIntervalFrames = 30;

        private readonly IPlaySettingsOpener opener;
        private readonly ILobbyHostSession hostSession;
        private LobbyPlanBoardInteractable board;
        private int framesUntilLookup;

        public LobbyPlanBoardPresenter(IPlaySettingsOpener opener, ILobbyHostSession hostSession)
        {
            this.opener = opener ?? throw new ArgumentNullException(nameof(opener));
            this.hostSession = hostSession ?? throw new ArgumentNullException(nameof(hostSession));
        }

        public LobbyPlanBoardInteractable Board => board;

        public void Start()
        {
            TryAttachFromScene();
        }

        public void Tick()
        {
            if (board != null)
            {
                return;
            }

            if (--framesUntilLookup > 0)
            {
                return;
            }

            TryAttachFromScene();
        }

        public void Dispose()
        {
            Detach();
        }

        /// <summary>
        /// Wires one board. Public so a test, or a scene that spawns its
        /// environment late, can hand the board over directly.
        /// </summary>
        public void Attach(LobbyPlanBoardInteractable target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            Detach();
            board = target;
            board.Bind(IsLocalHost, opener.OpenPlaySettingsFromWorld);
        }

        private void Detach()
        {
            if (board != null)
            {
                board.Unbind();
            }

            board = null;
        }

        private void TryAttachFromScene()
        {
            framesUntilLookup = LookupIntervalFrames;
            var found = UnityEngine.Object.FindFirstObjectByType<LobbyPlanBoardInteractable>(
                FindObjectsInactive.Include);
            if (found != null)
            {
                Attach(found);
            }
        }

        private bool IsLocalHost() => hostSession.IsLocalHost.CurrentValue;
    }
}
