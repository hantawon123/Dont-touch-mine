using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace Game.Server.Match
{
    public interface IMatchRuntimeContext
    {
        double ServerTime { get; }
        IReadOnlyList<Vector3> PlayerPositions { get; }
    }

    public sealed class MatchRuntimeController : ITickable
    {
        private readonly MatchSessionCoordinator session;
        private readonly IMatchRuntimeContext context;

        public MatchRuntimeController(
            MatchSessionCoordinator session,
            IMatchRuntimeContext context)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public bool IsStarted { get; private set; }

        public bool StartMatch()
        {
            if (IsStarted || !session.Start(context.ServerTime))
            {
                return false;
            }

            IsStarted = true;
            return true;
        }

        public void Tick()
        {
            if (IsStarted)
            {
                session.AdvanceTime(context.ServerTime, context.PlayerPositions);
            }
        }
    }
}
