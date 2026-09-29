using Game.Core.Flow;
using Game.Core.Ports;
using Game.Core.Presence;
using NUnit.Framework;

namespace Game.Architecture.Tests
{
    /// <summary>
    /// The rules for when a room report goes out, checked without a clock, a
    /// socket or a Fusion runner.
    /// </summary>
    public sealed class PresenceReportPlannerTests
    {
        [Test]
        public void WithoutARoomSession_TheReportIsOutOfRoom()
        {
            Assert.That(PresenceReportPlanner.Current(false, null, AppFlowState.Home), Is.EqualTo(PresenceReport.OutOfRoom));

            // A session flag with no valid name yet — mid-connect — is not a room
            // anyone could be told about.
            Assert.That(PresenceReportPlanner.Current(true, null, AppFlowState.Lobby), Is.EqualTo(PresenceReport.OutOfRoom));
            Assert.That(PresenceReportPlanner.Current(true, "  ", AppFlowState.Lobby), Is.EqualTo(PresenceReport.OutOfRoom));
        }

        [Test]
        public void TheFlowState_TellsTheLobbyFromTheMatch()
        {
            // Lobby and match are one Photon room, so the room code is the same
            // in every line below. Only the flow can tell them apart.
            Assert.That(PresenceReportPlanner.Current(true, "ROOM", AppFlowState.Lobby).Kind, Is.EqualTo(RoomSessionKind.Lobby));
            Assert.That(PresenceReportPlanner.Current(true, "ROOM", AppFlowState.InGame).Kind, Is.EqualTo(RoomSessionKind.Match));
            Assert.That(PresenceReportPlanner.Current(true, "ROOM", AppFlowState.Highlight).Kind, Is.EqualTo(RoomSessionKind.Match));
            Assert.That(PresenceReportPlanner.Current(true, "ROOM", AppFlowState.Result).Kind, Is.EqualTo(RoomSessionKind.Match));

            // A room session before the flow has caught up is a room being
            // joined, and rooms are joined into their lobby.
            Assert.That(PresenceReportPlanner.Current(true, "ROOM", AppFlowState.Home).Kind, Is.EqualTo(RoomSessionKind.Lobby));
            Assert.That(PresenceReportPlanner.Current(true, "ROOM", AppFlowState.RoomBrowser).Kind, Is.EqualTo(RoomSessionKind.Lobby));
        }

        [Test]
        public void TheFirstReport_AlwaysGoesOut()
        {
            var planner = new PresenceReportPlanner();

            Assert.That(planner.ShouldReport(PresenceReport.OutOfRoom, linkConnected: false), Is.True);
        }

        [Test]
        public void NothingGoesOut_WhileNothingChanges()
        {
            var planner = new PresenceReportPlanner();
            var lobby = new PresenceReport("ROOM", RoomSessionKind.Lobby);
            planner.Reported(lobby);

            // This is what replaced the thirty-second heartbeat: however many
            // times the loop asks, the same answer produces no traffic.
            for (var tick = 0; tick < 100; tick++)
            {
                Assert.That(planner.ShouldReport(lobby, linkConnected: true), Is.False);
            }
        }

        [Test]
        public void EnteringLeavingAndMoving_EachGoOut()
        {
            var planner = new PresenceReportPlanner();
            planner.Reported(PresenceReport.OutOfRoom);

            var first = new PresenceReport("ROOM-1", RoomSessionKind.Lobby);
            Assert.That(planner.ShouldReport(first, true), Is.True, "entering");
            planner.Reported(first);

            var second = new PresenceReport("ROOM-2", RoomSessionKind.Lobby);
            Assert.That(planner.ShouldReport(second, true), Is.True, "moving rooms");
            planner.Reported(second);

            Assert.That(planner.ShouldReport(PresenceReport.OutOfRoom, true), Is.True, "leaving");
        }

        [Test]
        public void TheLobbyBecomingAMatch_GoesOutThoughTheRoomIsTheSame()
        {
            var planner = new PresenceReportPlanner();
            planner.Reported(new PresenceReport("ROOM", RoomSessionKind.Lobby));

            Assert.That(planner.ShouldReport(new PresenceReport("ROOM", RoomSessionKind.Match), true), Is.True);
        }

        [Test]
        public void OutOfRoom_HasNoKindToDisagreeAbout()
        {
            var planner = new PresenceReportPlanner();
            planner.Reported(new PresenceReport(null, RoomSessionKind.Lobby));

            Assert.That(planner.ShouldReport(new PresenceReport(null, RoomSessionKind.Match), true), Is.False);
            Assert.That(new PresenceReport(null, RoomSessionKind.Lobby), Is.EqualTo(PresenceReport.OutOfRoom));
        }

        [Test]
        public void AReportThatDidNotLand_IsAskedForAgain()
        {
            var planner = new PresenceReportPlanner();
            planner.Reported(PresenceReport.OutOfRoom);
            var lobby = new PresenceReport("ROOM", RoomSessionKind.Lobby);

            Assert.That(planner.ShouldReport(lobby, true), Is.True);
            // Not Reported(): the send failed.
            Assert.That(planner.ShouldReport(lobby, true), Is.True, "still owed");

            planner.Reported(lobby);
            Assert.That(planner.ShouldReport(lobby, true), Is.False, "settled");
        }

        [Test]
        public void ALinkThatWasUpAllAlong_IsNotAComeback()
        {
            // 첫 질문에 링크가 켜져 있는 것은 "돌아온" 것이 아닙니다. 이것을 전이로
            // 세면 아무것도 바뀌지 않았는데 보고가 한 번 나가고, 그것이 이 클래스가
            // 없앤 30초 하트비트입니다.
            var planner = new PresenceReportPlanner();
            var lobby = new PresenceReport("ROOM", RoomSessionKind.Lobby);
            planner.Reported(lobby);

            Assert.That(planner.ShouldReport(lobby, linkConnected: true), Is.False);
        }

        [Test]
        public void TheLinkComingBack_RepeatsTheCurrentReportOnce()
        {
            var planner = new PresenceReportPlanner();
            var match = new PresenceReport("ROOM", RoomSessionKind.Match);
            planner.Reported(match);
            Assert.That(planner.ShouldReport(match, linkConnected: true), Is.False, "steady");

            // The link drops and returns. The server may have restarted in
            // between and know only that this client connected — which it reads
            // as merely online — not that it is mid-match.
            Assert.That(planner.ShouldReport(match, linkConnected: false), Is.False, "down: nothing to say");
            Assert.That(planner.ShouldReport(match, linkConnected: true), Is.True, "back: say it again");
            Assert.That(planner.ShouldReport(match, linkConnected: true), Is.False, "once");
        }

        [Test]
        public void HasReported_DecidesWhetherThereIsAPresenceToWithdraw()
        {
            var planner = new PresenceReportPlanner();
            Assert.That(planner.HasReported, Is.False);

            planner.Reported(PresenceReport.OutOfRoom);

            Assert.That(planner.HasReported, Is.True);
        }
    }
}
