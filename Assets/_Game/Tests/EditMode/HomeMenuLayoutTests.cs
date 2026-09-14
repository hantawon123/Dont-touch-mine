using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Client.Common;
using Game.Client.Home;
using Game.Core.Home;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// What the Home screen puts on screen, checked against the mock-up.
    /// </summary>
    /// <remarks>
    /// The presenter tests already cover what happens once an action is raised.
    /// What they cannot see is whether the label a player actually clicks is
    /// wired to the action the presenter expects — the menu is built in code,
    /// so nothing else records that "방 만들기" means
    /// <see cref="HomeMenuAction.CreateRoom"/>.
    /// <para>
    /// The layout is built by calling <c>BuildLayout</c> directly rather than by
    /// entering play mode. Edit mode never runs <c>Awake</c>, and the alternative
    /// is a play-mode test that spins up a scene to assert on four labels.
    /// </para>
    /// </remarks>
    public sealed class HomeMenuLayoutTests
    {
        private static readonly HomeMenuAction[] MenuOrder =
        {
            HomeMenuAction.CreateRoom,
            HomeMenuAction.FindRoom,
            HomeMenuAction.Character,
            HomeMenuAction.Settings
        };

        [Test]
        public void Menu_ListsTheFourEntriesTheMockUpDraws()
        {
            using var home = new BuiltHome();

            var labels = new List<string>();
            foreach (var action in MenuOrder)
            {
                labels.Add(home.Label(action.ToString()));
            }

            Assert.That(
                labels,
                Is.EqualTo(new[] { "방 만들기", "게임 찾기", "캐릭터", "환경 설정" }));
        }

        [Test]
        public void Menu_StacksEntriesAtTheDesignedPitch()
        {
            using var home = new BuiltHome();

            for (var index = 0; index < MenuOrder.Length; index++)
            {
                var item = home.Rect(MenuOrder[index].ToString());
                Assert.That(
                    item.anchoredPosition.y,
                    Is.EqualTo(-index * HomeStyle.Layout.MenuPitch).Within(0.01f),
                    $"{MenuOrder[index]} sits off the menu's pitch.");
            }
        }

        [Test]
        public void ClickingAnything_RaisesTheActionBehindIt()
        {
            var expected = new Dictionary<string, HomeMenuAction>
            {
                { HomeMenuAction.CreateRoom.ToString(), HomeMenuAction.CreateRoom },
                { HomeMenuAction.FindRoom.ToString(), HomeMenuAction.FindRoom },
                { HomeMenuAction.Character.ToString(), HomeMenuAction.Character },
                { HomeMenuAction.Settings.ToString(), HomeMenuAction.Settings },
                { "QuitButton", HomeMenuAction.Quit },
                { "ProfileChip", HomeMenuAction.ProfileSettings },
                { "FriendButton", HomeMenuAction.Friends },
                { "ServerButton", HomeMenuAction.ServerSettings }
            };

            using var home = new BuiltHome();
            var raised = new List<HomeMenuAction>();
            home.View.ActionClicked += raised.Add;

            foreach (var pair in expected)
            {
                raised.Clear();
                home.Rect(pair.Key).GetComponent<Button>().onClick.Invoke();
                Assert.That(raised, Is.EqualTo(new[] { pair.Value }), $"{pair.Key} is miswired.");
            }
        }

        [Test]
        public void ProfileChip_GrowsWithTheNameWithinTheDesignedWidths()
        {
            using var home = new BuiltHome();
            var chip = home.Rect("ProfileChip");

            home.View.SetNickname("김");
            var shortest = chip.sizeDelta.x;

            home.View.SetNickname("금오산냥냥이");
            var middling = chip.sizeDelta.x;

            home.View.SetNickname("이건바로열두글자이지렁롱");
            var longest = chip.sizeDelta.x;

            Assert.That(shortest, Is.EqualTo(HomeStyle.Layout.ChipMinWidth).Within(0.01f));
            Assert.That(longest, Is.GreaterThan(middling));
            Assert.That(middling, Is.GreaterThanOrEqualTo(shortest));
            Assert.That(longest, Is.LessThanOrEqualTo(HomeStyle.Layout.ChipMaxWidth));
            Assert.That(chip.sizeDelta.y, Is.EqualTo(HomeStyle.Layout.ChipHeight).Within(0.01f));
        }

        [Test]
        public void NicknameMessage_SitsClearOfTheFieldAndInsideItsEdge()
        {
            using var home = new BuiltHome();
            var message = home.Rect("NicknameMessage");
            var counter = home.Rect("NicknameCounter");

            // A note on the field, not part of its plate: a clear gap under
            // the field, and started a little inside its edge rather than
            // flush with the corner. The counter mirrors it on the right.
            Assert.That(message.anchoredPosition.x, Is.EqualTo(HomeStyle.Profile.TextInset).Within(0.01f));
            Assert.That(counter.anchoredPosition.x, Is.EqualTo(-HomeStyle.Profile.TextInset).Within(0.01f));
            Assert.That(HomeStyle.Profile.TextInset, Is.GreaterThan(HomeStyle.Profile.SidePadding));
            Assert.That(
                (HomeStyle.Profile.InputTop - HomeStyle.Profile.InputSize.y) - HomeStyle.Profile.MessageTop,
                Is.GreaterThanOrEqualTo(10f));
        }

        [Test]
        public void SettledNickname_TakesTheApplyRowAndTheCounterOffThePanel()
        {
            using var home = new BuiltHome();
            var panel = home.Rect("ProfileSettingsPanel");
            var apply = home.Rect("ApplyButton");
            var counter = home.Rect("NicknameCounter");

            home.View.SetNicknameSettled(true);

            // Nothing left to type, so nothing that talks about typing: no
            // greyed-out apply, no 3/12, and no empty row where they were.
            Assert.That(apply.gameObject.activeSelf, Is.False);
            Assert.That(counter.gameObject.activeSelf, Is.False);
            Assert.That(
                panel.sizeDelta.y,
                Is.EqualTo(HomeStyle.Profile.SettledPanelHeight).Within(0.01f));
            Assert.That(panel.sizeDelta.y, Is.LessThan(HomeStyle.Profile.PanelSize.y));

            home.View.SetNicknameSettled(false);

            Assert.That(apply.gameObject.activeSelf, Is.True);
            Assert.That(counter.gameObject.activeSelf, Is.True);
            Assert.That(
                panel.sizeDelta.y,
                Is.EqualTo(HomeStyle.Profile.PanelSize.y).Within(0.01f));
        }

        [Test]
        public void ProfileChip_StopsAtTheMaximumWidth()
        {
            using var home = new BuiltHome();
            var chip = home.Rect("ProfileChip");

            // Longer than a nickname is allowed to be, which is the case the
            // ceiling exists for: the chip must not run under the friend button.
            home.View.SetNickname(new string('가', 40));

            Assert.That(
                chip.sizeDelta.x,
                Is.EqualTo(HomeStyle.Layout.ChipMaxWidth).Within(0.01f));
        }

        [Test]
        public void FriendList_WithNobodyInIt_SaysSoUnderBothHeadings()
        {
            using var home = new BuiltHome();
            var view = (IHomeMenuView)home.View;

            view.SetFriends(
                System.Array.Empty<FriendSummary>(), System.Array.Empty<FriendSummary>());

            foreach (var name in new[] { "OnlineEmptyMessage", "OfflineEmptyMessage" })
            {
                var empty = home.Rect(name);
                Assert.That(empty.gameObject.activeSelf, Is.True, name);
                Assert.That(
                    empty.GetComponent<TMPro.TMP_Text>().text, Is.EqualTo("친구가 없어요"), name);
            }
        }

        [Test]
        public void FriendList_KeepsBothHeadingsAndMarksOnlyTheEmptyOne()
        {
            using var home = new BuiltHome();
            var view = (IHomeMenuView)home.View;

            view.SetFriends(
                new[] { new FriendSummary("p1", "가나다", FriendPresence.Online) },
                System.Array.Empty<FriendSummary>());

            Assert.That(
                home.Section("온라인").gameObject.activeSelf,
                Is.True,
                "머리글은 비어 있든 아니든 남는다.");
            Assert.That(home.Section("오프라인").gameObject.activeSelf, Is.True);

            Assert.That(
                home.Rect("OnlineEmptyMessage").gameObject.activeSelf,
                Is.False,
                "친구가 있는 섹션에 없다는 안내가 뜨면 안 된다.");
            Assert.That(home.Rect("OfflineEmptyMessage").gameObject.activeSelf, Is.True);
        }

        /// <summary>
        /// A Home screen assembled in memory, torn down with the test.
        /// </summary>
        /// <summary>
        /// An open panel marks the button that opened it, without the pointer.
        /// </summary>
        /// <remarks>
        /// The outline used to mean "the pointer is here" and nothing else, so
        /// a player with a panel open and the mouse anywhere else had no way to
        /// tell which of the three buttons they were inside.
        /// </remarks>
        [Test]
        public void OpeningAPanel_OutlinesTheButtonThatOpensIt()
        {
            using var home = new BuiltHome();

            foreach (var pair in PanelButtons)
            {
                var stroke = home.Stroke(pair.Key);
                Assert.That(stroke.enabled, Is.False, $"{pair.Key} starts outlined.");

                pair.Value(home.View, true);
                Assert.That(
                    stroke.enabled, Is.True, $"{pair.Key} is not outlined while its panel is up.");

                pair.Value(home.View, false);
                Assert.That(
                    stroke.enabled, Is.False, $"{pair.Key} stays outlined after its panel closes.");
            }
        }

        /// <summary>
        /// Opening one panel does not leave another button outlined.
        /// </summary>
        [Test]
        public void OpeningAPanel_LeavesTheOtherButtonsUnmarked()
        {
            using var home = new BuiltHome();

            home.View.SetFriendListVisible(true);

            Assert.That(home.Stroke("FriendButton").enabled, Is.True);
            Assert.That(home.Stroke("ProfileChip").enabled, Is.False);
            Assert.That(home.Stroke("ServerButton").enabled, Is.False);
        }

        /// <summary>
        /// A search that found somebody never also says nobody was found.
        /// </summary>
        /// <remarks>
        /// The two arrive separately: the rows come from the search, and the
        /// success then clears the last failure. That clear used to assume an
        /// empty result and put the message back over a player who was already
        /// on screen, which is what the panel showed.
        /// </remarks>
        [Test]
        public void ClearingAnErrorAfterAHit_DoesNotSayNobodyWasFound()
        {
            using var home = new BuiltHome();
            home.View.SetFriendSearchVisible(true);
            home.Typed("가짜크런키더블크런치바");

            home.View.SetFriendSearchResults(
                new[]
                {
                    new FriendSearchHit("p1", "가짜크런키더블크런치바", FriendRequestState.None)
                });
            Assert.That(
                home.SearchEmpty().activeSelf, Is.False, "The hit alone already reads as empty.");

            home.View.SetFriendActionError(string.Empty);

            Assert.That(
                home.SearchEmpty().activeSelf,
                Is.False,
                "Clearing the failure put the not-found line back over a player who was found.");
        }

        /// <summary>
        /// A search that found nobody still says so once the error clears.
        /// </summary>
        [Test]
        public void ClearingAnErrorWithNoHits_StillSaysNobodyWasFound()
        {
            using var home = new BuiltHome();
            home.View.SetFriendSearchVisible(true);
            home.Typed("없는사람");

            home.View.SetFriendSearchResults(Array.Empty<FriendSearchHit>());
            home.View.SetFriendActionError(string.Empty);

            Assert.That(home.SearchEmpty().activeSelf, Is.True);
        }

        /// <summary>
        /// Every menu line grows under the pointer and settles back after it.
        /// </summary>
        [Test]
        public void HoveringAMenuLine_GrowsItAndLettingGoSettlesItBack()
        {
            using var home = new BuiltHome();

            foreach (var action in MenuOrder)
            {
                var line = home.Rect(action.ToString());
                var pop = line.GetComponent<HomeLabelPop>();
                Assert.That(pop, Is.Not.Null, $"{action} does not answer the pointer.");
                Assert.That(line.localScale.x, Is.EqualTo(1f).Within(0.001f), $"{action} starts grown");

                pop.OnPointerEnter(null);
                pop.Advance(HomeStyle.Layout.MenuHoverSeconds);
                Assert.That(
                    line.localScale.x,
                    Is.EqualTo(HomeStyle.Layout.MenuHoverScale).Within(0.001f),
                    $"{action} did not grow");

                pop.OnPointerExit(null);
                pop.Advance(HomeStyle.Layout.MenuHoverSeconds);
                Assert.That(
                    line.localScale.x,
                    Is.EqualTo(1f).Within(0.001f),
                    $"{action} stayed grown after the pointer left");
            }
        }

        /// <summary>
        /// The growth stops at the hover size rather than running past it, and
        /// a line abandoned part-grown still arrives.
        /// </summary>
        [Test]
        public void TheGrowth_StopsAtTheHoverSizeAndSurvivesAHurriedPass()
        {
            using var home = new BuiltHome();
            var line = home.Rect(HomeMenuAction.CreateRoom.ToString());
            var pop = line.GetComponent<HomeLabelPop>();

            pop.OnPointerEnter(null);
            pop.Advance(HomeStyle.Layout.MenuHoverSeconds * 10f);

            Assert.That(
                line.localScale.x,
                Is.EqualTo(HomeStyle.Layout.MenuHoverScale).Within(0.001f),
                "it grew past the hover size");

            // The pointer crossed the line and left before the growth finished.
            pop.OnPointerExit(null);
            pop.Advance(HomeStyle.Layout.MenuHoverSeconds * 0.25f);
            Assert.That(line.localScale.x, Is.LessThan(HomeStyle.Layout.MenuHoverScale));

            pop.Advance(HomeStyle.Layout.MenuHoverSeconds);
            Assert.That(line.localScale.x, Is.EqualTo(1f).Within(0.001f), "left part-grown");
        }

        /// <summary>
        /// A line let go of while the pointer is on it comes back its own size
        /// at once, rather than easing back from under a pointer that is no
        /// longer going to leave.
        /// </summary>
        /// <remarks>
        /// Driven through <c>Release</c> rather than by hiding the line, which
        /// is what calls it in the game: edit mode runs no lifecycle callbacks,
        /// so switching the object off here would raise no <c>OnDisable</c>.
        /// </remarks>
        [Test]
        public void ALineLetGoOfWhileHovered_ComesBackItsOwnSizeAtOnce()
        {
            using var home = new BuiltHome();
            var line = home.Rect(HomeMenuAction.Character.ToString());
            var pop = line.GetComponent<HomeLabelPop>();

            pop.OnPointerEnter(null);
            pop.Advance(HomeStyle.Layout.MenuHoverSeconds);
            Assert.That(line.localScale.x, Is.EqualTo(HomeStyle.Layout.MenuHoverScale).Within(0.001f));

            pop.Release();

            Assert.That(line.localScale.x, Is.EqualTo(1f).Within(0.001f));

            // The pointer is forgotten too, so it does not grow back on the
            // next frame without the pointer ever returning.
            pop.Advance(HomeStyle.Layout.MenuHoverSeconds);
            Assert.That(line.localScale.x, Is.EqualTo(1f).Within(0.001f));
        }

        /// <summary>
        /// The cards live on a root the screen switching leaves alone, so an
        /// invitation can be answered from the room browser or the closet.
        /// </summary>
        [Test]
        public void TheInviteStack_SitsOnARootThatSurvivesScreenSwitching()
        {
            using var home = new BuiltHome();

            var invites = home.InviteRoot;
            Assert.That(invites, Is.Not.Null, "the cards were not given a root of their own.");
            Assert.That(
                invites.transform.parent,
                Is.Null,
                "the cards hang off the home screen and would be switched off with it.");
            Assert.That(
                invites.GetComponent<FrontendPersistentRoot>(),
                Is.Not.Null,
                "nothing tells the screen switching to leave this root alone.");

            var canvas = invites.GetComponent<Canvas>();
            Assert.That(canvas, Is.Not.Null, "the cards have no canvas to draw on.");
            Assert.That(
                canvas.sortingOrder,
                Is.GreaterThan(100),
                "a card would draw behind the home screen's own panels.");
            Assert.That(
                invites.GetComponent<GraphicRaycaster>(),
                Is.Not.Null,
                "the accept and decline buttons would take no clicks.");
        }

        /// <summary>
        /// The invite stack is three cards at the top left, all hidden until
        /// something arrives, spaced as the design draws them.
        /// </summary>
        [Test]
        public void InviteStack_HasThreeHiddenCardsAtTheDesignedPitch()
        {
            using var home = new BuiltHome();

            var stack = home.Rect("InviteStack");
            Assert.That(
                stack.anchoredPosition,
                Is.EqualTo(new Vector2(HomeStyle.Toast.Left, -HomeStyle.Toast.Top)));

            for (var slot = 0; slot < RoomInviteInbox.VisibleLimit; slot++)
            {
                var card = home.Rect($"Invite{slot}");
                Assert.That(
                    card.sizeDelta,
                    Is.EqualTo(new Vector2(HomeStyle.Toast.Width, HomeStyle.Toast.Height)),
                    $"card {slot} size");
                Assert.That(
                    card.anchoredPosition.y,
                    Is.EqualTo(-slot * (HomeStyle.Toast.Height + HomeStyle.Toast.Gap)).Within(0.01f),
                    $"card {slot} sits off the stack's pitch");
                Assert.That(card.gameObject.activeSelf, Is.False, $"card {slot} starts shown");
            }
        }

        /// <summary>
        /// Cards fill from the top in the order given and say who is asking.
        /// </summary>
        [Test]
        public void ShowingInvites_FillsCardsFromTheTopAndHidesTheRest()
        {
            using var home = new BuiltHome();

            home.View.SetRoomInvites(
                new[]
                {
                    new RoomInvite("a", "p1", "하나", "R1"),
                    new RoomInvite("b", "p2", "둘", "R2")
                });

            Assert.That(home.Rect("Invite0").gameObject.activeSelf, Is.True);
            Assert.That(home.Rect("Invite1").gameObject.activeSelf, Is.True);
            Assert.That(home.Rect("Invite2").gameObject.activeSelf, Is.False);
            Assert.That(
                home.Rect("Invite0").Find("Body").GetComponent<TMPro.TMP_Text>().text,
                Is.EqualTo("하나님이\n함께 플레이하자고 합니다!"));

            home.View.SetRoomInvites(Array.Empty<RoomInvite>());

            Assert.That(home.Rect("Invite0").gameObject.activeSelf, Is.False, "cleared");
        }

        /// <summary>
        /// A card's buttons answer for the invite that card is showing, not for
        /// whichever one was there when the card was built.
        /// </summary>
        [Test]
        public void PressingACardsButtons_RaisesTheInviteItIsShowing()
        {
            using var home = new BuiltHome();
            var accepted = new List<string>();
            var declined = new List<string>();
            home.View.RoomInviteAccepted += accepted.Add;
            home.View.RoomInviteDeclined += declined.Add;

            home.View.SetRoomInvites(
                new[]
                {
                    new RoomInvite("a", "p1", "하나", "R1"),
                    new RoomInvite("b", "p2", "둘", "R2")
                });
            home.Rect("Invite1").Find("Accept").GetComponent<Button>().onClick.Invoke();
            home.Rect("Invite0").Find("Decline").GetComponent<Button>().onClick.Invoke();

            Assert.That(accepted, Is.EqualTo(new[] { "b" }));
            Assert.That(declined, Is.EqualTo(new[] { "a" }));

            // The same card now shows a different invite and answers for it.
            home.View.SetRoomInvites(new[] { new RoomInvite("c", "p3", "셋", "R3") });
            home.Rect("Invite0").Find("Accept").GetComponent<Button>().onClick.Invoke();

            Assert.That(accepted, Is.EqualTo(new[] { "b", "c" }));
        }

        [Test]
        public void SuspendedNotice_StartsHiddenAndCoversTheScreenWhenShown()
        {
            using var home = new BuiltHome();

            var notice = home.Rect("SuspendedNotice");
            Assert.That(notice, Is.Not.Null, "정지 안내가 만들어지지 않았습니다.");
            Assert.That(notice.gameObject.activeSelf, Is.False,
                "정지되지 않은 사람에게 안내가 잠깐이라도 보이면 안 됩니다.");

            home.View.SetSuspendedNoticeVisible(true);
            Assert.That(notice.gameObject.activeSelf, Is.True);
            Assert.That(home.View.IsSuspendedNoticeVisible, Is.True);

            // 화면 전체를 덮어야 뒤의 버튼을 가립니다.
            Assert.That(notice.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(notice.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(notice.offsetMin, Is.EqualTo(Vector2.zero));
            Assert.That(notice.offsetMax, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void SuspendedNotice_SwallowsClicksMeantForTheMenu()
        {
            // 이게 없으면 안내는 살아 있는 버튼 위에 얹힌 그림일 뿐입니다.
            using var home = new BuiltHome();

            var scrim = home.Rect("SuspendedNotice").GetComponent<Image>();
            Assert.That(scrim, Is.Not.Null);
            Assert.That(scrim.raycastTarget, Is.True);
        }

        [Test]
        public void SuspendedNotice_IsBuiltLastSoItDrawsOverEverything()
        {
            // 유니티는 나중 형제를 먼저 히트테스트하고 나중에 그립니다. 먼저 만들면
            // 안내 아래의 메뉴가 그대로 눌립니다.
            using var home = new BuiltHome();

            var notice = home.Rect("SuspendedNotice");
            var canvas = notice.parent;
            Assert.That(
                notice.GetSiblingIndex(),
                Is.EqualTo(canvas.childCount - 1),
                "정지 안내는 캔버스의 마지막 자식이어야 합니다.");
        }

        [Test]
        public void SuspendedNotice_SaysWhatHappenedAndOffersNothingToPress()
        {
            using var home = new BuiltHome();
            home.View.SetSuspendedNoticeVisible(true);

            var notice = home.Rect("SuspendedNotice");
            var texts = notice.GetComponentsInChildren<TMPro.TMP_Text>(true);
            var lines = new List<string>();
            foreach (var text in texts)
            {
                lines.Add(text.text);
            }

            Assert.That(lines, Contains.Item(HomeMenuView.SuspendedTitle));
            Assert.That(lines, Contains.Item(HomeMenuView.SuspendedBody));

            // 닫기도 재시도도 두지 않습니다. 정지는 눌러서 풀리지 않고, 계정 발급을
            // 다시 불러도 같은 403 입니다. 누를 수 있는 것이 있으면 눌러보게 됩니다.
            Assert.That(
                notice.GetComponentsInChildren<Button>(true),
                Is.Empty,
                "정지 안내에는 누를 수 있는 것이 없어야 합니다.");
        }

        private static readonly Dictionary<string, Action<HomeMenuView, bool>> PanelButtons =
            new Dictionary<string, Action<HomeMenuView, bool>>
            {
                { "FriendButton", (view, visible) => view.SetFriendListVisible(visible) },
                { "ProfileChip", (view, visible) => view.SetProfileSettingsVisible(visible) },
                { "ServerButton", (view, visible) => view.SetServerSettingsVisible(visible) }
            };

        private sealed class BuiltHome : IDisposable
        {
            private readonly GameObject root;

            public BuiltHome()
            {
                root = new GameObject("HomeMenuViewUnderTest");
                View = root.AddComponent<HomeMenuView>();

                var build = typeof(HomeMenuView).GetMethod(
                    "BuildLayout", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(build, Is.Not.Null, "HomeMenuView.BuildLayout is gone.");
                build.Invoke(View, null);
            }

            public HomeMenuView View { get; }

            /// <remarks>
            /// Both roots are searched. The invite cards sit on a scene root of
            /// their own so that browsing rooms does not switch them off with
            /// the home screen, which puts them outside this object.
            /// </remarks>
            public RectTransform Rect(string name)
            {
                foreach (var candidate in root.GetComponentsInChildren<RectTransform>(true))
                {
                    if (candidate.name == name)
                    {
                        return candidate;
                    }
                }

                var invites = InviteRoot;
                if (invites != null)
                {
                    foreach (var candidate in invites.GetComponentsInChildren<RectTransform>(true))
                    {
                        if (candidate.name == name)
                        {
                            return candidate;
                        }
                    }
                }

                Assert.Fail($"Home does not draw anything named {name}.");
                return null;
            }

            /// <summary>The cards' own scene root, or null before it is built.</summary>
            public GameObject InviteRoot
            {
                get
                {
                    var field = typeof(HomeMenuView).GetField(
                        "inviteRoot", BindingFlags.Instance | BindingFlags.NonPublic);
                    Assert.That(field, Is.Not.Null, "HomeMenuView.inviteRoot is gone.");
                    return (GameObject)field.GetValue(View);
                }
            }

            /// <summary>
            /// A section heading, found by the words it shows rather than by
            /// name: every heading is called "Section".
            /// </summary>
            public RectTransform Section(string label)
            {
                foreach (var candidate in root.GetComponentsInChildren<TMPro.TMP_Text>(true))
                {
                    if (candidate.text == label)
                    {
                        return candidate.rectTransform;
                    }
                }

                Assert.Fail($"Home does not draw a section headed {label}.");
                return null;
            }

            public string Label(string name)
            {
                return Rect(name).GetComponent<TMPro.TMP_Text>().text;
            }

            /// <summary>
            /// The outline drawn under a bottom-bar button.
            /// </summary>
            public Image Stroke(string buttonName)
            {
                var stroke = Rect(buttonName).Find("Stroke");
                Assert.That(stroke, Is.Not.Null, $"{buttonName} draws no Stroke.");
                return stroke.GetComponent<Image>();
            }

            /// <summary>
            /// The line that says a search found nobody.
            /// </summary>
            public GameObject SearchEmpty()
            {
                return Private<TMPro.TMP_Text>("searchEmptyText").gameObject;
            }

            /// <summary>
            /// Puts a query in the search box, which is what tells the panel a
            /// search was asked for at all.
            /// </summary>
            public void Typed(string query)
            {
                Private<TMPro.TMP_InputField>("friendSearchInput").text = query;
            }

            private T Private<T>(string name) where T : class
            {
                var field = typeof(HomeMenuView).GetField(
                    name, BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null, $"HomeMenuView.{name} is gone.");
                var value = field.GetValue(View) as T;
                Assert.That(value, Is.Not.Null, $"HomeMenuView.{name} was never built.");
                return value;
            }

            /// <remarks>
            /// The invite root is destroyed here rather than left to the view's
            /// own <c>OnDestroy</c>: edit mode runs no lifecycle callbacks, so
            /// it would outlive the test and be found by the next one.
            /// </remarks>
            public void Dispose()
            {
                var invites = InviteRoot;
                if (invites != null)
                {
                    UnityEngine.Object.DestroyImmediate(invites);
                }

                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
