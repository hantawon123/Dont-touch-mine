using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Bootstrap;
using Game.Client.Settings;
using Game.Core.Backend;
using Game.Core.Ports;
using Game.Core.Settings;
using NUnit.Framework;

namespace Game.Architecture.Tests
{
    /// <summary>
    /// What the screen does with the answer to a sent piece of feedback.
    /// </summary>
    /// <remarks>
    /// The point of these is the failure path. A refusal must leave the panel
    /// standing with what was written still in it and 보내기 alive again —
    /// somebody who filled five hundred characters and lost them to a timeout
    /// does not write them a second time.
    /// <para>
    /// <b>There is no presenter here.</b> Only the bridge is wired to the fake screen,
    /// so nothing opens or closes the writing panel and its visibility means nothing.
    /// What these tests watch is what the bridge asks the screen to do:
    /// <see cref="FakeSettingsView.SentCalls"/>, the notices, and 보내기.
    /// </para>
    /// <para>
    /// The sends here complete inline: a UniTask that is already finished
    /// resumes its awaiter synchronously, so an assertion straight after the
    /// press sees the answer. Where the wait itself matters, a
    /// <see cref="UniTaskCompletionSource{T}"/> holds it open on purpose.
    /// </para>
    /// </remarks>
    public sealed class SettingsFeedbackBridgeTests
    {
        private FakeSettingsView view;
        private FakeFeedbackGateway gateway;

        [SetUp]
        public void SetUp()
        {
            view = new FakeSettingsView();
            gateway = new FakeFeedbackGateway();
        }

        private SettingsFeedbackBridge Started()
        {
            var bridge = new SettingsFeedbackBridge(view, gateway);
            bridge.Start();
            return bridge;
        }

        [Test]
        public void Sent_AsksTheScreenToCloseAndThanks()
        {
            using var bridge = Started();
            view.Feedback();

            view.SubmitFeedback("소리가 너무 작아요");

            Assert.That(gateway.Calls, Is.EqualTo(1));
            Assert.That(gateway.LastMessage, Is.EqualTo("소리가 너무 작아요"));
            Assert.That(view.SentCalls, Is.EqualTo(1), "성공만 패널을 내립니다.");
            Assert.That(view.Notices, Is.EqualTo(new[] { SettingsStyle.FeedbackSentMessage }));
            Assert.That(view.NoticeIsSuccess, Is.EqualTo(new[] { true }));
        }

        [Test]
        public void Sent_ThanksInTheAppliedLanguage()
        {
            var store = new InMemoryGeneralSettingsStore();
            store.Save(new GeneralSettings("en"));
            var general = new GeneralSettingsSystem(store);
            using var locale = new UiLocale(general);
            using var bridge = new SettingsFeedbackBridge(view, gateway, locale);
            bridge.Start();
            view.Feedback();

            view.SubmitFeedback("too quiet");

            Assert.That(view.Notices, Is.EqualTo(new[] { "Sent. Thank you." }));
        }

        [Test]
        public void Refused_KeepsThePanelUpAndArmsSendAgain()
        {
            gateway.Answer = BackendResult.Failed(BackendFailure.Offline);

            using var bridge = Started();
            view.Feedback();
            view.TypeFeedback("소리가 너무 작아요");

            view.SubmitFeedback("소리가 너무 작아요");

            Assert.That(view.SentCalls, Is.Zero, "거절은 패널을 내리지 않습니다 - 쓴 글이 사라집니다.");
            Assert.That(view.SubmitEnabled, Is.True, "Retrying the same text has to be possible.");
            Assert.That(
                view.Notices,
                Is.EqualTo(new[]
                {
                    $"{SettingsStyle.FeedbackOfflineMessage}. {SettingsStyle.FeedbackKeptMessage}"
                }));
            Assert.That(view.NoticeIsSuccess, Is.EqualTo(new[] { false }));
        }

        [Test]
        public void RefusedAsTooLong_SaysSo()
        {
            // The box will not take more than the limit, so this arrives when
            // the two limits have drifted apart. Saying "보내지 못했습니다" then
            // sends whoever hits it looking in the wrong place.
            gateway.Answer = BackendResult.Failed(BackendFailure.InvalidRequest);

            using var bridge = Started();
            view.Feedback();

            view.SubmitFeedback("가");

            Assert.That(view.Notices, Has.Exactly(1).StartsWith(SettingsStyle.FeedbackTooLongMessage));
        }

