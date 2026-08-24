using System;
using System.Collections.Generic;
using Game.Core.Match;
using UnityEngine;
using VContainer.Unity;

namespace Game.Server.Match
{
    public interface IMatchRuntimeContext : IMatchClock
    {
        IReadOnlyList<Vector3> PlayerPositions { get; }
    }

    public sealed class MatchRuntimeController : ITickable
    {
        private readonly MatchSessionCoordinator session;
        private readonly IMatchRuntimeContext context;
        private bool isStarted;

        public MatchRuntimeController(
            MatchSessionCoordinator session,
            IMatchRuntimeContext context)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public bool StartMatch()
        {
            if (isStarted || !session.Start(context.ServerTime))
            {
                return false;
            }

            isStarted = true;
            return true;
        }

        public void Tick()
        {
            if (isStarted)
            {
                session.AdvanceTime(context.ServerTime, context.PlayerPositions);
            }
        }
    }
}
