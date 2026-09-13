using System;
using System.Collections.Generic;
using Game.Client.Home;
using Game.Client.Lobby;
using Game.Core.Home;
using Game.Core.Lobby;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.Architecture.Tests
{
    public sealed class LobbyPlayerListViewTests
    {
        [Test]
        public void ReportClicked_CarriesTheBackendAccount_NotThePhotonPlayerId()
        {
            // Every report was answered 404 because this row handed the report
            // API a Photon player id (S15P21D205-926). The presenter tests could
            // not see it - they drive a fake view and raise the event by hand,
            // so the value this row picks was never exercised.
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = canvas.AddComponent<LobbyPlayerListView>();
                view.EnsureLayout();

                string reported = null;
                view.ReportClicked += (id, _) => reported = id;

                view.SetParticipants(
                    new[]
                    {
                        new LobbyParticipant("P1", "방장", true, "11111111-1111-1111-1111-111111111111"),
                        new LobbyParticipant("P2", "게스트", false, "22222222-2222-2222-2222-222222222222"),
                    },
                    localIsHost: true,
                    localPlayerId: "P1");

                ClickReportOn(canvas, "Row_P2");

                Assert.That(reported, Is.EqualTo("22222222-2222-2222-2222-222222222222"));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void AParticipantWithoutAnAccount_HasNoReportButton()
        {
            // Nothing the server could be told about, and a blank id would only
            // turn the 404 into a 400.
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = canvas.AddComponent<LobbyPlayerListView>();
                view.EnsureLayout();

                view.SetParticipants(
                    new[]
                    {
                        new LobbyParticipant("P1", "방장", true, "11111111-1111-1111-1111-111111111111"),
                        new LobbyParticipant("P2", "계정없음", false),
                    },
                    localIsHost: true,
                    localPlayerId: "P1");

                Assert.That(FindReport(canvas, "Row_P2"), Is.Null);

                // The row itself is still there; only the report is missing.
                Assert.That(FindRow(canvas, "Row_P2"), Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void YourOwnRow_HasNoReportButton()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = canvas.AddComponent<LobbyPlayerListView>();
                view.EnsureLayout();

                view.SetParticipants(
                    new[]
                    {
                        new LobbyParticipant("P1", "나", true, "11111111-1111-1111-1111-111111111111"),
                    },
                    localIsHost: true,
                    localPlayerId: "P1");

                Assert.That(FindReport(canvas, "Row_P1"), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        private static Transform FindRow(GameObject canvas, string rowName)
        {
            foreach (var rect in canvas.GetComponentsInChildren<RectTransform>(true))
            {
                if (rect.name == rowName)
                {
                    return rect;
                }
            }

            return null;
        }

        private static Button FindReport(GameObject canvas, string rowName)
        {
            var row = FindRow(canvas, rowName);
            return row == null ? null : row.Find("Report")?.GetComponent<Button>();
        }

        private static void ClickReportOn(GameObject canvas, string rowName)
        {
            var report = FindReport(canvas, rowName);
            Assert.That(report, Is.Not.Null, $"{rowName} 에 신고 버튼이 없습니다.");
            report.onClick.Invoke();
        }

        [Test]
        public void EnsureLayout_BuildsBothColumnsWithBoldTitles()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = canvas.AddComponent<LobbyPlayerListView>();
                view.EnsureLayout();

                Assert.That(view.ParticipantsTitleText, Is.EqualTo(LobbyPlayerListView.ParticipantsTitle));
                Assert.That(view.FriendsTitleText, Is.EqualTo(LobbyPlayerListView.FriendsTitle));
                var participantsTitle = canvas.transform
                    .Find("Columns/Participants/Title")
                    .GetComponent<TMP_Text>();
                var friendsTitle = canvas.transform
                    .Find("Columns/Friends/Title")
                    .GetComponent<TMP_Text>();
                Assert.That(participantsTitle.fontSize, Is.EqualTo(LobbyPlayerListView.TitleFontSize));
                Assert.That(participantsTitle.font, Is.EqualTo(HomeUiFonts.ApplyBold()));
                Assert.That(friendsTitle.fontSize, Is.EqualTo(LobbyPlayerListView.TitleFontSize));
                Assert.That(friendsTitle.font, Is.EqualTo(HomeUiFonts.ApplyBold()));
                Assert.That(view.GetComponent<RectTransform>().sizeDelta.x, Is.EqualTo(LobbyPlayerListView.ModalWidth));
                var participants = canvas.transform.Find("Columns/Participants") as RectTransform;
                var friends = canvas.transform.Find("Columns/Friends") as RectTransform;
                Assert.That(
                    participants.anchorMax.x - participants.anchorMin.x,
                    Is.EqualTo(LobbyPlayerListView.ColumnWidthRatio).Within(0.001f));
                Assert.That(
                    friends.anchorMax.x - friends.anchorMin.x,
                    Is.EqualTo(LobbyPlayerListView.ColumnWidthRatio).Within(0.001f));
                var title = participants.Find("Title") as RectTransform;
                var scroll = participants.Find("Scroll") as RectTransform;
                Assert.That(title.offsetMin.x, Is.EqualTo(LobbyPlayerListView.TitleLeftPadding));
                Assert.That(title.offsetMax.x, Is.EqualTo(-LobbyPlayerListView.ColumnInnerPadding));
                Assert.That(title.offsetMax.y, Is.EqualTo(-LobbyPlayerListView.ColumnInnerPadding));
                Assert.That(scroll.offsetMin, Is.EqualTo(new Vector2(
                    LobbyPlayerListView.ColumnInnerPadding,
                    LobbyPlayerListView.ColumnInnerPadding)));
                Assert.That(scroll.offsetMax.x, Is.EqualTo(-LobbyPlayerListView.ColumnInnerPadding));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void SetParticipants_HostSeesKickOnOthersAndLeaderIconOnHost()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = canvas.AddComponent<LobbyPlayerListView>();
                view.SetParticipants(
                    new[]
                    {
                        new LobbyParticipant("host-1", "방장닉", true),
                        new LobbyParticipant("player-2", "게스트닉", false),
                    },
                    localIsHost: true,
                    localPlayerId: "host-1");

                var hostRow = canvas.transform.Find("Columns/Participants/Scroll/RowRoot/Row_host-1");
                var guestRow = canvas.transform.Find("Columns/Participants/Scroll/RowRoot/Row_player-2");
                Assert.That(hostRow, Is.Not.Null);
                Assert.That(guestRow, Is.Not.Null);
                Assert.That(hostRow.Find("Leader").gameObject.activeSelf, Is.True);
                Assert.That(guestRow.Find("Leader").gameObject.activeSelf, Is.False);
                Assert.That(hostRow.Find("Kick"), Is.Null);
                Assert.That(guestRow.Find("Kick"), Is.Not.Null);

                var kick = guestRow.Find("Kick").GetComponent<TMP_Text>();
                Assert.That(kick.text, Is.EqualTo("강퇴"));
                Assert.That(kick.fontSize, Is.EqualTo(LobbyPlayerListView.KickFontSize));
                Assert.That(kick.color, Is.EqualTo(LobbyPlayerListView.KickColor));
                Assert.That(kick.font, Is.EqualTo(HomeUiFonts.ApplyRegular()));
                var kickRect = guestRow.Find("Kick") as RectTransform;
                Assert.That(kickRect.anchorMin.x, Is.EqualTo(1f));
                Assert.That(kickRect.anchoredPosition.x, Is.EqualTo(-LobbyPlayerListView.ActionRight));

                var name = guestRow.Find("Name").GetComponent<TMP_Text>();
                Assert.That(name.text, Is.EqualTo("게스트닉"));
                Assert.That(name.color, Is.EqualTo(Color.white));
                Assert.That(
                    hostRow.Find("Name").GetComponent<TMP_Text>().color,
                    Is.EqualTo(LobbyPlayerListView.SelfNicknameColor));
                Assert.That(name.fontSize, Is.EqualTo(LobbyPlayerListView.NicknameFontSize));
                Assert.That(name.font, Is.EqualTo(HomeUiFonts.ApplyRegular()));
                Assert.That(
                    name.rectTransform.anchoredPosition.x,
                    Is.EqualTo(
                        LobbyPlayerListView.AvatarLeft
                        + LobbyPlayerListView.AvatarSize
                        + LobbyPlayerListView.NicknameLeft));

                var avatar = guestRow.Find("Avatar") as RectTransform;
                Assert.That(avatar.sizeDelta, Is.EqualTo(new Vector2(
                    LobbyPlayerListView.AvatarSize,
                    LobbyPlayerListView.AvatarSize)));
                Assert.That(avatar.anchoredPosition.x, Is.EqualTo(LobbyPlayerListView.AvatarLeft));

                var leader = hostRow.Find("Leader") as RectTransform;
                var hostName = hostRow.Find("Name") as RectTransform;
                Assert.That(
                    leader.anchoredPosition.x,
                    Is.EqualTo(hostName.anchoredPosition.x + hostName.sizeDelta.x + LobbyPlayerListView.LeaderIconGap)
                        .Within(0.5f));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void SetParticipants_GuestDoesNotSeeKick()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = canvas.AddComponent<LobbyPlayerListView>();
                view.SetParticipants(
                    new[]
                    {
                        new LobbyParticipant("host-1", "방장닉", true),
                        new LobbyParticipant("player-2", "게스트닉", false),
                    },
                    localIsHost: false,
                    localPlayerId: "player-2");

                var hostRow = canvas.transform.Find("Columns/Participants/Scroll/RowRoot/Row_host-1");
                var guestRow = canvas.transform.Find("Columns/Participants/Scroll/RowRoot/Row_player-2");
                Assert.That(hostRow.Find("Kick"), Is.Null);
                Assert.That(guestRow.Find("Kick"), Is.Null);
                Assert.That(
                    guestRow.Find("Name").GetComponent<TMP_Text>().color,
                    Is.EqualTo(LobbyPlayerListView.SelfNicknameColor));
                Assert.That(
                    hostRow.Find("Name").GetComponent<TMP_Text>().color,
                    Is.EqualTo(Color.white));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void SetParticipants_OthersGetAReportTooltipAndSelfDoesNot()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = canvas.AddComponent<LobbyPlayerListView>();
                view.SetParticipants(
                    new[]
                    {
                        // 계정 식별자를 함께 줍니다. 실제 참가자는 연결 토큰으로 이 값을
                        // 들고 오고, 없으면 신고 버튼이 붙지 않습니다(S15P21D205-926).
                        new LobbyParticipant("host-1", "방장닉", true, "account-host-1"),
                        new LobbyParticipant("player-2", "게스트닉", false, "account-player-2"),
                    },
                    localIsHost: false,
                    localPlayerId: "player-2");

                var hostRow = canvas.transform.Find("Columns/Participants/Scroll/RowRoot/Row_host-1");
                var selfRow = canvas.transform.Find("Columns/Participants/Scroll/RowRoot/Row_player-2");
                Assert.That(hostRow.GetComponent<LobbyReportHover>(), Is.Not.Null);
                Assert.That(selfRow.GetComponent<LobbyReportHover>(), Is.Null);
                Assert.That(selfRow.Find("Report"), Is.Null);

                var tooltip = hostRow.Find("Report") as RectTransform;
                Assert.That(tooltip, Is.Not.Null);
                Assert.That(tooltip.gameObject.activeSelf, Is.False);
                Assert.That(tooltip.anchorMin, Is.EqualTo(new Vector2(0.5f, 0f)));
                Assert.That(tooltip.pivot, Is.EqualTo(new Vector2(0.5f, 1f)));
                Assert.That(tooltip.anchoredPosition.y, Is.EqualTo(-LobbyPlayerListView.ReportTooltipGap));
                var bridge = tooltip.Find(LobbyPlayerListView.ReportBridgeName) as RectTransform;
                Assert.That(bridge, Is.Not.Null);
                Assert.That(bridge.anchorMin, Is.EqualTo(new Vector2(0f, 1f)));
                Assert.That(
                    bridge.sizeDelta.y,
                    Is.EqualTo(
                        LobbyPlayerListView.ReportTooltipGap
                        + LobbyPlayerListView.ReportTooltipOverlap));
                var label = tooltip.Find("Label").GetComponent<TMP_Text>();
                Assert.That(label.text, Is.EqualTo(LobbyPlayerListView.ReportLabel));
                Assert.That(label.fontSize, Is.EqualTo(18f));
                Assert.That(label.font, Is.EqualTo(HomeUiFonts.ApplyRegular()));
                Assert.That(label.color, Is.EqualTo(LobbyPlayerListView.ReportTooltipLabel));
                Assert.That(
                    tooltip.GetComponent<Image>().color,
                    Is.EqualTo(LobbyPlayerListView.ReportTooltipFill));

                hostRow.GetComponent<LobbyReportHover>().ShowTooltip();
                Assert.That(tooltip.gameObject.activeSelf, Is.True);

                var reported = new List<(string Id, string Name)>();
                view.ReportClicked += (id, name) => reported.Add((id, name));
                tooltip.GetComponent<Button>().onClick.Invoke();
                // 계정 식별자입니다. 여기가 Photon 번호("host-1")를 기대하고 있었고,
                // 그래서 모든 신고가 404 로 버려지는 동안에도 이 테스트는 초록불이었습니다
                // (S15P21D205-926).
                Assert.That(reported, Is.EqualTo(new[] { ("account-host-1", "방장닉") }));
                Assert.That(tooltip.gameObject.activeSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void SetParticipants_HostCanReportAGuestAndStillKick()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = canvas.AddComponent<LobbyPlayerListView>();
                view.SetParticipants(
                    new[]
                    {
                        new LobbyParticipant("host-1", "방장닉", true, "account-host-1"),
                        new LobbyParticipant("player-2", "게스트닉", false, "account-player-2"),
                    },
                    localIsHost: true,
                    localPlayerId: "host-1");

                var hostRow = canvas.transform.Find("Columns/Participants/Scroll/RowRoot/Row_host-1");
                var guestRow = canvas.transform.Find("Columns/Participants/Scroll/RowRoot/Row_player-2");
                Assert.That(hostRow.Find("Report"), Is.Null);
                Assert.That(guestRow.Find("Kick"), Is.Not.Null);
                var tooltip = guestRow.Find("Report") as RectTransform;
                Assert.That(tooltip, Is.Not.Null);
                Assert.That(tooltip.gameObject.activeSelf, Is.False);
                Assert.That(tooltip.anchorMin, Is.EqualTo(new Vector2(0.5f, 1f)));
                Assert.That(tooltip.pivot, Is.EqualTo(new Vector2(0.5f, 0f)));
                Assert.That(tooltip.anchoredPosition.y, Is.EqualTo(LobbyPlayerListView.ReportTooltipGap));

                guestRow.GetComponent<LobbyReportHover>().ShowTooltip();
                var reported = new List<(string Id, string Name)>();
                view.ReportClicked += (id, name) => reported.Add((id, name));
                tooltip.GetComponent<Button>().onClick.Invoke();
                Assert.That(reported, Is.EqualTo(new[] { ("account-player-2", "게스트닉") }));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void SetFriends_ShowsNicknamesAndPlusButtons()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = canvas.AddComponent<LobbyPlayerListView>();
                view.SetFriends(new[]
                {
                    new FriendSummary("f-1", "초대할까말까할까말까", FriendPresence.Online),
                    new FriendSummary("f-2", "오프라인친구", FriendPresence.Offline),
                });

                var root = canvas.transform.Find("Columns/Friends/Scroll/RowRoot");
                var online = root.Find(LobbyPlayerListView.OnlineItemsName);
                Assert.That(root.Find("OnlineSection").gameObject.activeSelf, Is.True);
                Assert.That(root.Find("OnlineSection").GetComponent<TMP_Text>().text,
                    Is.EqualTo(LobbyPlayerListView.OnlineSectionTitle));
                Assert.That(root.Find("OnlineSection").GetComponent<TMP_Text>().color,
                    Is.EqualTo(LobbyPlayerListView.OnlineSectionColor));
                Assert.That(root.Find("WaitingSection").gameObject.activeSelf, Is.False);
                Assert.That(root.Find("InGameSection").gameObject.activeSelf, Is.False);
                Assert.That(online.gameObject.activeSelf, Is.True);
                Assert.That(online.childCount, Is.EqualTo(1));
                Assert.That(root.Find(LobbyPlayerListView.WaitingItemsName).gameObject.activeSelf, Is.False);
                Assert.That(root.Find(LobbyPlayerListView.InGameItemsName).gameObject.activeSelf, Is.False);
                Assert.That(
                    online.Find("Friend_f-1/Name").GetComponent<TMP_Text>().text,
                    Is.EqualTo("초대할까말까할까말까"));
                Assert.That(online.Find("Friend_f-2"), Is.Null);
                var add = online.Find("Friend_f-1/Add") as RectTransform;
                Assert.That(add, Is.Not.Null);
                Assert.That(add.sizeDelta, Is.EqualTo(new Vector2(
                    LobbyPlayerListView.AddButtonSize,
                    LobbyPlayerListView.AddButtonSize)));
                Assert.That(add.anchorMin.x, Is.EqualTo(1f));
                Assert.That(add.anchoredPosition.x, Is.EqualTo(-LobbyPlayerListView.ActionRight));
                Assert.That(add.GetComponent<Button>().interactable, Is.True);

                var invited = new List<(string Id, string Name)>();
                view.InviteClicked += (id, name) => invited.Add((id, name));
                add.GetComponent<Button>().onClick.Invoke();
                Assert.That(invited, Is.EqualTo(new[] { ("f-1", "초대할까말까할까말까") }));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void Invite_LocksThatPlayersPlusForTenSeconds()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var now = 100f;
                var view = canvas.AddComponent<LobbyPlayerListView>();
                view.SetInviteClock(() => now);
                view.SetFriends(new[]
                {
                    new FriendSummary("f-1", "첫번째", FriendPresence.Online),
                    new FriendSummary("f-2", "두번째", FriendPresence.Online),
                });

                var invited = new List<string>();
                view.InviteClicked += (id, _) => invited.Add(id);

                var first = canvas.transform
                    .Find("Columns/Friends/Scroll/RowRoot/"
                        + LobbyPlayerListView.OnlineItemsName
                        + "/Friend_f-1/Add");
                var second = canvas.transform
                    .Find("Columns/Friends/Scroll/RowRoot/"
                        + LobbyPlayerListView.OnlineItemsName
                        + "/Friend_f-2/Add");
                first.GetComponent<Button>().onClick.Invoke();
                first.GetComponent<Button>().onClick.Invoke();

                var lockedIcon = first.GetComponent<Image>().sprite;
                var readyIcon = second.GetComponent<Image>().sprite;
                Assert.That(invited, Is.EqualTo(new[] { "f-1" }));
                Assert.That(first.GetComponent<Button>().interactable, Is.False);
                Assert.That(lockedIcon, Is.Not.Null);
                Assert.That(readyIcon, Is.Not.Null);
                Assert.That(lockedIcon, Is.Not.EqualTo(readyIcon));
                Assert.That(second.GetComponent<Button>().interactable, Is.True);
                Assert.That(
                    second.GetComponent<Image>().sprite,
                    Is.EqualTo(readyIcon));

                view.SetFriends(new[]
                {
                    new FriendSummary("f-1", "첫번째", FriendPresence.Online),
                    new FriendSummary("f-2", "두번째", FriendPresence.Online),
                });
                first = canvas.transform
                    .Find("Columns/Friends/Scroll/RowRoot/"
                        + LobbyPlayerListView.OnlineItemsName
                        + "/Friend_f-1/Add");
                Assert.That(first.GetComponent<Button>().interactable, Is.False);
                Assert.That(first.GetComponent<Image>().sprite, Is.EqualTo(lockedIcon));

                now += LobbyPlayerListView.InviteCooldownSeconds;
                view.RefreshInviteCooldowns();
                Assert.That(first.GetComponent<Button>().interactable, Is.True);
                Assert.That(first.GetComponent<Image>().sprite, Is.EqualTo(readyIcon));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void SetFriends_GroupsByPresenceAndLocksBusyInvites()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = canvas.AddComponent<LobbyPlayerListView>();
                view.SetFriends(new[]
                {
                    new FriendSummary("online-1", "접속중", FriendPresence.Online),
                    new FriendSummary("lobby-1", "로비대기", FriendPresence.InLobby),
                    new FriendSummary("game-1", "한판중", FriendPresence.InGame),
                });

                var root = canvas.transform.Find("Columns/Friends/Scroll/RowRoot");
                var online = root.Find(LobbyPlayerListView.OnlineItemsName + "/Friend_online-1/Add")
                    .GetComponent<Button>();
                var waiting = root.Find(LobbyPlayerListView.WaitingItemsName + "/Friend_lobby-1/Add")
                    .GetComponent<Button>();
                var playing = root.Find(LobbyPlayerListView.InGameItemsName + "/Friend_game-1/Add")
                    .GetComponent<Button>();
                Assert.That(
                    root.Find(LobbyPlayerListView.OnlineItemsName + "/Friend_online-1/Name")
                        .GetComponent<TMP_Text>().text,
                    Is.EqualTo("접속중"));
                Assert.That(
                    root.Find(LobbyPlayerListView.WaitingItemsName + "/Friend_lobby-1/Name")
                        .GetComponent<TMP_Text>().text,
                    Is.EqualTo("로비대기"));
                Assert.That(
                    root.Find(LobbyPlayerListView.InGameItemsName + "/Friend_game-1/Name")
                        .GetComponent<TMP_Text>().text,
                    Is.EqualTo("한판중"));
                Assert.That(root.Find("OnlineSection").gameObject.activeSelf, Is.True);
                Assert.That(root.Find("WaitingSection").gameObject.activeSelf, Is.True);
                Assert.That(root.Find("InGameSection").gameObject.activeSelf, Is.True);
                Assert.That(root.Find("WaitingSection").GetComponent<TMP_Text>().color,
                    Is.EqualTo(LobbyPlayerListView.WaitingSectionColor));
                Assert.That(root.Find("InGameSection").GetComponent<TMP_Text>().color,
                    Is.EqualTo(LobbyPlayerListView.InGameSectionColor));
                Assert.That(online.interactable, Is.True);
                Assert.That(waiting.interactable, Is.False);
                Assert.That(playing.interactable, Is.False);
                Assert.That(
                    waiting.GetComponent<Image>().sprite,
                    Is.EqualTo(LobbyPlayerListSprites.PlusGray));
                Assert.That(
                    playing.GetComponent<Image>().sprite,
                    Is.EqualTo(LobbyPlayerListSprites.PlusGray));

                var invited = new List<string>();
                view.InviteClicked += (id, _) => invited.Add(id);
                waiting.onClick.Invoke();
                playing.onClick.Invoke();
                Assert.That(invited, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void SetFriends_HidesEmptySectionTitlesUntilSomeoneAppears()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = canvas.AddComponent<LobbyPlayerListView>();
                view.SetFriends(Array.Empty<FriendSummary>());

                var root = canvas.transform.Find("Columns/Friends/Scroll/RowRoot");
                Assert.That(root.Find("OnlineSection").gameObject.activeSelf, Is.False);
                Assert.That(root.Find("WaitingSection").gameObject.activeSelf, Is.False);
                Assert.That(root.Find("InGameSection").gameObject.activeSelf, Is.False);

                view.SetFriends(new[]
                {
                    new FriendSummary("game-1", "한판중", FriendPresence.InGame),
                });
                Assert.That(root.Find("OnlineSection").gameObject.activeSelf, Is.False);
                Assert.That(root.Find("WaitingSection").gameObject.activeSelf, Is.False);
                Assert.That(root.Find("InGameSection").gameObject.activeSelf, Is.True);
                Assert.That(root.Find("InGameSection").GetComponent<TMP_Text>().text,
                    Is.EqualTo(LobbyPlayerListView.InGameSectionTitle));
                Assert.That(
                    root.Find(LobbyPlayerListView.InGameItemsName + "/Friend_game-1/Name")
                        .GetComponent<TMP_Text>().text,
                    Is.EqualTo("한판중"));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }
    }
}
