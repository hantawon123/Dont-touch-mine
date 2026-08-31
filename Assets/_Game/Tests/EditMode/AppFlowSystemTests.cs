using System.Collections.Generic;
using Game.Core.Flow;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class AppFlowSystemTests
    {
        [TestCase(AppFlowState.Lobby)]
        [TestCase(AppFlowState.InGame)]
        [TestCase(AppFlowState.Highlight)]
        [TestCase(AppFlowState.Result)]
        public void ExitSession_ReturnsDirectlyToBrowserOnce(AppFlowState phase)
        {
            var flow = new AppFlowSystem();
            Assert.That(flow.TryExitSession(), Is.False);
            flow.TryTransitionTo(AppFlowState.Lobby);
            flow.TryRestoreSessionState(phase);
            var changes = new List<AppFlowState>();
            flow.StateChanged += changes.Add;
            Assert.That(flow.TryExitSession(), Is.True);
            Assert.That(flow.TryExitSession(), Is.False);
            Assert.That(changes, Is.EqualTo(new[] { AppFlowState.RoomBrowser }));
            Assert.That(flow.TryRestoreSessionState(phase), Is.False,
                "Late state replication must not re-enter the abandoned room.");
        }

        [Test]
        public void RestoreSessionState_IsAtomicAndDoesNotRelaxNormalTransitions()
        {
            var flow = new AppFlowSystem();
            Assert.That(flow.TryRestoreSessionState(AppFlowState.InGame), Is.False);
            flow.TryTransitionTo(AppFlowState.RoomBrowser);
            Assert.That(flow.TryRestoreSessionState(AppFlowState.InGame), Is.False);
            flow.TryTransitionTo(AppFlowState.Lobby);
            flow.TryTransitionTo(AppFlowState.InGame);
            flow.TryTransitionTo(AppFlowState.Highlight);
            Assert.That(flow.TryTransitionTo(AppFlowState.InGame), Is.False);
            var changes = new List<AppFlowState>();
            flow.StateChanged += changes.Add;
            Assert.That(flow.TryRestoreSessionState(AppFlowState.InGame), Is.True);
            Assert.That(flow.TryRestoreSessionState(AppFlowState.InGame), Is.True);
            Assert.That(changes, Is.EqualTo(new[] { AppFlowState.InGame }));
            Assert.That(flow.TryTransitionTo(AppFlowState.Result), Is.False);
            Assert.That(flow.TryRestoreSessionState(AppFlowState.Home), Is.False);
            Assert.That(flow.TryRestoreSessionState(AppFlowState.RoomBrowser), Is.False);
            Assert.That(flow.TryRestoreSessionState((AppFlowState)999), Is.False);
            Assert.That(flow.CurrentState, Is.EqualTo(AppFlowState.InGame));
        }

        [Test]
        public void TryTransitionTo_FollowsCompleteGameFlow()
        {
            var flow = new AppFlowSystem();
            var changedStates = new List<AppFlowState>();
            flow.StateChanged += changedStates.Add;

            Assert.That(flow.CurrentState, Is.EqualTo(AppFlowState.Home));
            Assert.That(flow.TryTransitionTo(AppFlowState.RoomBrowser), Is.True);
            Assert.That(flow.TryTransitionTo(AppFlowState.Lobby), Is.True);
            Assert.That(flow.TryTransitionTo(AppFlowState.InGame), Is.True);
            Assert.That(flow.TryTransitionTo(AppFlowState.Result), Is.False);
            Assert.That(flow.TryTransitionTo(AppFlowState.Highlight), Is.True);
            Assert.That(flow.TryTransitionTo(AppFlowState.Result), Is.True);
            Assert.That(flow.TryTransitionTo(AppFlowState.Home), Is.False);
            Assert.That(flow.TryTransitionTo(AppFlowState.Lobby), Is.True);
            Assert.That(changedStates, Is.EqualTo(new[]
            {
                AppFlowState.RoomBrowser,
                AppFlowState.Lobby,
                AppFlowState.InGame,
                AppFlowState.Highlight,
                AppFlowState.Result,
                AppFlowState.Lobby
            }));
        }

        [Test]
        public void TryTransitionTo_AllowsQuickPlayAndBackNavigation()
        {
            var quickPlayFlow = new AppFlowSystem();
            Assert.That(quickPlayFlow.TryTransitionTo(AppFlowState.Lobby), Is.True);

            var backFlow = new AppFlowSystem();
            Assert.That(backFlow.TryTransitionTo(AppFlowState.RoomBrowser), Is.True);
            Assert.That(backFlow.TryTransitionTo(AppFlowState.Lobby), Is.True);
            Assert.That(backFlow.TryTransitionTo(AppFlowState.RoomBrowser), Is.True);
            Assert.That(backFlow.TryTransitionTo(AppFlowState.Home), Is.True);
        }

        [TestCase(AppFlowState.Home)]
        [TestCase(AppFlowState.InGame)]
        [TestCase(AppFlowState.Result)]
        [TestCase((AppFlowState)999)]
        public void TryTransitionTo_RejectsInvalidTransition(AppFlowState nextState)
        {
            var flow = new AppFlowSystem();
            var changedCount = 0;
            flow.StateChanged += _ => changedCount++;

            Assert.That(flow.TryTransitionTo(nextState), Is.False);
            Assert.That(flow.CurrentState, Is.EqualTo(AppFlowState.Home));
            Assert.That(changedCount, Is.Zero);
        }
    }
}
