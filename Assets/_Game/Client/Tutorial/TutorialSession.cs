using System;
using Game.Core.Tutorial;
using UnityEngine;

namespace Game.Client.Tutorial
{
    public sealed class TutorialSession : MonoBehaviour
    {
        private readonly TutorialProgress progress = new();

        public TutorialStep CurrentStep => progress.CurrentStep;
        public bool IsComplete => progress.IsComplete;
        public event Action<TutorialStep> StepChanged;

        public bool ObserveMovement(TutorialMovementObservation observation)
        {
            if (!progress.ObserveMovement(observation)) return false;
            StepChanged?.Invoke(progress.CurrentStep);
            Debug.Log($"[Tutorial] Step advanced: {progress.CurrentStep}", this);
            return true;
        }

        public void RetryCurrentStep() => progress.RetryCurrentStep();

        public bool ObserveInteraction(TutorialInteractionAction action)
        {
            if (!progress.ObserveInteraction(action)) return false;
            StepChanged?.Invoke(progress.CurrentStep);
            Debug.Log($"[Tutorial] Step advanced: {progress.CurrentStep}", this);
            return true;
        }
    }
}
