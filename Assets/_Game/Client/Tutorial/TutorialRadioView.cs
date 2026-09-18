using System;
using System.Collections;
using Game.Core.Tutorial;
using TMPro;
using UnityEngine;

namespace Game.Client.Tutorial
{
    public sealed class TutorialRadioView : MonoBehaviour
    {
        [SerializeField] private TutorialSession session;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private float followUpDelay = 1.5f;
        [SerializeField] private float fadeDuration = 0.18f;

        private TutorialRadioPresenter presenter;
        private Coroutine sequence;
        private CanvasGroup radioGroup;

        public string CurrentMessage => messageText != null ? messageText.text : string.Empty;

        private void Awake()
        {
            if (session == null || messageText == null)
            {
                Debug.LogError("TutorialRadioView requires TutorialSession and message text.", this);
                enabled = false;
                return;
            }

            var radioPanel = messageText.transform.parent.parent.gameObject;
            radioGroup = radioPanel.GetComponent<CanvasGroup>();
            if (radioGroup == null)
                radioGroup = radioPanel.AddComponent<CanvasGroup>();
            radioGroup.alpha = 1f;

            var keyGuide = KeySettingGuideView.Ensure(transform);
            keyGuide.AlwaysVisible = true;

            presenter = new TutorialRadioPresenter(this, session);
            presenter.Start();
        }

        private void OnDestroy() => presenter?.Dispose();

        internal void Show(string message)
        {
            if (sequence != null)
                StopCoroutine(sequence);
            messageText.text = message;
            radioGroup.alpha = 1f;
        }

        internal void ShowThen(string first, string next)
        {
            if (sequence != null)
                StopCoroutine(sequence);
            sequence = StartCoroutine(ShowSequence(first, next));
        }

        private IEnumerator ShowSequence(string first, string next)
        {
            messageText.text = first;
            radioGroup.alpha = 1f;
            yield return new WaitForSecondsRealtime(followUpDelay);
            yield return Fade(1f, 0f);
            messageText.text = next;
            yield return Fade(0f, 1f);
            sequence = null;
        }

        private IEnumerator Fade(float from, float to)
        {
            if (fadeDuration <= 0f)
            {
                radioGroup.alpha = to;
                yield break;
            }

            for (var elapsed = 0f; elapsed < fadeDuration; elapsed += Time.unscaledDeltaTime)
            {
                radioGroup.alpha = Mathf.Lerp(from, to, elapsed / fadeDuration);
                yield return null;
            }

            radioGroup.alpha = to;
        }
    }

    internal sealed class TutorialRadioPresenter : IDisposable
    {
        private const string Intro = "신입, 들리나. 여긴 우리 아지트의 훈련 구역이다. 지금부터 내 지시에 따라 움직여.";

        private readonly TutorialRadioView view;
        private readonly TutorialSession session;
        private TutorialStep displayedStep;

        public TutorialRadioPresenter(TutorialRadioView view, TutorialSession session)
        {
            this.view = view;
            this.session = session;
            displayedStep = session.CurrentStep;
        }

        public void Start()
        {
            session.StepChanged += OnStepChanged;
            session.StepRetried += OnStepRetried;
            view.ShowThen(Intro, Instruction(displayedStep));
        }

        public void Dispose()
        {
            session.StepChanged -= OnStepChanged;
            session.StepRetried -= OnStepRetried;
        }

        private void OnStepChanged(TutorialStep nextStep)
        {
            var completedStep = displayedStep;
            displayedStep = nextStep;
            view.ShowThen(Completion(completedStep), Instruction(nextStep));
        }

        private void OnStepRetried(TutorialStep step) => view.ShowThen(Retry(step), Instruction(step));

        private static string Instruction(TutorialStep step) => step switch
        {
            TutorialStep.MoveAndLook => "주변을 살피면서 앞으로 이동해. 좋은 도둑은 발보다 눈이 먼저 움직이는 법이지.",
            TutorialStep.Sprint => "일이 틀어지면 망설일 시간이 없다. 건너편까지 전력으로 달려.",
            TutorialStep.Jump => "앞이 끊겨 있군. 달려가서 뛰어넘어. 떨어지면 다시 올라오게 해주지. 한 번만.",
            TutorialStep.Crouch => "통로가 낮다. 몸을 숙이고 지나가. 머리를 부딪치면 네 책임이다.",
            TutorialStep.Prone => "이번 통로는 더 낮아. 바닥에 엎드려서 통과해. 옷이 더러워지는 건 업무에 포함된다.",
            TutorialStep.PickUp => "상자 하나가 보일 거다. 가까이 가서 들어 올려. 오늘부터 네가 지켜야 할 물건이다.",
            TutorialStep.Drop => "표시된 구역까지 운반한 다음 바닥에 내려놔. 던지지 말고 얌전히.",
            TutorialStep.Throw => "이번엔 표적을 봐. 힘을 조절해서 상자를 던져.",
            TutorialStep.Place => "상자를 지정된 자리에 정확히 배치해. 현장에서는 몇 센티미터가 계획을 망치기도 한다.",
            TutorialStep.UseShredder => "마지막 처리다. 상자를 들고 파쇄기에 넣어. 증거를 남기지 마.",
            TutorialStep.Complete => "훈련은 끝났다. 앞의 문을 직접 열어. 밖으로 나가면 실전이다.",
            _ => string.Empty
        };

        private static string Completion(TutorialStep step) => step switch
        {
            TutorialStep.MoveAndLook => "좋아. 적어도 벽을 보고 걷지는 않겠군.",
            TutorialStep.Sprint => "그 정도면 경비원 하나쯤은 따돌리겠어.",
            TutorialStep.Jump => "착지는 거칠지만 넘어오긴 했군.",
            TutorialStep.Crouch => "조용히 움직이는 법을 조금은 아는군.",
            TutorialStep.Prone => "좋아. 체면보다 임무가 먼저라는 건 이해했군.",
            TutorialStep.PickUp => "단단히 잡아. 우리 물건은 잃어버리는 순간 남의 물건이 된다.",
            TutorialStep.Drop => "좋아. 내려놓는 것과 떨어뜨리는 것의 차이는 아는군.",
            TutorialStep.Throw => "정확하군. 필요할 때는 물건도 훌륭한 도구가 된다.",
            TutorialStep.Place => "좋아. 정리할 줄 아는 도둑은 오래 살아남지.",
            TutorialStep.UseShredder => "깨끗하군. 이제 저 물건이 있었다는 걸 아는 사람은 우리뿐이다.",
            _ => Instruction(step)
        };

        private static string Retry(TutorialStep step) => step == TutorialStep.Jump
            ? "아래에 숨을 생각은 아니었겠지. 다시 뛰어."
            : "집중해. 같은 실수는 두 번이면 습관이다.";
    }
}
