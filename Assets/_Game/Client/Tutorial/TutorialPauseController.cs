using Cysharp.Threading.Tasks;
using Game.Client.Cameras;
using Game.Client.Common;
using Game.Client.Interactions;
using Game.Client.Players;
using Game.Client.Settings;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Game.Client.Tutorial
{
    public sealed class TutorialPauseController : MonoBehaviour
    {
        [SerializeField] private string destinationScene = "Home";
        [SerializeField] private string exitTitle = "튜토리얼을 종료하겠습니까?";
        [SerializeField] private PlayerMovement player;
        [SerializeField] private PlayerCameraController cameraRig;
        private SettingsView modal;
        private bool leaving;
        private int openedFrame = -1;
        public bool IsOpen { get; private set; }

        private void Start()
        {
            var modalHost = new GameObject("Tutorial Exit Confirmation");
            modalHost.transform.SetParent(transform, false);
            modalHost.SetActive(false);
            modal = modalHost.AddComponent<SettingsView>();
            modal.ConfigureAsModalOnly();
            modal.ConfirmAccepted += Leave;
            modal.ConfirmDeclined += Resume;
            modal.ConfirmDismissed += ResumeAfterOpeningFrame;
            cameraRig.SetEscapeReleasesCursor(false);
            if (EventSystem.current == null)
            {
                var events = new GameObject("Tutorial EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform);
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
        }

        private void Update()
        {
            if (!leaving && !IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                Toggle();
        }

        public void Toggle()
        {
            if (IsOpen) { Resume(); return; }
            IsOpen = true;
            openedFrame = Time.frameCount;
            SetInputBlocked(true);
            modal.gameObject.SetActive(true);
            modal.ShowLeaveConfirmation(exitTitle);
        }

        public void Resume()
        {
            if (leaving) return;
            modal.HideConfirm();
            modal.gameObject.SetActive(false);
            IsOpen = false;
            WebPointerInput.DiscardHeldButtons();
            SetInputBlocked(false);
        }

        private void SetInputBlocked(bool blocked)
        {
            player.IsMovementLocked = blocked;
            player.GetComponent<ItemPlacementController>().IsInputLocked = blocked;
            cameraRig.SetCursorCaptureEnabled(!blocked);
        }

        private void Leave() => LeaveAsync().Forget(Debug.LogException);

        private void ResumeAfterOpeningFrame()
        {
            if (Time.frameCount != openedFrame)
                Resume();
        }

        private async UniTask LeaveAsync()
        {
            if (leaving) return;
            leaving = true;
            modal.HideConfirm();
            modal.gameObject.SetActive(false);
            var loading = FindAnyObjectByType<LoadingView>(FindObjectsInactive.Include) ?? LoadingView.Create(null);
            loading.Show();
            await SceneLoadSlicer.YieldFrame();
            await SceneLoadSlicer.LoadSingleAsync(destinationScene);
            new PlayerPrefsTutorialCompletionStore().MarkCurrentVersionCompleted();
        }

        private void OnDestroy()
        {
            if (modal != null)
            {
                modal.ConfirmAccepted -= Leave;
                modal.ConfirmDeclined -= Resume;
                modal.ConfirmDismissed -= ResumeAfterOpeningFrame;
            }
            if (player != null) player.IsMovementLocked = false;
            if (cameraRig != null) cameraRig.SetEscapeReleasesCursor(true);
        }
    }
}
