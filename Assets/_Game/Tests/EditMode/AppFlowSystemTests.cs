using System.Collections.Generic;
using Game.Core.Flow;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class AppFlowSystemTests
    {
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
