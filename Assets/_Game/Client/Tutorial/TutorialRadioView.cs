using System;
using System.Collections;
using Game.Core.Tutorial;
using Game.Core.Settings;
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
        private KeySettingGuideView keyGuide;
        [SerializeField] private Game.Client.Interactions.PlayerInteractor interactor;
        private Game.Client.Interactions.ItemPlacementController placement;
        private TutorialChecklistView checklist;
        private bool wasCarrying;
        private bool wasPlacing;
        private UiLocale locale;

        [VContainer.Inject]
        public void BindLocale(UiLocale value) => locale = value;

        public string CurrentMessage => messageText != null ? messageText.text : string.Empty;

        /// <summary>
        /// The tutorial scene injects nothing, so the applied locale is taken
        /// from the running game rather than left as Korean.
        /// </summary>
        private UiLocale Locale => locale ?? UiLocale.Current;

        private string Copy(string key) =>
            locale != null
                ? locale.Get(key)
                : UiLocale.Applied(key);

        private string Language =>
            locale != null ? locale.LanguageCode : UiLocale.AppliedLanguage;

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

            keyGuide = KeySettingGuideView.Ensure(transform);
            keyGuide.AlwaysVisible = true;
            keyGuide.ShowFocusCaption = false;
            keyGuide.transform.localScale = Vector3.one * 1.18f;
            placement = interactor.GetComponent<Game.Client.Interactions.ItemPlacementController>();
            checklist = TutorialChecklistView.Create(transform);

        }

        private void Start()
        {
            if (presenter != null || session == null)
            {
                return;
            }

            presenter = new TutorialRadioPresenter(this, session, Locale);
            presenter.Start();
        }

        private void OnDestroy() => presenter?.Dispose();

        private void Update()
        {
            var carrying = interactor.CarriedItem != null;
            var placing = placement.IsPlacing;
            if (carrying == wasCarrying && placing == wasPlacing) return;
            wasCarrying = carrying;
            wasPlacing = placing;
            HighlightStep(session.CurrentStep);
        }

        internal void HighlightStep(TutorialStep step)
        {
            var carrying = interactor.CarriedItem != null;
            var placing = placement.IsPlacing;
            var hint = step >= TutorialStep.PickUp && step <= TutorialStep.UseShredder && !carrying
                ? string.Format(Copy(UiText.Tutorial.HintPickUp), KeySettingGuideView.CurrentKeyLabel(ControlAction.Interact))
                : step == TutorialStep.Place && !placing
                    ? string.Format(Copy(UiText.Tutorial.HintPlacement), KeySettingGuideView.CurrentKeyLabel(ControlAction.PlacementMode))
                    : step == TutorialStep.Place ? Copy(UiText.Tutorial.HintPlace) : string.Empty;
            checklist.Show(step, hint, Language);
            if (keyGuide != null)
            {
                keyGuide.ShowChrome(Locale);
            }

            if (step >= TutorialStep.PickUp && step <= TutorialStep.UseShredder && !carrying)
            {
                keyGuide.SetFocus(Copy(UiText.Tutorial.FocusPickUp), ControlAction.Interact);
                return;
            }
            if (step == TutorialStep.Place && !placing)
            {
                keyGuide.SetFocus(Copy(UiText.Tutorial.FocusPlacement), ControlAction.PlacementMode);
                return;
            }
            switch (step)
            {
                case TutorialStep.MoveAndLook:
                    keyGuide.SetFocus(Copy(UiText.Tutorial.FocusMove), ControlAction.MoveForward, ControlAction.MoveLeft, ControlAction.MoveBackward, ControlAction.MoveRight); break;
                case TutorialStep.Sprint:
                    keyGuide.SetFocus(Copy(UiText.Tutorial.FocusSprint), ControlAction.Sprint, ControlAction.MoveForward); break;
                case TutorialStep.Jump:
                    keyGuide.SetFocus(Copy(UiText.Tutorial.FocusJump), ControlAction.Jump, ControlAction.Sprint); break;
                case TutorialStep.Crouch:
                    keyGuide.SetFocus(Copy(UiText.Tutorial.FocusCrouch), ControlAction.Crouch, ControlAction.ToggleView); break;
                case TutorialStep.Prone:
                    keyGuide.SetFocus(Copy(UiText.Tutorial.FocusProne), ControlAction.Prone); break;
                case TutorialStep.PickUp:
                    keyGuide.SetFocus(Copy(UiText.Tutorial.FocusLookPickUp), ControlAction.Interact); break;
                case TutorialStep.Drop:
                    keyGuide.SetFocus(Copy(UiText.Tutorial.FocusDrop), ControlAction.Interact); break;
                case TutorialStep.Throw:
                    keyGuide.SetFocus(Copy(UiText.Tutorial.FocusThrow), ControlAction.PrimaryAction); break;
                case TutorialStep.Place:
                    keyGuide.SetFocus(Copy(UiText.Tutorial.FocusPlace), ControlAction.PlacementMode, ControlAction.RotateLeft, ControlAction.RotateRight, ControlAction.RaiseObject, ControlAction.LowerObject, ControlAction.PrimaryAction); break;
                case TutorialStep.UseShredder:
                    keyGuide.SetFocus(Copy(UiText.Tutorial.FocusShredder), ControlAction.Interact); break;
                case TutorialStep.Complete:
                    keyGuide.SetFocus(Copy(UiText.Tutorial.FocusExit), ControlAction.Interact); break;
            }
        }

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
        private readonly TutorialRadioView view;
        private readonly TutorialSession session;
        private readonly UiLocale locale;
        private TutorialStep displayedStep;

        public TutorialRadioPresenter(TutorialRadioView view, TutorialSession session, UiLocale locale = null)
        {
            this.view = view;
            this.session = session;
            this.locale = locale ?? UiLocale.Current;
            displayedStep = session.CurrentStep;
        }

        private string Language =>
            locale != null ? locale.LanguageCode : UiLocale.AppliedLanguage;

        private string Intro =>
            UiTextCatalog.Shipped.Get(UiText.Tutorial.Intro, Language);

        public void Start()
        {
            session.StepChanged += OnStepChanged;
            session.StepRetried += OnStepRetried;
            if (locale != null)
            {
                locale.Changed += OnLocaleChanged;
            }

            view.HighlightStep(displayedStep);
            view.ShowThen(Intro, Instruction(displayedStep, Language));
        }

        public void Dispose()
        {
            session.StepChanged -= OnStepChanged;
            session.StepRetried -= OnStepRetried;
            if (locale != null)
            {
                locale.Changed -= OnLocaleChanged;
            }
        }

        private void OnLocaleChanged()
        {
            view.HighlightStep(displayedStep);
            view.Show(Instruction(displayedStep, Language));
        }

        private void OnStepChanged(TutorialStep nextStep)
        {
            var completedStep = displayedStep;
            displayedStep = nextStep;
            view.HighlightStep(nextStep);
            view.ShowThen(Completion(completedStep), Instruction(nextStep, Language));
        }

        private void OnStepRetried(TutorialStep step) =>
            view.ShowThen(Retry(step), Instruction(step, Language));

        internal static string Instruction(TutorialStep step) =>
            Instruction(step, UiLocale.AppliedLanguage);

        internal static string Instruction(TutorialStep step, string language) => step switch
        {
            TutorialStep.MoveAndLook => UiTextCatalog.Shipped.Get(UiText.Tutorial.InstructMove, language),
            TutorialStep.Sprint => UiTextCatalog.Shipped.Get(UiText.Tutorial.InstructSprint, language),
            TutorialStep.Jump => UiTextCatalog.Shipped.Get(UiText.Tutorial.InstructJump, language),
            TutorialStep.Crouch => UiTextCatalog.Shipped.Get(UiText.Tutorial.InstructCrouch, language),
            TutorialStep.Prone => UiTextCatalog.Shipped.Get(UiText.Tutorial.InstructProne, language),
            TutorialStep.PickUp => UiTextCatalog.Shipped.Get(UiText.Tutorial.InstructPickUp, language),
            TutorialStep.Drop => UiTextCatalog.Shipped.Get(UiText.Tutorial.InstructDrop, language),
            TutorialStep.Throw => UiTextCatalog.Shipped.Get(UiText.Tutorial.InstructThrow, language),
            TutorialStep.Place => UiTextCatalog.Shipped.Get(UiText.Tutorial.InstructPlace, language),
            TutorialStep.UseShredder => UiTextCatalog.Shipped.Get(UiText.Tutorial.InstructShredder, language),
            TutorialStep.Complete => UiTextCatalog.Shipped.Get(UiText.Tutorial.InstructComplete, language),
            _ => string.Empty
        };

        private string Completion(TutorialStep step) => step switch
        {
            TutorialStep.MoveAndLook => UiTextCatalog.Shipped.Get(UiText.Tutorial.CompleteMove, Language),
            TutorialStep.Sprint => UiTextCatalog.Shipped.Get(UiText.Tutorial.CompleteSprint, Language),
            TutorialStep.Jump => UiTextCatalog.Shipped.Get(UiText.Tutorial.CompleteJump, Language),
            TutorialStep.Crouch => UiTextCatalog.Shipped.Get(UiText.Tutorial.CompleteCrouch, Language),
            TutorialStep.Prone => UiTextCatalog.Shipped.Get(UiText.Tutorial.CompleteProne, Language),
            TutorialStep.PickUp => UiTextCatalog.Shipped.Get(UiText.Tutorial.CompletePickUp, Language),
            TutorialStep.Drop => UiTextCatalog.Shipped.Get(UiText.Tutorial.CompleteDrop, Language),
            TutorialStep.Throw => UiTextCatalog.Shipped.Get(UiText.Tutorial.CompleteThrow, Language),
            TutorialStep.Place => UiTextCatalog.Shipped.Get(UiText.Tutorial.CompletePlace, Language),
            TutorialStep.UseShredder => UiTextCatalog.Shipped.Get(UiText.Tutorial.CompleteShredder, Language),
            _ => Instruction(step, Language)
        };

        private string Retry(TutorialStep step) =>
            UiTextCatalog.Shipped.Get(
                step == TutorialStep.Jump ? UiText.Tutorial.RetryJump : UiText.Tutorial.RetryGeneric,
                Language);
    }
}
