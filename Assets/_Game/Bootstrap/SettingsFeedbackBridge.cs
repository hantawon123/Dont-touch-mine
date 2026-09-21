using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Client.Settings;
using Game.Core.Backend;
using Game.Core.Ports;
using Game.Core.Settings;
using UnityEngine;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>
    /// Carries what a player wrote in 설정 → 피드백 보내기 to the server.
    /// </summary>
    /// <remarks>
    /// The same shape <see cref="HomeProfileBridge"/> uses: the screen raises an
    /// event, this sends it on, and the answer decides what the screen says
    /// next. The panel's own state — what is typed, whether 보내기 is armed —
    /// stays with <see cref="SettingsPresenter"/>, which knows nothing about
    /// the network.
    /// <para>
    /// <b>Failure leaves what was written alone.</b> Somebody who filled the box
    /// and lost it to a timeout does not write it again, so a refusal re-arms
    /// 보내기 and says why, and nothing clears the box. Only a send that the
    /// server confirmed closes the panel.
    /// </para>
    /// <para>
    /// <b>A send outlives the screen.</b> Pressing 보내기 and leaving immediately
    /// still reaches the server; only the answer is dropped, because by then there is
    /// no panel to close and no notice to show. Cancelling the request instead would
    /// throw away what the player wrote at the exact moment they believed they had
    /// sent it.
    /// </para>
    /// <para>
    /// Sign-in is not a dependency here, unlike the profile bridge. That one
    /// runs at startup and would race sign-in; this one runs when a player
    /// presses a button on a screen two navigations deep. If somehow nothing is
    /// signed in, the gateway refuses before sending
    /// (<see cref="BackendFailure.NotSignedIn"/>) and that reads on screen as
    /// what it is.
    /// </para>
    /// </remarks>
    public sealed class SettingsFeedbackBridge : IStartable, IDisposable
    {
        private readonly ISettingsView view;
        private readonly IFeedbackGateway feedback;
        private readonly UiLocale locale;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();

        /// <summary>
        /// Whether a send is in flight.
        /// </summary>
        /// <remarks>
        /// The presenter also darkens 보내기 on press, so this is the second
        /// guard rather than the first. It is here because the button comes back
        /// alive as soon as the player types another character, and typing while
        /// a slow send is in flight must not queue a second one.
        /// </remarks>
        private bool sending;

        public SettingsFeedbackBridge(
            ISettingsView view, IFeedbackGateway feedback, UiLocale locale = null)
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.feedback = feedback ?? throw new ArgumentNullException(nameof(feedback));
            this.locale = locale;
        }

        public void Start()
        {
            view.FeedbackSubmitted += OnFeedbackSubmitted;
        }

        public void Dispose()
        {
            view.FeedbackSubmitted -= OnFeedbackSubmitted;
            lifetime.Cancel();
            lifetime.Dispose();
        }

        private void OnFeedbackSubmitted(string message)
        {
            SendAsync(message).Forget();
        }

        private async UniTaskVoid SendAsync(string message)
        {
            if (sending || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            sending = true;

            try
            {
                // 화면 수명을 걸지 않습니다. CancellationToken.None 입니다.
                //
                // lifetime.Token 을 넘기면 보내기를 누른 직후 화면을 나가는 순간 요청이
                // 취소되고, 플레이어가 쓴 글은 아무 데도 남지 않습니다. 화면이 사라져도
                // 서버에 적히는 것이 이 기능의 목적입니다.
                //
                // lifetime 은 그 뒤에도 씁니다. 다만 "요청을 끊을까" 가 아니라 "화면을
                // 건드려도 되는가" 를 정하는 데만 씁니다 - 바로 아래 검사가 그것입니다.
                var result = await feedback.SendAsync(message, CancellationToken.None);

                if (lifetime.IsCancellationRequested)
                {
                    return;
                }

                if (result.Ok)
                {
                    // Down first, then the thanks over the screen behind it.
                    view.FeedbackSent();
                    view.ShowNotice(Copy(UiText.Settings.FeedbackSend), Copy(UiText.Settings.FeedbackSent));
                    return;
                }

                // 우리가 끊는 일은 없지만(위의 None) 앱이 닫히는 중이면 전송 계층이
                // 취소를 돌려줍니다. 그때는 알릴 화면도 없습니다.
                if (result.Failure == BackendFailure.Cancelled)
                {
                    return;
                }

                view.SetFeedbackSubmitEnabled(true);
                view.ShowNotice(
                    Copy(UiText.Settings.FeedbackSend),
                    $"{Explain(result.Failure)}. {Copy(UiText.Settings.FeedbackKept)}");

                Debug.LogWarning($"[Feedback] Refused: {result.Failure}.");
            }
            finally
            {
                sending = false;
            }
        }

        /// <summary>
        /// What to say about a refusal.
        /// </summary>
        /// <remarks>
        /// Only the failures a player can do something about get their own line.
        /// The rest share one message: a code the player cannot act on is noise,
        /// and the code itself goes to the log instead.
        /// </remarks>
        private string Copy(string key) =>
            locale != null
                ? locale.Get(key)
                : UiTextCatalog.Shipped.Get(key, "ko");

        private string Explain(BackendFailure failure)
        {
            switch (failure)
            {
                case BackendFailure.NotSignedIn:
                    return Copy(UiText.Settings.FeedbackNotSignedIn);

                case BackendFailure.Offline:
                case BackendFailure.Timeout:
                    return Copy(UiText.Settings.FeedbackOffline);

                case BackendFailure.InvalidRequest:
                    return Copy(UiText.Settings.FeedbackTooLong);

                default:
                    return Copy(UiText.Settings.FeedbackFailed);
            }
        }
    }
}
