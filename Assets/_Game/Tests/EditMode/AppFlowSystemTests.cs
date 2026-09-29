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
        public void ExitSession_ReturnsDirectlyHomeOnce(AppFlowState phase)
        {
            var flow = new AppFlowSystem();
            Assert.That(flow.TryExitSession(), Is.False);
            flow.TryTransitionTo(AppFlowState.Lobby);
            flow.TryRestoreSessionState(phase);
            var changes = new List<AppFlowState>();
            flow.StateChanged += changes.Add;
            Assert.That(flow.TryExitSession(), Is.True);
            Assert.That(flow.TryExitSession(), Is.False);
            Assert.That(changes, Is.EqualTo(new[] { AppFlowState.Home }));
            Assert.That(flow.TryRestoreSessionState(phase), Is.False,
                "Late state replication must not re-enter the abandoned room.");
        }

        [Test]
        public void TryTransitionTo_SettingsIsADetourFromHome()
        {
            var flow = new AppFlowSystem();
            Assert.That(flow.TryTransitionTo(AppFlowState.Settings), Is.True);

            // The way on from the settings screen is back where it was opened
            // from. The browser is reached through Home, not from here.
            Assert.That(flow.TryTransitionTo(AppFlowState.RoomBrowser), Is.False);
            Assert.That(flow.TryTransitionTo(AppFlowState.Home), Is.True);

            flow.TryTransitionTo(AppFlowState.RoomBrowser);
            Assert.That(flow.TryTransitionTo(AppFlowState.Settings), Is.False,
                "Settings opens from Home only; the browser has no button for it.");
        }

        [Test]
        public void TryTransitionTo_TutorialIsADetourFromHome()
        {
            var flow = new AppFlowSystem();

            Assert.That(flow.TryTransitionTo(AppFlowState.Tutorial), Is.True);
            Assert.That(flow.TryTransitionTo(AppFlowState.RoomBrowser), Is.False);
            Assert.That(flow.TryTransitionTo(AppFlowState.Home), Is.True);
        }

        /// <summary>
        /// An invite accepted from a detour screen puts the player in a room,
        /// and leaving that room has to work.
        /// </summary>
        /// <remarks>
        /// The bug this stands for: 환경설정 or 캐릭터 옷장 refused the move to
        /// Lobby, so the player sat in a game the flow believed was the
        /// settings screen. 게임 나가기 then asked to leave a session, Settings
        /// is not one, and nothing happened — the loading cover stayed up for
        /// ever because the return to Home was never started.
        /// </remarks>
        [TestCase(AppFlowState.Settings)]
        [TestCase(AppFlowState.CharacterCloset)]
        public void AnInviteFromADetour_ReachesARoomThatCanBeLeft(AppFlowState detour)
        {
            var flow = new AppFlowSystem();
            Assert.That(flow.TryTransitionTo(detour), Is.True);

            Assert.That(
                flow.TryTransitionTo(AppFlowState.Lobby),
                Is.True,
                "An invite arrives from a friend, not from a button on this screen.");

            Assert.That(flow.TryExitSession(AppFlowState.Home), Is.True);
            Assert.That(flow.CurrentState, Is.EqualTo(AppFlowState.Home));
        }

        /// <summary>
        /// Home on screen with the flow still on a detour is the state that
        /// left Home with half its menu dead. Showing Home puts it right.
        /// </summary>
        [TestCase(AppFlowState.CharacterCloset)]
        [TestCase(AppFlowState.Settings)]
        [TestCase(AppFlowState.Tutorial)]
        [TestCase(AppFlowState.RoomBrowser)]
        public void ShowingHome_PutsAStrandedDetourBackToHome(AppFlowState detour)
        {
            var flow = new AppFlowSystem();
            Assert.That(flow.TryTransitionTo(detour), Is.True);

            Assert.That(flow.TryReconcileToHome(), Is.True, "Something was wrong and was corrected.");
            Assert.That(flow.CurrentState, Is.EqualTo(AppFlowState.Home));
            Assert.That(flow.TryTransitionTo(AppFlowState.Settings), Is.True, "And the menu works again.");
        }

        [Test]
        public void ShowingHome_WhenAlreadyHome_ChangesNothing()
        {
            var flow = new AppFlowSystem();
            var changes = 0;
            flow.StateChanged += _ => changes++;

            Assert.That(flow.TryReconcileToHome(), Is.False);
            Assert.That(changes, Is.Zero);
        }

        /// <summary>
        /// A session is a claim about a room the player may still be in. Only
        /// the room's own exit ends it; a screen must not guess.
        /// </summary>
        [TestCase(AppFlowState.Lobby)]
        [TestCase(AppFlowState.InGame)]
        [TestCase(AppFlowState.Highlight)]
        [TestCase(AppFlowState.Result)]
        public void ShowingHome_LeavesASessionStateAlone(AppFlowState session)
        {
            var flow = new AppFlowSystem();
            Assert.That(flow.TryRestoreSessionState(session), Is.False, "Not a session yet.");
            flow.TryTransitionTo(AppFlowState.Lobby);
            flow.TryRestoreSessionState(session);
            Assume.That(flow.CurrentState, Is.EqualTo(session));

            Assert.That(flow.TryReconcileToHome(), Is.False);
            Assert.That(flow.CurrentState, Is.EqualTo(session));
        }

        [Test]
        public void ExitSession_KickCanReturnToBrowser_AndRejectsSessionDestination()
        {
            var flow = new AppFlowSystem();
            flow.TryTransitionTo(AppFlowState.Lobby);
            Assert.That(flow.TryExitSession(AppFlowState.InGame), Is.False);
            Assert.That(flow.CurrentState, Is.EqualTo(AppFlowState.Lobby));
            Assert.That(flow.TryExitSession(AppFlowState.RoomBrowser), Is.True);
            Assert.That(flow.CurrentState, Is.EqualTo(AppFlowState.RoomBrowser));
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
