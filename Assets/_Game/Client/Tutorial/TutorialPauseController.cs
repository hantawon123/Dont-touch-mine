using Cysharp.Threading.Tasks;
using Game.Client.Cameras;
using Game.Client.Common;
using Game.Client.Interactions;
using Game.Client.Lobby;
using Game.Client.Players;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Game.Client.Tutorial
{
    public sealed class TutorialPauseController : MonoBehaviour
    {
        [SerializeField] private string destinationScene = "Home";
        [SerializeField] private PlayerMovement player;
        [SerializeField] private PlayerCameraController cameraRig;
        private LobbyConfirmView modal;
        private bool leaving;
        public bool IsOpen { get; private set; }

        private void Start()
        {
            modal = LobbyConfirmView.Create(transform);
            modal.Confirmed += Leave;
            modal.Cancelled += Resume;
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
            if (!leaving && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                Toggle();
        }

        public void Toggle()
        {
            if (IsOpen) { Resume(); return; }
            IsOpen = true;
            SetInputBlocked(true);
            modal.Show("튜토리얼을 나가시겠습니까?\n완료하지 않은 훈련은 저장되지 않습니다.", "나가기");
        }

        public void Resume()
        {
            if (leaving) return;
            modal.Hide();
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

        private async UniTask LeaveAsync()
        {
            if (leaving) return;
            leaving = true;
            modal.Hide();
            var loading = FindAnyObjectByType<LoadingView>(FindObjectsInactive.Include) ?? LoadingView.Create(null);
            loading.Show();
            await SceneLoadSlicer.YieldFrame();
            await SceneLoadSlicer.LoadSingleAsync(destinationScene);
            new PlayerPrefsTutorialCompletionStore().MarkCurrentVersionCompleted();
        }

        private void OnDestroy()
        {
            if (modal != null) { modal.Confirmed -= Leave; modal.Cancelled -= Resume; }
            if (player != null) player.IsMovementLocked = false;
            if (cameraRig != null) cameraRig.SetEscapeReleasesCursor(true);
        }
    }
}