        [Test]
        public void BeforeSignIn_SaysThereIsNoConnection()
        {
            // The gateway refuses without sending, and this is the one refusal
            // that is not about the network being down.
            gateway.Answer = BackendResult.Failed(BackendFailure.NotSignedIn);

            using var bridge = Started();
            view.Feedback();

            view.SubmitFeedback("서버가 없을 때");

            Assert.That(
                view.Notices,
                Has.Exactly(1).StartsWith(SettingsStyle.FeedbackNotSignedInMessage));
        }

        [Test]
        public void Cancelled_SaysNothing()
        {
            // The screen is going away. A notice would land on whatever screen
            // came next.
            gateway.Answer = BackendResult.Failed(BackendFailure.Cancelled);

            using var bridge = Started();
            view.Feedback();

            view.SubmitFeedback("떠나는 중");

            Assert.That(view.Notices, Is.Empty);
            Assert.That(view.SentCalls, Is.Zero);
        }

        [Test]
        public void WhileASendIsInFlight_ASecondPressIsIgnored()
        {
            gateway.Pending = new UniTaskCompletionSource<BackendResult>();

            using var bridge = Started();
            view.Feedback();
            view.SubmitFeedback("한 번만 보내야 합니다");

            // The presenter darkens 보내기 on the first press, but typing
            // another character arms it again while the first send is still out.
            view.TypeFeedback("한 번만 보내야 합니다!");
            view.SubmitFeedback("한 번만 보내야 합니다!");

            Assert.That(gateway.Calls, Is.EqualTo(1));

            gateway.Pending.TrySetResult(BackendResult.Success());
            Assert.That(view.SentCalls, Is.EqualTo(1), "한 번 보냈고 한 번 닫습니다.");
        }

        [Test]
        public void ASendIsNotTiedToTheScreen()
        {
            using var bridge = Started();
            view.Feedback();

            view.SubmitFeedback("화면 수명과 무관해야 합니다");

            Assert.That(
                gateway.TokenCanBeCancelled,
                Is.False,
                "화면이 사라질 때 취소되는 토큰을 넘기면 안 됩니다.");
        }

        [Test]
        public void LeavingRightAfterPressingSend_StillReachesTheServer()
        {
            // 보내기를 누르고 곧바로 나가는 경우입니다. 요청은 이미 나갔고, 답이 와도
            // 알릴 화면이 없으므로 화면은 건드리지 않습니다.
            gateway.Pending = new UniTaskCompletionSource<BackendResult>();

            var bridge = Started();
            view.Feedback();
            view.SubmitFeedback("나가면서 보낸 글");
            bridge.Dispose();

            gateway.Pending.TrySetResult(BackendResult.Success());

            Assert.That(gateway.Calls, Is.EqualTo(1), "요청은 나가 있어야 합니다.");
            Assert.That(view.Notices, Is.Empty, "사라진 화면에 안내를 띄우지 않습니다.");
        }

        [Test]
        public void Blank_IsNotSent()
        {
            using var bridge = Started();
            view.Feedback();

            view.SubmitFeedback("   ");

            Assert.That(gateway.Calls, Is.Zero);
            Assert.That(view.Notices, Is.Empty);
        }

        [Test]
        public void AfterTheScreenIsGone_NothingIsSent()
        {
            var bridge = Started();
            view.Feedback();
            bridge.Dispose();

            view.SubmitFeedback("설정 화면이 닫힌 뒤");

            Assert.That(gateway.Calls, Is.Zero);
        }

        private sealed class FakeFeedbackGateway : IFeedbackGateway
        {
            public int Calls { get; private set; }

            public string LastMessage { get; private set; }

            /// <summary>What a send answers, unless <see cref="Pending"/> holds it open.</summary>
            public BackendResult Answer { get; set; } = BackendResult.Success();

            /// <summary>Set to keep a send in flight until the test finishes it.</summary>
            public UniTaskCompletionSource<BackendResult> Pending { get; set; }

            /// <summary>
            /// Whether the token handed in could ever be cancelled.
            /// </summary>
            /// <remarks>
            /// <see cref="CancellationToken.None"/> answers false. That is the point:
            /// a send must not be tied to the screen's lifetime, or leaving right after
            /// pressing 보내기 throws away what was written.
            /// </remarks>
            public bool TokenCanBeCancelled { get; private set; }

            public UniTask<BackendResult> SendAsync(string message, CancellationToken cancellation)
            {
                Calls++;
                LastMessage = message;
                TokenCanBeCancelled = cancellation.CanBeCanceled;

                return Pending != null ? Pending.Task : UniTask.FromResult(Answer);
            }
        }
    }
}
