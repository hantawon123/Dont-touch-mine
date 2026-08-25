using System;
using System.Collections.Generic;
using Game.Core.Flow;
using Game.Core.Match;
using Game.Server.Items;
using UnityEngine;
using VContainer.Unity;

namespace Game.Server.Match
{
    public interface IMatchRuntimeContext : IMatchClock
    {
        IReadOnlyList<Vector3> PlayerPositions { get; }
        IReadOnlyList<Pose> PlayerPoses { get; }
        IReadOnlyList<WorldObjectState> ReplayObjects { get; }
    }

    public sealed class MatchRuntimeController : ITickable
    {
        private MatchSessionCoordinator session;
        private readonly IMatchRuntimeContext context;
        private readonly AppFlowSystem appFlow;
        private bool isStarted;

        public MatchRuntimeController(
            MatchSessionCoordinator session,
            IMatchRuntimeContext context,
            AppFlowSystem appFlow)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            this.appFlow = appFlow ?? throw new ArgumentNullException(nameof(appFlow));
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

        public bool TryPrepareRematch(MatchSessionCoordinator nextSession)
        {
            if (nextSession == null)
            {
                throw new ArgumentNullException(nameof(nextSession));
            }

            if (!isStarted ||
                session.CurrentPhase != MatchPhase.Result ||
                nextSession.CurrentPhase != MatchPhase.Waiting)
            {
                return false;
            }

            session = nextSession;
            isStarted = false;
            return true;
        }

        public void Tick()
        {
            if (isStarted)
            {
                session.TryRecordReplayFrame(
                    context.ServerTime,
                    context.PlayerPoses,
                    context.ReplayObjects);
                session.AdvanceTime(context.ServerTime, context.PlayerPositions);
                SyncAppFlow();
            }
        }

        private void SyncAppFlow()
        {
            if (session.CurrentPhase == MatchPhase.Highlight)
            {
                TransitionTo(AppFlowState.Highlight);
                return;
            }

            if (session.CurrentPhase != MatchPhase.Result)
            {
                return;
            }

            if (appFlow.CurrentState == AppFlowState.InGame)
            {
                TransitionTo(AppFlowState.Highlight);
            }

            TransitionTo(AppFlowState.Result);
        }

        private void TransitionTo(AppFlowState state)
        {
            if (appFlow.CurrentState != state && !appFlow.TryTransitionTo(state))
            {
                throw new InvalidOperationException(
                    $"Application flow cannot enter {state} from {appFlow.CurrentState}.");
            }
        }
    }
}
