using Cysharp.Threading.Tasks;
using Game.Client.Common;
using Game.Client.Interactions;
using UnityEngine;

namespace Game.Client.Tutorial
{
    /// <summary>The final door uses the same remappable interaction action as picking up an item.</summary>
    public sealed class TutorialExitDoor : MonoBehaviour, IInteractable
    {
        [SerializeField] private string destinationScene = "Home";
        private TutorialSession session;
        private bool isLoading;

        public string InteractionPrompt => "튜토리얼 종료";
        public bool IsLoading => isLoading;

        private void Awake() => session = FindAnyObjectByType<TutorialSession>();

        public bool CanInteract(PlayerInteractor interactor) =>
            !isLoading && interactor != null && session != null && session.IsComplete;

        public void Interact(PlayerInteractor interactor)
        {
            if (!CanInteract(interactor))
                return;
            isLoading = true;
            LeaveAsync().Forget(exception =>
            {
                isLoading = false;
                Debug.LogException(exception, this);
            });
        }

        private async UniTask LeaveAsync()
        {
            var loadingView = FindAnyObjectByType<LoadingView>(FindObjectsInactive.Include);
            if (loadingView != null)
            {
                loadingView.Show();
                await SceneLoadSlicer.YieldFrame();
            }

            // Record completion only after the destination actually loaded.
            await SceneLoadSlicer.LoadSingleAsync(destinationScene);
            new PlayerPrefsTutorialCompletionStore().MarkCurrentVersionCompleted();
        }
    }
}
